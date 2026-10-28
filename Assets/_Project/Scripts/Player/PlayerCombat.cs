using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerCombat : NetworkBehaviour
{
    [Header("Push")]
    [SerializeField, Min(0f)] private float pushRange = 2.2f;
    [SerializeField, Min(0f)] private float pushForce = 4f;
    [SerializeField, Min(0f)] private float pushCooldown = 1f;
    [SerializeField, Range(0f, 360f)] private float pushAngle = 110f;
    [SerializeField, Range(0f, 1f)] private float cooperationPushMultiplier = 0.45f;

    [Header("Grab")]
    [SerializeField, Min(0f)] private float grabRange = 1.8f;
    [SerializeField, Min(0.1f)] private float grabDuration = 1.4f;
    [SerializeField, Range(0f, 1f)] private float grabMoveMultiplier = 0.3f;
    [SerializeField, Min(0f)] private float grabCooldown = 2f;
    [SerializeField, Range(0f, 360f)] private float grabAngle = 100f;

    [Header("Hit")]
    [SerializeField, Min(0f)] private float hitRange = 2f;
    [SerializeField, Min(0f)] private float hitForce = 6f;
    [SerializeField, Min(0f)] private float hitCooldown = 1.2f;
    [SerializeField] private bool hitEnabledInCooperation;
    [SerializeField, Range(0f, 1f)] private float cooperationHitMultiplier = 0.25f;

    [Header("Feedback")]
    [SerializeField] private Color grabbedColor = new Color(1f, 0.72f, 0.18f);
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.18f;

    private static readonly NetworkVariableReadPermission ReadEveryone = NetworkVariableReadPermission.Everyone;
    private static readonly NetworkVariableWritePermission WriteServer = NetworkVariableWritePermission.Server;
    private readonly NetworkVariable<bool> isGrabbed = new NetworkVariable<bool>(false, ReadEveryone, WriteServer);
    private MaterialPropertyBlock materialProperties;

    private PlayerMovement movement;
    private GameStateManager gameStateManager;
    private PlayerCombat heldTarget;
    private PlayerCombat grabbedBy;
    private Renderer[] renderers;
    private float grabEndsAt;
    private float nextPushTime;
    private float nextGrabTime;
    private float nextHitTime;
    private float hitFlashEndsAt;

    public bool IsGrabbed => isGrabbed.Value;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        renderers = GetComponentsInChildren<Renderer>(true);
        materialProperties = new MaterialPropertyBlock();
    }

    public override void OnNetworkSpawn()
    {
        isGrabbed.OnValueChanged += HandleGrabbedChanged;
        gameStateManager = FindFirstObjectByType<GameStateManager>();
        UpdateVisuals();
    }

    public override void OnNetworkDespawn()
    {
        isGrabbed.OnValueChanged -= HandleGrabbedChanged;
        if (IsServer)
        {
            ForceEndGrabServer();
        }
    }

    private void Update()
    {
        if (IsServer)
        {
            UpdateServerGrabTimer();
        }

        if (IsOwner)
        {
            ReadLocalInput();
        }

        if (hitFlashEndsAt > 0f && Time.time >= hitFlashEndsAt)
        {
            hitFlashEndsAt = 0f;
            UpdateVisuals();
        }
    }

    private void ReadLocalInput()
    {
        if (NetworkManager == null || gameStateManager == null || !gameStateManager.CanPlay ||
            DoorPuzzleUI.IsPanelVisible ||
            Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.fKey.wasPressedThisFrame)
            {
                RequestPushServerRpc();
            }

            if (keyboard.gKey.wasPressedThisFrame)
            {
                RequestGrabStartServerRpc();
            }

            if (keyboard.gKey.wasReleasedThisFrame)
            {
                RequestGrabEndServerRpc();
            }

            if (keyboard.hKey.wasPressedThisFrame)
            {
                RequestHitServerRpc();
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
            !NetworkDebugUI.IsPointerOverMenu(mouse.position.ReadValue()))
        {
            RequestHitServerRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPushServerRpc(RpcParams rpcParams = default)
    {
        if (!IsRequestFromOwner(rpcParams.Receive.SenderClientId) || !CanInteractInCurrentState() ||
            Time.time < nextPushTime)
        {
            return;
        }

        PlayerCombat target = FindTarget(pushRange, pushAngle);
        if (target == null)
        {
            return;
        }

        float force = pushForce;
        if (CurrentGameState == GameState.Cooperation)
        {
            force *= cooperationPushMultiplier;
        }

        nextPushTime = Time.time + pushCooldown;
        ApplyKnockback(target, force);
        Debug.Log($"Client {OwnerClientId} pushed client {target.OwnerClientId}.");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestGrabStartServerRpc(RpcParams rpcParams = default)
    {
        if (!IsRequestFromOwner(rpcParams.Receive.SenderClientId) || !CanInteractInCurrentState() ||
            isGrabbed.Value || heldTarget != null || grabbedBy != null || Time.time < nextGrabTime)
        {
            return;
        }

        PlayerCombat target = FindTarget(grabRange, grabAngle);
        if (target == null || target.isGrabbed.Value || target.heldTarget != null || target.grabbedBy != null)
        {
            return;
        }

        heldTarget = target;
        target.grabbedBy = this;
        target.isGrabbed.Value = true;
        target.movement?.SetGrabbedServer(true, grabMoveMultiplier);
        grabEndsAt = Time.time + grabDuration;
        nextGrabTime = Time.time + grabCooldown;

        Debug.Log($"Client {OwnerClientId} grabbed client {target.OwnerClientId}.");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestGrabEndServerRpc(RpcParams rpcParams = default)
    {
        if (IsRequestFromOwner(rpcParams.Receive.SenderClientId))
        {
            EndGrabServer();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestHitServerRpc(RpcParams rpcParams = default)
    {
        if (!IsRequestFromOwner(rpcParams.Receive.SenderClientId) || !CanInteractInCurrentState() ||
            Time.time < nextHitTime)
        {
            return;
        }

        GameState state = CurrentGameState;
        if (state == GameState.Cooperation && !hitEnabledInCooperation)
        {
            return;
        }

        PlayerCombat target = FindTarget(hitRange, 180f);
        if (target == null)
        {
            return;
        }

        float force = hitForce;
        if (state == GameState.Cooperation)
        {
            force *= cooperationHitMultiplier;
        }

        nextHitTime = Time.time + hitCooldown;
        ApplyKnockback(target, force);
        target.ShowHitFlashClientRpc();
        Debug.Log($"Client {OwnerClientId} hit client {target.OwnerClientId}.");
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ShowHitFlashClientRpc()
    {
        hitFlashEndsAt = Time.time + hitFlashDuration;
        UpdateVisuals();
    }

    private void ApplyKnockback(PlayerCombat target, float force)
    {
        Vector3 awayDirection = Vector3.ProjectOnPlane(
            target.transform.position - transform.position, Vector3.up);

        if (awayDirection.sqrMagnitude < 0.001f)
        {
            awayDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        awayDirection.Normalize();
        Vector3 velocity = awayDirection * force + Vector3.up * 0.55f;
        target.movement?.ApplyExternalVelocityRpc(velocity);
    }

    private PlayerCombat FindTarget(float range, float angle)
    {
        if (!IsServer || NetworkManager == null)
        {
            return null;
        }

        Vector3 facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        PlayerCombat closest = null;
        float closestDistance = float.PositiveInfinity;

        foreach (NetworkClient client in NetworkManager.ConnectedClients.Values)
        {
            NetworkObject playerObject = client.PlayerObject;
            if (playerObject == null || playerObject.OwnerClientId == OwnerClientId)
            {
                continue;
            }

            PlayerCombat candidate = playerObject.GetComponent<PlayerCombat>();
            if (candidate == null || !candidate.IsSpawned)
            {
                continue;
            }

            Vector3 offset = candidate.transform.position - transform.position;
            float distance = offset.magnitude;
            Vector3 direction = Vector3.ProjectOnPlane(offset, Vector3.up).normalized;
            float angleToTarget = Vector3.Angle(facing, direction);

            if (distance > range || angleToTarget > angle * 0.5f || distance >= closestDistance)
            {
                continue;
            }

            closest = candidate;
            closestDistance = distance;
        }

        return closest;
    }

    private bool CanInteractInCurrentState()
    {
        return IsServer && gameStateManager != null && gameStateManager.CanPlay;
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

    private bool IsRequestFromOwner(ulong senderClientId)
    {
        return IsServer && IsSpawned && senderClientId == OwnerClientId;
    }

    private void UpdateServerGrabTimer()
    {
        if (heldTarget == null)
        {
            return;
        }

        if (!heldTarget.IsSpawned || !IsSpawned || Time.time >= grabEndsAt ||
            CurrentGameState == GameState.Finished)
        {
            EndGrabServer();
        }
    }

    private void EndGrabServer()
    {
        if (!IsServer || heldTarget == null)
        {
            return;
        }

        PlayerCombat target = heldTarget;
        heldTarget = null;
        grabEndsAt = 0f;

        if (target != null)
        {
            if (target.grabbedBy == this)
            {
                target.grabbedBy = null;
            }

            if (target.IsSpawned)
            {
                target.isGrabbed.Value = false;
                target.movement?.SetGrabbedServer(false);
            }
        }
    }

    public void ForceEndGrabServer()
    {
        if (!IsServer)
        {
            return;
        }

        if (heldTarget != null)
        {
            EndGrabServer();
        }

        if (grabbedBy != null)
        {
            PlayerCombat grabber = grabbedBy;
            grabber.EndGrabServer();
            grabbedBy = null;
            if (IsSpawned)
            {
                isGrabbed.Value = false;
                movement?.SetGrabbedServer(false);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pushRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, grabRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hitRange);
    }

    private void HandleGrabbedChanged(bool previousValue, bool newValue)
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (renderers == null)
        {
            return;
        }

        bool flashActive = hitFlashEndsAt > Time.time;
        if (!isGrabbed.Value && !flashActive)
        {
            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.SetPropertyBlock(null);
                }
            }

            return;
        }

        Color color = flashActive ? hitFlashColor : grabbedColor;
        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(materialProperties);
            materialProperties.SetColor("_BaseColor", color);
            materialProperties.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(materialProperties);
        }
    }
}
