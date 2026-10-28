using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class RaftMovingPlatform : MonoBehaviour
{
    [SerializeField] private Transform raftTransform;

    private readonly HashSet<PlayerMovement> riders = new HashSet<PlayerMovement>();
    private Vector3 previousPosition;
    private Quaternion previousRotation;

    private void Awake()
    {
        if (raftTransform == null)
        {
            raftTransform = transform.parent;
        }

        if (raftTransform != null)
        {
            previousPosition = raftTransform.position;
            previousRotation = raftTransform.rotation;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TrackRider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TrackRider(other);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player != null)
        {
            riders.Remove(player);
        }
    }

    private void LateUpdate()
    {
        if (raftTransform == null)
        {
            return;
        }

        Vector3 currentPosition = raftTransform.position;
        Quaternion currentRotation = raftTransform.rotation;
        Quaternion rotationDelta = currentRotation * Quaternion.Inverse(previousRotation);

        // A reset/respawn is a teleport, not a movement delta for old riders.
        if ((currentPosition - previousPosition).sqrMagnitude > 4f)
        {
            riders.Clear();
            previousPosition = currentPosition;
            previousRotation = currentRotation;
            return;
        }

        if ((currentPosition - previousPosition).sqrMagnitude > 0.000001f ||
            Quaternion.Angle(previousRotation, currentRotation) > 0.001f)
        {
            foreach (PlayerMovement rider in riders)
            {
                if (rider == null || !rider.IsSpawned || !rider.IsOwner || !rider.IsGrounded)
                {
                    continue;
                }

                Bounds riderBounds = GetComponent<Collider>().bounds;
                riderBounds.Expand(0.5f);
                if (!riderBounds.Intersects(rider.GetComponent<CharacterController>().bounds)) continue;

                Vector3 localOffset = Quaternion.Inverse(previousRotation) *
                                      (rider.transform.position - previousPosition);
                Vector3 carriedPosition = currentPosition + currentRotation * localOffset;
                rider.ApplyMovingPlatformDelta(carriedPosition - rider.transform.position, rotationDelta);
            }
        }

        previousPosition = currentPosition;
        previousRotation = currentRotation;
    }

    private void OnDisable()
    {
        riders.Clear();
    }

    private void TrackRider(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player != null && player.IsSpawned && player.IsOwner)
        {
            riders.Add(player);
        }
    }
}
