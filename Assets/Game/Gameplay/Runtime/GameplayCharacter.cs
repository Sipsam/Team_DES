using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class GameplayCharacter : NetworkBehaviour
    {
        public Animator animator;
        [SerializeField] MeshRenderer capeRenderer;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color32[] CapeColors =
        {
            new(168, 49, 31, 255), new(205, 154, 54, 255),
            new(32, 130, 101, 255), new(59, 95, 169, 255)
        };
        internal static int CapeColorCount => CapeColors.Length;
        readonly NetworkVariable<byte> capeColor = new(byte.MaxValue);
        readonly NetworkVariable<FixedString64Bytes> nickname = new();
        readonly NetworkVariable<byte> slot = new(byte.MaxValue);
        readonly NetworkVariable<bool> foundationReady = new();
        internal string Nickname => nickname.Value.ToString();
        internal int LobbySlot => slot.Value;
        internal byte CapeColorIndex => capeColor.Value;
        internal bool FoundationReady => foundationReady.Value;
        internal void MarkFoundationReadyServer() => foundationReady.Value = true;
        MaterialPropertyBlock capeProperties;
        internal void SetCapeColorServer(byte color)
        {
            if (IsServer) capeColor.Value = color;
        }

        void ApplyCapeColor(byte previous, byte current)
        {
            ApplyCapeAppearance(capeRenderer, capeProperties, current);
        }

        internal static void ApplyCapeAppearance(MeshRenderer renderer, MaterialPropertyBlock properties, byte index)
        {
            properties.Clear();
            if (index < CapeColorCount) properties.SetColor(BaseColor, CapeColors[index]);
            renderer.SetPropertyBlock(properties);
        }

        static readonly string[] MotionStates = { "Idle", "Walk", "Run", "Jump", "Walk_Carry" };
        readonly NetworkVariable<byte> motion = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> carrying = new(false);
        public readonly NetworkVariable<Unity.Collections.FixedString64Bytes> CheckpointName = new();
        public readonly NetworkVariable<int> RespawnCount = new();
        Vector3 previous;
        Transform visualRoot, head;
        internal Vector3 NameplateAnchor => head ? head.position + Vector3.up * 1.05f :
            (visualRoot ? visualRoot.position : transform.position) + Vector3.up * 2.2f;
        internal Unity.Collections.FixedString64Bytes ApprovedNickname => nickname.Value;
        Pose visualRest;
        Transform leftArm, rightArm;
        Quaternion leftArmRest, rightArmRest;
        bool seatVisual, wasRowing;
        float sampleTime;
        byte rendered = byte.MaxValue;
        byte DisplayMotion => holder.Value != NoPlayer ? (byte)0 : motion.Value;
        public string AnimationState => MotionStates[DisplayMotion];
        bool awaitingFoundationReady;

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        internal void ConfirmFoundationSpawnRpc()
        {
            if (IsOwner) awaitingFoundationReady = true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void ReportFoundationReadyRpc(RpcParams rpcParams = default)
        {
            V3GameplayBootstrap.Current?.state.RegisterFoundationReady(this, rpcParams.Receive.SenderClientId);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                var coordinator = NetworkManager.GetComponent<NetworkSpawnCoordinator>();
                if (coordinator.TryGetSlot(OwnerClientId, out int approvedSlot))
                {
                    slot.Value = (byte)approvedSlot;
                    nickname.Value = new FixedString64Bytes(coordinator.GetApprovedNickname(OwnerClientId));
                }
            }
            awaitingFoundationReady = false;
            InitializeContest();
            BindAudio();
            previous = transform.position; sampleTime = Time.time;
            animator.applyRootMotion = false;
            if (IsOwner) GetComponent<GameplayInput>().Activate();
            visualRoot = transform.Find("VisualRoot");
            visualRest = new Pose(visualRoot.localPosition, visualRoot.localRotation);
            head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (!head)
            {
                foreach (var bone in visualRoot.GetComponentsInChildren<Transform>(true))
                    if (bone.name == "Head") { head = bone; break; }
            }
            BindContestPose();
            capeProperties ??= new MaterialPropertyBlock();
            capeColor.OnValueChanged += ApplyCapeColor;
            ApplyCapeColor(byte.MaxValue, capeColor.Value);
        }
        public void SetCarryingServer(bool value) { if (IsServer) carrying.Value = value; }
        void LateUpdate()
        {
            if (!IsSpawned) return;
            if (awaitingFoundationReady && IsOwner && NetworkManager.IsConnectedClient &&
                V3GameplayBootstrap.Current is { ReadyForNetworkPlay: true } bootstrap &&
                bootstrap.standbyCamera.GetComponent<ThirdPersonCamera>().IsInitializedFor(transform.Find("CameraTarget")))
            {
                awaitingFoundationReady = false;
                ReportFoundationReadyRpc();
            }
            if (IsOwner && Time.time - sampleTime >= .08f)
            {
                float speed = Vector3.ProjectOnPlane(transform.position - previous, Vector3.up).magnitude / (Time.time - sampleTime);
                var poleMotor = GetComponent<PoleMotor>();
                bool rowing = RiverGameplay.Current is { } river &&
                    river.IsSeated(OwnerClientId);
                motion.Value = rowing ? (byte)0 : poleMotor != null && poleMotor.Attached ? (poleMotor.Orbiting ? (byte)3 : (byte)4) : GetComponent<MountainReturnLaunch>()?.State.Value == LaunchState.Landing ? (byte)0 : (GetComponent<MountainReturnLaunch>()?.InFlight == true || !GetComponent<PlayerMovement>().IsGrounded) ? (byte)3 : speed < .15f ? (byte)0 : carrying.Value ? (byte)4 : speed > 4f ? (byte)2 : (byte)1;
                if (holder.Value != NoPlayer) motion.Value = 0;
                else if (ownerMotion == ContestMotion.Thrown) motion.Value = 3;
                previous = transform.position; sampleTime = Time.time;
            }
            RenderRaftRider();
            RenderContest();
            if (rendered != DisplayMotion)
            {
                rendered = DisplayMotion;
                animator.speed = rendered == 2 ? PlayerMovement.SprintSpeedScale : 1f;
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
