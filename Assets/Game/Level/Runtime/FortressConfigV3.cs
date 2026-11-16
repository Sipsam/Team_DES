using System;
using UnityEngine;
namespace TwinEdge.Levels
{
    public enum FortressPartV3 { CastleExterior, CastleInterior, UndergroundAccess, UndergroundCavern }
    [Serializable] public sealed class FortressTowerV3 { public Vector2 position; public float radius=9, height=32; }
    [Serializable] public sealed class FortressRoomV3 { public string id; public Rect plan; public float height; public bool hint; }
    [Serializable] public sealed class CavernPlatformV3 { public string id, activatedBy; public Vector3 position; public Vector2 size; public bool fixedLanding; public float seconds=25; }
    [CreateAssetMenu(menuName="Twin Edge/V3 Fortress Config")]
    public sealed class FortressConfigV3 : ScriptableObject
    {
        [Tooltip("Provisional dimensions; local X/Z, Y relative to gate floor. No jump balance approval.")]
        public Vector3 origin = new Vector3(50,58,2144);
        public Vector3 approach = new Vector3(50,58,2070);
        public Vector2[] curtain;
        public FortressTowerV3[] towers;
        public float wallHeight=20, wallThickness=4, gateWidth=12, gateHeight=12;
        public Rect keep = new Rect(-42,35,86,220);
        public Rect courtyard = new Rect(-30,3,60,28);
        public float keepHeight=32, hallHeight=44, sharedWallX=0;
        public FortressRoomV3[] rooms;
        public Vector3 undergroundDoor=new Vector3(0,0,245);
        public Rect stairVoid=new Rect(16,251,58,24);
        public Vector3[] stairs;
        public float stairWidth=6;
        public Vector2[] cave;
        public float ceiling=-28, foundationBottom=-8, caveFloor=-92, lava=-86;
        public Vector3 start=new Vector3(40,-60,240), seal=new Vector3(14,-52,53);
        public CavernPlatformV3[] platforms;
    }
}
