#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class RiverGameplay
    {
        bool recoveryFixture;
        byte recoverySoloHit;
        Vector3 recoverySoloRock;
        internal void EnableRecoveryFixture()
        {
            if (IsServer) recoveryFixture = true;
        }

        internal Vector3 RecoveryFixtureRock(bool outbound)
        {
            if (!outbound && recoverySoloHit > 0) return recoverySoloRock;
            float z = outbound ? channel.LowerDock.bounds.center.z + 29 : channel.UpperDock.bounds.center.z - 29;
            var sample = channel.Sample(z);
            return channel.Point(z, outbound ? -3 : Berth(1).x + 5 - sample.center.x);
        }

        Vector3 TickRecoveryFixture()
        {
            var s = Read(1);
            if (Returning && s.Status == RiverRaftStatus.Sailing && s.Hits > recoverySoloHit)
            {
                recoverySoloHit = s.Hits;
                var center = channel.Sample(s.Position.z);
                float z = s.Position.z - 24;
                float offset = s.Position.x - center.center.x;
                recoverySoloRock = channel.Point(z, offset);
            }
            return RecoveryFixtureRock(!Returning);
        }

        internal bool InWhirlpool(int index)
        {
            Vector2 p = Flat(Read(index).Position);
            for (int i = 0; i < hazardFeed.Count; i++)
                if (hazards[i].Kind == HazardKind.Whirlpool && hazardFeed[i].Phase == HazardPhase.Active &&
                    Vector2.Distance(p, Flat(hazardFeed[i].Position)) < hazards[i].Radius) return true;
            return false;
        }

        internal bool TryNearbyWarning(Vector3 p, out Vector3 aim)
        {
            for (int i = 0; i < hazardFeed.Count; i++)
            {
                var state = hazardFeed[i];
                if (state.Phase == HazardPhase.Warning && Vector3.Distance(state.Aim, p) < 40)
                {
                    aim = state.Aim;
                    return true;
                }
            }
            aim = default;
            return false;
        }

        internal Vector3 ReplayCourse(float z, bool outbound)
        {
            var sample = channel.Sample(z);
            var dock = outbound ? channel.UpperDock.bounds : channel.LowerDock.bounds;
            float berthX = dock.min.x - Radius(outbound ? 0 : 1) - .9f;
            float distance = Mathf.Abs(z - dock.center.z);
            float besideDock = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(outbound ? 72 : 115, 9, distance));
            float x = Mathf.Lerp(sample.center.x, berthX, besideDock);
            float yaw = Quaternion.LookRotation(sample.upstream * (outbound ? 1 : -1)).eulerAngles.y;
            return new Vector3(x, yaw, distance < 16 && !outbound ? 0 : sample.upstream.x / sample.upstream.z);
        }

