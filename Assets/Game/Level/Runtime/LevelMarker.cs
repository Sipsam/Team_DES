using UnityEngine;
namespace TwinEdge.Levels
{
    public enum MarkerKind { PlayerSpawn, Checkpoint, SectionEntrance, SectionExit, BridgeAnchor, RaftDock, DoorOpening, PuzzlePlacement, PlatformPlacement, HazardArea, GemSealLocation, ReturnStart, FinishLine }
    public sealed class LevelMarker : MonoBehaviour
    {
        public string id;
        public SectionId section;
        public MarkerKind kind;
        public Vector3 reservedSize = Vector3.one;
        public string[] connectedIds = new string[0];
        [TextArea] public string notes;
        public bool show = true;
        private void OnDrawGizmos()
        {
            if (!show) return;
            Gizmos.color = kind == MarkerKind.HazardArea ? new Color(1,.2f,0,.5f) : Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * reservedSize.y / 2, reservedSize);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 3);
            Gizmos.DrawSphere(Vector3.zero,.35f);
        }
    }
}
