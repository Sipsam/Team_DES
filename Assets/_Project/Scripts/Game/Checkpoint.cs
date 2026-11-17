using UnityEngine;

public enum CheckpointPhase { Both, Cooperation, Competition }

[RequireComponent(typeof(Collider))]
public sealed class Checkpoint : MonoBehaviour
{
    [SerializeField] private RespawnManager respawnManager;
    [SerializeField] private Vector3 respawnOffset = new Vector3(0f, 0.1f, 0f);
    [SerializeField] private CheckpointPhase phase;
    [SerializeField] private Transform respawnPoint;

    public void Configure(RespawnManager manager, CheckpointPhase checkpointPhase)
    { respawnManager = manager; phase = checkpointPhase; }
    private TwinEdge.Gameplay.CooperativePole summitPole;
    private string summitId;
    public void RequireSummitLanding(TwinEdge.Gameplay.CooperativePole pole, string id)
    { summitPole = pole; summitId = id; }

    // Server polling also handles owner-authoritative replicated transforms without Rigidbody triggers.
    private readonly System.Collections.Generic.HashSet<ulong> registered = new();
    private void Update()
    {
        if (respawnManager == null || !respawnManager.IsServer || !respawnManager.IsSpawned) return;
        foreach (var client in respawnManager.NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject != null ? client.PlayerObject.GetComponent<PlayerMovement>() : null;
            if (player != null && CanRegister(player))
            { if (registered.Add(client.ClientId)) RegisterPlayerCheckpoint(player); }
            else registered.Remove(client.ClientId);
        }
    }
    public bool CanRegister(PlayerMovement player)
    {
        GameStateManager state = FindFirstObjectByType<GameStateManager>();
        if (state == null || !state.CanPlay || player == null) return false;
        if (phase == CheckpointPhase.Cooperation && state.CurrentState != GameState.Cooperation) return false;
        if (phase == CheckpointPhase.Competition && state.CurrentState != GameState.Competition) return false;
        // Pole arrivals can be anywhere on the real summit, while the respawn pose stays at its existing marker.
        if (summitPole != null)
            return TwinEdge.Gameplay.PoleGeometry.Summit(player.transform.position, summitPole.mountain, .9f) == summitId
                && summitPole.SafeLanding(player.transform.position, out var landed) && landed == summitId;
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
        Vector3 offset = respawnOffset;
        if (FindFirstObjectByType<GameStateManager>()?.foundationMode == true)
            offset += point.right * (player.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId ? -1.5f : 1.5f);
        manager?.SetCheckpoint(player, point.position + offset, point.rotation, gameObject.name);
    }
}
