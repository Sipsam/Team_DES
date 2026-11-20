using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class GameplayCharacter : NetworkBehaviour
    {
        public Animator animator;
        readonly NetworkVariable<byte> motion = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> carrying = new(false);
        public readonly NetworkVariable<Unity.Collections.FixedString64Bytes> CheckpointName = new();
        public readonly NetworkVariable<int> RespawnCount = new();
        Vector3 previous;
        Transform visualRoot, band;
        Pose visualRest, bandRest;
        Transform leftArm, rightArm;
        Quaternion leftArmRest, rightArmRest;
        bool seatVisual, wasRowing;
        float sampleTime;
        byte rendered = byte.MaxValue;
        public string AnimationState => new[] { "Idle", "Walk", "Run", "Jump", "Walk_Carry" }[Mathf.Min(motion.Value, 4)];
        public override void OnNetworkSpawn()
        {
            InitializeContest();
            previous = transform.position; sampleTime = Time.time;
            animator.applyRootMotion = false;
            if (IsOwner) GetComponent<GameplayInput>().Activate();
            // A small identity band preserves the source materials and avoids model duplication.
            band = transform.Find("IdentityBand");
            visualRoot = transform.Find("VisualRoot");
            visualRest = new Pose(visualRoot.localPosition, visualRoot.localRotation);
            bandRest = new Pose(band.localPosition, band.localRotation);
            foreach (var joint in visualRoot.GetComponentsInChildren<Transform>())
            {
                if (joint.name == "UpperArm.L") leftArm = joint;
                if (joint.name == "UpperArm.R") rightArm = joint;
            }
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId ? Color.white : Color.cyan);
            band.GetComponent<Renderer>().SetPropertyBlock(block);
        }
        public void SetCarryingServer(bool value) { if (IsServer) carrying.Value = value; }
        void LateUpdate()
        {
            if (!IsSpawned) return;
            if (IsOwner && Time.time - sampleTime >= .08f)
            {
                float speed = Vector3.ProjectOnPlane(transform.position - previous, Vector3.up).magnitude / (Time.time - sampleTime);
                var poleMotor = GetComponent<PoleMotor>();
                bool rowing = RiverGameplay.Current is { } river &&
                    river.IsSeated(OwnerClientId);
                motion.Value = rowing ? (byte)0 : poleMotor != null && poleMotor.Attached ? (poleMotor.Orbiting ? (byte)3 : (byte)4) : GetComponent<MountainReturnLaunch>()?.State.Value == LaunchState.Landing ? (byte)0 : (GetComponent<MountainReturnLaunch>()?.InFlight == true || !GetComponent<PlayerMovement>().IsGrounded) ? (byte)3 : speed < .15f ? (byte)0 : carrying.Value ? (byte)4 : speed > 4f ? (byte)2 : (byte)1;
                if (holder.Value != NoPlayer || ownerMotion == ContestMotion.Thrown) motion.Value = 3;
                else if (held.Value != NoPlayer) motion.Value = (byte)(speed < .15f ? 0 : 4);
                previous = transform.position; sampleTime = Time.time;
            }
            RenderRaftRider();
            RenderContest();
            if (rendered != motion.Value)
            {
                rendered = motion.Value;
                animator.CrossFadeInFixedTime(AnimationState, .12f, 0);
            }
        }
        void RenderRaftRider()
        {
            if (RiverGameplay.Current is { } river && river.TrySeatPose(OwnerClientId, out var seat, out float stroke, out int side))
            {
                // Interpolate the visual against the same hull on both peers while the root retains owner authority.
                if (!IsOwner)
                {
                    visualRoot.SetPositionAndRotation(seat.position + seat.rotation * visualRest.position, seat.rotation * visualRest.rotation);
                    band.SetPositionAndRotation(seat.position + seat.rotation * bandRest.position, seat.rotation * bandRest.rotation);
                    seatVisual = true;
                }
                if (!wasRowing)
                {
                    if (leftArm != null) leftArmRest = leftArm.localRotation;
                    if (rightArm != null) rightArmRest = rightArm.localRotation;
                }
                var arm = side == 0 ? leftArm : rightArm;
                if (arm != null)
                {
                    arm.localRotation = side == 0 ? leftArmRest : rightArmRest;
                    float phase = (float)NetworkManager.ServerTime.Time * 6;
                    float angle = -(12 + Mathf.Sin(phase) * 16) * Mathf.Clamp(stroke, -1, 1);
                    arm.RotateAround(arm.position, visualRoot.right, angle);
                }
                wasRowing = true;
                return;
            }

            if (seatVisual)
            {
                visualRoot.SetLocalPositionAndRotation(visualRest.position, visualRest.rotation);
                band.SetLocalPositionAndRotation(bandRest.position, bandRest.rotation);
                seatVisual = false;
            }
            if (wasRowing)
            {
                if (leftArm != null) leftArm.localRotation = leftArmRest;
                if (rightArm != null) rightArm.localRotation = rightArmRest;
            }
            wasRowing = false;
        }

        public void ResetAfterRespawn()
        {
            previous = transform.position; sampleTime = Time.time; rendered = byte.MaxValue;
            if (IsOwner) motion.Value = 0;
            if (IsOwner && Camera.main != null) Camera.main.GetComponent<ThirdPersonCamera>()?.SetTarget(transform.Find("CameraTarget"));
        }
    }
}
