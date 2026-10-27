using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public enum RaftControlSide
{
    Left,
    Right
}

public enum RaftState
{
    Waiting,
    Ready,
    Moving,
    Completed
}

[RequireComponent(typeof(NetworkObject))]
public sealed class RaftController : NetworkBehaviour
{
    private const ulong NoClient = ulong.MaxValue;
    private const float InputTimeout = 0.4f;
    private const float InputKeepAliveInterval = 0.12f;

    [Header("References")]
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private Transform leftControlPoint;
    [SerializeField] private Transform rightControlPoint;
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private Transform raftEnd;
    [SerializeField] private GameObject competitionPlatformRoute;

    [Header("Server Raft Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 3f;
    [SerializeField, Min(0f)] private float turnSpeed = 30f;
    [SerializeField, Min(0.1f)] private float completionRadius = 2f;

    [Header("Server Validation")]
    [SerializeField, Min(0.1f)] private float controlPointValidationRange = 2.5f;

    [Header("Cooperation Recovery")]
    [SerializeField, Min(0f)] private float emptyRaftResetDelay = 4f;

    private readonly NetworkVariable<ulong> leftOccupant = new NetworkVariable<ulong>(
        NoClient, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<ulong> rightOccupant = new NetworkVariable<ulong>(
        NoClient, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> cooperationCompleted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<RaftState> raftState = new NetworkVariable<RaftState>(
        RaftState.Waiting, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float leftInput;
    private float rightInput;
    private float lastLeftInputTime;
    private float lastRightInputTime;
    private float lastSentInput;
    private float nextInputKeepAliveTime;
    private int lastSentSide = -1;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool hasInitialPose;
    private float emptyRaftTimer;

    public ulong LeftOccupantClientId => leftOccupant.Value;
    public ulong RightOccupantClientId => rightOccupant.Value;
    public bool IsCooperationCompleted => cooperationCompleted.Value;
    public RaftState CurrentState => raftState.Value;

    public bool IsLocalPlayerAtControlPoint
    {
        get
        {
            if (NetworkManager == null || !NetworkManager.IsClient)
            {
                return false;
            }

            ulong localClientId = NetworkManager.LocalClientId;
            return leftOccupant.Value == localClientId || rightOccupant.Value == localClientId;
        }
    }

    private void Awake()
    {
        CaptureInitialPose();
    }

    private void CaptureInitialPose()
    {
        initialPosition = respawnPoint != null ? respawnPoint.position : transform.position;
        initialRotation = respawnPoint != null ? respawnPoint.rotation : transform.rotation;
        hasInitialPose = true;
    }

    public override void OnNetworkSpawn()
    {
        leftOccupant.OnValueChanged += HandleOccupancyChanged;
        rightOccupant.OnValueChanged += HandleOccupancyChanged;
        raftState.OnValueChanged += HandleRaftStateChanged;

        if (gameStateManager == null)
        {
            gameStateManager = FindFirstObjectByType<GameStateManager>();
        }

        if (gameStateManager != null)
        {
            gameStateManager.StateChanged += HandleGameStateChanged;
            ApplyGameState(gameStateManager.CurrentState);
        }

        if (IsServer)
        {
            if (!hasInitialPose)
            {
                CaptureInitialPose();
            }

            // In-scene NetworkObjects keep their runtime Transform after a previous session ends.
            // Restore the explicit spawn marker before this session accepts raft input.
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            leftOccupant.Value = NoClient;
            rightOccupant.Value = NoClient;
            cooperationCompleted.Value = false;
            leftInput = 0f;
            rightInput = 0f;
            lastLeftInputTime = Time.time;
            lastRightInputTime = Time.time;
            emptyRaftTimer = 0f;
            SetRaftState(RaftState.Waiting);
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        RefreshDebugStatus();
    }

    public override void OnNetworkDespawn()
    {
        leftOccupant.OnValueChanged -= HandleOccupancyChanged;
        rightOccupant.OnValueChanged -= HandleOccupancyChanged;
        raftState.OnValueChanged -= HandleRaftStateChanged;

        if (gameStateManager != null)
        {
            gameStateManager.StateChanged -= HandleGameStateChanged;
        }

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    private void Update()
    {
        if (IsClient)
        {
            SendLocalControlInput();
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer)
        {
            return;
        }

        ClearStaleInputs();

        GameState state = CurrentGameState;
        int occupiedCount = GetOccupiedCount();
        if (state == GameState.Finished)
        {
            emptyRaftTimer = 0f;
            SetRaftState(RaftState.Waiting);
            return;
        }

        if (state == GameState.Cooperation && cooperationCompleted.Value)
        {
            emptyRaftTimer = 0f;
            SetRaftState(RaftState.Completed);
            return;
        }

        if (state == GameState.Cooperation && TryRecoverAbandonedRaft())
        {
            return;
        }

        bool enoughPlayers = state == GameState.Cooperation ? occupiedCount == 2 : occupiedCount > 0;
        if (!enoughPlayers)
        {
            SetRaftState(RaftState.Waiting);
            return;
        }

        float drive;
        float steer;
        if (state == GameState.Competition && occupiedCount == 1)
        {
            drive = leftOccupant.Value != NoClient ? leftInput : rightInput;
            steer = 0f;
        }
        else
        {
            drive = (leftInput + rightInput) * 0.5f;
            // Left control forward steers right; right control forward steers left.
            steer = (leftInput - rightInput) * 0.5f;
        }

        if (Mathf.Abs(drive) < 0.01f && Mathf.Abs(steer) < 0.01f)
        {
            SetRaftState(RaftState.Ready);
            return;
        }

        SetRaftState(RaftState.Moving);
        transform.Rotate(0f, steer * turnSpeed * Time.fixedDeltaTime, 0f, Space.World);
        transform.position += transform.forward * (drive * moveSpeed * Time.fixedDeltaTime);

        if (state == GameState.Cooperation && raftEnd != null &&
            Vector3.Distance(transform.position, raftEnd.position) <= completionRadius)
        {
            cooperationCompleted.Value = true;
            leftInput = 0f;
            rightInput = 0f;
            SetRaftState(RaftState.Completed);
            Debug.Log("Raft Cooperation Completed");
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestControlPointServerRpc(int sideValue, RpcParams rpcParams = default)
    {
        if (!IsServer || sideValue < (int)RaftControlSide.Left || sideValue > (int)RaftControlSide.Right)
        {
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
            client.PlayerObject == null || CurrentGameState == GameState.Finished)
        {
            return;
        }

        RaftControlSide side = (RaftControlSide)sideValue;
        Transform point = GetControlPoint(side);
        if (point == null || Vector3.Distance(client.PlayerObject.transform.position, point.position) >
            controlPointValidationRange)
        {
            return;
        }

        ulong currentOccupant = GetOccupant(side);
        if (currentOccupant == clientId)
        {
            return;
        }

        if (currentOccupant != NoClient)
        {
            return;
        }

        if (GetOccupant(OtherSide(side)) == clientId)
        {
            if (CurrentGameState == GameState.Cooperation)
            {
                return;
            }

            SetOccupant(OtherSide(side), NoClient);
        }

        SetOccupant(side, clientId);
        SetInput(side, 0f);
        SetInputTime(side, Time.time);
        RefreshDebugStatus();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestReleaseControlPointServerRpc(int sideValue, RpcParams rpcParams = default)
    {
        if (!IsServer || sideValue < (int)RaftControlSide.Left || sideValue > (int)RaftControlSide.Right)
        {
            return;
        }

        RaftControlSide side = (RaftControlSide)sideValue;
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (GetOccupant(side) != clientId)
        {
            return;
        }

        SetOccupant(side, NoClient);
        SetInput(side, 0f);
        RefreshDebugStatus();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone,
        Delivery = RpcDelivery.Unreliable)]
    private void SubmitControlInputServerRpc(int sideValue, float input, RpcParams rpcParams = default)
    {
        if (!IsServer || sideValue < (int)RaftControlSide.Left || sideValue > (int)RaftControlSide.Right)
        {
            return;
        }

        RaftControlSide side = (RaftControlSide)sideValue;
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (GetOccupant(side) != clientId || CurrentGameState == GameState.Finished)
        {
            return;
        }

        SetInput(side, Mathf.Clamp(input, -1f, 1f));
        SetInputTime(side, Time.time);
    }

    private void SendLocalControlInput()
    {
        if (!IsSpawned || NetworkManager == null || !NetworkManager.IsClient)
        {
            return;
        }

        ulong localClientId = NetworkManager.LocalClientId;
        int side = leftOccupant.Value == localClientId ? (int)RaftControlSide.Left :
            rightOccupant.Value == localClientId ? (int)RaftControlSide.Right : -1;

        if (side < 0)
        {
            lastSentSide = -1;
            lastSentInput = 0f;
            return;
        }

        Keyboard keyboard = Keyboard.current;
        float input = 0f;
        if (Cursor.lockState == CursorLockMode.Locked && keyboard != null)
        {
            if (keyboard.wKey.isPressed) input += 1f;
            if (keyboard.sKey.isPressed) input -= 1f;
        }

        bool changed = side != lastSentSide || !Mathf.Approximately(input, lastSentInput);
        bool keepAlive = Mathf.Abs(input) > 0.01f && Time.time >= nextInputKeepAliveTime;
        if (!changed && !keepAlive)
        {
            return;
        }

        SubmitControlInputServerRpc(side, input);
        lastSentSide = side;
        lastSentInput = input;
        nextInputKeepAliveTime = Time.time + InputKeepAliveInterval;
    }

    private void ClearStaleInputs()
    {
        if (leftOccupant.Value == NoClient || Time.time - lastLeftInputTime > InputTimeout)
        {
            leftInput = 0f;
        }

        if (rightOccupant.Value == NoClient || Time.time - lastRightInputTime > InputTimeout)
        {
            rightInput = 0f;
        }
    }

    private bool TryRecoverAbandonedRaft()
    {
        if (GetOccupiedCount() > 0 || IsAnyPlayerOnRaftDeck())
        {
            emptyRaftTimer = 0f;
            return false;
        }

        bool raftMovedFromStart =
            (transform.position - initialPosition).sqrMagnitude > 0.25f ||
            Quaternion.Angle(transform.rotation, initialRotation) > 5f;

        if (!raftMovedFromStart)
        {
            emptyRaftTimer = 0f;
            return false;
        }

        emptyRaftTimer += Time.fixedDeltaTime;
        if (emptyRaftTimer < emptyRaftResetDelay)
        {
            SetRaftState(RaftState.Waiting);
            return true;
        }

        transform.SetPositionAndRotation(initialPosition, initialRotation);
        leftInput = 0f;
        rightInput = 0f;
        lastLeftInputTime = Time.time;
        lastRightInputTime = Time.time;
        emptyRaftTimer = 0f;
        SetRaftState(RaftState.Waiting);
        Debug.Log("Both players left the raft. Raft returned to its start position.");
        return true;
    }

    private bool IsAnyPlayerOnRaftDeck()
    {
        if (NetworkManager == null)
        {
            return false;
        }

        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
            {
                continue;
            }

            Vector3 localPosition = transform.InverseTransformPoint(client.PlayerObject.transform.position);
            bool overDeck = Mathf.Abs(localPosition.x) <= 2.5f &&
                            Mathf.Abs(localPosition.z) <= 2.5f &&
                            localPosition.y >= -0.25f && localPosition.y <= 2.25f;
            if (overDeck)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        bool released = false;
        if (leftOccupant.Value == clientId)
        {
            leftOccupant.Value = NoClient;
            leftInput = 0f;
            released = true;
        }

        if (rightOccupant.Value == clientId)
        {
            rightOccupant.Value = NoClient;
            rightInput = 0f;
            released = true;
        }

        if (released)
        {
            RefreshDebugStatus();
        }
    }

    private void HandleGameStateChanged(GameState previous, GameState next)
    {
        ApplyGameState(next);
        if (IsServer && next == GameState.Finished)
        {
            leftOccupant.Value = NoClient;
            rightOccupant.Value = NoClient;
            leftInput = 0f;
            rightInput = 0f;
            SetRaftState(RaftState.Waiting);
        }
    }

    private void ApplyGameState(GameState state)
    {
        if (competitionPlatformRoute != null)
        {
            competitionPlatformRoute.SetActive(state == GameState.Competition);
        }
    }

    private void HandleOccupancyChanged(ulong previous, ulong next)
    {
        RefreshDebugStatus();
    }

    private void HandleRaftStateChanged(RaftState previous, RaftState next)
    {
        Debug.Log($"Raft State: {next}");
    }

    private void RefreshDebugStatus()
    {
        string left = leftOccupant.Value == NoClient ? "Empty" : $"Player {leftOccupant.Value + 1}";
        string right = rightOccupant.Value == NoClient ? "Empty" : $"Player {rightOccupant.Value + 1}";
        Debug.Log($"Raft Controls — Left: {left}, Right: {right}, State: {raftState.Value}");
    }

    private GameState CurrentGameState
    {
        get
        {
            if (gameStateManager == null)
            {
                gameStateManager = FindFirstObjectByType<GameStateManager>();
            }

            return gameStateManager != null ? gameStateManager.CurrentState : GameState.Finished;
        }
    }

    private int GetOccupiedCount()
    {
        int count = 0;
        if (leftOccupant.Value != NoClient) count++;
        if (rightOccupant.Value != NoClient) count++;
        return count;
    }

    private ulong GetOccupant(RaftControlSide side)
    {
        return side == RaftControlSide.Left ? leftOccupant.Value : rightOccupant.Value;
    }

    private void SetOccupant(RaftControlSide side, ulong clientId)
    {
        if (side == RaftControlSide.Left)
        {
            leftOccupant.Value = clientId;
        }
        else
        {
            rightOccupant.Value = clientId;
        }
    }

    private Transform GetControlPoint(RaftControlSide side)
    {
        return side == RaftControlSide.Left ? leftControlPoint : rightControlPoint;
    }

    private void SetInput(RaftControlSide side, float input)
    {
        if (side == RaftControlSide.Left) leftInput = input;
        else rightInput = input;
    }

    private void SetInputTime(RaftControlSide side, float time)
    {
        if (side == RaftControlSide.Left) lastLeftInputTime = time;
        else lastRightInputTime = time;
    }

    private void SetRaftState(RaftState nextState)
    {
        if (raftState.Value != nextState)
        {
            raftState.Value = nextState;
        }
    }

    private static RaftControlSide OtherSide(RaftControlSide side)
    {
        return side == RaftControlSide.Left ? RaftControlSide.Right : RaftControlSide.Left;
    }
}
