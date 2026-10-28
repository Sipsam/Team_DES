using UnityEngine;

// Scene presentation follows the existing replicated state; it owns no gameplay state.
public sealed class PrototypeLevelFlow : MonoBehaviour
{
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private GameObject waitingBarrier;
    [SerializeField] private GameObject[] cooperationDevices = new GameObject[0];
    [SerializeField] private GameObject[] returnGuides = new GameObject[0];

    private void Update()
    {
        if (gameStateManager == null) return;
        bool cooperation = gameStateManager.CurrentState == GameState.Cooperation;
        if (waitingBarrier != null) waitingBarrier.SetActive(!gameStateManager.IsGameReady);
        foreach (GameObject item in cooperationDevices)
            if (item != null && item.activeSelf != cooperation) item.SetActive(cooperation);
        bool returning = gameStateManager.CurrentState == GameState.Competition;
        foreach (GameObject item in returnGuides)
            if (item != null && item.activeSelf != returning) item.SetActive(returning);
    }
}
