using System.Collections;
using System.Linq;
using TwinEdge.Levels;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Gameplay
{
    public sealed class V3GameplayBootstrap : MonoBehaviour
    {
        public string levelScene = "LevelBlockoutV3";
        public NetworkManager manager;
        public MultiplayerSessionManager sessions;
        public GameStateManager state;
        public RespawnManager respawns;
        public Transform[] spawnPoints;
        public Transform interactionSample;
        public Camera standbyCamera;
        public CooperativePole cooperativePole;
        [SerializeField, Min(1)] float finishWidth = 4.5f;
        [SerializeField, Min(.1f)] float finishDwell = .35f;
        BoxCollider finish;
        ulong finishCandidate = ulong.MaxValue;
        double finishEnteredAt;
        bool ready;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool debug;
#endif
        string code = "";
        string error;
        public static V3GameplayBootstrap Current { get; private set; }

        void OnEnable()
        {
            Current = this;
            SceneManager.sceneLoaded += DisableLevelPreview;
            var scene = SceneManager.GetSceneByName(levelScene);
            if (scene.IsValid() && scene.isLoaded) DisableLevelPreview(scene, LoadSceneMode.Additive);
        }
        void OnDisable()
        {
            SceneManager.sceneLoaded -= DisableLevelPreview;
            if (Current == this) Current = null;
        }
        void DisableLevelPreview(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != levelScene) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var preview in root.GetComponentsInChildren<Camera>(true))
                    if (preview.name == "MountainPreviewCamera") preview.enabled = false;
            state.GetComponent<BridgeGameplay>().Bind(scene);
            state.GetComponent<RiverGameplay>().Bind(scene);
            state.GetComponent<CastleGameplay>().Bind(scene);
            state.GetComponent<UndergroundGameplay>().Bind(scene);
        }

        IEnumerator Start()
        {
            if (!SceneManager.GetSceneByName(levelScene).isLoaded)
                yield return SceneManager.LoadSceneAsync(levelScene, LoadSceneMode.Additive);
            DisableLevelPreview(SceneManager.GetSceneByName(levelScene), LoadSceneMode.Additive);
            var markers = SceneManager.GetSceneByName(levelScene).GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LevelMarker>()).ToDictionary(marker => marker.id);
            if (!markers.TryGetValue("Mountain.V3.Start", out var start) ||
                !markers.TryGetValue("Mountain.V3.InitialSpawn", out var initial) ||
                !markers.TryGetValue("Mountain.V3.ReturnFinish", out var returnFinish) ||
                !markers.ContainsKey("Mountain.V3.TrailEntrance"))
            {
                error = "V3 mountain route markers missing. Session disabled.";
                yield break;
            }
            Physics.SyncTransforms();
            cooperativePole.ConfigureHome(start.transform.position);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                Vector3 slot = initial.transform.position + initial.transform.right * (i == 0 ? -1.5f : 1.5f);
                spawnPoints[i].SetPositionAndRotation(SafeGround(slot), initial.transform.rotation);
            }
            CreateFinish(SafeGround(returnFinish.transform.position));
            state.ContestInteractionSample = interactionSample.GetComponent<NetworkInteractionTarget>();
            interactionSample.position = SafeGround(start.transform.position + start.transform.forward * 3);
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            interactionSample.GetComponent<Collider>().enabled = false;
            interactionSample.GetComponent<Renderer>().enabled = false;
