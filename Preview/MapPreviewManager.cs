using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using REPOLib.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MapVoteWithPreview.Preview
{
    public enum PreviewState
    {
        Idle,
        Loading,
        Previewing,
        Unloading
    }

    public class MapPreviewManager : MonoBehaviour
    {
        public static MapPreviewManager Instance { get; private set; }
        public static PreviewState State { get; private set; } = PreviewState.Idle;

        private static string _previewLevelName;
        private static Scene _previewScene;
        private static FreecamController _freecam;

        private static List<Camera> _disabledCameras = new();
        private static List<AudioListener> _disabledListeners = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        public static void StartPreview(string levelName)
        {
            if (State != PreviewState.Idle) return;
            if (!MapVote.PreviewEnabled.Value) return;
            if (Instance == null) return;

            _previewLevelName = levelName;
            Instance.StartCoroutine(LoadPreviewScene(levelName));
        }

        public static void StopPreview()
        {
            if (State != PreviewState.Previewing) return;
            if (Instance == null) return;

            Instance.StartCoroutine(UnloadPreviewScene());
        }

        public static void ForceClose()
        {
            if (State == PreviewState.Idle) return;

            if (_freecam != null)
            {
                Destroy(_freecam.gameObject);
                _freecam = null;
            }

            RestoreLobby();

            if (_previewScene.IsValid() && _previewScene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(_previewScene);
            }

            BroadcastPreviewEnd();

            State = PreviewState.Idle;
            _previewLevelName = null;
        }

        /// <summary>
        /// Handles an OnPreviewStart networked event broadcast by another client.
        /// </summary>
        public static void HandlePreviewStart(EventData data)
        {
            string payload = (string)data.CustomData;
            var parts = payload.Split('|');
            if (parts.Length < 2) return;
            string playerName = parts[0];
            string levelName = parts[1];
            MapVote.PreviewingPlayers[playerName] = levelName;
            MapVote.UpdateButtonLabels();
        }

        /// <summary>
        /// Handles an OnPreviewEnd networked event broadcast by another client.
        /// </summary>
        public static void HandlePreviewEnd(EventData data)
        {
            string playerName = (string)data.CustomData;
            MapVote.PreviewingPlayers.Remove(playerName);
            MapVote.UpdateButtonLabels();
        }

        private static IEnumerator LoadPreviewScene(string levelName)
        {
            State = PreviewState.Loading;
            MapVote.Logger.LogInfo($"[PREVIEW] Loading scene: {levelName}");

            DisableLobby();

            if (MapVote.VotePopup != null)
            {
                MapVote.VotePopup.ClosePage(true);
            }

            AsyncOperation loadOp = null;
            try
            {
                loadOp = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
            }
            catch (System.Exception ex)
            {
                MapVote.Logger.LogError($"[PREVIEW] Failed to load scene: {ex.Message}");
                RestoreLobby();
                State = PreviewState.Idle;
                yield break;
            }

            if (loadOp == null)
            {
                MapVote.Logger.LogError("[PREVIEW] LoadSceneAsync returned null");
                RestoreLobby();
                State = PreviewState.Idle;
                yield break;
            }

            yield return loadOp;

            _previewScene = SceneManager.GetSceneByName(levelName);
            if (!_previewScene.IsValid())
            {
                MapVote.Logger.LogError("[PREVIEW] Loaded scene is not valid");
                RestoreLobby();
                State = PreviewState.Idle;
                yield break;
            }

            // Disable all game logic and audio in the preview scene
            foreach (var root in _previewScene.GetRootGameObjects())
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    mb.enabled = false;
                foreach (var audio in root.GetComponentsInChildren<AudioSource>(true))
                    audio.enabled = false;
            }

            // Create freecam
            var freecamObj = new GameObject("MapPreviewFreecam");
            freecamObj.hideFlags = HideFlags.HideAndDontSave;
            _freecam = freecamObj.AddComponent<FreecamController>();
            _freecam.Initialize(MapVote.FreecamSpeed.Value, new Vector3(0f, 5f, 0f));

            BroadcastPreviewStart(levelName);

            State = PreviewState.Previewing;
            MapVote.Logger.LogInfo($"[PREVIEW] Now previewing: {levelName}");
        }

        private static IEnumerator UnloadPreviewScene()
        {
            State = PreviewState.Unloading;

            if (_freecam != null)
            {
                Destroy(_freecam.gameObject);
                _freecam = null;
            }

            if (_previewScene.IsValid() && _previewScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_previewScene);
            }

            RestoreLobby();
            BroadcastPreviewEnd();

            bool isInMenu = RunManager.instance.levelCurrent.name != MapVote.TRUCK_LEVEL_NAME;
            MapVote.CreateVotePopup(isInMenu);

            State = PreviewState.Idle;
            _previewLevelName = null;
            MapVote.Logger.LogInfo("[PREVIEW] Preview closed");
        }

        private static void DisableLobby()
        {
            _disabledCameras.Clear();
            _disabledListeners.Clear();

            foreach (var cam in FindObjectsOfType<Camera>())
            {
                if (cam.enabled)
                {
                    cam.enabled = false;
                    _disabledCameras.Add(cam);
                }
            }

            foreach (var listener in FindObjectsOfType<AudioListener>())
            {
                if (listener.enabled)
                {
                    listener.enabled = false;
                    _disabledListeners.Add(listener);
                }
            }
        }

        private static void RestoreLobby()
        {
            foreach (var cam in _disabledCameras)
            {
                if (cam != null) cam.enabled = true;
            }
            foreach (var listener in _disabledListeners)
            {
                if (listener != null) listener.enabled = true;
            }
            _disabledCameras.Clear();
            _disabledListeners.Clear();
        }

        private static void BroadcastPreviewStart(string levelName)
        {
            if (!SemiFunc.IsMultiplayer()) return;
            string playerName = PhotonNetwork.LocalPlayer.NickName;
            MapVote.OnPreviewStart?.RaiseEvent(
                $"{playerName}|{levelName}",
                NetworkingEvents.RaiseOthers,
                SendOptions.SendReliable);
        }

        private static void BroadcastPreviewEnd()
        {
            if (!SemiFunc.IsMultiplayer()) return;
            string playerName = PhotonNetwork.LocalPlayer.NickName;
            MapVote.OnPreviewEnd?.RaiseEvent(
                playerName,
                NetworkingEvents.RaiseOthers,
                SendOptions.SendReliable);
        }

        private void OnGUI()
        {
            if (State != PreviewState.Previewing) return;

            float btnWidth = 150f;
            float btnHeight = 40f;
            float x = (Screen.width - btnWidth) / 2f;
            float y = Screen.height - btnHeight - 20f;

            if (GUI.Button(new Rect(x, y, btnWidth, btnHeight), "Back to Vote"))
            {
                StopPreview();
            }

            string mapName = Utilities.RemoveLevelPrefix(_previewLevelName ?? "");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(10f, 10f, 600f, 30f), $"Previewing: {mapName} | WASD move | Mouse look | Shift fast | Space/Ctrl up/down", style);
        }
    }
}
