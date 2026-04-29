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
        private static FreecamController _freecam;
        private static GameObject _previewRoot;

        private static List<GameObject> _disabledRootObjects = new();
        private static bool _cancelRequested;

        // Saved render settings to restore
        private static bool _savedFog;
        private static Color _savedFogColor;
        private static float _savedFogStart;
        private static float _savedFogEnd;
        private static FogMode _savedFogMode;
        private static Color _savedAmbientLight;

        // Grid spacing matching the game's module size (3 tiles * 5 units = 15)
        private const float MODULE_SPACING = 15f;

        // Loading screen
        private static bool _showLoadingScreen;
        private static string _loadingLevelName;
        private static Texture2D _blackTex;

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
            if (!Plugin.PreviewEnabled.Value) return;
            if (Instance == null) return;

            _previewLevelName = levelName;
            _cancelRequested = false;
            Instance.StartCoroutine(BuildPreview(levelName));
        }

        public static void StopPreview()
        {
            if (State != PreviewState.Previewing) return;
            if (Instance == null) return;

            CleanupPreview();

            RestoreLobby();
            BroadcastPreviewEnd();

            bool isInMenu = RunManager.instance.levelCurrent.name != MapVote.MapVote.TRUCK_LEVEL_NAME;
            MapVote.MapVote.CreateVotePopup(isInMenu);

            State = PreviewState.Idle;
            _previewLevelName = null;
            Plugin.Log.LogInfo("[PREVIEW] Preview closed");
        }

        public static void ForceClose()
        {
            if (State == PreviewState.Idle) return;

            _cancelRequested = true;

            // Stop all coroutines on the instance to kill any running BuildPreview/BuildGridPreview
            if (Instance != null)
                Instance.StopAllCoroutines();

            CleanupPreview();
            RestoreLobby();
            BroadcastPreviewEnd();

            State = PreviewState.Idle;
            _previewLevelName = null;
            _cancelRequested = false;
        }

        private static void CleanupPreview()
        {
            _showLoadingScreen = false;

            if (_freecam != null)
            {
                Destroy(_freecam.gameObject);
                _freecam = null;
            }

            if (_previewRoot != null)
            {
                Destroy(_previewRoot);
                _previewRoot = null;
            }
        }

        public static void HandlePreviewStart(EventData data)
        {
            string payload = (string)data.CustomData;
            var parts = payload.Split('|');
            if (parts.Length < 2) return;
            Plugin.PreviewingPlayers[parts[0]] = parts[1];
            MapVote.MapVote.UpdateButtonLabels();
        }

        public static void HandlePreviewEnd(EventData data)
        {
            string playerName = (string)data.CustomData;
            Plugin.PreviewingPlayers.Remove(playerName);
            MapVote.MapVote.UpdateButtonLabels();
        }

        private static Level _savedLevel;

        private static IEnumerator BuildPreview(string levelName)
        {
            State = PreviewState.Loading;
            Plugin.Log.LogInfo($"[PREVIEW] Building preview for: {levelName}");

            var runManager = Object.FindObjectOfType<RunManager>();
            if (runManager == null)
            {
                Plugin.Log.LogError("[PREVIEW] RunManager not found");
                State = PreviewState.Idle;
                yield break;
            }

            var level = runManager.levels.Find(l => l.name == levelName);
            if (level == null)
            {
                Plugin.Log.LogError($"[PREVIEW] Level '{levelName}' not found");
                State = PreviewState.Idle;
                yield break;
            }

            if (MapVote.MapVote.VotePopup != null)
                MapVote.MapVote.VotePopup.ClosePage(true);

            // Show loading screen immediately
            _loadingLevelName = levelName.Replace("Level - ", "");
            _showLoadingScreen = true;

            // Wait 2 frames so the loading screen renders before the freeze
            yield return null;
            yield return null;

            // Find LevelGenerator BEFORE disabling lobby (it's on a root object)
            var levelGen = Object.FindObjectOfType<LevelGenerator>();

            DisableLobby();

            // Save render settings
            _savedFog = RenderSettings.fog;
            _savedFogColor = RenderSettings.fogColor;
            _savedFogStart = RenderSettings.fogStartDistance;
            _savedFogEnd = RenderSettings.fogEndDistance;
            _savedFogMode = RenderSettings.fogMode;
            _savedAmbientLight = RenderSettings.ambientLight;

            // Try procedural generation first, fallback to grid
            bool generated = false;
            try
            {
                generated = TryProceduralGeneration(runManager, level, levelGen);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[PREVIEW] Procedural generation failed: {ex.Message}, falling back to grid");
            }

            if (!generated)
            {
                yield return Instance.StartCoroutine(BuildGridPreview(level));
            }

            // Apply level's visual settings (exact game values)
            RenderSettings.fog = true;
            RenderSettings.fogColor = level.FogColor;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = level.FogStartDistance;
            RenderSettings.fogEndDistance = level.FogEndDistance > 0 ? level.FogEndDistance : 15f;
            RenderSettings.ambientLight = level.AmbientColor;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

            // Create freecam
            Vector3 camStart = _previewRoot != null
                ? _previewRoot.transform.position + new Vector3(15f, 8f, 15f)
                : new Vector3(0f, 5f, 0f);

            var freecamObj = new GameObject("MapPreviewFreecam");
            freecamObj.hideFlags = HideFlags.HideAndDontSave;
            _freecam = freecamObj.AddComponent<FreecamController>();
            _freecam.Initialize(Plugin.FreecamSpeed.Value, camStart);
            _freecam.ApplyLevelEffects(level);

            // Hide loading screen
            _showLoadingScreen = false;

            BroadcastPreviewStart(levelName);

            State = PreviewState.Previewing;
            Plugin.Log.LogInfo($"[PREVIEW] Now previewing: {levelName}");
        }

        private class PreviewTile
        {
            public int x, y;
            public bool active;
            public bool first;
            public Module.Type type;
            public bool connectedTop, connectedBot, connectedRight, connectedLeft;
            public int connections;
        }

        private static bool GridCheck(PreviewTile[,] grid, int x, int y, int w, int h)
        {
            return x >= 0 && x < w && y >= 0 && y < h && grid[x, y].active;
        }

        private static bool TryProceduralGeneration(RunManager runManager, Level level, LevelGenerator levelGen)
        {
            Plugin.Log.LogInfo("[PREVIEW] Attempting procedural generation (exact game algorithm)...");

            _savedLevel = runManager.levelCurrent;

            // Constants from the game
            float TileSize = 5f;
            float ModWidth = 3f;
            float moduleWidth = ModWidth * TileSize; // 15 units

            // Grid 9x9 for preview
            int LW = 9;
            int LH = 9;

            // Module amount scaled for the grid
            int moduleAmount = Mathf.Max(level.ModuleAmount > 0 ? level.ModuleAmount : 6, 6);
            moduleAmount = Mathf.Min(moduleAmount * 2, LW * LH - 1);

            int deadEndAmount = Mathf.CeilToInt(moduleAmount / 3f);
            int extractionAmount = moduleAmount >= 15 ? 4 : moduleAmount >= 10 ? 3 : moduleAmount >= 8 ? 2 : moduleAmount >= 6 ? 1 : 0;

            // === TILE GENERATION (exact game algorithm) ===
            var grid = new PreviewTile[LW, LH];
            for (int x = 0; x < LW; x++)
                for (int y = 0; y < LH; y++)
                    grid[x, y] = new PreviewTile { x = x, y = y };

            // Start tile at center-bottom (exact: LevelWidth/2, 0)
            int startX = LW / 2;
            int startY = 0;
            grid[startX, startY].active = true;
            grid[startX, startY].first = true;
            int remaining = moduleAmount;

            // Random walk (exact game code)
            int cx = startX, cy = startY;
            int safetyLimit = 10000;
            while (remaining > 0 && safetyLimit-- > 0)
            {
                int dx = -999, dy = -999;
                while (cx + dx < 0 || cx + dx >= LW || cy + dy < 0 || cy + dy >= LH)
                {
                    dx = 0; dy = 0;
                    int r = (cy == 1) ? Random.Range(0, 3) : Random.Range(0, 4);
                    switch (r)
                    {
                        case 0: dx = -1; break;
                        case 1: dx = 1; break;
                        case 2: dy = 1; break;
                        case 3: dy = -1; break;
                    }
                }
                cx += dx; cy += dy;
                if (!grid[cx, cy].active)
                {
                    grid[cx, cy].active = true;
                    remaining--;
                }
            }

            // === EXTRACTION PLACEMENT (exact game algorithm) ===
            var possibleExtractionTiles = new List<PreviewTile>();
            for (int x = 0; x < LW; x++)
            {
                for (int y = 0; y < LH; y++)
                {
                    if (grid[x, y].active) continue;
                    int adj = 0;
                    if (GridCheck(grid, x, y + 1, LW, LH)) adj++;
                    if (GridCheck(grid, x + 1, y, LW, LH)) adj++;
                    if (GridCheck(grid, x, y - 1, LW, LH)) adj++;
                    if (GridCheck(grid, x - 1, y, LW, LH)) adj++;
                    if (adj == 1)
                        possibleExtractionTiles.Add(grid[x, y]);
                }
            }

            // Seed reference: truck position (center, below start)
            var seedTile = new PreviewTile { x = startX, y = -1 };
            var extractionTiles = new List<PreviewTile> { seedTile };

            int extRemaining = extractionAmount;
            while (extRemaining > 0 && possibleExtractionTiles.Count > 0)
            {
                // Find farthest from all placed extractions (exact game: max of min distances)
                PreviewTile best = null;
                float bestDist = 0f;
                foreach (var tile in possibleExtractionTiles)
                {
                    float minDist = float.MaxValue;
                    foreach (var ext in extractionTiles)
                    {
                        float d = Vector2.Distance(new Vector2(ext.x, ext.y), new Vector2(tile.x, tile.y));
                        if (d < minDist) minDist = d;
                    }
                    if (minDist > bestDist)
                    {
                        bestDist = minDist;
                        best = tile;
                    }
                }

                if (best == null) break;

                best.type = Module.Type.Extraction;
                best.active = true;
                extractionTiles.Add(best);
                possibleExtractionTiles.Remove(best);

                // Remove adjacent candidates (exact game code)
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    foreach (var cand in possibleExtractionTiles)
                    {
                        if ((cand.x == best.x && cand.y == best.y - 1) ||
                            (cand.x == best.x + 1 && cand.y == best.y) ||
                            (cand.x == best.x && cand.y == best.y + 1) ||
                            (cand.x == best.x - 1 && cand.y == best.y))
                        {
                            possibleExtractionTiles.Remove(cand);
                            changed = true;
                            break;
                        }
                    }
                }
                extRemaining--;
            }

            // Dead end placement
            int deRemaining = deadEndAmount;
            while (deRemaining > 0 && possibleExtractionTiles.Count > 0)
            {
                var tile = possibleExtractionTiles[Random.Range(0, possibleExtractionTiles.Count)];
                tile.type = Module.Type.DeadEnd;
                tile.active = true;
                extractionTiles.Add(tile);
                possibleExtractionTiles.Remove(tile);

                bool changed2 = true;
                while (changed2)
                {
                    changed2 = false;
                    foreach (var cand in possibleExtractionTiles)
                    {
                        if ((cand.x == tile.x && cand.y == tile.y - 1) ||
                            (cand.x == tile.x + 1 && cand.y == tile.y) ||
                            (cand.x == tile.x && cand.y == tile.y + 1) ||
                            (cand.x == tile.x - 1 && cand.y == tile.y))
                        {
                            possibleExtractionTiles.Remove(cand);
                            changed2 = true;
                            break;
                        }
                    }
                }
                deRemaining--;
            }

            // === SPAWN ===
            Vector3 origin = new Vector3(0f, -3000f, 0f);
            _previewRoot = new GameObject("MapPreviewRoot");
            _previewRoot.transform.position = origin;

            // Collect prefab pools
            List<GameObject> normalMods = new();
            AddPrefabRefs(normalMods, level.ModulesNormal1);
            AddPrefabRefs(normalMods, level.ModulesNormal2);
            AddPrefabRefs(normalMods, level.ModulesNormal3);

            List<GameObject> passageMods = new();
            AddPrefabRefs(passageMods, level.ModulesPassage1);
            AddPrefabRefs(passageMods, level.ModulesPassage2);
            AddPrefabRefs(passageMods, level.ModulesPassage3);

            List<GameObject> deadEndMods = new();
            AddPrefabRefs(deadEndMods, level.ModulesDeadEnd1);
            AddPrefabRefs(deadEndMods, level.ModulesDeadEnd2);
            AddPrefabRefs(deadEndMods, level.ModulesDeadEnd3);

            List<GameObject> extractionMods = new();
            AddPrefabRefs(extractionMods, level.ModulesExtraction1);
            AddPrefabRefs(extractionMods, level.ModulesExtraction2);
            AddPrefabRefs(extractionMods, level.ModulesExtraction3);

            // Spawn start room at origin — exactly like the game does (Vector3.zero)
            // The game puts the start room BELOW the grid, and the first grid tile (startX,0)
            // at z=7.5 is where the first module goes. The start room connects to it via
            // the bottom connection of tile (startX,0).
            if (level.StartRooms != null && level.StartRooms.Count > 0)
            {
                var startRef = level.StartRooms[Random.Range(0, level.StartRooms.Count)];
                if (startRef != null && startRef.Prefab != null)
                {
                    Plugin.Log.LogInfo($"[PREVIEW] Start room at origin (0,0,0) — game exact");
                    var clone = Object.Instantiate(startRef.Prefab, origin, Quaternion.identity, _previewRoot.transform);

                    var module = clone.GetComponent<Module>();
                    if (module != null)
                    {
                        module.ConnectingTop = true; // connects to first module above
                        module.ConnectingBottom = true; // truck below
                        module.ConnectingRight = false;
                        module.ConnectingLeft = false;
                        module.First = true;
                        module.SetupDone = true;

                        foreach (var propSwitch in clone.GetComponentsInChildren<ModulePropSwitch>(true))
                        {
                            propSwitch.Module = module;
                            try { propSwitch.Setup(); } catch { }
                        }
                    }

                    StripComponents(clone);
                }
            }

            // Spawn a connect object between start room (origin) and first tile (startX,0) at z=7.5
            // This is the "truck connection" — game line 1120-1122
            if (level.ConnectObject != null)
            {
                // Position: same as bottom of tile (startX,0) = (0, 0, 7.5 - 7.5) = (0, 0, 0)
                var truckConnPos = origin;
                Plugin.Log.LogInfo($"[CONN] Truck connection at pos=(0,0,0)");
                var truckConn = Object.Instantiate(level.ConnectObject, truckConnPos, Quaternion.identity, _previewRoot.transform);
                StripComponents(truckConn);
            }

            // Log start room position
            Plugin.Log.LogInfo($"[PREVIEW] Start room at grid({startX},{startY}), world origin={origin}, moduleWidth={moduleWidth}");
            Plugin.Log.LogInfo($"[PREVIEW] Start grid formula: x={startX * moduleWidth - (LW / 2) * moduleWidth}, z={startY * moduleWidth + moduleWidth / 2f}");

            // Spawn modules (exact game position formula)
            int spawned = 0;
            int passageAmount = 0;

            for (int x = 0; x < LW; x++)
            {
                for (int y = 0; y < LH; y++)
                {
                    if (!grid[x, y].active) continue;
                    // Don't skip first tile — spawn a module there to fill the gap
                    // between start room (at origin) and the grid position

                    // Exact game formula
                    Vector3 position = origin + new Vector3(
                        (float)x * moduleWidth - (float)(LW / 2) * moduleWidth,
                        0f,
                        (float)y * moduleWidth + moduleWidth / 2f
                    );

                    Vector3 rotation = Vector3.zero;
                    GameObject prefab = null;

                    if (grid[x, y].type == Module.Type.Extraction)
                    {
                        if (extractionMods.Count > 0)
                            prefab = extractionMods[Random.Range(0, extractionMods.Count)];
                        // Rotate to face neighbor (exact game order)
                        if (GridCheck(grid, x, y - 1, LW, LH)) rotation = Vector3.zero;
                        if (GridCheck(grid, x - 1, y, LW, LH)) rotation = new Vector3(0f, 90f, 0f);
                        if (GridCheck(grid, x, y + 1, LW, LH)) rotation = new Vector3(0f, 180f, 0f);
                        if (GridCheck(grid, x + 1, y, LW, LH)) rotation = new Vector3(0f, -90f, 0f);
                    }
                    else if (grid[x, y].type == Module.Type.DeadEnd)
                    {
                        if (deadEndMods.Count > 0)
                            prefab = deadEndMods[Random.Range(0, deadEndMods.Count)];
                        if (GridCheck(grid, x, y - 1, LW, LH)) rotation = Vector3.zero;
                        if (GridCheck(grid, x - 1, y, LW, LH)) rotation = new Vector3(0f, 90f, 0f);
                        if (GridCheck(grid, x, y + 1, LW, LH)) rotation = new Vector3(0f, 180f, 0f);
                        if (GridCheck(grid, x + 1, y, LW, LH)) rotation = new Vector3(0f, -90f, 0f);
                    }
                    else
                    {
                        // Passage check (exact game logic)
                        bool canPassage = passageAmount < (level.PassageMaxAmount > 0 ? level.PassageMaxAmount : 2);
                        bool isVertical = GridCheck(grid, x, y + 1, LW, LH) &&
                                          (GridCheck(grid, x, y - 1, LW, LH) || grid[x, y].first) &&
                                          !GridCheck(grid, x + 1, y, LW, LH) &&
                                          !GridCheck(grid, x - 1, y, LW, LH);
                        bool isHorizontal = !grid[x, y].first &&
                                            GridCheck(grid, x + 1, y, LW, LH) &&
                                            GridCheck(grid, x - 1, y, LW, LH) &&
                                            !GridCheck(grid, x, y + 1, LW, LH) &&
                                            !GridCheck(grid, x, y - 1, LW, LH);

                        if (canPassage && isVertical && passageMods.Count > 0)
                        {
                            prefab = passageMods[Random.Range(0, passageMods.Count)];
                            rotation = Random.Range(0, 100) < 50 ? new Vector3(0f, 180f, 0f) : Vector3.zero;
                            passageAmount++;
                        }
                        else if (canPassage && isHorizontal && passageMods.Count > 0)
                        {
                            prefab = passageMods[Random.Range(0, passageMods.Count)];
                            rotation = Random.Range(0, 100) < 50 ? new Vector3(0f, -90f, 0f) : new Vector3(0f, 90f, 0f);
                            passageAmount++;
                        }
                        else
                        {
                            if (normalMods.Count > 0)
                                prefab = normalMods[Random.Range(0, normalMods.Count)];
                            float[] rots = { 0f, 90f, 180f, 270f };
                            rotation = new Vector3(0f, rots[Random.Range(0, 4)], 0f);
                        }
                    }

                    if (prefab != null)
                    {
                        Plugin.Log.LogInfo($"[PREVIEW] Module grid({x},{y}) type={grid[x,y].type} pos={position - origin} rot={rotation} prefab={prefab.name}");
                        var clone = Object.Instantiate(prefab, position, Quaternion.Euler(rotation), _previewRoot.transform);

                        // Set module connections BEFORE stripping (so ModulePropSwitch.Setup works)
                        bool cTop = GridCheck(grid, x, y + 1, LW, LH);
                        bool cBot = GridCheck(grid, x, y - 1, LW, LH) || grid[x, y].first;
                        bool cRight = GridCheck(grid, x + 1, y, LW, LH);
                        bool cLeft = GridCheck(grid, x - 1, y, LW, LH);

                        var module = clone.GetComponent<Module>();
                        if (module != null)
                        {
                            module.ConnectingTop = cTop;
                            module.ConnectingBottom = cBot;
                            module.ConnectingRight = cRight;
                            module.ConnectingLeft = cLeft;
                            module.First = grid[x, y].first;
                            module.SetupDone = true;

                            // Run ModulePropSwitch.Setup() to toggle walls/doors
                            foreach (var propSwitch in clone.GetComponentsInChildren<ModulePropSwitch>(true))
                            {
                                propSwitch.Module = module;
                                try { propSwitch.Setup(); } catch { }
                            }
                        }

                        StripComponents(clone);
                        spawned++;
                    }
                }
            }

            // === CONNECT OBJECTS (exact game algorithm) ===
            if (level.ConnectObject != null)
            {
                for (int x = 0; x < LW; x++)
                {
                    for (int y = 0; y < LH; y++)
                    {
                        if (!grid[x, y].active) continue;

                        float num = (float)x * moduleWidth - (float)(LW / 2) * moduleWidth;
                        float num2 = (float)y * moduleWidth + moduleWidth / 2f;

                        // Top connection
                        if (y + 1 < LH && grid[x, y + 1].active && !grid[x, y + 1].connectedBot)
                        {
                            var pos = origin + new Vector3(num, 0f, num2 + moduleWidth / 2f);
                            Plugin.Log.LogInfo($"[CONN] Top: grid({x},{y})->({x},{y+1}) pos={pos - origin}");
                            var conn = Object.Instantiate(level.ConnectObject, pos, Quaternion.identity, _previewRoot.transform);
                            StripComponents(conn);
                            grid[x, y].connectedTop = true;
                        }

                        // Right connection
                        if (x + 1 < LW && grid[x + 1, y].active && !grid[x + 1, y].connectedLeft)
                        {
                            var pos = origin + new Vector3(num + moduleWidth / 2f, 0f, num2);
                            Plugin.Log.LogInfo($"[CONN] Right: grid({x},{y})->({x+1},{y}) pos={pos - origin} rot=90");
                            var conn = Object.Instantiate(level.ConnectObject, pos, Quaternion.Euler(0f, 90f, 0f), _previewRoot.transform);
                            StripComponents(conn);
                            grid[x, y].connectedRight = true;
                        }

                        // Bottom connection (skip truck connection — no truck in preview)
                        if (y - 1 >= 0 && grid[x, y - 1].active && !grid[x, y - 1].connectedTop)
                        {
                            var pos = origin + new Vector3(num, 0f, num2 - moduleWidth / 2f);
                            Plugin.Log.LogInfo($"[CONN] Bottom: grid({x},{y})->({x},{y-1}) pos={pos - origin}");
                            var conn = Object.Instantiate(level.ConnectObject, pos, Quaternion.identity, _previewRoot.transform);
                            StripComponents(conn);
                            grid[x, y].connectedBot = true;
                        }

                        // Left connection
                        if (x - 1 >= 0 && grid[x - 1, y].active && !grid[x - 1, y].connectedRight)
                        {
                            var pos = origin + new Vector3(num - moduleWidth / 2f, 0f, num2);
                            Plugin.Log.LogInfo($"[CONN] Left: grid({x},{y})->({x-1},{y}) pos={pos - origin}");
                            var conn = Object.Instantiate(level.ConnectObject, pos, Quaternion.identity, _previewRoot.transform);
                            StripComponents(conn);
                            grid[x, y].connectedLeft = true;
                        }
                    }
                }
            }

            // No extra directional lights — use only the lights from the module prefabs
            // The game uses dark ambient + module lights only

            runManager.levelCurrent = _savedLevel;

            Plugin.Log.LogInfo($"[PREVIEW] Procedural generation complete: {spawned} modules + start room on {LW}x{LH} grid");
            return spawned > 0;
        }

        private static IEnumerator BuildGridPreview(Level level)
        {
            Vector3 previewOrigin = new Vector3(0f, -3000f, 0f);
            _previewRoot = new GameObject("MapPreviewRoot");
            _previewRoot.transform.position = previewOrigin;

            List<GameObject> modulesToSpawn = new();

            if (level.StartRooms != null)
            {
                foreach (var room in level.StartRooms)
                {
                    if (room != null)
                    {
                        var prefab = room.Prefab;
                        if (prefab != null) modulesToSpawn.Add(prefab);
                    }
                }
            }

            AddPrefabRefs(modulesToSpawn, level.ModulesNormal1);
            AddPrefabRefs(modulesToSpawn, level.ModulesNormal2);
            AddPrefabRefs(modulesToSpawn, level.ModulesNormal3);
            AddPrefabRefs(modulesToSpawn, level.ModulesPassage1);
            AddPrefabRefs(modulesToSpawn, level.ModulesPassage2);
            AddPrefabRefs(modulesToSpawn, level.ModulesPassage3);
            AddPrefabRefs(modulesToSpawn, level.ModulesDeadEnd1);
            AddPrefabRefs(modulesToSpawn, level.ModulesDeadEnd2);
            AddPrefabRefs(modulesToSpawn, level.ModulesDeadEnd3);
            AddPrefabRefs(modulesToSpawn, level.ModulesExtraction1);
            AddPrefabRefs(modulesToSpawn, level.ModulesExtraction2);
            AddPrefabRefs(modulesToSpawn, level.ModulesExtraction3);

            if (modulesToSpawn.Count == 0) yield break;

            int totalModules = modulesToSpawn.Count;
            int gridSize = Mathf.CeilToInt(Mathf.Sqrt(totalModules));

            for (int i = 0; i < totalModules; i++)
            {
                int row = i / gridSize;
                int col = i % gridSize;
                Vector3 pos = previewOrigin + new Vector3(col * MODULE_SPACING, 0f, row * MODULE_SPACING);
                var clone = Object.Instantiate(modulesToSpawn[i], pos, Quaternion.identity, _previewRoot.transform);
                StripComponents(clone);
                if (i % 3 == 2) yield return null;
            }

            // No extra lights — use module lights only
        }

        private static void AddPrefabRefs(List<GameObject> list, List<PrefabRef> source)
        {
            if (source == null) return;
            foreach (var prefabRef in source)
            {
                if (prefabRef == null) continue;
                var prefab = prefabRef.Prefab;
                if (prefab != null)
                    list.Add(prefab);
            }
        }

        private static void StripComponents(GameObject obj)
        {
            // Disable non-visual components instead of removing (avoids dependency errors)
            foreach (var comp in obj.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp != null) comp.enabled = false;
            }
            foreach (var comp in obj.GetComponentsInChildren<Collider>(true))
            {
                if (comp != null) comp.enabled = false;
            }
            foreach (var comp in obj.GetComponentsInChildren<Rigidbody>(true))
            {
                if (comp != null) comp.isKinematic = true;
            }
            foreach (var comp in obj.GetComponentsInChildren<AudioSource>(true))
            {
                if (comp != null) comp.enabled = false;
            }
            foreach (var comp in obj.GetComponentsInChildren<Animator>(true))
            {
                if (comp != null) comp.enabled = false;
            }
            foreach (var comp in obj.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (comp != null) comp.Stop();
            }
        }

        private static readonly HashSet<string> KEEP_ACTIVE = new()
        {
            "MapPreviewRoot", "MapPreviewFreecam", "MapVotePreview", "MapVoteWithPreview",
            "REPOPingMod", "BepInEx_Manager", "PhotonMono", "PunVoiceClient(Clone)",
            "NetworkManager", "Steam Manager", "PhotonHandler"
        };

        private static void DisableLobby()
        {
            _disabledRootObjects.Clear();

            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                if (!root.activeSelf) continue;
                if (KEEP_ACTIVE.Contains(root.name)) continue;
                if (root.hideFlags == HideFlags.HideAndDontSave) continue;

                root.SetActive(false);
                _disabledRootObjects.Add(root);
            }

            Plugin.Log.LogInfo($"[PREVIEW] Disabled {_disabledRootObjects.Count} root objects");
        }

        private static void RestoreLobby()
        {
            foreach (var obj in _disabledRootObjects)
            {
                if (obj != null) obj.SetActive(true);
            }

            // Restore render settings
            RenderSettings.fog = _savedFog;
            RenderSettings.fogColor = _savedFogColor;
            RenderSettings.fogStartDistance = _savedFogStart;
            RenderSettings.fogEndDistance = _savedFogEnd;
            RenderSettings.fogMode = _savedFogMode;
            RenderSettings.ambientLight = _savedAmbientLight;

            Plugin.Log.LogInfo($"[PREVIEW] Re-enabled {_disabledRootObjects.Count} root objects");
            _disabledRootObjects.Clear();
        }

        private static void BroadcastPreviewStart(string levelName)
        {
            if (!SemiFunc.IsMultiplayer()) return;
            string playerName = PhotonNetwork.LocalPlayer.NickName;
            Plugin.OnPreviewStart?.RaiseEvent(
                $"{playerName}|{levelName}",
                NetworkingEvents.RaiseOthers,
                SendOptions.SendReliable);
        }

        private static void BroadcastPreviewEnd()
        {
            if (!SemiFunc.IsMultiplayer()) return;
            string playerName = PhotonNetwork.LocalPlayer.NickName;
            Plugin.OnPreviewEnd?.RaiseEvent(
                playerName,
                NetworkingEvents.RaiseOthers,
                SendOptions.SendReliable);
        }

        private void OnGUI()
        {
            // Loading screen overlay
            if (_showLoadingScreen)
            {
                if (_blackTex == null)
                {
                    _blackTex = new Texture2D(1, 1);
                    _blackTex.SetPixel(0, 0, Color.black);
                    _blackTex.Apply();
                }

                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _blackTex);

                var titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 36,
                    alignment = TextAnchor.MiddleCenter
                };
                titleStyle.normal.textColor = Color.white;

                var subtitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter
                };
                subtitleStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

                GUI.Label(new Rect(0, Screen.height / 2f - 40f, Screen.width, 50f),
                    _loadingLevelName, titleStyle);
                GUI.Label(new Rect(0, Screen.height / 2f + 20f, Screen.width, 30f),
                    "Loading preview...", subtitleStyle);
                return;
            }

            if (State != PreviewState.Previewing) return;

            // Back button
            float btnWidth = 150f;
            float btnHeight = 40f;
            float x = (Screen.width - btnWidth) / 2f;
            float y = Screen.height - btnHeight - 20f;

            if (GUI.Button(new Rect(x, y, btnWidth, btnHeight), "Back to Vote"))
            {
                StopPreview();
            }

            // Info label
            string mapName = (_previewLevelName ?? "").Replace("Level - ", "");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(10f, 10f, 600f, 30f),
                $"Previewing: {mapName} | WASD move | Mouse look | Shift fast | Space/Ctrl up/down", style);
        }
    }
}