#endif
            standbyCamera.transform.SetPositionAndRotation(initial.transform.position + Vector3.up * 6 - initial.transform.forward * 9,
                Quaternion.Euler(22, initial.transform.eulerAngles.y, 0));
            // Existing marker IDs are the source of truth. Only planning-approved outbound destinations are enabled.
            AddCheckpoint("Mountain.V3.Final", "Big summit", CheckpointPhase.Cooperation);
            AddCheckpoint("Valley.V3.LowerCheckpoint", "Lower dock", CheckpointPhase.Cooperation);
            AddCheckpoint("Valley.V3.UpperCheckpoint", "Upper dock / castle rooms", CheckpointPhase.Cooperation);
            var fortress = FindObjectsByType<FortressSectionV3>(FindObjectsSortMode.None).FirstOrDefault();
            if (fortress != null) CreateCheckpoint("Underground start", fortress.config.origin + fortress.config.start, CheckpointPhase.Cooperation);
            var colliders = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
            respawns.ConfigureFallRecovery(colliders.Min(c => c.bounds.min.y) - 15f);
            ready = true;
        }
        internal bool ReadyForNetworkPlay => ready && state.IsSpawned && cooperativePole.IsSpawned &&
            state.GetComponent<BridgeGameplay>().IsSpawned && state.GetComponent<RiverGameplay>().IsSpawned &&
            state.GetComponent<CastleGameplay>().IsSpawned && state.GetComponent<UndergroundGameplay>().IsSpawned &&
            state.GetComponent<GemOwnership>().IsSpawned;

        void CreateFinish(Vector3 ground)
        {
            var gate = new GameObject("V3 Summit Finish");
            gate.transform.SetParent(transform);
            gate.transform.position = ground;
            finish = gate.AddComponent<BoxCollider>();
            finish.isTrigger = true;
            finish.center = Vector3.up * .85f;
            finish.size = new Vector3(finishWidth, 2f, finishWidth);
            GemOwnership.Current?.BindFinish(finish, cooperativePole?.mountain);

            var finishMaterial = interactionSample.GetComponent<MeshRenderer>().sharedMaterial;
            var color = new MaterialPropertyBlock();
            color.SetColor("_BaseColor", new Color(1f, .72f, .14f));
            for (int i = 0; i < 4; i++)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                var renderer = marker.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = finishMaterial;
                renderer.SetPropertyBlock(color);
                var markerCollider = marker.GetComponent<Collider>();
                markerCollider.enabled = false;
                Destroy(markerCollider);
                marker.name = "Finish boundary";
                marker.transform.SetParent(gate.transform, false);
                marker.transform.localPosition = new Vector3((i < 2 ? -.5f : .5f) * finishWidth, .45f, (i % 2 == 0 ? -.5f : .5f) * finishWidth);
                marker.transform.localScale = new Vector3(.18f, .48f, .18f);
            }
        }

        void LateUpdate()
        {
            if (!ready || !manager.IsServer || !state.IsSpawned) return;
            var gem = GemOwnership.Current;
            gem?.TickDroppedServer();
            if (state.CanPlay && state.Phase == FoundationPhase.Return &&
                gem is { State: GemState.Carried } && manager.ConnectedClients.TryGetValue(gem.Owner, out var peer) &&
                peer.PlayerObject != null && peer.PlayerObject.TryGetComponent<GameplayCharacter>(out var player) &&
                player.CanContestFinish && finish.bounds.Contains(player.transform.position + Vector3.up * .3f) && player.HasGround())
            {
                double now = manager.ServerTime.Time;
                if (finishCandidate != gem.Owner)
                {
                    finishCandidate = gem.Owner;
                    finishEnteredAt = now;
                }
                else if (now - finishEnteredAt >= finishDwell) state.TryFinishFoundation(player, finish);
            }
            else finishCandidate = ulong.MaxValue;
        }

        public static Vector3 SafeGround(Vector3 candidate)
        {
            if (!Physics.Raycast(candidate + Vector3.up * 6, Vector3.down, out var hit, 18, ~0, QueryTriggerInteraction.Ignore))
                throw new System.InvalidOperationException("No V3 collider under gameplay point: " + candidate);
            return hit.point + Vector3.up * .12f;
        }
        void AddCheckpoint(string id, string label, CheckpointPhase phase)
        {
            var marker = FindObjectsByType<LevelMarker>(FindObjectsSortMode.None).SingleOrDefault(m => m.id == id);
            if (marker != null) CreateCheckpoint(label, marker.transform.position, phase);
        }
        void CreateCheckpoint(string label, Vector3 position, CheckpointPhase phase)
        {
            var obj = new GameObject("GameplayCheckpoint_" + label);
            obj.transform.SetParent(transform); obj.transform.position = SafeGround(position);
            var box = obj.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(9, 4, 9); box.center = Vector3.up * 2;
            var checkpoint = obj.AddComponent<Checkpoint>(); checkpoint.Configure(respawns, phase);
            if (label == "Big summit" && cooperativePole != null && cooperativePole.mountain != null)
                checkpoint.RequireSummitLanding(cooperativePole, cooperativePole.mountain.destination.id);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame) debug = !debug;
        }
#endif

        void OnGUI()
        {
            if (MenuVisible) DrawRoomMenu();
            var player = manager.LocalClient?.PlayerObject;
            if (player != null && state.Phase >= FoundationPhase.Outbound) DrawMatchHud(player);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debug) DrawDiagnostics(player);
