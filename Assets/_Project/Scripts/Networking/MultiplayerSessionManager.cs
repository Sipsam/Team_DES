using System;
using System.Threading.Tasks;
using TwinEdge.Gameplay;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Multiplayer;
using UnityEngine;

public sealed class MultiplayerSessionManager : MonoBehaviour
{
    internal enum RoomIssue { None, InvalidCode, NotFound, Full, InvalidNickname, Connection, HostLeft, Rebuilding }
    const string HostLeftReason = "Host closed the room.";
    [SerializeField] private NetworkManager networkManager;

    private ISession activeSession;
    internal string MatchRoomId => activeSession?.Id;
    private Task initializationTask;
    private bool operationInProgress;
    private bool leaving;
    private Task disconnectCleanup;

    public event Action<string> StatusChanged;
    public event Action<string> JoinCodeChanged;

    public string CurrentJoinCode { get; private set; } = string.Empty;
    public string CurrentStatus { get; private set; } = "Offline. Create or join a room.";
    public bool IsBusy => operationInProgress || (networkManager != null && networkManager.ShutdownInProgress);
    public bool HasSession => activeSession != null;
    internal RoomIssue Issue { get; private set; }
    internal string InterruptedRoomCode { get; private set; } = string.Empty;
    internal bool WasDisconnected { get; private set; }

    private void Awake()
    {
        if (networkManager == null)
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
        }

