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

public enum FoundationPhase { WaitingForPlayers, Preparing, Outbound, SealPending, Return, Finished }

[RequireComponent(typeof(NetworkObject))]
public sealed class GameStateManager : NetworkBehaviour
{
    public bool foundationMode;
    internal TwinEdge.Gameplay.NetworkInteractionTarget ContestInteractionSample { get; set; }
    readonly NetworkVariable<FoundationPhase> foundationPhase = new(FoundationPhase.WaitingForPlayers);
    public FoundationPhase Phase => foundationPhase.Value;
    readonly NetworkVariable<bool> developmentReturnRequested = new();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool DevelopmentReturnRequested => developmentReturnRequested.Value;

    public void RequestDevelopmentReturn()
    {
        if (Phase == FoundationPhase.Outbound && !AdvanceFoundation(FoundationPhase.SealPending)) return;
        if (!IsServer || !IsGameReady || Phase != FoundationPhase.SealPending) return;
        developmentReturnRequested.Value = true;
        AdvanceFoundation(FoundationPhase.Return);
    }
#endif
    public string FoundationPhaseLabel => foundationMode ? foundationPhase.Value.ToString() : CurrentState.ToString();
    public event Action<FoundationPhase, FoundationPhase> FoundationPhaseChanged;
    public void StartFoundation()
    {
        if (!IsServer || !foundationMode || !IsGameReady || Phase != FoundationPhase.Preparing) return;
        courseStartedAt.Value = NetworkManager.ServerTime.Time;
        foundationPhase.Value = FoundationPhase.Outbound;
    }
    // Trusted server hook. SealPending stays cooperative until GemOwnership commits acquisition.
    public bool AdvanceFoundation(FoundationPhase next)
    {
        if (!IsServer || !foundationMode || !IsGameReady ||
            !((foundationPhase.Value == FoundationPhase.Outbound && next == FoundationPhase.SealPending) ||
              (foundationPhase.Value == FoundationPhase.SealPending && next == FoundationPhase.Return))) return false;
        foundationPhase.Value = next;
        if (next == FoundationPhase.Return) currentState.Value = GameState.Competition;
        return true;
    }
    internal bool TryFinishFoundation(TwinEdge.Gameplay.GameplayCharacter player, BoxCollider finish)
    {
        if (!IsServer || !foundationMode || !CanPlay || Phase != FoundationPhase.Return || HasWinner || NetworkManager.ConnectedClientsList.Count != 2 ||
            player == null || !player.IsSpawned || !player.CanContestFinish ||
            TwinEdge.Gameplay.GemOwnership.Current is not { State: TwinEdge.Gameplay.GemState.Carried } gem ||
            gem.Owner != player.OwnerClientId ||
            !NetworkManager.ConnectedClients.TryGetValue(player.OwnerClientId, out var peer) || peer.PlayerObject != player.NetworkObject ||
            finish == null || !finish.enabled || !finish.gameObject.activeInHierarchy ||
            !finish.bounds.Contains(player.transform.position + Vector3.up * .3f) || !player.HasGround()) return false;
        courseFinishedAt.Value = NetworkManager.ServerTime.Time;
        winnerClientId.Value = player.OwnerClientId;
        currentState.Value = GameState.Finished;
        foundationPhase.Value = FoundationPhase.Finished;
        return true;
    }

    void OnFoundationChanged(FoundationPhase before, FoundationPhase after) => FoundationPhaseChanged?.Invoke(before, after);

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
    public bool CanPlay => IsGameReady && CurrentState != GameState.Finished && (!foundationMode || foundationPhase.Value >= FoundationPhase.Outbound);
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
        if (foundationMode)
        {
            if (count < 2 && foundationPhase.Value < FoundationPhase.Outbound) foundationPhase.Value = FoundationPhase.WaitingForPlayers;
            else if (count == 2 && foundationPhase.Value == FoundationPhase.WaitingForPlayers) foundationPhase.Value = FoundationPhase.Preparing;
        }
        if (!foundationMode && count == 2 && courseStartedAt.Value < 0d)
        {
            courseStartedAt.Value = NetworkManager.ServerTime.Time;
            Debug.Log("Game ready. Cooperation started.");
        }
    }

    public override void OnNetworkSpawn()
    {
        foundationPhase.OnValueChanged += OnFoundationChanged;
        currentState.OnValueChanged += HandleStateChanged;
        winnerClientId.OnValueChanged += HandleWinnerChanged;
        if (IsServer)
        {
            if (foundationMode && NetworkManager.NetworkConfig.EnableSceneManagement)
                NetworkManager.SceneManager.SetClientSynchronizationMode(UnityEngine.SceneManagement.LoadSceneMode.Additive);
            arrivedClientIds.Clear();
            turnPointArrivalCount.Value = 0;
            winnerClientId.Value = ulong.MaxValue;
            currentState.Value = GameState.Cooperation;
            foundationPhase.Value = FoundationPhase.WaitingForPlayers;
            developmentReturnRequested.Value = false;
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
        foundationPhase.OnValueChanged -= OnFoundationChanged;
        currentState.OnValueChanged -= HandleStateChanged;
        winnerClientId.OnValueChanged -= HandleWinnerChanged;
        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    public bool TryTransitionTo(GameState nextState)
    {
        if (foundationMode || !IsServer || !IsGameReady || !IsValidTransition(currentState.Value, nextState))
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
        if (foundationMode || !IsServer || currentState.Value != GameState.Competition || HasWinner ||
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

        Debug.Log($"Player {nextValue} wins { (foundationMode ? "V3 Foundation" : "Prototype") }.");
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
