using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public sealed class MultiplayerSessionManager : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;

    private ISession activeSession;
    private Task initializationTask;
    private bool operationInProgress;

    public event Action<string> StatusChanged;
    public event Action<string> JoinCodeChanged;

    public string CurrentJoinCode { get; private set; } = string.Empty;
    public string CurrentStatus { get; private set; } = "Offline. Create or join a room.";
    public bool IsBusy => operationInProgress;
    public bool HasSession => activeSession != null;

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
    }

    private void OnDestroy()
    {
        if (networkManager == null)
        {
            return;
        }

        networkManager.OnClientConnectedCallback -= HandleClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    public async Task CreateRoomAsync()
    {
        if (operationInProgress || activeSession != null)
        {
            return;
        }

        operationInProgress = true;
        SetStatus("Initializing Unity Services...");

        try
        {
            await EnsureServicesReadyAsync();

            SessionOptions options = new SessionOptions { MaxPlayers = 2 }.WithRelayNetwork();
            activeSession = await MultiplayerService.Instance.CreateSessionAsync(options);
            CurrentJoinCode = activeSession.Code;
            JoinCodeChanged?.Invoke(CurrentJoinCode);

            Debug.Log($"Relay session created. Join Code: {CurrentJoinCode}", this);
            SetStatus("Room created. Waiting for player...");
            RefreshConnectionStatus();
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not create Relay session: {exception}", this);
            SetStatus($"Room creation failed: {exception.Message}");
            await CleanupFailedOperationAsync();
        }
        finally
        {
            operationInProgress = false;
        }
    }

    public async Task JoinRoomAsync(string joinCode)
    {
        string sanitizedCode = string.IsNullOrWhiteSpace(joinCode)
            ? string.Empty
            : joinCode.Trim().Replace(" ", string.Empty).ToUpperInvariant();

        if (string.IsNullOrEmpty(sanitizedCode))
        {
            SetStatus("Enter a Join Code first.");
            return;
        }

        if (operationInProgress || activeSession != null)
        {
            return;
        }

        operationInProgress = true;
        SetStatus("Joining Relay session...");

        try
        {
            await EnsureServicesReadyAsync();
            activeSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(sanitizedCode);
            CurrentJoinCode = activeSession.Code;
            JoinCodeChanged?.Invoke(CurrentJoinCode);

            Debug.Log($"Joined Relay session {activeSession.Id} with code {CurrentJoinCode}.", this);
            SetStatus("Session joined. Connecting to host...");
            RefreshConnectionStatus();
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not join Relay session with code '{sanitizedCode}': {exception}", this);
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
        if (operationInProgress)
        {
            return;
        }

        operationInProgress = true;
        ISession sessionToLeave = activeSession;
        activeSession = null;

        try
        {
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
            operationInProgress = false;
        }
    }

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
        ISession failedSession = activeSession;
        activeSession = null;

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

        ISession disconnectedSession = activeSession;
        activeSession = null;
        CurrentJoinCode = string.Empty;
        JoinCodeChanged?.Invoke(CurrentJoinCode);
        SetStatus("Connection ended. Return to the room menu.");

        if (disconnectedSession != null)
        {
            _ = LeaveDisconnectedSessionAsync(disconnectedSession);
        }
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
