using TwinEdge.Audio;
using System;
using System.Linq;
using TwinEdge.Levels;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Gameplay
{
    public sealed partial class BridgeGameplay
    {
        [SerializeField, Min(.5f)] float fallDepth = 1.65f;

        [Header("Bridge return tuning")]
        [SerializeField, Range(0, .4f)] float hazardFraction = .20f;
        [SerializeField, Min(1)] int safeEndPlanks = 5;
        [SerializeField, Min(.1f)] float walkJumpSpeed = 3.05f;
        [SerializeField, Min(.1f)] float sprintJumpSpeed = 5.25f;

        const int TriggerReach = 3;
        const float WarningSeconds = .65f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock warningProperties;
        NetworkList<int> warning;
        float[] collapseAt;
        bool[] hazard;
        Pose summitRetry, dockRetry;
        Checkpoint returnApproach;
        bool retryPositionsReady;

        public bool ReturnActive => IsSpawned && InstalledCount == PlankCount && PlankCount > 0 && levelScene.isLoaded &&
            game != null && game.FoundationPhaseLabel == nameof(FoundationPhase.Return);
        public bool InBridgeAirspace(Vector3 position, float margin = 0)
        {
            if (planks == null || !levelScene.isLoaded) return false;
            var delta = position - StartPoint;
            float along = Vector3.Dot(delta, Forward);
            return along >= -margin && along <= Length + margin &&
                Mathf.Abs(Vector3.Dot(delta, BridgeRight)) <= layout.bridgeWidth * .5f + margin;
        }

        public bool IsBelowSpan(Vector3 feet)
        {
            if (!InBridgeAirspace(feet, 3f) || feet.y >= StartPoint.y - fallDepth) return false;
            float along = Vector3.Dot(feet - StartPoint, Forward);
            // The Canyon landing overlaps the last few metres of the deck.
            return along > 0 && along < Length - 4f;
        }

        void BindReturn(Scene scene)
        {
            hazard = null;
            collapseAt = new float[PlankCount];
            retryPositionsReady = false;
            var markers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelMarker>());
            var summit = markers.Single(marker => marker.id == "Mountain.V3.Final");
            var dock = markers.Single(marker => marker.id == "Valley.V3.LowerCheckpoint");
            Physics.SyncTransforms();
            summitRetry = new Pose(V3GameplayBootstrap.SafeGround(summit.transform.position), summit.transform.rotation);
            dockRetry = new Pose(V3GameplayBootstrap.SafeGround(dock.transform.position), dock.transform.rotation);
            if (returnApproach != null) Destroy(returnApproach.gameObject);
            returnApproach = V3GameplayBootstrap.CreateFoundationCheckpoint(transform, GetComponent<RespawnManager>(),
                "Return / Canyon bridge approach", FoundationPhase.Return, EndPoint + Forward * 4,
                Quaternion.LookRotation(-Forward), new Vector3(layout.walkWidth, 3, 5), 1.5f,
                progressGate: () => ReturnActive);
            retryPositionsReady = true;
        }

        internal bool NearbyWarning(Vector3 feet)
        {
            if (!ReturnActive) return false;
            int current = Mathf.RoundToInt(Vector3.Dot(feet - StartPoint, Forward) / Pitch);
            for (int i = 0; i < warning.Count; i++)
                if (Mathf.Abs(warning[i] - current) <= TriggerReach) return true;
            return false;
        }

        public float BridgeJumpSpeed(bool sprint) => sprint ? sprintJumpSpeed : walkJumpSpeed;

        public bool UsesBridgeJump(Vector3 feet) =>
            ReturnActive && InBridgeAirspace(feet) && Mathf.Abs(feet.y - StartPoint.y) < .3f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal bool IsWarningForTest(int index) => warning.Contains(index);
        internal int CollapseCountForTest => collapsed.Count;
        internal bool[] ReadServerHazardsForTest() => IsServer && hazard != null ? (bool[])hazard.Clone() : null;
#endif

        public static bool[] SelectHazards(int count, int safeEnds, float fraction, int seed)
        {
            var choices = new bool[count];
            var rng = new System.Random(seed);
            var candidates = Enumerable.Range(safeEnds, Mathf.Max(0, count - safeEnds * 2)).ToArray();
            int remaining = Mathf.Min(candidates.Length, Mathf.RoundToInt(count * fraction));
            for (int i = candidates.Length - 1; i >= 0 && remaining > 0; i--)
            {
                int other = rng.Next(i + 1);
                int index = candidates[other];
                candidates[other] = candidates[i];
                choices[index] = true;
                bool triple = false;
                for (int start = Mathf.Max(0, index - 2); start <= index && start + 2 < count; start++)
                    triple |= choices[start] && choices[start + 1] && choices[start + 2];
                if (triple) choices[index] = false;
                else remaining--;
            }
            return choices;
        }

        internal bool TryGetReturnSlot(Vector3 feet, out int slot)
        {
            slot = -1;
            if (!InBridgeAirspace(feet) || Mathf.Abs(feet.y - StartPoint.y) > .3f) return false;
            // Map even a seam or missing tread to its ordered deck slot.
            slot = Mathf.Min(PlankCount - 1, Mathf.FloorToInt(Vector3.Dot(feet - StartPoint, Forward) / Pitch));
            return true;
        }

        void TickReturn()
        {
            if (!ReturnActive || !game.CanPlay) return;
            if (hazard == null) hazard = SelectHazards(PlankCount, safeEndPlanks, hazardFraction, Guid.NewGuid().GetHashCode());
            float now = Time.time;
            foreach (var peer in NetworkManager.ConnectedClientsList)
            {
                var player = peer.PlayerObject;
                if (player == null) continue;
                if (!TryGetReturnSlot(player.transform.position, out int slot)) continue;
                int end = Mathf.Min(PlankCount - 1, slot + TriggerReach);
                for (int index = Mathf.Max(0, slot - TriggerReach); index <= end; index++)
                {
                    if (!hazard[index] || removed[index] || collapseAt[index] != 0) continue;
                    collapseAt[index] = now + WarningSeconds;
                    warning.Add(index);
                }
            }

            for (int i = warning.Count - 1; i >= 0; i--)
            {
                int index = warning[i];
                if (now < collapseAt[index]) continue;
                collapsed.Add(index);
                warning.RemoveAt(i);
            }
        }

        void OnWarning(NetworkListEvent<int> change)
        {
            if (planks == null || !levelScene.isLoaded) return;
            if (change.Type == NetworkListEvent<int>.EventType.Add)
            {
                SetWarning(change.Value, true);
                game.PlayLiveCue(AudioCueId.BridgeWarning, planks[change.Value].transform.position);
            }
            else if (change.Type == NetworkListEvent<int>.EventType.RemoveAt)
                SetWarning(change.Value, false);
            else RefreshDeck();
        }

        void SetWarning(int index, bool active)
        {
            foreach (var renderer in plankRenderers[index])
            {
                if (active)
                {
                    renderer.GetPropertyBlock(warningProperties);
                    warningProperties.SetColor(BaseColor, new Color(1f, .35f, .08f));
                    renderer.SetPropertyBlock(warningProperties);
                }
                else renderer.SetPropertyBlock(null);
            }
        }

        internal bool TryFallCheckpoint(PlayerMovement player, Vector3 lastDeckFeet, out Pose pose, out string label)
        {
            pose = default;
            label = null;
            if (!retryPositionsReady || !levelScene.isLoaded || !InBridgeAirspace(lastDeckFeet, 1f)) return false;
            if (ReturnActive && NetworkManager.GetComponent<NetworkSpawnCoordinator>().TryGetSlot(player.OwnerClientId, out int slot))
            {
                pose = returnApproach.PoseForSlot(slot);
                label = returnApproach.name;
                return true;
            }
            bool back = game.Phase == FoundationPhase.Return;
            pose = back ? dockRetry : summitRetry;
            label = back ? "Lower dock" : "Big summit";
            return true;
        }

        public bool TryGetResumeSpawn(out Pose pose, out string label)
        {
            pose = default;
            label = null;
            if (!IsServer || !retryPositionsReady || !levelScene.isLoaded || !constructionReached) return false;
            bool back = game.CurrentState == GameState.Competition;
            var point = back ? dockRetry : summitRetry;
            pose = new Pose(point.position + Vector3.right * 1.35f, point.rotation);
            label = back ? "Lower dock" : "Big summit";
            return true;
        }
    }
}
