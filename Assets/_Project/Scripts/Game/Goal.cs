using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class Goal : MonoBehaviour
{
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private GameObject goalVisual;

    private void Update()
    {
        if (gameStateManager == null) gameStateManager = FindFirstObjectByType<GameStateManager>();
        bool active = gameStateManager != null && gameStateManager.CanPlay &&
            gameStateManager.CurrentState == GameState.Competition;
        GetComponent<Collider>().enabled = active;
        if (goalVisual != null) goalVisual.SetActive(active);
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

        manager?.TryFinish(player.OwnerClientId);
    }
}
