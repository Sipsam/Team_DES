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

        readonly NetworkVariable<byte> motion = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> carrying = new(false);
        public readonly NetworkVariable<Unity.Collections.FixedString64Bytes> CheckpointName = new();
        public readonly NetworkVariable<int> RespawnCount = new();
        Transform visualRoot, head;
        internal Vector3 NameplateAnchor => head ? head.position + Vector3.up * 1.05f :
            (visualRoot ? visualRoot.position : transform.position) + Vector3.up * 2.2f;
        internal Unity.Collections.FixedString64Bytes ApprovedNickname => nickname.Value;
        Pose visualRest;
        bool toolVisual;
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
            ResetMotionPresentation();
            BindEffects();
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
            bool seated = RenderRaftRider();
            SampleOwnerMotion(seated);
            UpdateMovementAudio();
            bool specialPose = seated || Attached;
            if (!specialPose)
            {
                RenderContest();
                specialPose = contestPoseApplied || RenderTimberGrip() || RenderStationGesture();
            }
            RenderMotion(specialPose);
            RenderMovementEffects(seated);
            RenderAttackTrail();
        }

        public void ResetAfterRespawn()
        {
            RestoreContestPose();
            RestoreToolVisual();
            stationGestureAt = 0;
            ResetMotionPresentation();
            if (IsOwner) motion.Value = 0;
            if (IsOwner && Camera.main != null) Camera.main.GetComponent<ThirdPersonCamera>()?.SetTarget(transform.Find("CameraTarget"));
        }
    }
}
