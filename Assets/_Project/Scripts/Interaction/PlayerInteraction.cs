using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public sealed class PlayerInteraction : NetworkBehaviour
{
    [SerializeField, Min(0.1f)] private float interactionRadius = 2.5f;
    [SerializeField] private LayerMask interactionLayers = ~0;

    private readonly Collider[] overlapResults = new Collider[32];
    private bool puzzleUiInputBlocked;

    private void Update()
    {
        if (IsSpawned && !IsOwner)
        {
            return;
        }

        if (puzzleUiInputBlocked)
        {
            return;
        }

        if (IsSpawned && NetworkManager != null && NetworkManager.ConnectedClientsIds.Count < 2)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
        {
            return;
        }

        IInteractable interactable = FindClosestInteractable();
        interactable?.Interact(this);
    }

    public override void OnNetworkSpawn()
    {
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
