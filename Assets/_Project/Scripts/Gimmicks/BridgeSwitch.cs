using UnityEngine;

public enum BridgeSwitchId
{
    A,
    B
}

public sealed class BridgeSwitch : MonoBehaviour, IInteractable
{
    [SerializeField] private BridgeController bridgeController;
    [SerializeField] private BridgeSwitchId switchId;
    [SerializeField] private Color inactiveColor = Color.gray;
    [SerializeField] private Color activatedColor = Color.green;

    public bool IsActivated { get; private set; }
    public bool CanInteract => !IsActivated && bridgeController != null && bridgeController.CanActivateSwitch;

    private Renderer switchRenderer;

    private void Awake()
    {
        switchRenderer = GetComponentInChildren<Renderer>();
        UpdateVisual();
    }

    public void Interact(PlayerInteraction player)
    {
        if (IsActivated || bridgeController == null)
        {
            return;
        }

        if (bridgeController.ActivateSwitch(switchId))
        {
            IsActivated = true;
            UpdateVisual();
        }
    }

    private void UpdateVisual()
    {
        if (switchRenderer != null)
        {
            switchRenderer.material.color = IsActivated ? activatedColor : inactiveColor;
        }
    }
}
