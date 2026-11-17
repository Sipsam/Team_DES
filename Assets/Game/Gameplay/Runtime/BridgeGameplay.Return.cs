using System;
using System.Collections.Generic;
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

        readonly Dictionary<Collider, int> supportIds = new();
        bool[] hazard;
        Pose summitRetry, dockRetry;
        bool retryPositionsReady;
        int lastReportedPlank = -1;
        float nextContactReport;

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
            supportIds.Clear();
            for (int i = 0; i < plankSupports.Length; i++)
                foreach (var support in plankSupports[i])
                    if (!support.isTrigger) supportIds.Add(support, i);
            retryPositionsReady = false;
            var markers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelMarker>());
            var summit = markers.Single(marker => marker.id == "Mountain.V3.Final");
            var dock = markers.Single(marker => marker.id == "Valley.V3.LowerCheckpoint");
            Physics.SyncTransforms();
            summitRetry = new Pose(V3GameplayBootstrap.SafeGround(summit.transform.position), summit.transform.rotation);
            dockRetry = new Pose(V3GameplayBootstrap.SafeGround(dock.transform.position), dock.transform.rotation);
            retryPositionsReady = true;
        }

        public float BridgeJumpSpeed(bool sprint) => sprint ? sprintJumpSpeed : walkJumpSpeed;

        public bool UsesBridgeJump(Vector3 feet) =>
            ReturnActive && InBridgeAirspace(feet) && Mathf.Abs(feet.y - StartPoint.y) < .3f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

        void TickReturn()
        {
            if (!ReturnActive || !game.CanPlay) return;
            if (hazard == null) hazard = SelectHazards(PlankCount, safeEndPlanks, hazardFraction, Guid.NewGuid().GetHashCode());
            foreach (var peer in NetworkManager.ConnectedClientsList)
            {
                var player = peer.PlayerObject;
                if (player == null) continue;
                var feet = player.transform.position;
                if (!InBridgeAirspace(feet) || Mathf.Abs(feet.y - StartPoint.y) > .15f) continue;
                // The server also checks the replicated feet in case a contact event did not reach it.
                if (Physics.Raycast(feet + Vector3.up * .12f, Vector3.down, out var hit, .28f, ~0, QueryTriggerInteraction.Ignore) &&
                    supportIds.TryGetValue(hit.collider, out int index) && hazard[index])
                    CollapseServer(index);
            }
        }

        public void ReportSupport(PlayerMovement player, ControllerColliderHit hit)
        {
            if (!ReturnActive || !game.CanPlay || hit.normal.y < .75f || hit.moveDirection.y > 0 ||
                !supportIds.TryGetValue(hit.collider, out int index) || IsCollapsed(index)) return;
            if (lastReportedPlank == index && Time.unscaledTime < nextContactReport) return;
            lastReportedPlank = index;
            nextContactReport = Time.unscaledTime + .2f;
            SupportContactRpc(index, player.transform.position);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void SupportContactRpc(int index, Vector3 feet, RpcParams rpc = default)
        {
            if (!ReturnActive || !game.CanPlay || hazard == null || index < 0 || index >= PlankCount ||
                !hazard[index] || removed[index] || !float.IsFinite(feet.x) || !float.IsFinite(feet.y) || !float.IsFinite(feet.z))
                return;
            var player = Player(rpc.Receive.SenderClientId);
            if (player == null || !InBridgeAirspace(feet, .45f) ||
                (feet - player.transform.position).sqrMagnitude > 2.25f || Mathf.Abs(feet.y - StartPoint.y) > .2f)
                return;

            foreach (var support in plankSupports[index])
            {
                if (support.isTrigger || !support.enabled) continue;
                var closest = support.ClosestPoint(feet);
                if (Mathf.Abs(feet.y - closest.y) < .2f &&
                    Vector3.ProjectOnPlane(closest - feet, Vector3.up).sqrMagnitude < .45f * .45f)
                {
                    CollapseServer(index);
                    break;
                }
            }
        }

        public bool TryFallCheckpoint(Vector3 lastDeckFeet, out Pose pose, out string label)
        {
            pose = default;
            label = null;
            if (!retryPositionsReady || !levelScene.isLoaded || !InBridgeAirspace(lastDeckFeet, 1f)) return false;
            bool back = game.CurrentState == GameState.Competition;
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
