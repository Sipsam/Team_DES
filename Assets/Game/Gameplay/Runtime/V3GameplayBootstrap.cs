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
        bool ready, debug;
        string code = "";
        string error;
        void OnEnable()
        {
            SceneManager.sceneLoaded += DisableLevelPreview;
            var scene = SceneManager.GetSceneByName(levelScene);
            if (scene.IsValid() && scene.isLoaded) DisableLevelPreview(scene, LoadSceneMode.Additive);
        }
        void OnDisable() => SceneManager.sceneLoaded -= DisableLevelPreview;
        void DisableLevelPreview(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != levelScene) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var preview in root.GetComponentsInChildren<Camera>(true))
                    if (preview.name == "MountainPreviewCamera") preview.enabled = false;
            state.GetComponent<BridgeGameplay>()?.Bind(scene);
        }

        IEnumerator Start()
        {
            if (!SceneManager.GetSceneByName(levelScene).isLoaded)
                yield return SceneManager.LoadSceneAsync(levelScene, LoadSceneMode.Additive);
            DisableLevelPreview(SceneManager.GetSceneByName(levelScene), LoadSceneMode.Additive);
            var start = FindObjectsByType<LevelMarker>(FindObjectsSortMode.None).SingleOrDefault(m => m.id == "Mountain.V3.Start");
            if (start == null) { error = "V3 start marker missing. Session disabled."; yield break; }
            Physics.SyncTransforms();
            if (cooperativePole != null) cooperativePole.ConfigureHome(start.transform.position);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                spawnPoints[i].SetPositionAndRotation(SafeGround(start.transform.position + start.transform.right * (i == 0 ? -1.5f : 1.5f)), start.transform.rotation);
            }
            interactionSample.position = SafeGround(start.transform.position + start.transform.forward * 3);
            standbyCamera.transform.SetPositionAndRotation(start.transform.position + new Vector3(0, 6, -9), Quaternion.Euler(22, 0, 0));
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
        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame) debug = !debug;
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 380, 480), GUI.skin.box);
            GUILayout.Label("TWIN EDGE / V3 GAMEPLAY");
            if (!ready) GUILayout.Label(error ?? "Loading V3 colliders...");
            GUILayout.Label(sessions.CurrentStatus);
            GUILayout.Label("Room: " + sessions.CurrentJoinCode);
            GUI.enabled = ready && !sessions.IsBusy && !manager.IsListening;
            if (GUILayout.Button("Create room (Relay Host)")) _ = sessions.CreateRoomAsync();
            code = GUILayout.TextField(code, 24);
            if (GUILayout.Button("Join room code")) _ = sessions.JoinRoomAsync(code);
            GUI.enabled = !sessions.IsBusy && sessions.HasSession;
            if (GUILayout.Button("Leave room")) _ = sessions.LeaveRoomAsync();
            GUI.enabled = manager.IsServer && state.IsGameReady;
            if (GUILayout.Button("Start game")) state.StartFoundation();
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (GUILayout.Button("DEV: Return (server phase transition)"))
            {
                if (state.AdvanceFoundation(FoundationPhase.SealPending)) state.AdvanceFoundation(FoundationPhase.Return);
            }
#endif
            GUI.enabled = true;
            GUILayout.Label($"Players: {state.ConnectedPlayerCount}/2 | Phase: {state.FoundationPhaseLabel}");
            var player = manager.LocalClient?.PlayerObject;
            if (player != null)
            {
                var character = player.GetComponent<GameplayCharacter>();
                var interaction = player.GetComponent<PlayerInteraction>();
                GUILayout.Label($"Player {player.OwnerClientId} | Checkpoint: {character.CheckpointName.Value}");
                GUILayout.Label($"Respawns: {character.RespawnCount.Value}");
                if (BridgeGameplay.Current is { } bridge && bridge.PlankCount > 0 &&
                    Vector3.Distance(player.transform.position, bridge.StartPoint) <= bridge.Length + 10 &&
                    Mathf.Abs(player.transform.position.x - bridge.StartPoint.x) < 12 &&
                    Mathf.Abs(player.transform.position.y - bridge.StartPoint.y) < 8 &&
                    player.transform.position.z >= bridge.StartPoint.z - 16)
                    GUILayout.Label(bridge.CompletedDeliveries == bridge.TotalDeliveries
                        ? "Bridge complete. The timber creaks."
                        : $"Bridge construction {bridge.CompletedDeliveries}/{bridge.TotalDeliveries}");
                GUILayout.Label(interaction.CurrentTarget switch
                {
                    NetworkInteractionTarget target => target.Prompt,
                    BridgeGameplay construction => construction.Prompt,
                    _ => "E: interact"
                });
                if (debug) GUILayout.Label($"DEBUG (F3)\nPosition: {player.transform.position:F2}\nGrounded: {player.GetComponent<PlayerMovement>().IsGrounded}\nOwner: {player.IsOwner}\nAnimator: {character.AnimationState}\nTarget: {(interaction.CurrentTarget as MonoBehaviour)?.name}");
            }
            GUILayout.Label("WASD / Shift / Space / E | Esc: menu, cancel\nClick world: capture mouse | F3: debug");
            GUILayout.EndArea();
        }
        public static bool PointerOverMenu(Vector2 point) => point.x < 404 && point.y > Screen.height - 504;
    }
}
