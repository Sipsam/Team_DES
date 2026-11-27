using TwinEdge.Audio;
using System.Collections;
using System.Linq;
using TwinEdge.Levels;
using TwinEdge.Presentation;
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
        [SerializeField] Font presentationFont;
        [SerializeField] Sprite presentationEmblem;
        [SerializeField] internal AnimationClip victory;
        [SerializeField] internal AnimationClip defeat;
        [SerializeField, Min(1)] float finishWidth = 4.5f;
        [SerializeField, Min(.1f)] float finishDwell = .35f;
        BoxCollider finish;
        ulong finishCandidate = ulong.MaxValue;
        double finishEnteredAt;
        bool ready;
        bool networkReload;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool debug;
#endif
        public bool WorldReady => ready;
        public bool WorldFailed { get; private set; }
        public static V3GameplayBootstrap Current { get; private set; }

        public PresentationRoot Presentation { get; private set; }
        public SessionPresentation SessionUI { get; private set; }

        void Awake()
        {
            // The authored session root starts inactive. NGO keeps the first instance across loads.
            if (NetworkManager.Singleton && NetworkManager.Singleton != manager)
            {
                Destroy(manager.gameObject);
                manager = NetworkManager.Singleton;
                sessions = manager.GetComponent<MultiplayerSessionManager>();
                networkReload = manager.IsListening;
            }
            else manager.gameObject.SetActive(true);
            manager.GetComponent<NetworkSpawnCoordinator>().BindFoundationSpawns(spawnPoints);
        }

        void OnEnable()
        {
            Current = this;
            if (!GetComponent<AudioService>()) gameObject.AddComponent<AudioService>();
            AudioService.UseListener(standbyCamera.GetComponent<AudioListener>());
            Presentation = PresentationRoot.Create(gameObject.scene);
            SessionUI = Presentation.gameObject.AddComponent<SessionPresentation>();
            SessionUI.Initialize(this, presentationFont, presentationEmblem);
            SceneManager.sceneLoaded += DisableLevelPreview;
            var scene = SceneManager.GetSceneByName(levelScene);
            if (scene.IsValid() && scene.isLoaded) DisableLevelPreview(scene, LoadSceneMode.Additive);
        }
        void OnDisable()
        {
            SceneManager.sceneLoaded -= DisableLevelPreview;
            AudioService.StopSession(keepMenuMusic: false);
            if (Current == this) Current = null;
            if (Presentation)
            {
                Presentation.gameObject.SetActive(false);
                Destroy(Presentation.gameObject);
                Presentation = null;
            }
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
            while (networkReload && manager.IsListening && !SceneManager.GetSceneByName(levelScene).isLoaded)
                yield return null;
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
                WorldFailed = true;
                Debug.LogError("V3 mountain route markers missing. Session disabled.", this);
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
            var poleStart = CreateFoundationCheckpoint(transform, respawns, "Pole start", FoundationPhase.Outbound,
                start.transform.position, Quaternion.identity, new Vector3(cooperativePole.length + 4, 3, 6),
                cooperativePole.length * .5f - 2, -2);
            poleStart.RequireSummitLanding(cooperativePole, cooperativePole.mountain.start.id);
            // Keep the existing route checkpoints bound to their saved markers.
            AddCheckpoint("Mountain.V3.Final", "Big summit", CheckpointPhase.Cooperation);
            AddCheckpoint("Valley.V3.LowerCheckpoint", "Lower dock", CheckpointPhase.Cooperation);
            AddCheckpoint("Valley.V3.UpperCheckpoint", "Upper dock / castle rooms", CheckpointPhase.Cooperation);
            var fortress = FindObjectsByType<FortressSectionV3>(FindObjectsSortMode.None).FirstOrDefault();
            if (fortress != null) CreateCheckpoint("Underground start", fortress.config.origin + fortress.config.start, CheckpointPhase.Cooperation);
            if (fortress != null)
            {
                Vector3 origin = fortress.config.origin;
                Vector3 cameraPosition = origin + new Vector3(205, 135, 210);
                Vector3 aim = origin + new Vector3(-160, 15, -190);
                SessionUI.SetWorldCamera(new Pose(cameraPosition, Quaternion.LookRotation(aim - cameraPosition)));
            }
            var colliders = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
            respawns.ConfigureFallRecovery(colliders.Min(c => c.bounds.min.y) - 15f);
            ready = true;
            var section = FindFirstObjectByType<MountainSectionV3>();
            if (section != null)
            {
                var boundaries = new GameObject("Mountain Landing Boundaries");
                boundaries.transform.SetParent(transform, false);
                yield return boundaries.AddComponent<SummitBoundaries>()
                    .Build(cooperativePole, section, manager.NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>());
            }
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
            if (state.AcceptsGameplay && state.Phase == FoundationPhase.Return &&
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

        internal static Checkpoint CreateFoundationCheckpoint(Transform parent, RespawnManager manager,
            string label, FoundationPhase phase, Vector3 center, Quaternion facing, Vector3 size,
            float slotSpacing, float slotDepth = 0, System.Func<bool> progressGate = null)
        {
            var poses = new Pose[2];
            for (int slot = 0; slot < poses.Length; slot++)
            {
                Vector3 candidate = center + facing * new Vector3(slot == 0 ? -slotSpacing : slotSpacing, 0, slotDepth);
                if (!Physics.Raycast(candidate + Vector3.up * 1.25f, Vector3.down, out var hit, 3,
                        ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < .7f)
                    throw new System.InvalidOperationException($"No walkable V3 support at {label} slot {slot}: {candidate}");
                poses[slot] = new Pose(hit.point + Vector3.up * .12f, facing);
            }

            var obj = new GameObject("GameplayCheckpoint_" + label);
            obj.transform.SetParent(parent);
            obj.transform.SetPositionAndRotation(center, facing);
            var box = obj.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var checkpoint = obj.AddComponent<Checkpoint>();
            checkpoint.ConfigureFoundation(manager, phase, poses, progressGate);
            return checkpoint;
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
        void OnGUI()
        {
            if (debug) DrawDiagnostics(manager.LocalClient?.PlayerObject);
        }

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
                cooperativePole?.DrawDiagnostics();
                CastleGameplay.Current?.DrawDiagnostics(player);
                UndergroundGameplay.Current?.DrawDiagnostics(player);
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
#endif

        public static bool PointerOverMenu(Vector2 point)
        {
            if (Current == null || PresentationRoot.BlocksGameplay) return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var guiPoint = new Vector2(point.x, Screen.height - point.y);
            if (Current.debug && Current.DiagnosticsArea.Contains(guiPoint)) return true;
#endif
            return false;
        }
    }
}
