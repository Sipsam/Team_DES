public interface IInteractable
{
    bool CanInteract { get; }
    void Interact(PlayerInteraction player);
}
