using Unity.Netcode;
using UnityEngine;

public enum DoorPuzzleStationType
{
    Info,
    Input
}

[RequireComponent(typeof(Collider))]
public sealed class DoorPuzzleStation : MonoBehaviour
{
    [SerializeField] private DoorPuzzleController puzzleController;
    [SerializeField] private DoorPuzzleStationType stationType;
    [SerializeField, Min(0.05f)] private float retryInterval = 0.3f;
    [SerializeField] private bool competitionOnly;

    private float nextRetryTime;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRequestStation(other, true);
    }

    private void OnTriggerStay(Collider other)
    {
        if (Time.time >= nextRetryTime)
        {
            TryRequestStation(other, false);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.IsSpawned || !player.IsOwner)
        {
            return;
        }

        DoorPuzzleUI.AllowStationReentry();
        if (puzzleController != null)
        {
            puzzleController.RequestReleaseStationServerRpc((int)stationType);
        }
    }

    private void TryRequestStation(Collider other, bool immediate)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.IsSpawned || !player.IsOwner || puzzleController == null ||
            NetworkManager.Singleton == null || DoorPuzzleUI.IsStationSuppressed ||
            DoorPuzzleUI.IsPanelVisible)
        {
            return;
        }

        if (stationType == DoorPuzzleStationType.Info &&
            puzzleController.CurrentGameState != GameState.Cooperation)
        {
            return;
        }

        if ((competitionOnly && puzzleController.CurrentGameState != GameState.Competition) ||
            (!competitionOnly && puzzleController.HasDedicatedShortcutStation &&
             puzzleController.CurrentGameState == GameState.Competition)) return;

        if ((puzzleController.CurrentGameState == GameState.Cooperation &&
             (puzzleController.IsDoorOpen || puzzleController.IsStationOccupied(stationType))) ||
            puzzleController.IsLocalPlayerAssignedToAnyStation())
        {
            return;
        }

        if (!immediate && Time.time < nextRetryTime)
        {
            return;
        }

        nextRetryTime = Time.time + retryInterval;
        puzzleController.RequestStationServerRpc((int)stationType, competitionOnly);
    }
}
