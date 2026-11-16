using UnityEngine;

public sealed class RaftPair : MonoBehaviour
{
    public Transform leftRaft;
    public Transform rightRaft;
    public Transform leftSeat;
    public Transform rightSeat;
    public GameObject lashing;
    public float returnLaneOffset = 1.5f;

    public void SplitForReturn()
    {
        if (leftRaft == null || rightRaft == null || lashing == null || !lashing.activeSelf) return;

        Vector3 sideways = transform.right * returnLaneOffset;
        leftRaft.position -= sideways;
        rightRaft.position += sideways;
        lashing.SetActive(false);
    }

    [ContextMenu("Split for return")]
    private void PreviewSplit() => SplitForReturn();

    [ContextMenu("Join for outbound")]
    public void JoinForOutbound()
    {
        if (leftRaft == null || rightRaft == null || lashing == null || lashing.activeSelf) return;
        Vector3 sideways = transform.right * returnLaneOffset;
        leftRaft.position += sideways;
        rightRaft.position -= sideways;
        lashing.SetActive(true);
    }
}
