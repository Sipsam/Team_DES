using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float rotationSpeed = 12f;
    [SerializeField] private Transform cameraTransform;

    [Header("Jump and Gravity")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
    [SerializeField, Min(0f)] private float gravity = 20f;
    [SerializeField, Min(0f)] private float groundedDownForce = 2f;
    [SerializeField, Min(0f)] private float externalVelocityDecay = 8f;

    private CharacterController characterController;
    private RaftController raftController;
    private bool insideRaftControlPoint;
    private bool puzzleUiInputBlocked;
    private GameStateManager gameStateManager;
    private float verticalSpeed;
    private Vector3 externalVelocity;
    private readonly NetworkVariable<float> movementSpeedMultiplier = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private Pose initialSpawnPose;

    public bool IsGrounded { get; private set; }
    public Pose InitialSpawnPose => initialSpawnPose;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (IsSpawned && !IsOwner)
        {
            return;
        }

        bool canReadInput = !puzzleUiInputBlocked && (!IsSpawned ||
            (gameStateManager != null && gameStateManager.CanPlay));

        IsGrounded = characterController.isGrounded;

        if (IsGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -groundedDownForce;
        }

        Vector2 input = canReadInput ? ReadMovementInput() : Vector2.zero;
        if (insideRaftControlPoint || (raftController != null && raftController.IsLocalPlayerAtControlPoint))
        {
            // W/S operate the raft while assigned to a control point; A/D still let the player leave it.
            input.y = 0f;
        }

        Vector3 moveDirection = GetCameraRelativeDirection(input);

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }

        Keyboard keyboard = Keyboard.current;
        if (canReadInput && IsGrounded && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            verticalSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
        }

        verticalSpeed -= gravity * Time.deltaTime;
        Vector3 velocity = moveDirection * (moveSpeed * movementSpeedMultiplier.Value);
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

        Camera mainCamera = Camera.main;
        ThirdPersonCamera thirdPersonCamera = mainCamera != null
            ? mainCamera.GetComponent<ThirdPersonCamera>()
            : null;

        if (thirdPersonCamera != null)
        {
            cameraTransform = thirdPersonCamera.transform;
            thirdPersonCamera.SetTarget(transform.Find("CameraTarget"));
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        ThirdPersonCamera thirdPersonCamera = mainCamera != null
            ? mainCamera.GetComponent<ThirdPersonCamera>()
            : null;

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.ClearTarget(transform.Find("CameraTarget"));
        }
    }

    public void ResetVerticalVelocity()
    {
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

        externalVelocity += Vector3.ClampMagnitude(velocity, 12f);
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    public void RespawnAtOwnerClientRpc(Vector3 position, Quaternion rotation,
        RpcParams rpcParams = default)
    {
        if (!IsOwner)
        {
            return;
        }

        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (controllerWasEnabled)
        {
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(position, rotation);
        GetComponent<OwnerNetworkTransform>()?.Teleport(position, rotation, transform.localScale);
        insideRaftControlPoint = false;
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

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Transform reference = cameraTransform;
        if (reference == null && Camera.main != null)
        {
            reference = Camera.main.transform;
        }

        Vector3 forward = reference != null ? reference.forward : transform.forward;
        Vector3 right = reference != null ? reference.right : transform.right;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        right = Vector3.ProjectOnPlane(right, Vector3.up).normalized;

        Vector3 direction = forward * input.y + right * input.x;
        return Vector3.ClampMagnitude(direction, 1f);
    }
}
