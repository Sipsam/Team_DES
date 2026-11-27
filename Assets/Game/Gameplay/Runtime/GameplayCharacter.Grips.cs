using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class GameplayCharacter
    {
        Vector3 stationTarget;
        double stationGestureAt;
        int stationGestureLife;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float nextGripTrace;
#endif

        internal void PresentStationServer(Vector3 target)
        {
            StationGestureRpc(target, RespawnCount.Value, Now);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        void StationGestureRpc(Vector3 target, int life, double occurredAt)
        {
            if (life != RespawnCount.Value || Now - occurredAt >= .32) return;
            stationTarget = target;
            stationGestureAt = occurredAt;
            stationGestureLife = life;
        }

        bool RenderStationGesture()
        {
            float age = (float)(Now - stationGestureAt);
            if (stationGestureAt <= 0 || age is < 0 or >= .32f || stationGestureLife != RespawnCount.Value ||
                IsAirMotion(DisplayMotion)) return false;
            var shoulder = contestJoints[3];
            Vector3 reach = stationTarget - shoulder.position;
            if (reach.sqrMagnitude > 2.5f * 2.5f) return false;
            var fist = contestJoints[5];
            float weight = Mathf.Sin(age / .32f * Mathf.PI);
            Vector3 poseTarget = shoulder.position + Vector3.ClampMagnitude(reach, .45f);
            CaptureAnimatedPose();
            AimGrip(shoulder, contestJoints[4], fist, Vector3.Lerp(fist.position, poseTarget, weight), visualRoot.right);
            return true;
        }

        bool RenderRaftRider()
        {
            if (RiverGameplay.Current is { } river &&
                river.TryOarGrip(OwnerClientId, out var seat, out var left, out var right))
            {
                visualRoot.SetPositionAndRotation(seat.position + seat.rotation * visualRest.position,
                    seat.rotation * visualRest.rotation);
                toolVisual = true;
                ApplyGrips(left, right, "Oar");
                return true;
            }
            RestoreToolVisual();
            return false;
        }

        void RestoreToolVisual()
        {
            if (!toolVisual) return;
            visualRoot.SetLocalPositionAndRotation(visualRest.position, visualRest.rotation);
            toolVisual = false;
        }

        internal void RenderPoleGrip(PoleSnapshot snap)
        {
            if (!IsSpawned || contestPoseApplied || contestJoints == null) return;
            bool left = snap.left == OwnerClientId;
            Vector3 endpoint = left ? snap.a : snap.b;
            Vector3 axis = ((left ? snap.b : snap.a) - endpoint).normalized;
            // Both grips are on the end of the existing pole, measured inward from the endpoint.
            Vector3 grip = endpoint + Vector3.up * 1.1f;
            Vector3 nearGrip = grip + axis * .10f;
            Vector3 farGrip = grip + axis * .29f;
            bool orbiting = snap.Paired && snap.Orbit == OwnerClientId;
            if (orbiting && snap.phase == PolePhase.Rotating)
            {
                // The airborne clip tucks the body away from the pole. Keep the rig within arm's reach.
                Vector3 leftOffset = GripReachOffset(0, nearGrip);
                Vector3 rightOffset = GripReachOffset(3, farGrip);
                Vector3 offset = leftOffset.sqrMagnitude > rightOffset.sqrMagnitude ? leftOffset : rightOffset;
                visualRoot.position += Vector3.ClampMagnitude(offset, .27f);
                toolVisual = true;
            }
            ApplyGrips(nearGrip, farGrip, orbiting ? "PoleOrbit" : "PolePivot");
        }

        Vector3 GripReachOffset(int arm, Vector3 target)
        {
            Vector3 shoulder = contestJoints[arm].position;
            Vector3 elbow = contestJoints[arm + 1].position;
            Vector3 wrist = contestJoints[arm + 2].position;
            float reach = Vector3.Distance(shoulder, elbow) + Vector3.Distance(elbow, wrist) - .008f;
            Vector3 delta = target - shoulder;
            return delta - Vector3.ClampMagnitude(delta, reach);
        }

        bool RenderTimberGrip()
        {
            var bridge = BridgeGameplay.Current;
            if (!bridge || !bridge.TryTimberGrip(OwnerClientId, out var rider, out var left, out var right)) return false;
            // The replicated cargo can lead the interpolated remote root by a few frames.
            Vector3 offset = rider.position - transform.position;
            offset.y = 0;
            visualRoot.SetPositionAndRotation(transform.position + Vector3.ClampMagnitude(offset, .65f) +
                rider.rotation * visualRest.position, rider.rotation * visualRest.rotation);
            toolVisual = true;
            ApplyGrips(left, right, "Timber");
            return true;
        }

        void ApplyGrips(Vector3 left, Vector3 right, string mechanic)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool trace = GameplayEffects.Trace && Time.unscaledTime >= nextGripTrace;
            float beforeLeft = trace ? Vector3.Distance(contestJoints[2].position, left) : 0;
            float beforeRight = trace ? Vector3.Distance(contestJoints[5].position, right) : 0;
#endif
            CaptureAnimatedPose();
            AimGrip(contestJoints[0], contestJoints[1], contestJoints[2], left, -visualRoot.right);
            AimGrip(contestJoints[3], contestJoints[4], contestJoints[5], right, visualRoot.right);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (trace)
            {
                nextGripTrace = Time.unscaledTime + .35f;
                Debug.Log($"POLISH_GRIP {mechanic} slot={LobbySlot} owner={IsOwner} motion={DisplayMotion} " +
                    $"before={beforeLeft:F3}/{beforeRight:F3} after={Vector3.Distance(contestJoints[2].position, left):F3}/{Vector3.Distance(contestJoints[5].position, right):F3}");
            }
#endif
        }

        internal static void AimGrip(Transform shoulder, Transform elbow, Transform fist, Vector3 grip, Vector3 outward)
        {
            Vector3 upper = elbow.position - shoulder.position;
            Vector3 lower = fist.position - elbow.position;
            float a = upper.magnitude, b = lower.magnitude;
            Vector3 reach = grip - shoulder.position;
            if (a < .01f || b < .01f || reach.sqrMagnitude < .0001f) return;
            Vector3 axis = reach.normalized;
            float distance = Mathf.Clamp(reach.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
            float along = (a * a - b * b + distance * distance) / (2 * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            Vector3 elbowPlane = Vector3.ProjectOnPlane(outward * .7f + Vector3.down, axis).normalized;
            Vector3 targetElbow = shoulder.position + axis * along + elbowPlane * bend;
            Quaternion fistRotation = fist.rotation;
            shoulder.rotation = Quaternion.FromToRotation(upper, targetElbow - shoulder.position) * shoulder.rotation;
            Vector3 goal = shoulder.position + axis * distance;
            elbow.rotation = Quaternion.FromToRotation(fist.position - elbow.position, goal - elbow.position) * elbow.rotation;
            fist.rotation = fistRotation;
        }
    }
}
