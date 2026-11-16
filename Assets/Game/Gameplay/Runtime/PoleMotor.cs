using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    // Only attached characters enter this state. Normal PlayerMovement remains in charge otherwise.
    [DefaultExecutionOrder(10000)]
    public sealed class PoleMotor : MonoBehaviour
    {
        public bool Attached { get; private set; }
        public bool Orbiting => Attached && CooperativePole.Current.Snapshot.Orbit == identity.OwnerClientId;
        NetworkObject identity;
        CharacterController controller;
        PlayerMovement movement;
        bool controllerEnabled;
        bool suspended;
        OwnerNetworkTransform networkTransform;
        public void SuspendForRespawn() { suspended = true; if (Attached) Release(); Attached = false; }
        void Awake() { identity = GetComponent<NetworkObject>(); controller = GetComponent<CharacterController>(); movement = GetComponent<PlayerMovement>(); }
        void LateUpdate()
        {
            var pole = CooperativePole.Current;
            bool next = identity.IsSpawned && pole != null && pole.IsSpawned && pole.Snapshot.Has(identity.OwnerClientId);
            if (!next) suspended = false;
            next &= !suspended;
            if (next && !Attached)
            {
                controllerEnabled = controller.enabled; controller.enabled = false; movement.ResetVerticalVelocity();
                networkTransform = GetComponent<OwnerNetworkTransform>();
                if (networkTransform != null) networkTransform.enabled = false;
            }
            if (next)
            {
                var s = pole.Snapshot;
                Vector3 target = s.left == identity.OwnerClientId ? s.a : s.b;
                transform.position = target;
                Vector3 facing = (s.left == identity.OwnerClientId ? s.b : s.a) - target;
                facing.y = 0;
                if (facing.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(facing);
            }
            if (!next && Attached) Release();
            Attached = next;
        }
        void Release()
        {
            controller.enabled = controllerEnabled;
            movement.ResetVerticalVelocity();
            if (networkTransform != null) networkTransform.enabled = true;
            if (identity.IsSpawned && identity.IsOwner)
                GetComponent<OwnerNetworkTransform>()?.Teleport(transform.position, transform.rotation, transform.localScale);
        }
        void OnDisable() { if (Attached) Release(); Attached = false; }
    }
}
