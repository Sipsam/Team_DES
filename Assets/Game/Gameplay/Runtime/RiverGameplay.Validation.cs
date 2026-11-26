#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class RiverGameplay
    {
        bool recoveryFixture;
        readonly int[] recoveryBlockedContacts = new int[3];
        byte recoverySoloHit;
        Vector3 recoverySoloRock;
        internal void EnableRecoveryFixture()
        {
            if (IsServer) recoveryFixture = true;
        }

        internal int RecoveryBlockedContacts(int index) => recoveryBlockedContacts[index];
        internal int ReplicatedHazardCount => hazardFeed.Count;

        internal bool HullFlashVisible(int index)
        {
            var materials = hulls[index].FlashMaterials;
            if (materials.Length == 0 || !hulls[index].View.gameObject.activeSelf) return false;
            var block = new MaterialPropertyBlock();
            var (renderer, slot) = materials[0];
            renderer.GetPropertyBlock(block, slot);
            return block.GetColor(BaseColor) == DamageColor;
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

        internal Vector3 ReplayCourse(float z, bool outbound, bool avoidReturnHazards)
        {
            var sample = channel.Sample(z);
            var dock = outbound ? channel.UpperDock.bounds : channel.LowerDock.bounds;
            float berthX = dock.min.x - Radius(outbound ? 0 : 1) - .9f;
            float distance = Mathf.Abs(z - dock.center.z);
            float besideDock = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(outbound ? 72 : 115, 9, distance));
            float x = Mathf.Lerp(sample.center.x, berthX, besideDock);
            if (!outbound && avoidReturnHazards)
            {
                // Steer wide of the two upper predators and cross to the inside of the bend before the rock.
                x += ReturnClearance(z, .82f, -10, 58);
                x += ReturnClearance(z, .57f, -10, 44);
                x += ReturnClearance(z, .48f, 10, 46);
                x += ReturnClearance(z, .31f, 7, 26);
            }
            float yaw = Quaternion.LookRotation(sample.upstream * (outbound ? 1 : -1)).eulerAngles.y;
            return new Vector3(x, yaw, distance < 16 && !outbound ? 0 : sample.upstream.x / sample.upstream.z);
        }

        float ReturnClearance(float z, float fraction, float offset, float approach)
        {
            float center = Mathf.Lerp(channel.Start, channel.End, fraction);
            return offset * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(10, approach, Mathf.Abs(z - center))));
        }

#if UNITY_EDITOR
        public void ValidateSavedChannel(UnityEngine.SceneManagement.Scene scene, TwinEdge.Levels.ValleyConfigV3 config, out string report)
        {
            Physics.SyncTransforms();
            Bind(scene);
            if (hulls[0].Oars.Length != 2 || hazards[2].Jaw == null || warningMaterial == null ||
                System.Array.Exists(hulls, hull => hull.FlashMaterials.Length == 0))
                throw new System.InvalidOperationException("V3 river is missing working oars, jaw, warning material or raft tint slots.");
            ValidateHazardLayout();
            ValidateWaterFlow();
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
                $"Lower {channel.Start:F0} to upper {channel.End:F0} in +Z, current and scrolling water in -Z. " +
                $"{hazards.Length} hazards in both phases, with clear dock approaches and a shared-width passive avoidance route. " +
                $"Both docks support dry feet and connect to their checkpoints, three berths are inside the ribbon, hazard and model wrappers resolve. " +
                $"Lower checkpoint {channel.LowerCheckpoint}, upper checkpoint {channel.UpperCheckpoint}.";
        }

        void ValidateWaterFlow()
        {
            var filter = channel.WaterRenderer.GetComponent<MeshFilter>();
            var mesh = filter.sharedMesh;
            var vertices = mesh.vertices;
            var uv = mesh.uv;
            var water = channel.WaterRenderer.sharedMaterial;
            if (!water.IsKeywordEnabled("_RIVER_FLOW") || water.GetTextureScale("_BaseMap").y <= 0 ||
                uv.Length != vertices.Length || outboundFlow <= 0 || returnFlow <= outboundFlow)
                throw new System.InvalidOperationException("V3 river flow must use positive ribbon V scale and faster Return current.");
            for (int i = 2; i < vertices.Length; i += 2)
                if (filter.transform.TransformPoint(vertices[i]).z <= filter.transform.TransformPoint(vertices[i - 2]).z ||
                    uv[i].y <= uv[i - 2].y || !Mathf.Approximately(uv[i].y, uv[i + 1].y))
                    throw new System.InvalidOperationException("Saved water V must increase upstream on both banks.");
        }

        void ValidateHazardLayout()
        {
            if (hazards.Length != 15)
                throw new System.InvalidOperationException("The V3 river course must have 15 hazards in both phases.");
            foreach (var hazard in hazards)
            {
                float anchor = HazardZ(hazard);
                float minZ = anchor - (hazard.Kind == HazardKind.Driftwood ? 50 : 0) - hazard.Radius;
                float maxZ = anchor + (hazard.Kind == HazardKind.Driftwood ? 28 : 0) + hazard.Radius;
                if (minZ < channel.LowerDock.bounds.max.z + 40 || maxZ > channel.UpperDock.bounds.min.z - 40)
                    throw new System.InvalidOperationException($"Hazard {hazard.Kind} crowds a berth near Z={anchor:F1}.");
            }

            // Sample a connected path in ribbon offsets. Reserve the full active drift sweep and a metre of clearance.
            // Predators and whirlpools still require timing or steering in the live crossing.
            const int steps = 81, mid = steps / 2;
            var reachable = new bool[steps];
            var rowReachable = new bool[steps];
            reachable[mid] = true;
            for (float z = channel.Start; z <= channel.End; z++)
            {
                var row = channel.Sample(z);
                float padding = (sharedRadius + 1) / Mathf.Max(.45f, row.upstream.z);
                for (int slot = 0; slot < steps; slot++)
                {
                    int offset = slot - mid;
                    rowReachable[slot] = false;
                    if (Mathf.Abs(offset) + padding >= row.halfWidth ||
                        !(reachable[slot] || slot > 0 && reachable[slot - 1] ||
                          slot + 1 < steps && reachable[slot + 1])) continue;
                    bool clear = true;
                    foreach (var hazard in hazards)
                    {
                        float anchor = HazardZ(hazard);
                        if (hazard.Kind == HazardKind.Rock)
                            clear &= Vector2.Distance(Flat(row.center + Vector3.right * offset),
                                Flat(channel.Point(anchor, hazard.Offset))) > hazard.Radius + sharedRadius + 1;
                        if (hazard.Kind == HazardKind.Driftwood && z >= anchor - 50 - hazard.Radius - sharedRadius - 1 &&
                            z <= anchor + 28 + hazard.Radius + sharedRadius + 1)
                            clear &= Mathf.Abs(offset - hazard.Offset) > 5 + padding + hazard.Radius / row.upstream.z;
                    }
                    rowReachable[slot] = clear;
                }
                (reachable, rowReachable) = (rowReachable, reachable);
            }
            if (!reachable[mid])
                throw new System.InvalidOperationException("No connected shared-width route through passive obstacles.");
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
