using UnityEngine;

public sealed class PuzzleDoor : MonoBehaviour
{
    [SerializeField] private GameObject doorPanel;
    [SerializeField] private Collider blockingCollider;

    private Vector3 closedLocalPosition;
    private bool localDoorOpen;
    private bool hasClosedPose;

    private void Awake()
    {
        CaptureClosedPose();
    }

    private void CaptureClosedPose()
    {
        if (!hasClosedPose && doorPanel != null)
        {
            closedLocalPosition = doorPanel.transform.localPosition;
            hasClosedPose = true;
        }
    }

    public void SetOpen(bool open)
    {
        localDoorOpen = open;
        ApplyState();
    }

    public void SetLocalOpen(bool open)
    {
        localDoorOpen = open;
        ApplyState();
    }

    private void ApplyState()
    {
        CaptureClosedPose(); // Shortcut may receive its state while its parent is inactive.
        if (blockingCollider != null)
        {
            blockingCollider.enabled = !localDoorOpen;
        }

        if (doorPanel != null)
        {
            doorPanel.SetActive(true);
            doorPanel.transform.localPosition = closedLocalPosition +
                (localDoorOpen ? Vector3.up * 3.5f : Vector3.zero);
        }
    }
}
