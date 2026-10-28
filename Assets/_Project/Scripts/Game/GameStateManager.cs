using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum GameState
{
    Cooperation,
    Competition,
    Finished
}

[RequireComponent(typeof(NetworkObject))]
public sealed class GameStateManager : NetworkBehaviour
{
    public event Action<GameState, GameState> StateChanged;

    [Header("Integrated Course (optional for older scenes)")]
    [SerializeField] private bool requireAllGimmicks;
    [SerializeField] private BridgeController bridge;
    [SerializeField] private RaftController raft;
    [SerializeField] private DoorPuzzleController door;
    [SerializeField] private JumpPuzzleController jump;

    private readonly NetworkVariable<int> connectedPlayerCount = new NetworkVariable<int>(0);
    private readonly NetworkVariable<double> courseStartedAt = new NetworkVariable<double>(-1d);
    private readonly NetworkVariable<double> courseFinishedAt = new NetworkVariable<double>(-1d);

    private readonly NetworkVariable<GameState> currentState = new NetworkVariable<GameState>(
        GameState.Cooperation, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> turnPointArrivalCount = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<ulong> winnerClientId = new NetworkVariable<ulong>(
        ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly HashSet<ulong> arrivedClientIds = new HashSet<ulong>();

    public GameState CurrentState => currentState.Value;
    public int TurnPointArrivalCount => turnPointArrivalCount.Value;
    public ulong WinnerClientId => winnerClientId.Value;
    public bool HasWinner => winnerClientId.Value != ulong.MaxValue;
    public int ConnectedPlayerCount => connectedPlayerCount.Value;
    public bool IsGameReady => IsSpawned && connectedPlayerCount.Value == 2;
    public bool CanPlay => IsGameReady && CurrentState != GameState.Finished;
    public bool AllGimmicksCompleted => !requireAllGimmicks ||
        (bridge != null && bridge.IsCooperationCompleted &&
         raft != null && raft.IsCooperationCompleted &&
         door != null && door.IsDoorOpen && jump != null && jump.IsCooperationCompleted);
    public double ElapsedSeconds => courseStartedAt.Value < 0d || !IsSpawned ? 0d :
        Math.Max(0d, (courseFinishedAt.Value >= 0d ? courseFinishedAt.Value : NetworkManager.ServerTime.Time) -
                    courseStartedAt.Value);

    private void Update()
    {
        if (!IsSpawned || !IsServer) return;
        int count = 0;
        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            if (client.PlayerObject != null && client.PlayerObject.IsSpawned) count++;
        }
        connectedPlayerCount.Value = count;
        if (count == 2 && courseStartedAt.Value < 0d)
        {
            courseStartedAt.Value = NetworkManager.ServerTime.Time;
            Debug.Log("Game ready. Cooperation started.");
        }
    }

    public override void OnNetworkSpawn()
    {
        currentState.OnValueChanged += HandleStateChanged;
        winnerClientId.OnValueChanged += HandleWinnerChanged;
        if (IsServer)
        {
            arrivedClientIds.Clear();
            turnPointArrivalCount.Value = 0;
            winnerClientId.Value = ulong.MaxValue;
            currentState.Value = GameState.Cooperation;
            connectedPlayerCount.Value = 0;
            courseStartedAt.Value = -1d;
            courseFinishedAt.Value = -1d;
        }
        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        currentState.OnValueChanged -= HandleStateChanged;
        winnerClientId.OnValueChanged -= HandleWinnerChanged;
        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    public bool TryTransitionTo(GameState nextState)
    {
        if (!IsServer || !IsGameReady || !IsValidTransition(currentState.Value, nextState))
        {
            return false;
        }

        currentState.Value = nextState;
        return true;
    }

    public bool RegisterTurnPointArrival(ulong clientId)
    {
        if (!IsServer || !IsGameReady || !AllGimmicksCompleted || currentState.Value != GameState.Cooperation ||
            !NetworkManager.ConnectedClients.ContainsKey(clientId) || !arrivedClientIds.Add(clientId))
        {
            return false;
        }

        turnPointArrivalCount.Value = arrivedClientIds.Count;
        int playerCount = NetworkManager.ConnectedClientsIds.Count;
        if (playerCount >= 2 && arrivedClientIds.Count >= playerCount)
            TryTransitionTo(GameState.Competition);

        return true;
    }

    public bool TryFinish(ulong clientId)
    {
        if (!IsServer || currentState.Value != GameState.Competition || HasWinner ||
            !NetworkManager.ConnectedClients.ContainsKey(clientId))
        {
            return false;
        }

        winnerClientId.Value = clientId;
        if (!TryTransitionTo(GameState.Finished))
        {
            winnerClientId.Value = ulong.MaxValue;
            return false;
        }

        courseFinishedAt.Value = NetworkManager.ServerTime.Time;

        return true;
    }

    private void HandleStateChanged(GameState previousState, GameState nextState)
    {
        StateChanged?.Invoke(previousState, nextState);
        if (nextState == GameState.Competition)
        {
            Debug.Log("Competition Phase Started");
        }
    }

    private void HandleWinnerChanged(ulong previousValue, ulong nextValue)
    {
        if (nextValue == ulong.MaxValue)
        {
            return;
        }

        Debug.Log($"Player {nextValue + 1} Wins");
        Debug.Log("Prototype Finished");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (arrivedClientIds.Remove(clientId))
        {
            turnPointArrivalCount.Value = arrivedClientIds.Count;
        }
    }

    private static bool IsValidTransition(GameState current, GameState next)
    {
        return (current == GameState.Cooperation && next == GameState.Competition) ||
               (current == GameState.Competition && next == GameState.Finished);
    }
}
