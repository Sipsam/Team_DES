using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class TurnPoint : MonoBehaviour
{
    [SerializeField] private GameStateManager gameStateManager;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovement>() == null)
        {
            return;
        }

        GameStateManager manager = gameStateManager != null
            ? gameStateManager
            : FindFirstObjectByType<GameStateManager>();

        if (manager != null && manager.TryTransitionTo(GameState.Competition))
        {
            Debug.Log("Competition Phase Started");
        }
    }
}
