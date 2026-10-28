using UnityEngine;

public enum CheckpointPhase { Both, Cooperation, Competition }

[RequireComponent(typeof(Collider))]
public sealed class Checkpoint : MonoBehaviour
{
    [SerializeField] private RespawnManager respawnManager;
    [SerializeField] private Vector3 respawnOffset = new Vector3(0f, 0.1f, 0f);
    [SerializeField] private CheckpointPhase phase;
    [SerializeField] private Transform respawnPoint;

    public bool CanRegister(PlayerMovement player)
    {
        GameStateManager state = FindFirstObjectByType<GameStateManager>();
        if (state == null || !state.CanPlay || player == null) return false;
        if (phase == CheckpointPhase.Cooperation && state.CurrentState != GameState.Cooperation) return false;
        if (phase == CheckpointPhase.Competition && state.CurrentState != GameState.Competition) return false;
        Bounds bounds = GetComponent<Collider>().bounds;
        bounds.Expand(1.5f); // Allow for transform replication delay at trigger edges.
        return bounds.Contains(player.transform.position);
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.IsSpawned || !player.IsOwner)
        {
            return;
        }

        player.RequestCheckpointServerRpc(gameObject.name);
    }

    public void RegisterPlayerCheckpoint(PlayerMovement player)
    {
        if (!CanRegister(player)) return;
        RespawnManager manager = respawnManager != null
            ? respawnManager
            : FindFirstObjectByType<RespawnManager>();

        Transform point = respawnPoint != null ? respawnPoint : transform;
        manager?.SetCheckpoint(player, point.position + respawnOffset, point.rotation);
    }
}
