using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class MovingPlatform : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private JumpPuzzleController puzzleController;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 3f;
    [SerializeField, Min(0f)] private float waitTime = 0.75f;
    [SerializeField] private bool moveWhenEitherSwitchHeld = true;

    private Transform destination;
    private float waitTimer;

    private void Awake()
    {
        if (startPoint != null)
        {
            transform.position = startPoint.position;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && startPoint != null)
        {
            transform.SetPositionAndRotation(startPoint.position, startPoint.rotation);
            destination = endPoint;
            waitTimer = 0f;
        }
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || puzzleController == null || startPoint == null || endPoint == null)
        {
            return;
        }

        bool shouldMove = puzzleController.CanOperate && (moveWhenEitherSwitchHeld
            ? puzzleController.IsAnyHoldSwitchActive
            : puzzleController.IsSwitchAActive && puzzleController.IsSwitchBActive);
        if (!shouldMove)
        {
            waitTimer = 0f;
            return;
        }

        if (destination == null)
        {
            destination = endPoint;
        }

        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position, destination.position, moveSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, destination.position) <= 0.02f)
        {
            destination = destination == endPoint ? startPoint : endPoint;
            waitTimer = waitTime;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (startPoint == null || endPoint == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(startPoint.position, endPoint.position);
        Gizmos.DrawWireSphere(startPoint.position, 0.25f);
        Gizmos.DrawWireSphere(endPoint.position, 0.25f);
    }
}
