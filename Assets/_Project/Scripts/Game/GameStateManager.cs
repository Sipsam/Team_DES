using TwinEdge.Audio;
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

public enum MatchTransition : byte { None, Countdown, Seal, Return }

public struct MatchTransitionWindow : INetworkSerializable, IEquatable<MatchTransitionWindow>
{
    public MatchTransition Kind;
    public uint Sequence;
    public double Start;
    public double End;
    public Vector3 Point;
    public int CarrierSlot;
    public uint GemRevision;

    public bool Equals(MatchTransitionWindow other) => Sequence == other.Sequence && Kind == other.Kind;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Kind);
        serializer.SerializeValue(ref Sequence);
        serializer.SerializeValue(ref Start);
        serializer.SerializeValue(ref End);
        serializer.SerializeValue(ref Point);
        serializer.SerializeValue(ref CarrierSlot);
        serializer.SerializeValue(ref GemRevision);
    }
}

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
    public const double CountdownSeconds = 3;
    const double CountdownLeadSeconds = .75;
    public const double SealPresentationSeconds = 1.5;
    public const double ReturnPresentationSeconds = 2;
    public const double OutboundNoticeSeconds = .85;
    readonly NetworkVariable<MatchTransitionWindow> transition = new();

    public MatchTransitionWindow Transition => IsSpawned ? transition.Value : default;
    public double MatchServerTime => NetworkManager.ServerTime.Time;
    public bool TransitionBlocksInput => IsSpawned && Transition.Kind != MatchTransition.None &&
        MatchServerTime < Transition.End;

    public bool AcceptsGameplay => CanPlay && !TransitionBlocksInput;
    // A request made before or during a window cannot be applied after its deadline.
    internal uint ActionRevision => Transition.Sequence * 2 + (AcceptsGameplay ? 1u : 0u);
    internal bool AcceptsAction(uint revision) => AcceptsGameplay && revision == ActionRevision;

    void BeginTransition(MatchTransition kind, double seconds, Vector3 point = default, int carrierSlot = -1, uint gemRevision = 0)
    {
        double start = MatchServerTime + (kind == MatchTransition.Countdown ? CountdownLeadSeconds : 0);
        transition.Value = new MatchTransitionWindow
        {
            Kind = kind, Sequence = Transition.Sequence + 1, Start = start, End = start + seconds,
            Point = point, CarrierSlot = carrierSlot, GemRevision = gemRevision
        };
        TraceTransition("approved");
        if (kind != MatchTransition.Countdown)
            foreach (var peer in NetworkManager.ConnectedClientsList)
                peer.PlayerObject?.GetComponent<TwinEdge.Gameplay.GameplayCharacter>()?.ClearTransitionIntentServer();
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    void TraceTransition(string action) =>
        Debug.Log($"MATCH_TRANSITION {action} kind={Transition.Kind} seq={Transition.Sequence} time={MatchServerTime:F3} start={Transition.Start:F3} end={Transition.End:F3}");

    internal bool CommitFirstGemReturn(ulong carrier, Vector3 point, uint gemRevision)
    {
        if (!IsServer || !foundationMode || !AcceptsGameplay || Phase != FoundationPhase.SealPending ||
            !NetworkManager.GetComponent<NetworkSpawnCoordinator>().TryGetSlot(carrier, out int slot)) return false;
        BeginTransition(MatchTransition.Return, ReturnPresentationSeconds, point, slot, gemRevision);
        bool advanced = AdvanceFoundation(FoundationPhase.Return);
        if (advanced) EmitWorldCue(AudioCueId.GemPickup, point);
        return advanced;
    }

    internal void PresentSealServer(Vector3 point)
    {
        if (IsServer && foundationMode && AcceptsGameplay && Phase == FoundationPhase.SealPending)
            BeginTransition(MatchTransition.Seal, SealPresentationSeconds, point);
    }

    void CancelCountdown()
    {
        if (Transition.Kind == MatchTransition.Countdown)
        {
            TraceTransition("cancelled");
            transition.Value = new MatchTransitionWindow { Sequence = Transition.Sequence };
        }
    }

    uint countdownAudioSequence;
    int countdownAudioBeat = -1;

    void TickCountdownAudio()
    {
        var window = Transition;
        if (window.Kind != MatchTransition.Countdown || !HasLiveAudio) return;
        double age = MatchServerTime - window.Start;
        if (age < 0 || age >= CountdownSeconds + OutboundNoticeSeconds) return;
        int beat = (int)Math.Min(CountdownSeconds, Math.Floor(age));
        if (beat == CountdownSeconds && Phase != FoundationPhase.Outbound) return;
        if (countdownAudioSequence == window.Sequence && countdownAudioBeat == beat) return;
        countdownAudioSequence = window.Sequence;
        countdownAudioBeat = beat;
        if (age - beat < .25) PlayLiveCue(beat < CountdownSeconds ? AudioCueId.Click : AudioCueId.Confirm);
    }

    byte firstCape, secondCape;
    readonly NetworkVariable<bool> clientReady = new();
    readonly NetworkVariable<byte> rematchVotes = new();
    readonly NetworkVariable<bool> rematchLoadFailed = new();
    readonly NetworkVariable<int> winnerSlot = new(-1);
    ulong readyClientId = ulong.MaxValue;

    // This object is replaced at every rematch. The V3 transport never recycles its object ID.
    internal ulong MatchIdentity => NetworkObjectId;
    internal bool RematchLoadFailed => rematchLoadFailed.Value;
    internal bool HasRematchVote(int slot) => slot is 0 or 1 && (rematchVotes.Value & (1 << slot)) != 0;
    internal bool Won(int slot) => HasWinner && slot >= 0 && winnerSlot.Value == slot;

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    internal void SetRematchConsentRpc(ulong match, bool consent, RpcParams rpc = default)
    {
        if (!IsServer || !foundationMode || Phase != FoundationPhase.Finished || match != MatchIdentity ||
            !HasWinner || rematchVotes.Value == 3) return;
        ulong sender = rpc.Receive.SenderClientId;
        var coordinator = NetworkManager.GetComponent<NetworkSpawnCoordinator>();
        if (!coordinator.TryGetSlot(sender, out int slot) || slot is < 0 or > 1 ||
            !IsConnectedSlot(sender, slot) || HasRematchVote(slot) == consent) return;
        if (NetworkManager.ConnectedClientsList.Count != 2 || !IsGameReady) return;
        rematchLoadFailed.Value = false;
        rematchVotes.Value ^= (byte)(1 << slot);
    }

    public bool ClientReady => IsSpawned && clientReady.Value;
    public bool HostCanStart => IsHost && Phase == FoundationPhase.WaitingForPlayers &&
        ConnectedPlayerCount == 2 && ClientReady;

    bool IsConnectedSlot(ulong id, int slot) =>
        NetworkManager.ConnectedClients.TryGetValue(id, out var client) &&
        client.PlayerObject != null && client.PlayerObject.IsSpawned &&
        NetworkManager.GetComponent<NetworkSpawnCoordinator>().TryGetSlot(id, out int assigned) &&
        assigned == slot;

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    internal void SetLobbyReadyRpc(bool ready, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (!IsServer || !foundationMode || Phase != FoundationPhase.WaitingForPlayers ||
            sender == Unity.Netcode.NetworkManager.ServerClientId || !IsConnectedSlot(sender, 1)) return;
        readyClientId = ready ? sender : ulong.MaxValue;
        clientReady.Value = ready;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    internal void RequestMatchStartRpc(RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (!IsServer || !foundationMode || Phase != FoundationPhase.WaitingForPlayers ||
            sender != Unity.Netcode.NetworkManager.ServerClientId || !IsConnectedSlot(sender, 0) ||
            !clientReady.Value || !IsConnectedSlot(readyClientId, 1)) return;
        foundationPhase.Value = FoundationPhase.Preparing;
    }

    void ClearLobbyReady()
    {
        readyClientId = ulong.MaxValue;
        clientReady.Value = false;
    }

    void StartFoundation()
    {
        if (!IsServer || !foundationMode || !IsGameReady || Phase != FoundationPhase.Preparing) return;
        if (!IsConnectedSlot(Unity.Netcode.NetworkManager.ServerClientId, 0) ||
            !IsConnectedSlot(readyClientId, 1)) return;
        if (Transition.Kind != MatchTransition.Countdown)
        {
            BeginTransition(MatchTransition.Countdown, CountdownSeconds);
            return;
        }
        if (MatchServerTime < Transition.End) return;
        int count = TwinEdge.Gameplay.GameplayCharacter.CapeColorCount;
        firstCape = (byte)UnityEngine.Random.Range(0, count);
        secondCape = (byte)((firstCape + UnityEngine.Random.Range(1, count)) % count);
        foreach (var client in NetworkManager.ConnectedClientsList)
            AssignFoundationCape(client.PlayerObject.GetComponent<TwinEdge.Gameplay.GameplayCharacter>());
        courseStartedAt.Value = Transition.End;
        TraceTransition("outbound");
        ClearLobbyReady();
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
        if (!IsServer || !foundationMode || !AcceptsGameplay || Phase != FoundationPhase.Return || HasWinner || NetworkManager.ConnectedClientsList.Count != 2 ||
            player == null || !player.IsSpawned || !player.CanContestFinish ||
            TwinEdge.Gameplay.GemOwnership.Current is not { State: TwinEdge.Gameplay.GemState.Carried } gem ||
            gem.Owner != player.OwnerClientId ||
            !NetworkManager.ConnectedClients.TryGetValue(player.OwnerClientId, out var peer) || peer.PlayerObject != player.NetworkObject ||
            finish == null || !finish.enabled || !finish.gameObject.activeInHierarchy ||
            !finish.bounds.Contains(player.transform.position + Vector3.up * .3f) || !player.HasGround()) return false;
        if (!NetworkManager.GetComponent<NetworkSpawnCoordinator>().TryGetSlot(player.OwnerClientId, out int slot))
            return false;
        courseFinishedAt.Value = NetworkManager.ServerTime.Time;
        winnerSlot.Value = slot;
        winnerClientId.Value = player.OwnerClientId;
        currentState.Value = GameState.Finished;
        foundationPhase.Value = FoundationPhase.Finished;
        return true;
    }

    void OnFoundationChanged(FoundationPhase before, FoundationPhase after)
    {
        FoundationPhaseChanged?.Invoke(before, after);
        if (HasLiveAudio && IsHost && before == FoundationPhase.WaitingForPlayers && after == FoundationPhase.Preparing)
            AudioService.Play(AudioCueId.Start);
    }

    internal bool HasLiveAudio => foundationMode && IsSpawned && IsClient && NetworkManager.IsConnectedClient &&
        !NetworkManager.ShutdownInProgress && NetworkManager.LocalClient?.PlayerObject is { IsSpawned: true };

    internal void PlayLiveCue(AudioCueId cue, Vector3 position = default)
    {
        if (HasLiveAudio) AudioService.Play(cue, position);
    }

    void OnClientReadyChanged(bool before, bool after)
    {
        if (HasLiveAudio && !IsHost && Phase == FoundationPhase.WaitingForPlayers)
            AudioService.Play(after ? AudioCueId.Ready : AudioCueId.Unready);
    }

    // Transient server events that cannot be reconstructed faithfully from a replicated snapshot.
    internal void EmitWorldCue(AudioCueId cue, Vector3 position, ulong? recipient = null)
    {
        if (!IsSpawned || !IsServer || !foundationMode) return;
        if (recipient.HasValue && !NetworkManager.ConnectedClients.ContainsKey(recipient.Value)) return;
        WorldCueRpc(cue, position, NetworkManager.ServerTime.Time, recipient.HasValue
            ? RpcTarget.Single(recipient.Value, RpcTargetUse.Temp) : RpcTarget.ClientsAndHost);
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void WorldCueRpc(AudioCueId cue, Vector3 position, double occurredAt, RpcParams rpc = default)
    {
        if (HasLiveAudio && NetworkManager.ServerTime.Time - occurredAt < .7)
            AudioService.Play(cue, position);
    }

    public event Action<GameState, GameState> StateChanged;

    [Header("Integrated Course (optional for older scenes)")]
    [SerializeField] private bool requireAllGimmicks;
    [SerializeField] private BridgeController bridge;
    [SerializeField] private RaftController raft;
    [SerializeField] private DoorPuzzleController door;
    [SerializeField] private JumpPuzzleController jump;

    readonly HashSet<ulong> readyFoundationPeers = new();
    readonly NetworkVariable<int> readyFoundationCount = new(0);

    internal void RegisterFoundationReady(TwinEdge.Gameplay.GameplayCharacter player, ulong sender)
    {
        if (!IsServer || !foundationMode || !player.IsSpawned || player.OwnerClientId != sender ||
            !NetworkManager.ConnectedClients.TryGetValue(sender, out var peer) || peer.PlayerObject != player.NetworkObject)
            return;
        if (Phase >= FoundationPhase.Outbound) AssignFoundationCape(player);
        if (readyFoundationPeers.Add(sender)) player.MarkFoundationReadyServer();
    }

    void AssignFoundationCape(TwinEdge.Gameplay.GameplayCharacter player)
    {
        // The V3 course has two slots. Rejoining can change NGO IDs but restores the vacant slot.
        if (NetworkManager.GetComponent<NetworkSpawnCoordinator>().TryGetSlot(player.OwnerClientId, out int slot))
            player.SetCapeColorServer(slot == 0 ? firstCape : secondCape);
    }

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
    public bool IsGameReady => IsSpawned && connectedPlayerCount.Value == 2 && (!foundationMode || readyFoundationCount.Value == 2);
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
        if (!IsSpawned) return;
        TickCountdownAudio();
        if (!IsServer) return;
        int count = 0, initialized = 0;
        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            if (client.PlayerObject == null || !client.PlayerObject.IsSpawned) continue;
            count++;
            if (readyFoundationPeers.Contains(client.ClientId)) initialized++;
        }
        connectedPlayerCount.Value = count;
        if (foundationMode)
        {
            if (rematchVotes.Value == 3 && Phase == FoundationPhase.Finished)
            {
                if (initialized == 2 && count == 2 &&
                    NetworkManager.GetComponent<NetworkSpawnCoordinator>().TryRebuildMatch(TwinEdge.Gameplay.V3GameplayBootstrap.Current))
                    return;
                rematchVotes.Value = 0;
                rematchLoadFailed.Value = count == 2;
            }
            if (clientReady.Value && !IsConnectedSlot(readyClientId, 1))
            {
                CancelCountdown();
                ClearLobbyReady();
                foundationPhase.Value = FoundationPhase.WaitingForPlayers;
            }
            readyFoundationCount.Value = initialized;
            if (TwinEdge.Gameplay.V3GameplayBootstrap.Current?.ReadyForNetworkPlay == true) StartFoundation();
        }
        if (!foundationMode && count == 2 && courseStartedAt.Value < 0d)
        {
            courseStartedAt.Value = NetworkManager.ServerTime.Time;
            Debug.Log("Game ready. Cooperation started.");
        }
    }

    public override void OnNetworkSpawn()
    {
        countdownAudioSequence = 0;
        countdownAudioBeat = -1;
        foundationPhase.OnValueChanged += OnFoundationChanged;
        currentState.OnValueChanged += HandleStateChanged;
        winnerClientId.OnValueChanged += HandleWinnerChanged;
        clientReady.OnValueChanged += OnClientReadyChanged;
        if (IsServer)
        {
            if (foundationMode && NetworkManager.NetworkConfig.EnableSceneManagement)
                NetworkManager.SceneManager.SetClientSynchronizationMode(UnityEngine.SceneManagement.LoadSceneMode.Additive);
            arrivedClientIds.Clear();
            turnPointArrivalCount.Value = 0;
            winnerClientId.Value = ulong.MaxValue;
            winnerSlot.Value = -1;
            rematchVotes.Value = 0;
            rematchLoadFailed.Value = false;
            currentState.Value = GameState.Cooperation;
            foundationPhase.Value = FoundationPhase.WaitingForPlayers;
            developmentReturnRequested.Value = false;
            transition.Value = default;
            connectedPlayerCount.Value = 0;
            ClearLobbyReady();
            readyFoundationPeers.Clear();
            readyFoundationCount.Value = 0;
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
        if (foundationMode) AudioService.StopSession();
        clientReady.OnValueChanged -= OnClientReadyChanged;
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

        if (HasLiveAudio)
            AudioService.Play(nextValue == NetworkManager.LocalClientId ? AudioCueId.Win : AudioCueId.Lose);
        Debug.Log($"Player {nextValue} wins { (foundationMode ? "V3 Foundation" : "Prototype") }.");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (Phase == FoundationPhase.Finished) rematchVotes.Value = 0;
        if (clientId == readyClientId)
        {
            CancelCountdown();
            ClearLobbyReady();
            if (Phase == FoundationPhase.Preparing) foundationPhase.Value = FoundationPhase.WaitingForPlayers;
        }
        if (readyFoundationPeers.Remove(clientId)) readyFoundationCount.Value = readyFoundationPeers.Count;
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
