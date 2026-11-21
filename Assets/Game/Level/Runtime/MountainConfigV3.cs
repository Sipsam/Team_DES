using System;
using System.Collections.Generic;
using UnityEngine;

namespace TwinEdge.Levels
{
    public enum MountainShapeV3 { Automatic, Broad, Ridge, Cliff, Twin, Plateau, Broken }

    [Serializable]
    public sealed class MountainPeakV3
    {
        public MountainShapeV3 shape;
        [Range(0, 1)] public float asymmetry = .45f;
        [Tooltip("Flatten the permitted summit ellipse into real rock; retains the outer mountain shoulder.")]
        public bool landingTerrace;
        [Tooltip("Half-width of a rock-cut approach through a crest; zero preserves the original crest.")]
        public float landingApproachWidth;
        public Vector2 landingApproachDirection;
        public string id;
        public Vector2 position;
        public float height = 150;
        public Vector2 summitSize = new Vector2(22, 26);
        public float rotation, silhouette = .16f, shoulderWidth = 7;
    }

    [Serializable]
    public sealed class MountainPoleCanyonV3 {
        public string from, to, island;
        public float width=6, span=70, depth=25;
    }

    [CreateAssetMenu(menuName = "Twin Edge/V3 Mountain Field")]
    public sealed class MountainConfigV3 : ScriptableObject
    {
        [Tooltip("Metres. All spacing and landing sizes are untested design hypotheses.")]
        public Vector2 clusterSize = new Vector2(390, 340);
        public Vector2 clusterCenter = new Vector2(0, 30);
        public int seed = 31116;
        [Range(0, 120)] public float outerRangeSetback = 65;
        [Range(.3f, 1)] public float outerRangeHeightScale = .62f;
        [Range(.1f, .5f)] public float boundaryTaper = .3f;
        [Range(0, .15f)] public float samplingJitter = .06f;
        [Range(12, 60)] public int smallPeakCount = 27;
        public float minimumGap = 12, terrainHeightVariation = 9, basinHeight = -65;
        [Range(2, 8)] public float meshSpacing = 3;
        public MountainPeakV3 start = new MountainPeakV3 { id = "Start", position = new Vector2(0,-225), height = 132, summitSize = new Vector2(110,90), shoulderWidth = 15 };
        public MountainPeakV3 destination = new MountainPeakV3 { id = "Final", position = new Vector2(12,285), height = 190, summitSize = new Vector2(100,95), shoulderWidth = 15 };
        [Tooltip("Unordered field in normalized cluster coordinates (-.5 to .5). Y is world Z. Heights are absolute metres.")]
        public List<MountainPeakV3> smallPeaks = new List<MountainPeakV3>();
        [Tooltip("Unordered boundary summits in world X/Z. Connecting lower shoulders form the enclosing ranges.")]
        public List<MountainPeakV3> boundary = new List<MountainPeakV3>();
        public List<MountainPoleCanyonV3> poleCanyons = new List<MountainPoleCanyonV3>();
        [Tooltip("Mountain approach centerline, in world metres and increasing Z.")]
        public Vector3[] entryTrail = Array.Empty<Vector3>();
        public Vector3 bridgeAnchor = new Vector3(12,190,330);
        public float bridgeStubLength = 9, bridgeWidth = 7;
    }
}
