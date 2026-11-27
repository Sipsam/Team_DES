using System;
using UnityEngine;

namespace TwinEdge.Presentation
{
    public sealed class CourseMapData : ScriptableObject
    {
        public enum Zone { Mountain, Bridge, Canyon, River, CastleExterior, CastleInterior, Stair, Cavern, Finish }
        public enum Kind { Self, Partner, Gem, Goal, Checkpoint, Gimmick, Peak, Finish }

        [Serializable]
        public struct Sheet
        {
            public Zone Zone;
            public Rect Bounds;
            public Rect TextureBounds;
            public Texture2D Texture;
            public float Radius;
        }

        [Serializable]
        public struct Pin
        {
            public string Id;
            public Kind Kind;
            public Vector3 Position;
        }

        public Sheet Overview;
        public Sheet[] Sheets;
        public Pin[] Pins;
        public Vector3 BridgeStart, BridgeEnd, LowerDock, UpperDock, CastleGate, CaveStart, Seal, Finish, FirstSummit, LastSummit;
        public Vector3 StairTop;
        public float CavernCeiling;

        public static Vector2 Xz(Vector3 value) => new(value.x, value.z);

        public Zone At(Vector3 point)
        {
            Vector2 xz = Xz(point);
            if (point.y < CavernCeiling + 2 && Sheets[(int)Zone.Cavern].Bounds.Contains(xz)) return Zone.Cavern;
            var stair = Sheets[(int)Zone.Stair].Bounds;
            if (point.y < StairTop.y + 3 && stair.Contains(xz)) return Zone.Stair;
            if (CastleGameplayContains(point)) return Zone.CastleInterior;
            if (point.z >= CastleGate.z - 45) return Zone.CastleExterior;
            if (point.z >= LowerDock.z - 24) return Zone.River;
            if (point.z >= BridgeEnd.z + 14) return Zone.Canyon;
            if (point.z >= BridgeStart.z - 10) return Zone.Bridge;
            if ((point - Finish).sqrMagnitude < 65 * 65) return Zone.Finish;
            return Zone.Mountain;
        }

        bool CastleGameplayContains(Vector3 point) =>
            TwinEdge.Gameplay.CastleGameplay.Current?.Contains(point) == true ||
            (point.y >= CastleGate.y - 3 && point.y <= CastleGate.y + 13 &&
             Sheets[(int)Zone.CastleInterior].Bounds.Contains(Xz(point)));

        public static bool IsInteriorFloor(Zone zone) => zone is Zone.CastleInterior or Zone.Stair or Zone.Cavern;
        public static bool SeparateFloor(Zone a, Zone b) => a != b && (IsInteriorFloor(a) || IsInteriorFloor(b));
    }
}
