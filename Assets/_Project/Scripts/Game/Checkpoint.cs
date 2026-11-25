using UnityEngine;

public enum CheckpointPhase { Both, Cooperation, Competition }

[RequireComponent(typeof(Collider))]
public sealed class Checkpoint : MonoBehaviour
{
    [SerializeField] private RespawnManager respawnManager;
    [SerializeField] private Vector3 respawnOffset = new Vector3(0f, 0.1f, 0f);
    [SerializeField] private CheckpointPhase phase;
    [SerializeField] private Transform respawnPoint;

    private FoundationPhase? foundationPhase;
    private Pose[] slotPoses;
    private System.Func<bool> progressAllowsCheckpoint;

    public void Configure(RespawnManager manager, CheckpointPhase checkpointPhase)
    { respawnManager = manager; phase = checkpointPhase; }

    internal void ConfigureFoundation(RespawnManager manager, FoundationPhase requiredPhase,
        Pose[] poses, System.Func<bool> progressGate)
    {
        respawnManager = manager;
        foundationPhase = requiredPhase;
        slotPoses = poses;
        progressAllowsCheckpoint = progressGate;
    }

    internal Pose PoseForSlot(int slot) => slotPoses[slot];
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
            { if (registered.Add(client.ClientId)) AssignPlayerCheckpoint(player); }
            else registered.Remove(client.ClientId);
        }
    }
    public bool CanRegister(PlayerMovement player)
    {
        GameStateManager state = FindFirstObjectByType<GameStateManager>();
        if (state == null || !state.CanPlay || player == null) return false;
        if (TwinEdge.Gameplay.RiverGameplay.Current?.IsSeated(player.OwnerClientId) == true) return false;
        if (phase == CheckpointPhase.Cooperation && state.CurrentState != GameState.Cooperation) return false;
        if (phase == CheckpointPhase.Competition && state.CurrentState != GameState.Competition) return false;
        Bounds bounds = GetComponent<Collider>().bounds;
        if (!foundationPhase.HasValue) bounds.Expand(1.5f); // Legacy trigger tolerance.
        if (summitPole == null && !bounds.Contains(player.transform.position)) return false;
        if (foundationPhase.HasValue)
        {
            if (!state.foundationMode || state.Phase != foundationPhase || progressAllowsCheckpoint?.Invoke() == false ||
                (summitPole != null && !IsOnSummit(player)) ||
                player.GetComponent<TwinEdge.Gameplay.GameplayCharacter>() is not { CanContestFinish: true } character ||
                TwinEdge.Gameplay.CooperativePole.Current?.IsAttached(player.OwnerClientId) == true ||
                !character.HasGround()) return false;
            return true;
        }
        // Pole arrivals can be anywhere on the real summit, while the respawn pose stays at its existing marker.
        return summitPole == null || IsOnSummit(player);
    }

    private bool IsOnSummit(PlayerMovement player) =>
        TwinEdge.Gameplay.PoleGeometry.Summit(player.transform.position, summitPole.mountain, .9f) == summitId &&
        summitPole.SafeLanding(player.transform.position, out var landed) && landed == summitId;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (foundationPhase.HasValue) return; // Foundation checkpoints use server polling.
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.IsSpawned || !player.IsOwner)
        {
            return;
        }

        player.RequestCheckpointServerRpc(gameObject.name);
    }

    public void RegisterPlayerCheckpoint(PlayerMovement player)
    {
        if (!foundationPhase.HasValue && CanRegister(player)) AssignPlayerCheckpoint(player);
    }

    private void AssignPlayerCheckpoint(PlayerMovement player)
    {
        RespawnManager manager = respawnManager != null
            ? respawnManager
            : FindFirstObjectByType<RespawnManager>();

        if (slotPoses != null)
        {
            var spawns = manager != null ? manager.NetworkManager.GetComponent<NetworkSpawnCoordinator>() : null;
            if (spawns != null && spawns.TryGetSlot(player.OwnerClientId, out int slot) && slot < slotPoses.Length)
            {
                Pose pose = slotPoses[slot];
                manager.SetCheckpoint(player, pose.position, pose.rotation, gameObject.name);
            }
            return;
        }

        Transform point = respawnPoint != null ? respawnPoint : transform;
        Vector3 offset = respawnOffset;
        if (FindFirstObjectByType<GameStateManager>()?.foundationMode == true)
            offset += point.right * (player.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId ? -1.5f : 1.5f);
        manager?.SetCheckpoint(player, point.position + offset, point.rotation, gameObject.name);
    }
}
