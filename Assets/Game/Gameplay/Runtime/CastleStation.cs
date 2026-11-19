using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed class CastleStation : MonoBehaviour, IInteractable
    {
        internal CastleGameplay Castle;
        internal int Id, Slot, Stage, Value;
        internal CastleGameplay.Action Kind;
        public string Prompt { get; internal set; }

        public bool CanInteract => Unity.Netcode.NetworkManager.Singleton is { IsListening: true } manager &&
            Castle.CanUse(this, Castle.Slot(manager.LocalClientId));
        public void Interact(PlayerInteraction player) => Castle.Interact(this, player);
    }
}
