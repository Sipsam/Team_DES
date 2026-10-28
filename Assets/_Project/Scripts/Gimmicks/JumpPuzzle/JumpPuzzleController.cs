using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class JumpPuzzleController : NetworkBehaviour
{
    private const ulong EmptyClient = ulong.MaxValue;

    [Header("References")]
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private GameObject cooperativePlatforms;
    [SerializeField] private GameObject safeRoute;
    [SerializeField] private GameObject riskRoute;
    [SerializeField] private Collider endPlatformTrigger;
    [SerializeField] private Transform switchAPoint;
    [SerializeField] private Transform switchBPoint;
    [SerializeField, Min(0.1f)] private float switchValidationRange = 2.5f;

    private readonly NetworkVariable<ulong> switchAOccupant = new NetworkVariable<ulong>(
        EmptyClient, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<ulong> switchBOccupant = new NetworkVariable<ulong>(
        EmptyClient, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> cooperationCompleted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> cooperativeRouteUnlocked = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly HashSet<ulong> arrivedPlayers = new HashSet<ulong>();

    public bool IsAnyHoldSwitchActive =>
        switchAOccupant.Value != EmptyClient || switchBOccupant.Value != EmptyClient;
    public bool IsSwitchAActive => switchAOccupant.Value != EmptyClient;
    public bool IsSwitchBActive => switchBOccupant.Value != EmptyClient;
    public bool IsCooperationCompleted => cooperationCompleted.Value;
    public GameState CurrentGameState => gameStateManager != null
        ? gameStateManager.CurrentState
        : GameState.Finished;

    private void Awake()
    {
        if (gameStateManager == null)
        {
            gameStateManager = FindFirstObjectByType<GameStateManager>();
        }
    }

    public override void OnNetworkSpawn()
    {
        switchAOccupant.OnValueChanged += HandleSwitchOccupantChanged;
        switchBOccupant.OnValueChanged += HandleSwitchOccupantChanged;
        cooperativeRouteUnlocked.OnValueChanged += HandleRouteUnlockedChanged;

        if (gameStateManager == null)
        {
            gameStateManager = FindFirstObjectByType<GameStateManager>();
        }

        if (gameStateManager != null)
        {
            gameStateManager.StateChanged += HandleGameStateChanged;
        }

        if (IsServer)
        {
            switchAOccupant.Value = EmptyClient;
            switchBOccupant.Value = EmptyClient;
            cooperationCompleted.Value = false;
            cooperativeRouteUnlocked.Value = false;
            arrivedPlayers.Clear();
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        ApplyGameState(CurrentGameState);
        RefreshDebugState();
    }

    public override void OnNetworkDespawn()
    {
        switchAOccupant.OnValueChanged -= HandleSwitchOccupantChanged;
        switchBOccupant.OnValueChanged -= HandleSwitchOccupantChanged;
        cooperativeRouteUnlocked.OnValueChanged -= HandleRouteUnlockedChanged;

        if (gameStateManager != null)
        {
            gameStateManager.StateChanged -= HandleGameStateChanged;
        }

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestHoldSwitchServerRpc(int switchId, bool active, RpcParams rpcParams = default)
    {
        if (!IsServer || CurrentGameState != GameState.Cooperation ||
            switchId < 0 || switchId > 1)
        {
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
            client.PlayerObject == null)
        {
            return;
        }

        NetworkVariable<ulong> occupant = switchId == 0 ? switchAOccupant : switchBOccupant;
        Transform switchPoint = switchId == 0 ? switchAPoint : switchBPoint;
        if (active)
        {
            if (switchPoint == null || Vector3.Distance(
                    client.PlayerObject.transform.position, switchPoint.position) > switchValidationRange)
            {
                return;
            }

            ulong otherOccupant = switchId == 0 ? switchBOccupant.Value : switchAOccupant.Value;
            if (switchId == 1 &&
                (switchAOccupant.Value == EmptyClient || switchAOccupant.Value == clientId))
            {
                return;
            }

            if (occupant.Value == EmptyClient && otherOccupant != clientId)
            {
                occupant.Value = clientId;
                if (switchId == 1)
                {
                    cooperativeRouteUnlocked.Value = true;
                }
            }
        }
        else if (occupant.Value == clientId)
        {
            occupant.Value = EmptyClient;
        }

        RefreshDebugState();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestEndArrivalServerRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || CurrentGameState != GameState.Cooperation || cooperationCompleted.Value)
        {
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
            client.PlayerObject == null || endPlatformTrigger == null ||
            !endPlatformTrigger.bounds.Contains(client.PlayerObject.transform.position))
        {
            return;
        }

        arrivedPlayers.Add(clientId);
        RefreshDebugState();
        if (NetworkManager.ConnectedClientsIds.Count >= 2 &&
            arrivedPlayers.Count >= NetworkManager.ConnectedClientsIds.Count)
        {
            cooperationCompleted.Value = true;
            Debug.Log("Jump Puzzle Cooperation Completed");
        }
    }

    private void HandleGameStateChanged(GameState previous, GameState next)
    {
        ApplyGameState(next);
        if (IsServer && next != GameState.Cooperation)
        {
            switchAOccupant.Value = EmptyClient;
            switchBOccupant.Value = EmptyClient;
        }
    }

    private void HandleSwitchOccupantChanged(ulong previous, ulong next)
    {
        ApplyGameState(CurrentGameState);
        RefreshDebugState();
    }

    private void HandleRouteUnlockedChanged(bool previous, bool next)
    {
        ApplyGameState(CurrentGameState);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (switchAOccupant.Value == clientId) switchAOccupant.Value = EmptyClient;
        if (switchBOccupant.Value == clientId) switchBOccupant.Value = EmptyClient;
        arrivedPlayers.Remove(clientId);
        RefreshDebugState();
    }

    private void ApplyGameState(GameState state)
    {
        bool cooperation = state == GameState.Cooperation;
        if (cooperativePlatforms != null)
        {
            cooperativePlatforms.SetActive(cooperation && cooperativeRouteUnlocked.Value);
        }

        if (safeRoute != null) safeRoute.SetActive(state == GameState.Competition);
        if (riskRoute != null) riskRoute.SetActive(state == GameState.Competition);
    }

    private void RefreshDebugState()
    {
        string state = cooperationCompleted.Value ? "Completed" :
            arrivedPlayers.Count == 1 ? "One Player Crossing" : "Waiting";
        Debug.Log($"Jump Puzzle: {state}; Hold A: {IsSwitchAActive}, Hold B: {IsSwitchBActive}");
    }
}
