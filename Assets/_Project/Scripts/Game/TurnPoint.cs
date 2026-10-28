using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class TurnPoint : MonoBehaviour
{
    [SerializeField] private GameStateManager gameStateManager;

    private void OnTriggerStay(Collider other)
    {
        OnTriggerEnter(other);
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.IsSpawned)
        {
            return;
        }

        GameStateManager manager = gameStateManager != null
            ? gameStateManager
            : FindFirstObjectByType<GameStateManager>();

        manager?.RegisterTurnPointArrival(player.OwnerClientId);
    }
}
