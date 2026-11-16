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
        public Vector3[] descent = {new Vector3(12,190,650),new Vector3(12,184,710),new Vector3(90,160,790),new Vector3(-30,130,870),new Vector3(-100,100,950),new Vector3(10,70,1030),new Vector3(32,42,1150)};
        public Vector3[] river = {new Vector3(0,40,1150),new Vector3(65,41.5f,1270),new Vector3(80,43,1390),new Vector3(-80,45,1550),new Vector3(-65,47,1690),new Vector3(55,49,1830),new Vector3(50,50,1900)};
        public float[] widths = {44,48,58,48,64,54,46};
        public float bedDepth=7, innerBankSlope=.22f, outerBankSlope=.9f;
        public float raftLaneReservation=12, turningReservation=22;
        public Vector3 castleEntry = new Vector3(50,58,2070);
        public Vector2 castleSite = new Vector2(120,100);
    }
}
