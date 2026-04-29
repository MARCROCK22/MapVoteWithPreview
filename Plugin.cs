using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExitGames.Client.Photon;
using HarmonyLib;
using MapVote;
using MenuLib.MonoBehaviors;
using MonoMod.RuntimeDetour;
using REPOLib.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MapVoteWithPreview.Preview;

namespace MapVoteWithPreview
{
    [BepInPlugin("MARCROCK22.MapVoteWithPreview", "MapVoteWithPreview", "0.0.2")]
    [BepInDependency("Patrick.MapVote", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("REPOLib", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        // Config
        public static ConfigEntry<bool> PreviewEnabled;
        public static ConfigEntry<float> FreecamSpeed;

        // Network events
        public static NetworkedEvent OnPreviewStart;
        public static NetworkedEvent OnPreviewEnd;

        // Preview tracking
        public static Dictionary<string, string> PreviewingPlayers = new();

        private readonly Harmony _harmony = new Harmony("MARCROCK22.MapVoteWithPreview");

        // MonoMod hooks for closing preview on game start
        private static Hook _setRunLevelHook;
        private static Hook _buttonStartHook;

        private void Awake()
        {
            Log = Logger;

            gameObject.transform.parent = null;
            gameObject.hideFlags = HideFlags.HideAndDontSave;

            PreviewEnabled = Config.Bind("Preview", "Preview Enabled", true,
                "When true - enables the map preview freecam feature (Shift+Click on a map)");
            FreecamSpeed = Config.Bind("Preview", "Freecam Speed", 10f,
                new ConfigDescription("Speed of the freecam when previewing a map",
                    new AcceptableValueRange<float>(1f, 50f)));

            // Create preview manager
            var previewObj = new GameObject("MapPreviewManager");
            previewObj.transform.parent = null;
            previewObj.hideFlags = HideFlags.HideAndDontSave;
            previewObj.AddComponent<MapPreviewManager>();

            // Network events
            OnPreviewStart = new NetworkedEvent("MapVotePreview_Start", HandlePreviewStart);
            OnPreviewEnd = new NetworkedEvent("MapVotePreview_End", HandlePreviewEnd);

            // Harmony patches
            try
            {
                _harmony.PatchAll(typeof(CreateVotePopupPatch));
                _harmony.PatchAll(typeof(UpdateLabelPatch));
                Log.LogInfo("Harmony patches applied");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to apply Harmony patches: {ex}");
            }

            // MonoMod hooks to close preview on game start
            _setRunLevelHook = new Hook(
                AccessTools.DeclaredMethod(typeof(RunManager), nameof(RunManager.SetRunLevel)),
                HookSetRunLevel);

            _buttonStartHook = new Hook(
                AccessTools.DeclaredMethod(typeof(MenuPageLobby), nameof(MenuPageLobby.ButtonStart)),
                HookButtonStart);

            Logger.LogInfo("MapVoteWithPreview v0.0.2 loaded!");
        }

        // Close preview when level changes
        private static void HookSetRunLevel(Action<RunManager> orig, RunManager self)
        {
            MapPreviewManager.ForceClose();
            orig(self);
        }

        // Close preview when game starts
        private static void HookButtonStart(Action<MenuPageLobby> orig, MenuPageLobby self)
        {
            MapPreviewManager.ForceClose();
            orig(self);
        }

        private static void HandlePreviewStart(EventData data)
        {
            string payload = (string)data.CustomData;
            var parts = payload.Split('|');
            if (parts.Length < 2) return;
            PreviewingPlayers[parts[0]] = parts[1];
            MapVote.MapVote.UpdateButtonLabels();
        }

        private static void HandlePreviewEnd(EventData data)
        {
            string playerName = (string)data.CustomData;
            PreviewingPlayers.Remove(playerName);
            MapVote.MapVote.UpdateButtonLabels();
        }

        // === Harmony Patches ===

        [HarmonyPatch(typeof(MapVote.MapVote))]
        internal static class CreateVotePopupPatch
        {
            [HarmonyPatch(nameof(MapVote.MapVote.CreateVotePopup))]
            [HarmonyPostfix]
            private static void Postfix()
            {
                if (!PreviewEnabled.Value) return;

                foreach (var voteBtn in MapVote.MapVote.VoteOptionButtons)
                {
                    if (voteBtn.IsRandomButton) continue;

                    var btn = voteBtn.Button;
                    var levelName = voteBtn.Level;
                    var originalOnClick = btn.onClick;

                    btn.onClick = () =>
                    {
                        if (MapVote.MapVote.DisableInput) return;

                        if (UnityEngine.Input.GetKey(KeyCode.LeftShift))
                        {
                            MapPreviewManager.StartPreview(levelName);
                            return;
                        }

                        originalOnClick?.Invoke();
                    };
                }
            }
        }

        [HarmonyPatch(typeof(VoteOptionButton))]
        internal static class UpdateLabelPatch
        {
            [HarmonyPatch(nameof(VoteOptionButton.UpdateLabel))]
            [HarmonyPostfix]
            private static void Postfix(VoteOptionButton __instance)
            {
                if (__instance.IsRandomButton) return;
                if (PreviewingPlayers.Values.Any(v => v == __instance.Level))
                {
                    __instance.Button.labelTMP.text += " <color=#808080>(previewing)</color>";
                }
            }
        }
    }
}
