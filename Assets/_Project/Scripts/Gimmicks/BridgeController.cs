using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum BridgeState
{
    Incomplete,
    Complete,
    Competition
}

[RequireComponent(typeof(NetworkObject))]
public sealed class BridgeController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private GameObject bridgeFrame;
    [SerializeField] private GameObject cooperationPath;
    [SerializeField] private GameObject safeRoute;
    [SerializeField] private GameObject riskRoute;

    private readonly NetworkVariable<bool> switchAActivated = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> switchBActivated = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<BridgeState> bridgeState = new NetworkVariable<BridgeState>(
        BridgeState.Incomplete, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly HashSet<ulong> clientsWhoActivatedSwitch = new HashSet<ulong>();

    public event Action<BridgeSwitchId, bool> SwitchStateChanged;

    public BridgeState CurrentState => bridgeState.Value;
    public bool CanActivateSwitch => IsSpawned && IsServer && CurrentState == BridgeState.Incomplete &&
                                     (gameStateManager == null || gameStateManager.CurrentState == GameState.Cooperation);

    public bool IsSwitchActivated(BridgeSwitchId id)
    {
        return id == BridgeSwitchId.A ? switchAActivated.Value : switchBActivated.Value;
    }

    public override void OnNetworkSpawn()
    {
        switchAActivated.OnValueChanged += HandleSwitchAChanged;
        switchBActivated.OnValueChanged += HandleSwitchBChanged;
        bridgeState.OnValueChanged += HandleBridgeStateChanged;

        if (IsServer)
        {
            clientsWhoActivatedSwitch.Clear();
            switchAActivated.Value = false;
            switchBActivated.Value = false;
            bridgeState.Value = BridgeState.Incomplete;
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        if (gameStateManager != null)
        {
            gameStateManager.StateChanged += HandleGameStateChanged;
        }

        ApplyBridgeState();
        SwitchStateChanged?.Invoke(BridgeSwitchId.A, switchAActivated.Value);
        SwitchStateChanged?.Invoke(BridgeSwitchId.B, switchBActivated.Value);
    }

    public override void OnNetworkDespawn()
    {
        switchAActivated.OnValueChanged -= HandleSwitchAChanged;
        switchBActivated.OnValueChanged -= HandleSwitchBChanged;
        bridgeState.OnValueChanged -= HandleBridgeStateChanged;

        if (gameStateManager != null)
        {
            gameStateManager.StateChanged -= HandleGameStateChanged;
        }

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    public void RequestSwitchActivation(BridgeSwitchId switchId)
    {
        if (!IsSpawned)
        {
            return;
        }

        RequestSwitchActivationRpc((int)switchId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestSwitchActivationRpc(int switchIdValue, RpcParams rpcParams = default)
    {
        if (switchIdValue < (int)BridgeSwitchId.A || switchIdValue > (int)BridgeSwitchId.B || !CanActivateSwitch)
        {
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
            client.PlayerObject == null)
        {
            return;
        }

        BridgeSwitchId switchId = (BridgeSwitchId)switchIdValue;
        bool alreadyActivated = IsSwitchActivated(switchId);
        if (alreadyActivated || clientsWhoActivatedSwitch.Contains(clientId) ||
            Vector3.Distance(client.PlayerObject.transform.position, GetSwitchPosition(switchId)) > 3.5f)
        {
            return;
        }

        clientsWhoActivatedSwitch.Add(clientId);

        if (switchId == BridgeSwitchId.A)
        {
            switchAActivated.Value = true;
        }
        else
        {
            switchBActivated.Value = true;
        }

        if (switchAActivated.Value && switchBActivated.Value)
        {
            bridgeState.Value = BridgeState.Complete;
        }
    }

    private Vector3 GetSwitchPosition(BridgeSwitchId switchId)
    {
        BridgeSwitch[] switches = FindObjectsByType<BridgeSwitch>(FindObjectsSortMode.None);
        foreach (BridgeSwitch bridgeSwitch in switches)
        {
            if (bridgeSwitch.SwitchId == switchId)
            {
                return bridgeSwitch.transform.position;
            }
        }

        return transform.position;
    }

    private void HandleGameStateChanged(GameState previousState, GameState newState)
    {
        if (IsServer && newState == GameState.Competition && bridgeState.Value == BridgeState.Complete)
        {
            bridgeState.Value = BridgeState.Competition;
        }
    }

    private void HandleSwitchAChanged(bool previousValue, bool newValue)
    {
        SwitchStateChanged?.Invoke(BridgeSwitchId.A, newValue);
    }

    private void HandleSwitchBChanged(bool previousValue, bool newValue)
    {
        SwitchStateChanged?.Invoke(BridgeSwitchId.B, newValue);
    }

    private void HandleBridgeStateChanged(BridgeState previousValue, BridgeState newValue)
    {
        ApplyBridgeState();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        clientsWhoActivatedSwitch.Remove(clientId);
    }

    private void ApplyBridgeState()
    {
        if (bridgeFrame != null) bridgeFrame.SetActive(true);
        if (cooperationPath != null) cooperationPath.SetActive(bridgeState.Value != BridgeState.Incomplete);

        bool competition = bridgeState.Value == BridgeState.Competition;
        if (safeRoute != null) safeRoute.SetActive(competition);
        if (riskRoute != null) riskRoute.SetActive(competition);
    }
}
