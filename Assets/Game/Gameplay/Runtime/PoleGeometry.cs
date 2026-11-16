using System.Linq;
using TwinEdge.Levels;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public static class PoleGeometry
    {
        public static Vector3 Endpoint(Vector3 pivot, float length, float yaw, float pitch)
            => pivot + Quaternion.Euler(-pitch, yaw, 0) * Vector3.forward * length;

        // Summit IDs/config delimit permitted landing areas; the actual mesh supplies height/normal.
        public static string Summit(Vector3 feet, MountainConfigV3 config, float margin)
        {
            if (config == null) return null;
            foreach (var p in new[] { config.start, config.destination }.Concat(config.smallPeaks))
            {
                Vector2 center = p == config.start || p == config.destination ? p.position : config.clusterCenter + Vector2.Scale(p.position, config.clusterSize);
                Vector3 local = Quaternion.Euler(0, p.rotation, 0) * new Vector3(feet.x - center.x, 0, feet.z - center.y);
                float rx = p.summitSize.x * .5f - margin, rz = p.summitSize.y * .5f - margin;
                if (rx > 0 && rz > 0 && local.x * local.x / (rx * rx) + local.z * local.z / (rz * rz) <= 1 && Mathf.Abs(feet.y - p.height) < 6) return p.id;
            }
            return null;
        }
    }
}
