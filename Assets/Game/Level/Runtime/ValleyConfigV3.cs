using UnityEngine;
namespace TwinEdge.Levels
{
    public enum ValleyPartV3 { Bridge, CanyonDescent, River, CastleApproach }
    [CreateAssetMenu(menuName="Twin Edge/V3 Bridge River Settings")]
    public sealed class ValleyConfigV3 : ScriptableObject
    {
        [Tooltip("Provisional metres; not validated collapse or raft gameplay.")]
        public Vector3 bridgeStart = new Vector3(12,190,339), bridgeEnd = new Vector3(12,190,650);
        public float bridgeWidth=8, plankGap=.16f, canyonDepth=260, landingRadius=28;
        public int plankCount=198;
        public float walkWidth=12, cliffSlope=.8f, valleyWidth=320, silhouette=16;
        public const float StairWidth = 8f;
        public const float MaxStairRise = .16f;
        public Vector3 LowerStairLanding => river[0] + new Vector3(widths[0] * .5f + 10f, 2f, 0f);
        public Vector3 UpperStairTread => CanyonStairAxis(bridgeEnd.z + 14f) + Vector3.up * bridgeEnd.y;
        public Vector3 LowerStairTread => CanyonStairAxis(LowerStairLanding.z - 2f) + Vector3.up * LowerStairLanding.y;
        public Vector3[] CanyonRoute => new[] { bridgeEnd, UpperStairTread, LowerStairTread, LowerStairLanding };

        public Vector3 CanyonStairAxis(float z)
        {
            float t = Mathf.InverseLerp(bridgeEnd.z, LowerStairLanding.z, z);
            return new Vector3(Mathf.Lerp(bridgeEnd.x, LowerStairLanding.x, t), 0f, z);
        }

        public Vector3 CanyonStairProfile(float z)
        {
            float t = Mathf.InverseLerp(UpperStairTread.z, LowerStairTread.z, z);
            return CanyonStairAxis(z) + Vector3.up * Mathf.Lerp(bridgeEnd.y, LowerStairLanding.y, t);
        }

        public Vector3[] river = {new Vector3(0,40,1150),new Vector3(65,41.5f,1270),new Vector3(80,43,1390),new Vector3(-80,45,1550),new Vector3(-65,47,1690),new Vector3(55,49,1830),new Vector3(50,50,1900)};
        public Vector3 downstreamEnd = new(-190, 37.6f, 970);
        [Tooltip("Upstream bend between the dock and the outlet, in world X/Z.")]
        public Vector2 upstreamBend = new(-45, 1955);
        public Vector3 upstreamEnd = new(-166, 52.8f, 2110);
        public float[] widths = {44,48,58,48,64,54,46};
        public float bedDepth=7, innerBankSlope=.22f, outerBankSlope=.9f;
        public float raftLaneReservation=12, turningReservation=22;
        public Vector3 castleEntry = new Vector3(50,58,2070);
        public Vector2 castleSite = new Vector2(120,100);
    }
}
