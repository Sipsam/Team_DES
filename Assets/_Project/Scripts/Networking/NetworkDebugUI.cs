using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public sealed class NetworkDebugUI : MonoBehaviour
{
    private static readonly Rect MenuRect = new Rect(12f, 12f, 280f, 330f);
    private static NetworkDebugUI activeInstance;

    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private BridgeController bridgeController;
    [SerializeField] private string address = "127.0.0.1";
    [SerializeField, Min(1)] private int port = 7777;

    private string statusMessage = "Offline";

    private void Awake()
    {
        activeInstance = this;
        if (networkManager == null)
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
        }

        if (gameStateManager == null)
        {
            gameStateManager = FindFirstObjectByType<GameStateManager>();
        }

        if (bridgeController == null)
        {
            bridgeController = FindFirstObjectByType<BridgeController>();
        }
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

    public static bool IsPointerOverMenu(Vector2 screenPosition)
    {
        if (activeInstance == null || activeInstance.networkManager == null ||
            !activeInstance.networkManager.IsListening)
        {
            return false;
        }

        // Input System screen coordinates start at the bottom left; IMGUI starts at the top left.
        Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        return MenuRect.Contains(guiPosition);
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(MenuRect, GUI.skin.box);

        if (networkManager == null)
        {
            GUILayout.Label("NetworkManager not found.");
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label($"Status: {GetStatus()}");

        if (!networkManager.IsListening)
        {
            if (GUILayout.Button("Start Host", GUILayout.Height(32f)))
            {
                StartSession(asHost: true);
            }

            if (GUILayout.Button("Start Client", GUILayout.Height(32f)))
            {
                StartSession(asHost: false);
            }
        }
        else
        {
            GUILayout.Label("Press Esc to unlock the cursor for this menu.");
            if (GUILayout.Button("Stop Session", GUILayout.Height(32f)))
            {
                StopSession();
            }
        }

        if (gameStateManager != null)
        {
            int connectedPlayers = networkManager.IsListening
                ? networkManager.ConnectedClientsIds.Count
                : 0;
            GUILayout.Space(8f);
            GUILayout.Label($"Game State: {gameStateManager.CurrentState}");
            GUILayout.Label($"TurnPoint: {gameStateManager.TurnPointArrivalCount}/{connectedPlayers}");
            GUILayout.Label(gameStateManager.HasWinner
                ? $"Winner: Player {gameStateManager.WinnerClientId + 1} (Client {gameStateManager.WinnerClientId})"
                : "Winner: None");
        }

        if (bridgeController != null)
        {
            GUILayout.Label($"Bridge: {bridgeController.CurrentState}");
            GUILayout.Label($"Switch A: {(bridgeController.IsSwitchActivated(BridgeSwitchId.A) ? "Active" : "Inactive")}");
            GUILayout.Label($"Switch B: {(bridgeController.IsSwitchActivated(BridgeSwitchId.B) ? "Active" : "Inactive")}");
        }

        GUILayout.Label(statusMessage);
        GUILayout.EndArea();
    }

    private void StartSession(bool asHost)
    {
        UnityTransport transport = networkManager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            statusMessage = "UnityTransport missing.";
            Debug.LogError(statusMessage, networkManager);
            return;
        }

        ushort networkPort = (ushort)Mathf.Clamp(port, 1, ushort.MaxValue);
        transport.SetConnectionData(address, networkPort, "0.0.0.0");
        bool started = asHost ? networkManager.StartHost() : networkManager.StartClient();
        statusMessage = started
            ? (asHost ? "Host started." : $"Connecting to {address}:{networkPort}...")
            : "Could not start network session. See Console.";

        if (!started)
        {
            Debug.LogError(statusMessage, networkManager);
        }
    }

    private void StopSession()
    {
        networkManager.Shutdown();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        statusMessage = "Offline";
    }

    private string GetStatus()
    {
        if (networkManager == null || !networkManager.IsListening)
        {
            return "Offline";
        }

        if (networkManager.IsHost)
        {
            return "Host";
        }

        return networkManager.IsServer ? "Server" : "Client";
    }
}
