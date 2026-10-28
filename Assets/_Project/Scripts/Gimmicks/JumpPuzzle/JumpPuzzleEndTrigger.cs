using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class JumpPuzzleEndTrigger : MonoBehaviour
{
    [SerializeField] private JumpPuzzleController puzzleController;
    private float nextRequestTime;

    private void OnTriggerStay(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.IsSpawned || !player.IsOwner) return;
        if (Time.time < nextRequestTime) return;
        nextRequestTime = Time.time + 0.3f;
        OnTriggerEnter(other);
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.IsOwner || !player.IsSpawned || puzzleController == null ||
            NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            return;
        }

        puzzleController.RequestEndArrivalServerRpc();
    }
}
