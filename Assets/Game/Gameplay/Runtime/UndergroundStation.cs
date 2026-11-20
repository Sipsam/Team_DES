using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed class UndergroundStation : MonoBehaviour, IInteractable
    {
        UndergroundGameplay gameplay;
        int index;
        public string Prompt { get; private set; }
        public bool CanInteract => NetworkManager.Singleton != null && gameplay.CanUse(index, NetworkManager.Singleton.LocalClientId);
        public void Interact(PlayerInteraction player) => gameplay.Interact(index, player);

        internal void Initialize(UndergroundGameplay owner, int id, string prompt)
        {
            gameplay = owner;
            index = id;
            Prompt = prompt;
        }
    }
}
