using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public sealed class PlayerInteraction : NetworkBehaviour
{
    [SerializeField, Min(0.1f)] private float interactionRadius = 2.5f;
    [SerializeField] private LayerMask interactionLayers = ~0;

    private readonly Collider[] overlapResults = new Collider[32];
    private bool puzzleUiInputBlocked;
    public IInteractable CurrentTarget { get; private set; }
    private TwinEdge.Gameplay.GameplayInput gameplayInput;
    private GameStateManager gameStateManager;

    private void Update()
    {
        if (IsSpawned && !IsOwner)
        {
            return;
        }

        if (IsSpawned && GetComponent<TwinEdge.Gameplay.GameplayCharacter>()?.BlocksOwnerInteraction == true)
        {
            CurrentTarget = null;
            return;
        }

        if (IsSpawned && TwinEdge.Gameplay.RiverGameplay.Current?.HandleOwnerInteraction(this, gameplayInput) == true)
        {
            CurrentTarget = null;
            return;
        }

        if (IsSpawned && TwinEdge.Gameplay.CooperativePole.Current != null &&
            TwinEdge.Gameplay.CooperativePole.Current.IsAttached(OwnerClientId)) return;

        var bridge = TwinEdge.Gameplay.BridgeGameplay.Current;
        if (IsSpawned && bridge != null && bridge.Holds(OwnerClientId))
        {
            CurrentTarget = bridge;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) bridge.Cancel(this);
            else if (gameplayInput?.AcceptsInput == true && gameplayInput.Interact?.WasPressedThisFrame() == true)
                bridge.Interact(this);
            return;
        }

        if (gameplayInput != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TwinEdge.Gameplay.UndergroundGameplay.Current?.CancelOwner(this);
            foreach (var target in FindObjectsByType<TwinEdge.Gameplay.NetworkInteractionTarget>(FindObjectsSortMode.None)) target.Cancel(this);
        }
        if (GetComponent<TwinEdge.Gameplay.MountainReturnLaunch>()?.ControlsMovement == true) return;
        if (puzzleUiInputBlocked || (gameplayInput != null && !gameplayInput.AcceptsInput))
        {
            return;
        }

        if (IsSpawned && (gameStateManager == null || !gameStateManager.CanPlay))
        {
            return;
        }

        if (!(CurrentTarget is TwinEdge.Gameplay.NetworkInteractionTarget busy && busy.IsInteracting(this)))
            CurrentTarget = FindClosestInteractable();
        Keyboard keyboard = Keyboard.current;
        if (gameplayInput != null ? gameplayInput.Interact == null || !gameplayInput.Interact.WasPressedThisFrame() : keyboard == null || !keyboard.eKey.wasPressedThisFrame)
        {
            return;
        }

        IInteractable interactable = CurrentTarget;
        interactable?.Interact(this);
    }

    public override void OnNetworkSpawn()
    {
        gameStateManager = FindFirstObjectByType<GameStateManager>();
        gameplayInput = GetComponent<TwinEdge.Gameplay.GameplayInput>();
        enabled = IsOwner;
    }

    public void SetPuzzleUiInputBlocked(bool blocked)
    {
        puzzleUiInputBlocked = blocked;
    }

    private IInteractable FindClosestInteractable()
    {
        Vector3 interactionPoint = transform.position + Vector3.up;
        int hitCount = Physics.OverlapSphereNonAlloc(
            interactionPoint,
            interactionRadius,
            overlapResults,
            interactionLayers,
            QueryTriggerInteraction.Collide);

        IInteractable closestInteractable = null;
        float closestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapResults[i];
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            MonoBehaviour[] components = hit.GetComponentsInParent<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                if (component is not IInteractable candidate || !candidate.CanInteract)
                {
                    continue;
                }

                float distance = (hit.ClosestPoint(interactionPoint) - interactionPoint).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestInteractable = candidate;
                }

                break;
            }
        }

        return closestInteractable;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, interactionRadius);
    }
}
