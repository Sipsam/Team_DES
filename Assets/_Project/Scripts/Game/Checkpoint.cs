using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class Checkpoint : MonoBehaviour
{
    [SerializeField] private RespawnManager respawnManager;
    [SerializeField] private Vector3 respawnOffset = new Vector3(0f, 0.1f, 0f);

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
        RespawnManager manager = respawnManager != null
            ? respawnManager
            : FindFirstObjectByType<RespawnManager>();

        manager?.SetCheckpoint(player, transform.position + respawnOffset, transform.rotation);
    }
}
