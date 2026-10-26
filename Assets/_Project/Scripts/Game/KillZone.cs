using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class KillZone : MonoBehaviour
{
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

        player.RequestKillZoneRespawnServerRpc(gameObject.name);
    }

    public bool ContainsPlayerPosition(Vector3 position)
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider == null)
        {
            return false;
        }

        Bounds bounds = zoneCollider.bounds;
        bounds.Expand(4f);
        return bounds.Contains(position);
    }
}
