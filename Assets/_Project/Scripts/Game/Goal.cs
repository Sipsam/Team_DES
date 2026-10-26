using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class Goal : MonoBehaviour
{
    [SerializeField] private GameStateManager gameStateManager;

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

        manager?.TryFinish(player.OwnerClientId);
    }
}
