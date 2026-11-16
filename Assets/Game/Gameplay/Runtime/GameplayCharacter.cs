using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed class GameplayCharacter : NetworkBehaviour
    {
        public Animator animator;
        readonly NetworkVariable<byte> motion = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> carrying = new(false);
        public readonly NetworkVariable<Unity.Collections.FixedString64Bytes> CheckpointName = new();
        public readonly NetworkVariable<int> RespawnCount = new();
        Vector3 previous;
        float sampleTime;
        byte rendered = byte.MaxValue;
        public string AnimationState => new[] { "Idle", "Walk", "Run", "Jump", "Walk_Carry" }[Mathf.Min(motion.Value, 4)];
        public override void OnNetworkSpawn()
        {
            previous = transform.position; sampleTime = Time.time;
            animator.applyRootMotion = false;
            if (IsOwner) GetComponent<GameplayInput>().Activate();
            // A small identity band preserves the source materials and avoids model duplication.
            var band = transform.Find("IdentityBand").GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId ? Color.white : Color.cyan);
            band.SetPropertyBlock(block);
        }
        public void SetCarryingServer(bool value) { if (IsServer) carrying.Value = value; }
        void LateUpdate()
        {
            if (!IsSpawned) return;
            if (IsOwner && Time.time - sampleTime >= .08f)
            {
                float speed = Vector3.ProjectOnPlane(transform.position - previous, Vector3.up).magnitude / (Time.time - sampleTime);
                motion.Value = !GetComponent<PlayerMovement>().IsGrounded ? (byte)3 : speed < .15f ? (byte)0 : carrying.Value ? (byte)4 : speed > 4f ? (byte)2 : (byte)1;
                previous = transform.position; sampleTime = Time.time;
            }
            if (rendered != motion.Value)
            {
                rendered = motion.Value;
                animator.CrossFadeInFixedTime(AnimationState, .12f, 0);
            }
        }
        public void ResetAfterRespawn()
        {
            previous = transform.position; sampleTime = Time.time; rendered = byte.MaxValue;
            if (IsOwner) motion.Value = 0;
            if (IsOwner && Camera.main != null) Camera.main.GetComponent<ThirdPersonCamera>()?.SetTarget(transform.Find("CameraTarget"));
        }
    }
}
