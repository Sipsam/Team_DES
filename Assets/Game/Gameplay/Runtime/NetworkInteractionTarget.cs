using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    // Extend this component on gameplay wrappers, keeping imported visual prefabs intact.
    [RequireComponent(typeof(NetworkObject), typeof(Collider))]
    public class NetworkInteractionTarget : NetworkBehaviour, IInteractable
    {
        public float range = 2.5f;
        public float duration = .6f;
        public string prompt = "E: Test interaction / carry pose";
        readonly NetworkVariable<bool> activated = new(false);
        readonly NetworkVariable<ulong> user = new(ulong.MaxValue);
        double endsAt;
        PlayerInteraction activePlayer;
        public bool IsInteracting(PlayerInteraction player) => player != null && user.Value == player.OwnerClientId;
        public bool CanInteract => IsSpawned && user.Value == ulong.MaxValue;
        public string Prompt => user.Value != ulong.MaxValue ? "Interacting... Esc: cancel" : prompt + (activated.Value ? " [ON]" : " [OFF]");
        public void Interact(PlayerInteraction player)
        {
            var state = FindFirstObjectByType<GameStateManager>();
            if (player != null && player.IsOwner && state != null) BeginRpc(player.NetworkObject, state.ActionRevision);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void BeginRpc(NetworkObjectReference reference, uint action, RpcParams rpc = default)
        {
            if (!reference.TryGet(out var obj) || !obj.IsPlayerObject || !obj.IsSpawned || obj.OwnerClientId != rpc.Receive.SenderClientId || !CanInteract) return;
            if (obj.GetComponent<GameplayCharacter>()?.BlocksServerInteraction == true) return;
            var state = FindFirstObjectByType<GameStateManager>();
            if (state == null || !state.AcceptsAction(action) || !InRange(obj.transform.position)) return;
            activePlayer = obj.GetComponent<PlayerInteraction>();
            if (activePlayer == null) return;
            user.Value = obj.OwnerClientId; endsAt = NetworkManager.ServerTime.Time + duration;
            obj.GetComponent<PlayerMovement>().SetGrabbedServer(true, 0);
        }
        public void Cancel(PlayerInteraction player) { if (player != null && player.IsOwner) CancelRpc(); }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void CancelRpc(RpcParams rpc = default) { if (user.Value == rpc.Receive.SenderClientId) End(false); }
        bool InRange(Vector3 position) => (GetComponent<Collider>().ClosestPoint(position + Vector3.up) - position - Vector3.up).sqrMagnitude <= range * range;
        void Update()
        {
            if (!IsSpawned || !IsServer || user.Value == ulong.MaxValue) return;
            var state = FindFirstObjectByType<GameStateManager>();
            if (activePlayer == null || !activePlayer.IsSpawned || !InRange(activePlayer.transform.position) || state == null || !state.CanPlay) { End(false); return; }
            if (NetworkManager.ServerTime.Time >= endsAt) End(true);
        }
        internal void InterruptServer(ulong id)
        {
            if (IsSpawned && IsServer && user.Value == id) End(false);
        }
        void End(bool completed)
        {
            if (activePlayer != null && activePlayer.IsSpawned)
            {
                activePlayer.GetComponent<PlayerMovement>().SetGrabbedServer(false);
                if (completed) { activated.Value = !activated.Value; ApplyServer(activePlayer, activated.Value); }
            }
            activePlayer = null; user.Value = ulong.MaxValue;
        }
        protected virtual void ApplyServer(PlayerInteraction player, bool value) => player.GetComponent<GameplayCharacter>()?.SetCarryingServer(value);
    }
}