#endif
        }

        bool InMatch => manager.IsListening && state.Phase >= FoundationPhase.Outbound;
        bool MenuVisible => !InMatch || Cursor.lockState != CursorLockMode.Locked;
        Rect RoomArea => InMatch
            ? new Rect(12, Screen.height - 184, Mathf.Min(360, Screen.width - 24), 172)
            : new Rect(12, 12, Mathf.Min(360, Screen.width - 24), 260);
        Rect HudArea => new(12, 12, Mathf.Min(390, Screen.width - 24), Mathf.Max(0, Screen.height - 202));

        void DrawRoomMenu()
        {
            GUILayout.BeginArea(RoomArea);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(InMatch ? "ROOM MENU" : "TWIN EDGE");
            GUILayout.Label(sessions.CurrentStatus);
            if (!string.IsNullOrEmpty(sessions.CurrentJoinCode))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Room code " + sessions.CurrentJoinCode);
                if (GUILayout.Button("Copy", GUILayout.Width(64))) GUIUtility.systemCopyBuffer = sessions.CurrentJoinCode;
                GUILayout.EndHorizontal();
            }
            if (!sessions.HasSession && !manager.IsListening)
            {
                GUI.enabled = ready && !sessions.IsBusy;
                if (!ready) GUILayout.Label(error ?? "Loading V3 level...");
                if (GUILayout.Button("Create room")) _ = sessions.CreateRoomAsync();
                code = GUILayout.TextField(code, 24);
                if (GUILayout.Button("Join room")) _ = sessions.JoinRoomAsync(code);
                GUI.enabled = !sessions.IsBusy;
                if (GUILayout.Button("Quit game")) Application.Quit();
            }
            else
            {
                GUILayout.Label($"Players {state.ConnectedPlayerCount}/2");
                if (!InMatch)
                    GUILayout.Label(state.ConnectedPlayerCount < 2 ? "Waiting for your partner" : "Preparing both players...");
                GUI.enabled = !sessions.IsBusy && sessions.HasSession;
                if (GUILayout.Button("Leave room")) _ = sessions.LeaveRoomAsync();
            }
            GUI.enabled = true;
            GUILayout.Label(InMatch
                ? "WASD move | Shift sprint | Space jump | E interact\nClick to capture / attack | F grab, throw or resist"
                : "The match starts automatically when both players are ready.");
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        void DrawMatchHud(NetworkObject player)
        {
            var character = player.GetComponent<GameplayCharacter>();
            bool finished = state.Phase == FoundationPhase.Finished && state.HasWinner;
            GUILayout.BeginArea(HudArea);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(finished
                ? (player.OwnerClientId == state.WinnerClientId ? "VICTORY" : "DEFEAT") + $"  /  {state.ElapsedSeconds:F1}s"
                : state.Phase switch
                {
                    FoundationPhase.Return => "RETURN  /  Race to the mountain trail with the gem",
                    FoundationPhase.SealPending => "OUTBOUND  /  Unseal the gem together",
                    _ => "OUTBOUND  /  Reach the underground altar together"
                });
            if (!finished)
            {
                if (state.ConnectedPlayerCount < 2) GUILayout.Label("Waiting for your partner to reconnect");
                DrawCourseHud(player);
                character.DrawContestHud();
            }
            GemOwnership.Current?.DrawHud();
            GUILayout.Label($"Checkpoint {character.CheckpointName.Value} | Respawns {character.RespawnCount.Value}");
            GUILayout.Label(Cursor.lockState == CursorLockMode.Locked ? "Esc: release mouse / cancel" : "Click the world to capture the mouse");
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        void DrawCourseHud(NetworkObject player)
        {
            if (BridgeGameplay.Current is { } bridge && bridge.PlankCount > 0 &&
                Vector3.Distance(player.transform.position, bridge.StartPoint) <= bridge.Length + 10 &&
                Mathf.Abs(player.transform.position.x - bridge.StartPoint.x) < 12 &&
                Mathf.Abs(player.transform.position.y - bridge.StartPoint.y) < 8 &&
                player.transform.position.z >= bridge.StartPoint.z - 16)
                GUILayout.Label(state.Phase == FoundationPhase.Return ? "Bridge / Jump the gaps" :
                    $"Bridge / Timber deliveries {bridge.CompletedDeliveries}/{bridge.TotalDeliveries}");

            string prompt = player.GetComponent<PlayerInteraction>().CurrentTarget switch
            {
                NetworkInteractionTarget target when target.transform != interactionSample => target.Prompt,
                BridgeGameplay construction => construction.Prompt,
                RiverDockInteraction dock => dock.Prompt,
                CastleStation castle => castle.Prompt,
                UndergroundStation station => station.Prompt,
                _ => null
            };
            if (prompt != null) GUILayout.Label(prompt);
            var riverHud = RiverGameplay.Current?.Hud(player.GetComponent<PlayerMovement>());
            if (riverHud != null) GUILayout.Label(riverHud);
            cooperativePole?.DrawPlaytestHud(player);
            player.GetComponent<MountainReturnLaunch>()?.DrawPlaytestHud();
            CastleGameplay.Current?.DrawHud(player);
            UndergroundGameplay.Current?.DrawHud(player);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Rect DiagnosticsArea => new(Mathf.Max(410, Screen.width - 380), 12, 368, Screen.height - 24);

        void DrawDiagnostics(NetworkObject player)
        {
            GUILayout.BeginArea(DiagnosticsArea);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("DEVELOPMENT / F3");
            if (manager.IsServer && state.IsGameReady && GUILayout.Button("DEV: Return (server phase transition)"))
                state.RequestDevelopmentReturn();
            if (player != null)
            {
                var movement = player.GetComponent<PlayerMovement>();
                GUILayout.Label($"Position {player.transform.position:F2} | Grounded {movement.IsGrounded}");
                var river = RiverGameplay.Current?.DebugHud(movement);
                if (river != null) GUILayout.Label(river);
                CastleGameplay.Current?.DrawDiagnostics(player);
                UndergroundGameplay.Current?.DrawDiagnostics(player);
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
#endif

        public static bool PointerOverMenu(Vector2 point)
        {
            if (Current == null || !Current.MenuVisible) return false;
            var guiPoint = new Vector2(point.x, Screen.height - point.y);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Current.debug && Current.DiagnosticsArea.Contains(guiPoint)) return true;
#endif
            return Current.RoomArea.Contains(guiPoint) ||
                (CastleGameplay.Current?.IsKeypadOpen == true);
        }
    }
}
