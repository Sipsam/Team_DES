using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMovement : NetworkBehaviour
{
    internal const float SprintSpeedScale = 1.5f;
    private const float SprintMultiplier = 1.6f * SprintSpeedScale;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float rotationSpeed = 12f;
    [SerializeField] private Transform cameraTransform;

    [Header("Jump and Gravity")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
    [SerializeField, Min(0f)] private float gravity = 20f;
    [SerializeField, Min(0f)] private float groundedDownForce = 2f;
    [SerializeField, Min(0f)] private float externalVelocityDecay = 8f;

    private TwinEdge.Gameplay.GameplayInput gameplayInput;
    private TwinEdge.Gameplay.GameplayCharacter contest;
    private ThirdPersonCamera ownerCamera;
    public Vector3 Velocity => characterController != null ? characterController.velocity : Vector3.zero;
    private CharacterController characterController;
    private RaftController raftController;
    private bool insideRaftControlPoint;
    private bool hasRiverSeat;
    private bool puzzleUiInputBlocked;
    private GameStateManager gameStateManager;
    private float verticalSpeed;
    private float bridgeJumpSpeed;
    private bool sprintJump;
    private float bufferedJumpUntil = float.NegativeInfinity;
    private Vector3 externalVelocity;
    private readonly NetworkVariable<float> movementSpeedMultiplier = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private Pose initialSpawnPose;

    public bool IsGrounded { get; private set; }
    public Pose InitialSpawnPose => initialSpawnPose;

    internal float GroundMoveSpeed(bool sprint) => moveSpeed * (sprint ? SprintMultiplier : 1f);

    internal float GroundSpeedScale => movementSpeedMultiplier.Value *
        (IsSpawned
            ? (TwinEdge.Gameplay.CastleGameplay.Current?.SpeedScale(this) ?? 1f) *
              (TwinEdge.Gameplay.UndergroundGameplay.Current?.SpeedScale(this) ?? 1f)
            : 1f);

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        gameplayInput = GetComponent<TwinEdge.Gameplay.GameplayInput>();
        contest = GetComponent<TwinEdge.Gameplay.GameplayCharacter>();
    }

    private void Update()
    {
        if (IsSpawned && !IsOwner)
        {
            return;
        }

        bool riverSeat = IsSpawned && TwinEdge.Gameplay.RiverGameplay.Current?.ApplyOwnerSeat(this) == true;
        if (riverSeat)
        {
            if (!hasRiverSeat)
            {
                characterController.enabled = false;
                ResetVerticalVelocity();
            }
            hasRiverSeat = true;
            return;
        }
        if (hasRiverSeat) ReleaseRiverSeat();

        if (IsSpawned && TwinEdge.Gameplay.CooperativePole.Current != null &&
            TwinEdge.Gameplay.CooperativePole.Current.IsAttached(OwnerClientId))
        {
            ResetVerticalVelocity();
            return;
        }

        if (GetComponent<TwinEdge.Gameplay.MountainReturnLaunch>()?.ControlsMovement == true) return;

        // A pole release/respawn can arrive before its LateUpdate restores the controller.
        if (!characterController.enabled) return;
        if (IsSpawned && contest?.ApplyOwnerContestMovement() == true) return;

        bool canReadInput = (gameplayInput == null || gameplayInput.AcceptsInput) && !puzzleUiInputBlocked && (!IsSpawned ||
            (gameStateManager != null && gameStateManager.CanPlay));

        IsGrounded = characterController.isGrounded;

        if (IsGrounded)
        {
            bridgeJumpSpeed = 0f;
            sprintJump = false;
        }
        if (IsGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -groundedDownForce;
        }

        Vector2 input = canReadInput ? (gameplayInput != null ? gameplayInput.Move?.ReadValue<Vector2>() ?? Vector2.zero : ReadMovementInput()) : Vector2.zero;
        if (insideRaftControlPoint || (raftController != null && raftController.IsLocalPlayerAtControlPoint))
        {
            // W/S operate the raft while assigned to a control point; A/D still let the player leave it.
            input.y = 0f;
        }

        Vector3 moveDirection = GetCameraRelativeDirection(input);

        var bridge = TwinEdge.Gameplay.BridgeGameplay.Current;
        var castle = IsSpawned ? TwinEdge.Gameplay.CastleGameplay.Current : null;
        bool wantsSprint = canReadInput && gameplayInput?.Sprint?.IsPressed() == true && castle?.SprintBlocked(this) != true;
        bool sprint = wantsSprint && (IsSpawned && contest != null
            ? contest.ReadSprint(input.sqrMagnitude > .001f) : true);
        if (IsSpawned && bridge != null && bridge.Holds(OwnerClientId))
        {
            bridge.MoveHolder(this, characterController, moveDirection, sprint);
            verticalSpeed = 0f;
            externalVelocity = Vector3.zero;
            IsGrounded = characterController.isGrounded;
            return;
        }

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }

        Keyboard keyboard = Keyboard.current;
        bool pressedJump = canReadInput && (gameplayInput != null
            ? gameplayInput.Jump?.WasPressedThisFrame() == true : keyboard?.spaceKey.wasPressedThisFrame == true);
        bool jump = pressedJump && IsGrounded;
        bool onBridge = bridge?.UsesBridgeJump(transform.position) == true;
        if (IsSpawned && contest != null)
        {
            if (pressedJump) bufferedJumpUntil = Time.time + .12f;
            jump = canReadInput && IsGrounded && Time.time <= bufferedJumpUntil;
            if (jump)
            {
                bufferedJumpUntil = float.NegativeInfinity;
                jump = contest.RequestJump(wantsSprint, out bool fast);
                if (jump) sprint = fast;
            }
        }
        if (jump)
        {
            verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
            sprintJump = sprint;
            if (onBridge && bridge != null) bridgeJumpSpeed = bridge.BridgeJumpSpeed(sprint);
        }

        verticalSpeed -= gravity * Time.deltaTime;
        bool fastStride = sprint || sprintJump && wantsSprint && contest?.HasStamina == true;
        float horizontalSpeed = bridgeJumpSpeed > 0 ? bridgeJumpSpeed : GroundMoveSpeed(fastStride);
        Vector3 velocity = moveDirection * (horizontalSpeed * GroundSpeedScale);
        velocity += externalVelocity;
        velocity.y = verticalSpeed + externalVelocity.y;
        characterController.Move(velocity * Time.deltaTime);
        externalVelocity = Vector3.MoveTowards(
            externalVelocity, Vector3.zero, externalVelocityDecay * Time.deltaTime);

        IsGrounded = characterController.isGrounded;
    }

    public override void OnNetworkSpawn()
    {
        gameStateManager = FindFirstObjectByType<GameStateManager>();
        if (IsServer)
        {
            initialSpawnPose = new Pose(transform.position, transform.rotation);
            FindFirstObjectByType<RespawnManager>()?.RegisterSpawn(this);
        }

        if (!IsOwner)
        {
            return;
        }

        raftController = FindFirstObjectByType<RaftController>();

        Camera mainCamera = TwinEdge.Gameplay.V3GameplayBootstrap.Current?.standbyCamera ?? Camera.main;
        ownerCamera = mainCamera != null
            ? mainCamera.GetComponent<ThirdPersonCamera>()
            : null;

        if (ownerCamera != null)
        {
            cameraTransform = ownerCamera.transform;
            ownerCamera.SetTarget(transform.Find("CameraTarget"));
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
        {
            return;
        }

        if (hasRiverSeat) ReleaseRiverSeat();
        if (ownerCamera != null) ownerCamera.ClearTarget(transform.Find("CameraTarget"));
        ownerCamera = null;
    }

    public void ResetVerticalVelocity()
    {
        bridgeJumpSpeed = 0f;
        sprintJump = false;
        bufferedJumpUntil = float.NegativeInfinity;
        verticalSpeed = 0f;
        externalVelocity = Vector3.zero;
        IsGrounded = false;
    }

    public void SetGrabbedServer(bool grabbed, float moveMultiplier = 1f)
    {
        if (!IsServer)
        {
            return;
        }

        movementSpeedMultiplier.Value = grabbed ? Mathf.Clamp01(moveMultiplier) : 1f;
    }

    internal void ReconcileV3Jump(bool approved, bool sprint)
    {
        if (!approved) verticalSpeed = Mathf.Min(0, verticalSpeed);
        if (IsGrounded) return;
        if (bridgeJumpSpeed > 0)
            bridgeJumpSpeed = approved ? TwinEdge.Gameplay.BridgeGameplay.Current?.BridgeJumpSpeed(sprint) ?? 0 : 0;
        sprintJump = approved && sprint;
    }

    internal void ContinueContestVelocity(Vector3 velocity, bool preserveHorizontal)
    {
        verticalSpeed = velocity.y;
        if (preserveHorizontal) externalVelocity += Vector3.ProjectOnPlane(velocity, Vector3.up);
    }

    public void ApplyMovingPlatformDelta(Vector3 displacement, Quaternion rotationDelta)
    {
        if (IsSpawned && !IsOwner)
        {
            return;
        }

        if (characterController != null && characterController.enabled)
        {
            characterController.Move(displacement);
        }

        transform.rotation = rotationDelta * transform.rotation;
    }

    public void SetInsideRaftControlPoint(bool inside)
    {
        if (!IsSpawned || IsOwner)
        {
            insideRaftControlPoint = inside;
        }
    }

    public void SetPuzzleUiInputBlocked(bool blocked)
    {
        puzzleUiInputBlocked = blocked;
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void ApplyExternalVelocityRpc(Vector3 velocity)
    {
        if (!IsOwner)
        {
            return;
        }

        if (contest?.BlocksContestImpulse == true) return;
        externalVelocity += Vector3.ClampMagnitude(velocity, 12f);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    internal void ApplyApprovedSpawnRpc(Vector3 position, Quaternion rotation)
    {
        PlaceAtOwner(position, rotation);
        if (IsOwner && TwinEdge.Gameplay.V3GameplayBootstrap.Current?.state.Phase < FoundationPhase.Outbound)
            ownerCamera?.FaceInitialSpawn(rotation);
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    public void RespawnAtOwnerClientRpc(Vector3 position, Quaternion rotation, int life,
        RpcParams rpcParams = default)
    {
        if (!IsOwner)
        {
            return;
        }

        contest?.ResetContestOwner(life);
        TwinEdge.Gameplay.RiverGameplay.Current?.SuspendOwnerSeat(OwnerClientId);
        PlaceAtOwner(position, rotation);
    }

    private void ReleaseRiverSeat()
    {
        hasRiverSeat = false;
        characterController.enabled = true;
        ResetVerticalVelocity();
    }

    internal void PlaceAtOwner(Vector3 position, Quaternion rotation)
    {
        if (!IsOwner) return;
        if (hasRiverSeat) ReleaseRiverSeat();
        GetComponent<TwinEdge.Gameplay.PoleMotor>()?.SuspendForRespawn();
        GetComponent<TwinEdge.Gameplay.MountainReturnLaunch>()?.ResetOwner();

        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (controllerWasEnabled)
        {
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(position, rotation);
        GetComponent<OwnerNetworkTransform>()?.Teleport(position, rotation, transform.localScale);
        insideRaftControlPoint = false;
        GetComponent<TwinEdge.Gameplay.GameplayCharacter>()?.ResetAfterRespawn();
        ResetVerticalVelocity();

        if (controllerWasEnabled)
        {
            characterController.enabled = true;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestCheckpointServerRpc(string checkpointName, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (!IsServer || senderClientId != OwnerClientId)
        {
            return;
        }

        Checkpoint[] availableCheckpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
        foreach (Checkpoint checkpoint in availableCheckpoints)
        {
            if (checkpoint.gameObject.name != checkpointName || !checkpoint.CanRegister(this))
            {
                continue;
            }

            checkpoint.RegisterPlayerCheckpoint(this);
            return;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestKillZoneRespawnServerRpc(string killZoneName, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (!IsServer || senderClientId != OwnerClientId)
        {
            return;
        }

        KillZone[] killZones = FindObjectsByType<KillZone>(FindObjectsSortMode.None);
        foreach (KillZone killZone in killZones)
        {
            if (killZone.gameObject.name == killZoneName && killZone.ContainsPlayerPosition(transform.position))
            {
                FindFirstObjectByType<RespawnManager>()?.RespawnPlayer(this);
                return;
            }
        }
    }

    private static Vector2 ReadMovementInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed) horizontal -= 1f;
        if (keyboard.dKey.isPressed) horizontal += 1f;
        if (keyboard.sKey.isPressed) vertical -= 1f;
        if (keyboard.wKey.isPressed) vertical += 1f;

        return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
    }

    internal Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        GetMovementAxes(out var forward, out var right);
        Vector3 direction = forward * input.y + right * input.x;
        return Vector3.ClampMagnitude(direction, 1f);
    }

    internal void GetMovementAxes(out Vector3 forward, out Vector3 right)
    {
        Transform reference = cameraTransform;
        if (reference == null && Camera.main != null)
        {
            reference = Camera.main.transform;
        }

        forward = reference != null ? reference.forward : transform.forward;
        right = reference != null ? reference.right : transform.right;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        right = Vector3.ProjectOnPlane(right, Vector3.up).normalized;
    }
}
