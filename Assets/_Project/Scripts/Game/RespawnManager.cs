using UnityEngine;

public sealed class RespawnManager : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;

    private CharacterController playerController;
    private Vector3 checkpointPosition;
    private Quaternion checkpointRotation;

    private void Awake()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerMovement>();
        }

        if (player != null)
        {
            playerController = player.GetComponent<CharacterController>();
            checkpointPosition = player.transform.position;
            checkpointRotation = player.transform.rotation;
        }
    }

    public void SetCheckpoint(Vector3 position, Quaternion rotation)
    {
        checkpointPosition = position;
        checkpointRotation = rotation;
    }

    public void RespawnPlayer()
    {
        if (player == null)
        {
            return;
        }

        if (playerController == null)
        {
            playerController = player.GetComponent<CharacterController>();
        }

        bool controllerWasEnabled = playerController != null && playerController.enabled;
        if (controllerWasEnabled)
        {
            playerController.enabled = false;
        }

        player.transform.SetPositionAndRotation(checkpointPosition, checkpointRotation);
        player.ResetVerticalVelocity();

        if (controllerWasEnabled)
        {
            playerController.enabled = true;
        }
    }
}
