using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class HoldSwitch : MonoBehaviour
{
    [SerializeField] private JumpPuzzleController puzzleController;
    [SerializeField, Range(0, 1)] private int switchId;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        SetHeld(other, true);
    }

    private void OnTriggerExit(Collider other)
    {
        SetHeld(other, false);
    }

    private void SetHeld(Collider other, bool held)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.IsSpawned || !player.IsOwner || puzzleController == null ||
            NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            return;
        }

        puzzleController.RequestHoldSwitchServerRpc(switchId, held);
    }
}
