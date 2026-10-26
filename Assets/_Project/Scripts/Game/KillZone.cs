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
        if (other.GetComponentInParent<PlayerMovement>() == null)
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
