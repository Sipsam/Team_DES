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
        if (other.GetComponentInParent<PlayerMovement>() == null)
        {
            return;
        }

        RespawnManager manager = respawnManager != null
            ? respawnManager
            : FindFirstObjectByType<RespawnManager>();

        if (manager != null)
        {
            manager.SetCheckpoint(transform.position + respawnOffset, transform.rotation);
        }
    }
}