        if (networkManager == null)
        {
            SetStatus("NetworkManager not found.");
            Debug.LogError("MultiplayerSessionManager requires a NetworkManager.", this);
            enabled = false;
            return;
        }

        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        networkManager.OnClientStopped += HandleClientStopped;
    }

    private void OnDestroy()
    {
        if (networkManager == null)
        {
            return;
        }

        networkManager.OnClientConnectedCallback -= HandleClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        networkManager.OnClientStopped -= HandleClientStopped;
        DetachSession();
    }

    public async Task CreateRoomAsync(string nickname = null)
    {
        if (IsBusy || activeSession != null)
        {
            return;
        }

        ResetRoomIssue();
        if (!SetConnectionNickname(nickname)) return;
        operationInProgress = true;
        SetStatus("Initializing Unity Services...");

        try
        {
            await PrepareServicesAsync();

            SessionOptions options = new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork();
            AttachSession(await MultiplayerService.Instance.CreateSessionAsync(options));
            CurrentJoinCode = activeSession.Code;
            JoinCodeChanged?.Invoke(CurrentJoinCode);

            Debug.Log("Relay session created.", this);
            SetStatus("Room created. Waiting for player...");
            RefreshConnectionStatus();
        }
        catch (Exception exception)
        {
            Issue = ClassifyIssue(exception, networkManager.DisconnectReason);
            Debug.LogError($"Could not create Relay session ({exception.GetType().Name}, {Issue}).", this);
            SetStatus($"Room creation failed: {exception.Message}");
            await CleanupFailedOperationAsync();
        }
        finally
        {
            operationInProgress = false;
        }
    }

    internal static bool TryNormalizeJoinCode(string code, out string normalized)
    {
        normalized = (code ?? string.Empty).Trim().Replace(" ", string.Empty).ToUpperInvariant();
        if (normalized.Length != 6) return false;
        foreach (char c in normalized)
            if (!(c is >= 'A' and <= 'Z' or >= '0' and <= '9')) return false;
        return true;
    }

    public async Task JoinRoomAsync(string joinCode, string nickname = null)
    {
        if (IsBusy || activeSession != null) return;
        ResetRoomIssue();

        if (!TryNormalizeJoinCode(joinCode, out string sanitizedCode))
        {
            Issue = RoomIssue.InvalidCode;
            SetStatus("Enter a valid Join Code.");
            return;
        }

        if (!SetConnectionNickname(nickname)) return;
        operationInProgress = true;
        SetStatus("Joining Relay session...");

        try
        {
            await PrepareServicesAsync();
            AttachSession(await MultiplayerService.Instance.JoinSessionByCodeAsync(sanitizedCode));
            CurrentJoinCode = activeSession.Code;
            JoinCodeChanged?.Invoke(CurrentJoinCode);

            Debug.Log("Joined Relay session.", this);
            SetStatus("Session joined. Connecting to host...");
            RefreshConnectionStatus();
        }
        catch (Exception exception)
        {
            Issue = ClassifyIssue(exception, networkManager.DisconnectReason);
            Debug.LogWarning($"Could not join Relay session ({exception.GetType().Name}, {Issue}).", this);
            SetStatus($"Join failed: {exception.Message}");
            await CleanupFailedOperationAsync();
        }
        finally
        {
            operationInProgress = false;
        }
    }

    public async Task LeaveRoomAsync()
    {
        if (IsBusy)
        {
            return;
        }

        operationInProgress = true;
        leaving = true;
        ResetRoomIssue();
        ISession sessionToLeave = DetachSession();

        try
        {
            if (networkManager.IsServer)
            {
                for (int i = networkManager.ConnectedClientsIds.Count - 1; i >= 0; i--)
                {
                    ulong clientId = networkManager.ConnectedClientsIds[i];
                    if (clientId != networkManager.LocalClientId)
                        networkManager.DisconnectClient(clientId, HostLeftReason);
                }
            }
            if (sessionToLeave != null)
            {
                await sessionToLeave.LeaveAsync();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Leaving the session reported an error: {exception.Message}", this);
        }
        finally
        {
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            CurrentJoinCode = string.Empty;
            JoinCodeChanged?.Invoke(CurrentJoinCode);
            SetStatus("Left session.");
            leaving = false;
            operationInProgress = false;
        }
    }

    private bool SetConnectionNickname(string nickname)
    {
        if (networkManager.NetworkConfig.PlayerPrefab?.GetComponent<GameplayCharacter>() == null) return true;
        if (!PlayerNickname.TryNormalize(nickname ?? PlayerNickname.Fallback, out string normalized))
        {
            Issue = RoomIssue.InvalidNickname;
            return false;
        }
        networkManager.NetworkConfig.ConnectionData = PlayerNickname.Encode(normalized);
        return true;
    }

    private void ResetRoomIssue()
    {
        Issue = RoomIssue.None;
        InterruptedRoomCode = string.Empty;
        WasDisconnected = false;
    }

    private async Task PrepareServicesAsync()
    {
        if (disconnectCleanup != null) await disconnectCleanup;
        await EnsureServicesReadyAsync();
    }

    private static RoomIssue ClassifyIssue(Exception exception, string disconnectReason)
    {
        if (disconnectReason == NetworkSpawnCoordinator.RoomFullReason) return RoomIssue.Full;
        if (disconnectReason == NetworkSpawnCoordinator.RebuildingReason) return RoomIssue.Rebuilding;
        if (disconnectReason == NetworkSpawnCoordinator.InvalidNicknameReason) return RoomIssue.InvalidNickname;
        for (var cause = exception; cause != null; cause = cause.InnerException)
        {
            if (cause is LobbyServiceException lobby)
            {
                if (lobby.Reason == LobbyExceptionReason.LobbyFull) return RoomIssue.Full;
                if (lobby.Reason == LobbyExceptionReason.InvalidJoinCode) return RoomIssue.InvalidCode;
                if (lobby.Reason is LobbyExceptionReason.LobbyNotFound or LobbyExceptionReason.EntityNotFound)
                    return RoomIssue.NotFound;
            }
            if (cause is not SessionException session) continue;
            if (session.Error is SessionError.SessionNotFound or SessionError.SessionDeleted or SessionError.AllocationNotFound)
                return RoomIssue.NotFound;
            // The pinned Multiplayer SDK drops some Lobby error codes and keeps the service detail.
            if (session.Error != SessionError.Unknown) continue;
            string detail = session.Message;
            if (Contains(detail, "is full") || Contains(detail, "maximum number of players"))
                return RoomIssue.Full;
            if (Contains(detail, "contains an invalid character") || Contains(detail, "invalid join code") ||
                Contains(detail, "invalid session code") || Contains(detail, "code is invalid"))
                return RoomIssue.InvalidCode;
            if (Contains(detail, "not found") || Contains(detail, "could not find") ||
                Contains(detail, "does not exist"))
                return RoomIssue.NotFound;
        }
        return RoomIssue.Connection;
    }

    private static bool Contains(string detail, string text) =>
        detail.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;

    private void AttachSession(ISession session)
    {
        activeSession = session;
        session.Deleted += HandleSessionDeleted;
    }

    private ISession DetachSession()
    {
        ISession session = activeSession;
        if (session != null) session.Deleted -= HandleSessionDeleted;
        activeSession = null;
        return session;
    }

    private void HandleSessionDeleted() => EndConnection(RoomIssue.HostLeft);

    private async Task EnsureServicesReadyAsync()
    {
        if (initializationTask == null)
        {
            initializationTask = InitializeServicesAsync();
        }

        try
        {
            await initializationTask;
        }
        catch
        {
            initializationTask = null;
            throw;
        }
    }

    private static async Task InitializeServicesAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            // Separate app processes need distinct anonymous identities when testing Relay locally.
            string profileName = $"LocalTest_{System.Diagnostics.Process.GetCurrentProcess().Id}";
            InitializationOptions initializationOptions = new InitializationOptions().SetProfile(profileName);
            await UnityServices.InitializeAsync(initializationOptions);
            Debug.Log($"Unity Services initialized with Authentication profile '{profileName}'.");
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    private async Task CleanupFailedOperationAsync()
    {
        ISession failedSession = DetachSession();

        if (failedSession != null)
        {
            try
            {
                await failedSession.LeaveAsync();
            }
            catch (Exception cleanupException)
            {
                Debug.LogWarning($"Session cleanup failed: {cleanupException.Message}", this);
            }
        }

        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        CurrentJoinCode = string.Empty;
        JoinCodeChanged?.Invoke(CurrentJoinCode);
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"NGO client connected: {clientId}.", this);
        RefreshConnectionStatus();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        Debug.LogWarning($"NGO client disconnected: {clientId}.", this);

        if (networkManager != null && networkManager.IsServer && clientId != networkManager.LocalClientId)
        {
            RefreshConnectionStatus();
            return;
        }

        if (!operationInProgress && !leaving)
            EndConnection(networkManager.DisconnectReason == HostLeftReason ? RoomIssue.HostLeft : RoomIssue.Connection);
    }

    private void HandleClientStopped(bool wasHost)
    {
        if (!operationInProgress && !leaving && HasSession)
            EndConnection(networkManager.DisconnectReason == HostLeftReason ? RoomIssue.HostLeft : RoomIssue.Connection);
    }

    private void EndConnection(RoomIssue issue)
    {
        ISession disconnectedSession = DetachSession();
        if (disconnectedSession == null) return;
        WasDisconnected = true;
        Issue = issue;
        InterruptedRoomCode = CurrentJoinCode;
        CurrentJoinCode = string.Empty;
        JoinCodeChanged?.Invoke(CurrentJoinCode);
        SetStatus("Connection ended. Return to the room menu.");

        disconnectCleanup = LeaveDisconnectedSessionAsync(disconnectedSession);
    }

    private async Task LeaveDisconnectedSessionAsync(ISession disconnectedSession)
    {
        try
        {
            await disconnectedSession.LeaveAsync();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not clean up disconnected session: {exception.Message}", this);
        }
    }

    private void RefreshConnectionStatus()
    {
        if (networkManager == null || !networkManager.IsListening)
        {
            return;
        }

        int playerCount = networkManager.ConnectedClientsIds.Count;
        if (playerCount >= 2)
        {
            SetStatus("Game ready. Player connected.");
        }
        else
        {
            SetStatus("Waiting for player... (1/2)");
        }
    }

    private void SetStatus(string message)
    {
        CurrentStatus = message;
        StatusChanged?.Invoke(message);
    }
}
