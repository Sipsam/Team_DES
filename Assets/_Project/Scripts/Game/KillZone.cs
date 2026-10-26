using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class KillZone : MonoBehaviour
{
    [SerializeField] private RespawnManager respawnManager;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || (player.IsSpawned && !player.IsOwner))
        {
            return;
        }

        RespawnManager manager = respawnManager != null
            ? respawnManager
            : FindFirstObjectByType<RespawnManager>();

        if (manager != null)
        {
            manager.RespawnPlayer();
        }
    }
}
