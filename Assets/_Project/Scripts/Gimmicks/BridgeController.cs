using UnityEngine;

public enum BridgeState
{
    Incomplete,
    Complete,
    Competition
}

public sealed class BridgeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private GameObject bridgeFrame;
    [SerializeField] private GameObject cooperationPath;
    [SerializeField] private GameObject safeRoute;
    [SerializeField] private GameObject riskRoute;

    public BridgeState CurrentState { get; private set; } = BridgeState.Incomplete;
    public bool CanActivateSwitch => CurrentState == BridgeState.Incomplete &&
        (gameStateManager == null || gameStateManager.CurrentState == GameState.Cooperation);

    private bool switchAActivated;
    private bool switchBActivated;

    private void OnEnable()
    {
        if (gameStateManager != null)
        {
            gameStateManager.StateChanged += HandleGameStateChanged;
        }
    }

    private void OnDisable()
    {
        if (gameStateManager != null)
        {
            gameStateManager.StateChanged -= HandleGameStateChanged;
        }
    }

    private void Start()
    {
        ApplyBridgeState();
    }

    public bool ActivateSwitch(BridgeSwitchId switchId)
    {
        if (!CanActivateSwitch)
        {
            return false;
        }

        if (switchId == BridgeSwitchId.A)
        {
            switchAActivated = true;
        }
        else
        {
            switchBActivated = true;
        }

        if (switchAActivated && switchBActivated)
        {
            CurrentState = BridgeState.Complete;
            ApplyBridgeState();
        }

        return true;
    }

    private void HandleGameStateChanged(GameState previousState, GameState newState)
    {
        if (newState == GameState.Competition && CurrentState == BridgeState.Complete)
        {
            CurrentState = BridgeState.Competition;
            ApplyBridgeState();
        }
    }

    private void ApplyBridgeState()
    {
        if (bridgeFrame != null)
        {
            bridgeFrame.SetActive(true);
        }

        if (cooperationPath != null)
        {
            cooperationPath.SetActive(CurrentState == BridgeState.Complete);
        }

        if (safeRoute != null)
        {
            safeRoute.SetActive(CurrentState == BridgeState.Competition);
        }

        if (riskRoute != null)
        {
            riskRoute.SetActive(CurrentState == BridgeState.Competition);
        }
    }
}
