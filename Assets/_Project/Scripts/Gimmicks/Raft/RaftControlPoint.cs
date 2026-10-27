using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class RaftControlPoint : MonoBehaviour
{
    [SerializeField] private RaftController raftController;
    [SerializeField] private RaftControlSide side;
    [SerializeField, Min(0.05f)] private float retryInterval = 0.25f;

    private float nextRetryTime;

    private void Reset()
    {
        Collider pointCollider = GetComponent<Collider>();
        pointCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRequestControl(other, true);
    }

    private void OnTriggerStay(Collider other)
    {
        if (Time.time >= nextRetryTime)
        {
            TryRequestControl(other, false);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.IsSpawned || !player.IsOwner)
        {
            return;
        }

        player.SetInsideRaftControlPoint(false);
        if (raftController != null)
        {
            raftController.RequestReleaseControlPointServerRpc((int)side);
        }
    }

    private void TryRequestControl(Collider other, bool immediate)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.IsSpawned || !player.IsOwner || raftController == null ||
            NetworkManager.Singleton == null)
        {
            return;
        }

        player.SetInsideRaftControlPoint(true);

        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        ulong currentOccupant = side == RaftControlSide.Left
            ? raftController.LeftOccupantClientId
            : raftController.RightOccupantClientId;

        if (currentOccupant == localClientId || currentOccupant != ulong.MaxValue)
        {
            return;
        }

        if (!immediate && Time.time < nextRetryTime)
        {
            return;
        }

        nextRetryTime = Time.time + retryInterval;
        raftController.RequestControlPointServerRpc((int)side);
    }
}
