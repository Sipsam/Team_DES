using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class DoorPuzzleController : NetworkBehaviour
{
    private const ulong NoClient = ulong.MaxValue;

    [Header("References")]
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private DoorPuzzleUI puzzleUI;
    [SerializeField] private Transform infoStation;
    [SerializeField] private Transform inputStation;
    [SerializeField] private PuzzleDoor cooperationDoor;
    [SerializeField] private PuzzleDoor shortcutDoor;
    [SerializeField] private GameObject competitionRoute;
    [SerializeField] private GameObject shortcutPanelVisual;

    [Header("Server Validation")]
    [SerializeField, Min(0.1f)] private float stationValidationRange = 3.5f;

    private readonly NetworkVariable<ulong> infoOccupant = new NetworkVariable<ulong>(
        NoClient, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<ulong> inputOccupant = new NetworkVariable<ulong>(
        NoClient, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> doorOpened = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<ulong> shortcutPlayerOne = new NetworkVariable<ulong>(
        NoClient, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<ulong> shortcutPlayerTwo = new NetworkVariable<ulong>(
        NoClient, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly HashSet<ulong> clientsBlockedUntilStationExit = new HashSet<ulong>();

    private string answerCode = string.Empty;
    private float nextOccupancyValidationTime;

    public bool IsDoorOpen => doorOpened.Value;
    public GameState CurrentGameState => gameStateManager != null
        ? gameStateManager.CurrentState
        : GameState.Finished;

    private void Awake()
    {
        if (puzzleUI == null)
        {
            puzzleUI = GetComponent<DoorPuzzleUI>();
        }
    }

    public override void OnNetworkSpawn()
    {
        infoOccupant.OnValueChanged += HandleInfoOccupantChanged;
        inputOccupant.OnValueChanged += HandleInputOccupantChanged;
        doorOpened.OnValueChanged += HandleDoorOpenedChanged;
        shortcutPlayerOne.OnValueChanged += HandleShortcutPlayerChanged;
        shortcutPlayerTwo.OnValueChanged += HandleShortcutPlayerChanged;

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
            answerCode = Random.Range(0, 1000).ToString("D3");
            infoOccupant.Value = NoClient;
            inputOccupant.Value = NoClient;
            doorOpened.Value = false;
            shortcutPlayerOne.Value = NoClient;
            shortcutPlayerTwo.Value = NoClient;
            clientsBlockedUntilStationExit.Clear();
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        if (cooperationDoor != null)
        {
            cooperationDoor.SetOpen(doorOpened.Value);
        }

        ApplyGameState(CurrentGameState);
        ApplyShortcutDoorForLocalPlayer();
    }

    public override void OnNetworkDespawn()
    {
        infoOccupant.OnValueChanged -= HandleInfoOccupantChanged;
        inputOccupant.OnValueChanged -= HandleInputOccupantChanged;
        doorOpened.OnValueChanged -= HandleDoorOpenedChanged;
        shortcutPlayerOne.OnValueChanged -= HandleShortcutPlayerChanged;
        shortcutPlayerTwo.OnValueChanged -= HandleShortcutPlayerChanged;

        if (gameStateManager != null)
        {
            gameStateManager.StateChanged -= HandleGameStateChanged;
        }

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        puzzleUI?.HidePanel(false);
    }

    private void Update()
    {
        if (!IsServer || Time.time < nextOccupancyValidationTime)
        {
            return;
        }

        nextOccupancyValidationTime = Time.time + 0.2f;
        if (infoOccupant.Value != NoClient && !IsClientNearStation(infoOccupant.Value, infoStation))
        {
            ReleaseStation(infoOccupant.Value, (int)DoorPuzzleStationType.Info);
        }

        if (inputOccupant.Value != NoClient && !IsClientNearStation(inputOccupant.Value, inputStation))
        {
            ReleaseStation(inputOccupant.Value, (int)DoorPuzzleStationType.Input);
        }
    }

    public bool IsLocalPlayerAssignedToAnyStation()
    {
        return NetworkManager != null && NetworkManager.IsClient &&
               (infoOccupant.Value == NetworkManager.LocalClientId ||
                inputOccupant.Value == NetworkManager.LocalClientId);
    }

    public bool IsStationOccupied(DoorPuzzleStationType stationType)
    {
        return stationType == DoorPuzzleStationType.Info
            ? infoOccupant.Value != NoClient
            : inputOccupant.Value != NoClient;
    }

    public bool IsLocalPlayerShortcutUnlocked()
    {
        if (NetworkManager == null || !NetworkManager.IsClient)
        {
            return false;
        }

        return IsShortcutUnlocked(NetworkManager.LocalClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestStationServerRpc(int stationValue, RpcParams rpcParams = default)
    {
        if (!IsServer || stationValue < (int)DoorPuzzleStationType.Info ||
            stationValue > (int)DoorPuzzleStationType.Input)
        {
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        if (clientsBlockedUntilStationExit.Contains(clientId) ||
            !NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
            client.PlayerObject == null || CurrentGameState == GameState.Finished)
        {
            return;
        }

        DoorPuzzleStationType stationType = (DoorPuzzleStationType)stationValue;
        if (CurrentGameState == GameState.Competition)
        {
            if (stationType != DoorPuzzleStationType.Input || IsShortcutUnlocked(clientId) ||
                !IsPlayerNearStation(client.PlayerObject.transform.position, inputStation))
            {
                return;
            }

            ShowInputPanelClientRpc(true, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            return;
        }

        if (CurrentGameState != GameState.Cooperation || doorOpened.Value)
        {
            return;
        }

        if (stationType == DoorPuzzleStationType.Info)
        {
            if (infoOccupant.Value != NoClient || inputOccupant.Value == clientId ||
                !IsPlayerNearStation(client.PlayerObject.transform.position, infoStation))
            {
                return;
            }

            infoOccupant.Value = clientId;
            ShowInfoPanelClientRpc(answerCode, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            UpdateInputStatus("Partner is checking the code.");
        }
        else
        {
            if (inputOccupant.Value != NoClient || infoOccupant.Value == clientId ||
                !IsPlayerNearStation(client.PlayerObject.transform.position, inputStation))
            {
                return;
            }

            inputOccupant.Value = clientId;
            ShowInputPanelClientRpc(false, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            UpdateInputStatus(infoOccupant.Value == NoClient
                ? "Waiting for your partner to check the code."
                : "Enter the code your partner shared.");
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestReleaseStationServerRpc(int stationValue, RpcParams rpcParams = default)
    {
        if (!IsServer)
        {
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        clientsBlockedUntilStationExit.Remove(clientId);
        ReleaseStation(clientId, stationValue);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestCloseStationServerRpc(RpcParams rpcParams = default)
    {
        if (!IsServer)
        {
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        clientsBlockedUntilStationExit.Add(clientId);
        ReleaseStation(clientId, (int)DoorPuzzleStationType.Info);
        ReleaseStation(clientId, (int)DoorPuzzleStationType.Input);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitCooperationCodeServerRpc(string submittedCode, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!IsServer || CurrentGameState != GameState.Cooperation || doorOpened.Value ||
            inputOccupant.Value != clientId || infoOccupant.Value == NoClient ||
            infoOccupant.Value == clientId || !IsClientNearStation(clientId, inputStation) ||
            !IsClientNearStation(infoOccupant.Value, infoStation))
        {
            SendSubmitResult(clientId, false, "Waiting for a different player at InfoStation.", false);
            return;
        }

        if (!IsValidCode(submittedCode) || submittedCode != answerCode)
        {
            SendSubmitResult(clientId, false, "Wrong Code", false);
            return;
        }

        OpenCooperationDoor();
        SendSubmitResult(clientId, true, "Door Puzzle Completed", false);
        ClosePuzzlePanelsClientRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitShortcutCodeServerRpc(string submittedCode, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!IsServer || CurrentGameState != GameState.Competition || IsShortcutUnlocked(clientId) ||
            !IsClientNearStation(clientId, inputStation))
        {
            return;
        }

        if (!IsValidCode(submittedCode) || submittedCode != answerCode)
        {
            SendSubmitResult(clientId, false, "Wrong Code", true);
            return;
        }

        if (shortcutPlayerOne.Value == NoClient)
        {
            shortcutPlayerOne.Value = clientId;
        }
        else if (shortcutPlayerTwo.Value == NoClient)
        {
            shortcutPlayerTwo.Value = clientId;
        }
        else
        {
            return;
        }

        SendSubmitResult(clientId, true, "Memory Shortcut Unlocked", true);
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void ShowInfoPanelClientRpc(string code, RpcParams rpcParams = default)
    {
        puzzleUI?.ShowInfoPanel(code);
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void ShowInputPanelClientRpc(bool memoryShortcut, RpcParams rpcParams = default)
    {
        puzzleUI?.ShowInputPanel(memoryShortcut);
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void SetInputStatusClientRpc(string status, RpcParams rpcParams = default)
    {
        puzzleUI?.SetStatus(status, false);
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void SendSubmitResultClientRpc(bool success, string status, bool memoryShortcut,
        RpcParams rpcParams = default)
    {
        puzzleUI?.ShowSubmitResult(success, status, memoryShortcut);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ClosePuzzlePanelsClientRpc()
    {
        puzzleUI?.HidePanel(true);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ApplyCooperationDoorClientRpc(bool open)
    {
        if (cooperationDoor != null)
        {
            cooperationDoor.SetOpen(open);
        }
    }

    private void OpenCooperationDoor()
    {
        doorOpened.Value = true;
        ApplyCooperationDoorClientRpc(true);

        infoOccupant.Value = NoClient;
        inputOccupant.Value = NoClient;
        Debug.Log("Door Puzzle Completed");
    }

    private void ReleaseStation(ulong clientId, int stationValue)
    {
        if (stationValue == (int)DoorPuzzleStationType.Info && infoOccupant.Value == clientId)
        {
            infoOccupant.Value = NoClient;
            HidePanelClientRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
            UpdateInputStatus("Partner left InfoStation. Waiting for a partner.");
        }

        if (stationValue == (int)DoorPuzzleStationType.Input && inputOccupant.Value == clientId)
        {
            inputOccupant.Value = NoClient;
            HidePanelClientRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        // In Competition, InputStation is a non-exclusive shortcut prompt.
        if (stationValue == (int)DoorPuzzleStationType.Input && CurrentGameState == GameState.Competition)
        {
            HidePanelClientRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void HidePanelClientRpc(RpcParams rpcParams = default)
    {
        puzzleUI?.HidePanel(true);
    }

    private void UpdateInputStatus(string status)
    {
        if (inputOccupant.Value == NoClient || NetworkManager == null ||
            !NetworkManager.ConnectedClients.ContainsKey(inputOccupant.Value))
        {
            return;
        }

        SetInputStatusClientRpc(status, RpcTarget.Single(inputOccupant.Value, RpcTargetUse.Temp));
    }

    private void SendSubmitResult(ulong clientId, bool success, string status, bool memoryShortcut)
    {
        if (NetworkManager.ConnectedClients.ContainsKey(clientId))
        {
            SendSubmitResultClientRpc(success, status, memoryShortcut,
                RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
    }

    private void HandleInfoOccupantChanged(ulong previous, ulong next)
    {
        UpdateInputStatus(next == NoClient
            ? "Waiting for your partner to check the code."
            : "Enter the code your partner shared.");
    }

    private void HandleInputOccupantChanged(ulong previous, ulong next)
    {
        // Occupancy is replicated for debugging and exclusivity; the answer remains server-only.
    }

    private void HandleDoorOpenedChanged(bool previous, bool next)
    {
        if (cooperationDoor != null)
        {
            cooperationDoor.SetOpen(next);
        }
    }

    private void HandleShortcutPlayerChanged(ulong previous, ulong next)
    {
        ApplyShortcutDoorForLocalPlayer();
    }

    private void HandleGameStateChanged(GameState previous, GameState next)
    {
        ApplyGameState(next);
        if (IsServer && next == GameState.Competition)
        {
            if (infoOccupant.Value != NoClient)
            {
                HidePanelClientRpc(RpcTarget.Single(infoOccupant.Value, RpcTargetUse.Temp));
            }

            if (inputOccupant.Value != NoClient)
            {
                HidePanelClientRpc(RpcTarget.Single(inputOccupant.Value, RpcTargetUse.Temp));
            }

            infoOccupant.Value = NoClient;
            inputOccupant.Value = NoClient;
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        clientsBlockedUntilStationExit.Remove(clientId);
        if (infoOccupant.Value == clientId)
        {
            infoOccupant.Value = NoClient;
            UpdateInputStatus("Partner disconnected. Waiting for a partner.");
        }

        if (inputOccupant.Value == clientId)
        {
            inputOccupant.Value = NoClient;
        }

        if (shortcutPlayerOne.Value == clientId) shortcutPlayerOne.Value = NoClient;
        if (shortcutPlayerTwo.Value == clientId) shortcutPlayerTwo.Value = NoClient;
    }

    private void ApplyGameState(GameState state)
    {
        bool competition = state == GameState.Competition;
        if (competitionRoute != null)
        {
            competitionRoute.SetActive(competition);
        }

        if (shortcutPanelVisual != null)
        {
            shortcutPanelVisual.SetActive(competition);
        }
    }

    private void ApplyShortcutDoorForLocalPlayer()
    {
        if (shortcutDoor == null || NetworkManager == null || !NetworkManager.IsClient)
        {
            return;
        }

        shortcutDoor.SetLocalOpen(IsShortcutUnlocked(NetworkManager.LocalClientId));
    }

    private bool IsShortcutUnlocked(ulong clientId)
    {
        return shortcutPlayerOne.Value == clientId || shortcutPlayerTwo.Value == clientId;
    }

    private bool IsClientNearStation(ulong clientId, Transform station)
    {
        return NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
               client.PlayerObject != null && IsPlayerNearStation(client.PlayerObject.transform.position, station);
    }

    private bool IsPlayerNearStation(Vector3 playerPosition, Transform station)
    {
        return station != null && Vector3.Distance(playerPosition, station.position) <= stationValidationRange;
    }

    private static bool IsValidCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != 3)
        {
            return false;
        }

        return char.IsDigit(code[0]) && char.IsDigit(code[1]) && char.IsDigit(code[2]);
    }
}
