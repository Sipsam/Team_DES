using UnityEngine;
namespace TwinEdge.Levels
{
    public sealed class LevelRoute : MonoBehaviour
    {
        public RouteKind kind;
        public Vector3[] points;
        public bool bidirectional = true;
        public string notes;
        private void OnDrawGizmosSelected()
        {
            if (points == null) return;
            Gizmos.color = Color.yellow;
            for (int i=1;i<points.Length;i++) Gizmos.DrawLine(points[i-1],points[i]);
        }
    }
}