#if UNITY_EDITOR
        public void ValidateSavedChannel(UnityEngine.SceneManagement.Scene scene, TwinEdge.Levels.ValleyConfigV3 config, out string report)
        {
            Physics.SyncTransforms();
            Bind(scene);
            if (hulls[0].Oars.Length != 2 || hazards[2].Jaw == null || warningMaterial == null)
                throw new System.InvalidOperationException("V3 river is missing working oars, jaw or URP warning material.");
            int samples = 0;
            float minWidth = float.MaxValue, minDepth = float.MaxValue;
            for (float z = channel.Start + 4; z <= channel.End - 4; z += 2)
            {
                var row = channel.Sample(z);
                if (row.upstream.z <= 0 || row.halfWidth < sharedRadius * 3 ||
                    !Physics.Raycast(row.center + Vector3.up * .2f, Vector3.down, out var bed, 30,
                        ~0, QueryTriggerInteraction.Ignore) || bed.distance < sharedRadius)
                    throw new System.InvalidOperationException($"Blocked saved river center at {row.center}.");
                minWidth = Mathf.Min(minWidth, row.halfWidth * 2);
                minDepth = Mathf.Min(minDepth, bed.distance - .2f);
                samples++;
            }
            for (int i = 0; i < hulls.Length; i++)
            {
                var point = Berth(i);
                var row = channel.Sample(point.z);
                if (Mathf.Abs(point.x - row.center.x) + Radius(i) > row.halfWidth)
                    throw new System.InvalidOperationException($"Raft {i} berth is outside the saved water polygon.");
                var dockPosition = Landing(i > 0, i == 2 ? 1 : 0).position;
                if (!Physics.Raycast(dockPosition + Vector3.up * .4f, Vector3.down, out var hit, 1f,
                        ~0, QueryTriggerInteraction.Ignore) || hit.collider != (i == 0 ? channel.LowerDock : channel.UpperDock))
                    throw new System.InvalidOperationException($"Raft {i} cannot disembark on the saved dock collider.");
            }
            ValidateDockAccess(false, channel.LowerCheckpoint);
            ValidateDockAccess(true, channel.UpperCheckpoint);
            if (channel.Length < 750 || config.river[0].z != channel.Start || config.river[^1].z != channel.End)
                throw new System.InvalidOperationException("Saved water and ValleyConfigV3 endpoints differ.");
            if (channel.WaterStart != config.downstreamEnd.z || channel.WaterEnd != config.upstreamEnd.z)
                throw new System.InvalidOperationException("Visual river endpoints differ from the configured extensions.");
            float maxGrade = 0;
            Vector3 previous = channel.Sample(channel.WaterStart).center;
            for (float z = channel.WaterStart + 2; z <= channel.WaterEnd; z += 2)
            {
                var center = channel.Sample(z).center;
                var delta = center - previous;
                if (delta.y <= 0) throw new System.InvalidOperationException("River grade reverses at " + center);
                maxGrade = Mathf.Max(maxGrade, 100 * delta.y / new Vector2(delta.x, delta.z).magnitude);
                previous = center;
            }
            float rise = channel.Sample(channel.End).center.y - channel.Sample(channel.Start).center.y;
            report = $"RIVER STATIC PASS. {samples} bed and ribbon samples. Center distance {channel.Length:F1} m. " +
                $"Full water length {channel.WaterLength:F1}m; playable rise {rise:F2}m, mean grade {100 * rise / channel.Length:F2}%, max sampled grade {maxGrade:F2}%. " +
                $"Deck Y lower {channel.LowerDock.bounds.max.y:F2}, upper {channel.UpperDock.bounds.max.y:F2}. " +
                $"Minimum water width {minWidth:F1} m, minimum center depth {minDepth:F1} m. " +
                $"Lower {channel.Start:F0} to upper {channel.End:F0} in +Z, current in -Z. " +
                $"Both docks support dry feet and connect to their checkpoints, three berths are inside the ribbon, hazard and model wrappers resolve. " +
                $"Lower checkpoint {channel.LowerCheckpoint}, upper checkpoint {channel.UpperCheckpoint}.";
        }
        void ValidateDockAccess(bool upper, Vector3 marker)
        {
            Vector3 start = Landing(upper, 0).position;
            Vector3 checkpoint = V3GameplayBootstrap.SafeGround(marker);
            const float step = .6f;
            const int radius = 36;
            const int grid = radius * 2 + 1;
            var heights = new float[grid, grid];
            var explored = new bool[grid, grid];
            var open = new System.Collections.Generic.Queue<Vector2Int>();
            for (int x = 0; x < grid; x++)
                for (int z = 0; z < grid; z++)
                {
                    Vector3 p = start + new Vector3((x - radius) * step, 0, (z - radius) * step);
                    bool supported = Physics.Raycast(p + Vector3.up * 10, Vector3.down, out var floor, 16,
                        ~0, QueryTriggerInteraction.Ignore) && floor.normal.y > .7f &&
                        floor.point.y > channel.Sample(p.z).center.y + .15f;
                    heights[x, z] = supported ? floor.point.y : float.PositiveInfinity;
                }

            var origin = new Vector2Int(radius, radius);
            open.Enqueue(origin);
            explored[radius, radius] = true;
            var directions = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            while (open.Count > 0)
            {
                var current = open.Dequeue();
                var p = start + new Vector3((current.x - radius) * step, 0, (current.y - radius) * step);
                p.y = heights[current.x, current.y];
                if (Vector3.Distance(p, checkpoint) < 1.25f) return;
                foreach (var direction in directions)
                {
                    var next = current + direction;
                    if (next.x < 0 || next.x >= grid || next.y < 0 || next.y >= grid ||
                        explored[next.x, next.y] || Mathf.Abs(heights[next.x, next.y] - p.y) > .42f) continue;
                    explored[next.x, next.y] = true;
                    open.Enqueue(next);
                }
            }
            throw new System.InvalidOperationException($"No dry walkable approach from the {(upper ? "upper" : "lower")} dock to its V3 checkpoint.");
        }
#endif
    }
}
#endif
