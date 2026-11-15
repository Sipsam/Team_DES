using System;
using System.Collections.Generic;
using UnityEngine;

namespace TwinEdge.Levels
{
    public enum SectionId { Mountain, Bridge, River, Castle, Underground }
    public enum RouteKind { Walk, SpecialMovement, Water, Jump, Return }
    [Serializable]
    public sealed class MarkerOverride
    {
        public string id;
        public Vector3 localPosition;
        public float localYaw;
        public Vector3 reservedSize = Vector3.one;
    }
    [Serializable]
    public sealed class SectionLayout
    {
        public SectionId id;
        public Vector3 position;
        public float yaw;
        public Vector3 size;
        public SectionLayout(SectionId id, Vector3 position, Vector3 size)
        { this.id = id; this.position = position; this.size = size; }
    }

    [CreateAssetMenu(menuName = "Twin Edge/Level Layout")]
    public sealed class LevelLayoutConfig : ScriptableObject
    {
        public bool spatialV2;
        public float[] riverWidths = Array.Empty<float>();
        public Vector3 origin;
        public int seed = 1126;
        [Tooltip("All dimensions are provisional metres, not approved design measurements.")]
        public bool temporaryTraversal = true;
        public Vector3 worldSize = new Vector3(320, 240, 1600);
        public float playerHeight = 2, playerRadius = .45f, moveSpeed = 5, jumpHeight = 1.5f, gravity = 20;
        public float slopeLimit = 45, clearanceMargin = .15f;
        public float pathWidth = 8, bridgeWidth = 8, bridgeSpan = 40;
        public List<MarkerOverride> markerOverrides = new List<MarkerOverride>();
        public int terrainResolution = 513;
        public float terrainDepth = 80, terrainHeight = 220, gorgeDepth = 55;
        public float riverWidth = 24, riverDepth = 5, bankWidth = 10;
        public Vector3[] riverPath = { new Vector3(0,20,0), new Vector3(45,23,160), new Vector3(-35,26,360), new Vector3(0,30,580) };
        public float roomWidth = 14, roomLength = 26, roomHeight = 8, doorwayWidth = 4;
        public float sewerWidth = 5, sewerDepth = 8;
        public float jumpLaneWidth = 4, jumpPlatformLength = 4, jumpGap = 1.5f, undergroundHeight = 12;
        public List<SectionLayout> sections = new List<SectionLayout>
        {
            new SectionLayout(SectionId.Mountain, Vector3.zero, new Vector3(220,180,300)),
            new SectionLayout(SectionId.Bridge, new Vector3(0,65,280), new Vector3(80,80,90)),
            new SectionLayout(SectionId.River, new Vector3(0,0,370), new Vector3(140,60,580)),
            new SectionLayout(SectionId.Castle, new Vector3(0,34,1000), new Vector3(56,24,160)),
            new SectionLayout(SectionId.Underground, new Vector3(0,-15,1250), new Vector3(36,20,100))
        };
        public Vector3[] mountainPath = { new Vector3(0,60,0), new Vector3(-22,80,55), new Vector3(0,100,110), new Vector3(0,92,160), new Vector3(15,85,200), new Vector3(0,65,280) };
        public Vector3 World(SectionId id, Vector3 local)
        {
            var s = sections.Find(x => x.id == id);
            return origin + s.position + Quaternion.Euler(0,s.yaw,0) * local;
        }
        public float FlatJumpRange => 2 * moveSpeed * Mathf.Sqrt(2 * jumpHeight / gravity);
    }
}
