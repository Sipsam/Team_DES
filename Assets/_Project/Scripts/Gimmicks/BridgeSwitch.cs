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

    public BridgeSwitchId SwitchId => switchId;
    public bool IsActivated => bridgeController != null && bridgeController.IsSwitchActivated(switchId);
    public bool CanInteract => !IsActivated && bridgeController != null && bridgeController.CurrentState == BridgeState.Incomplete;

    private Renderer switchRenderer;

    private void Awake()
    {
        switchRenderer = GetComponentInChildren<Renderer>();
        UpdateVisual();
    }

    private void OnEnable()
    {
        if (bridgeController != null)
        {
            bridgeController.SwitchStateChanged += HandleSwitchStateChanged;
        }
    }

    private void OnDisable()
    {
        if (bridgeController != null)
        {
            bridgeController.SwitchStateChanged -= HandleSwitchStateChanged;
        }
    }

    private void Start()
    {
        UpdateVisual();
    }

    public void Interact(PlayerInteraction player)
    {
        if (IsActivated || bridgeController == null || player == null ||
            (player.IsSpawned && !player.IsOwner))
        {
            return;
        }

        bridgeController.RequestSwitchActivation(switchId);
    }

    private void HandleSwitchStateChanged(BridgeSwitchId changedSwitch, bool activated)
    {
        if (changedSwitch == switchId)
        {
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
