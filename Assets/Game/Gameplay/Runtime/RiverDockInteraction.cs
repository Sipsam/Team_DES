using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed class RiverDockInteraction : MonoBehaviour, IInteractable
    {
        internal RiverGameplay River;
        internal bool Upper;
        public bool CanInteract => River != null && River.CanBoardAt(Upper);
        public string Prompt => River.Prompt;
        public void Interact(PlayerInteraction player)
        {
            if (CanInteract) River.Board(player, Upper);
        }
    }
}
