using System.Collections.Generic;
using TwinEdge.Levels;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public static class PoleGeometry
    {
        public static Vector3 Endpoint(Vector3 pivot, float length, float yaw, float pitch)
            => pivot + Quaternion.Euler(-pitch, yaw, 0) * Vector3.forward * length;

        internal static IEnumerable<MountainPeakV3> Summits(MountainConfigV3 config)
        {
            yield return config.start;
            yield return config.destination;
            foreach (var peak in config.smallPeaks) yield return peak;
        }

        internal static Vector2 SummitCenter(MountainConfigV3 config, MountainPeakV3 peak)
            => peak == config.start || peak == config.destination
                ? peak.position
                : config.clusterCenter + Vector2.Scale(peak.position, config.clusterSize);

        // Summit IDs/config delimit permitted landing areas; the actual mesh supplies height/normal.
        public static string Summit(Vector3 feet, MountainConfigV3 config, float margin)
        {
            if (config == null) return null;
            if (WithinSummit(feet, config, config.start, margin)) return config.start.id;
            if (WithinSummit(feet, config, config.destination, margin)) return config.destination.id;
            foreach (var peak in config.smallPeaks)
                if (WithinSummit(feet, config, peak, margin)) return peak.id;
            return null;
        }

        static bool WithinSummit(Vector3 feet, MountainConfigV3 config, MountainPeakV3 peak, float margin)
        {
            if (Mathf.Abs(feet.y - peak.height) >= 6) return false;
            Vector2 center = SummitCenter(config, peak);
            Vector3 local = Quaternion.Euler(0, peak.rotation, 0) * new Vector3(feet.x - center.x, 0, feet.z - center.y);
            float rx = peak.summitSize.x * .5f - margin, rz = peak.summitSize.y * .5f - margin;
            return rx > 0 && rz > 0 && local.x * local.x / (rx * rx) + local.z * local.z / (rz * rz) <= 1;
        }
    }
}
