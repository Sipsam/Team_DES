using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class JumpPuzzleEndTrigger : MonoBehaviour
{
    [SerializeField] private JumpPuzzleController puzzleController;

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
