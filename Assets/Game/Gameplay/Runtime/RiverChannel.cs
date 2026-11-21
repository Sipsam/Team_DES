using System;
using System.Collections.Generic;
using System.Linq;
using TwinEdge.Levels;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Gameplay
{
    // The generated water mesh uses paired banks at each Z. Read its saved geometry in world space.
    internal sealed class RiverChannel
    {
        readonly Vector3[] left, right;
        readonly float[] distances;
        readonly int firstPlayableRow, lastPlayableRow;
        readonly float distanceAtStart;
        public readonly Transform Section;
        public readonly Collider LowerDock, UpperDock;
        public readonly Renderer WaterRenderer;
        public readonly Vector3 LowerCheckpoint, UpperCheckpoint;
        public float Start { get; }
        public float End { get; }
        public float WaterStart => left[0].z;
        public float WaterEnd => left[^1].z;
        public float WaterLength => distances[^1];
        public float Length { get; }

        public RiverChannel(Scene scene)
        {
            var river = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ValleySectionV3>())
                .Single(section => section.part == ValleyPartV3.River);
            Section = river.transform;
            Start = river.config.river[0].z;
            End = river.config.river[^1].z;
            var water = river.GetComponentsInChildren<MeshFilter>().Single(filter => filter.name == "WaterSurface");
            WaterRenderer = water.GetComponent<Renderer>();
            var rows = new SortedDictionary<float, (Vector3 left, Vector3 right)>();
            foreach (Vector3 vertex in water.sharedMesh.vertices)
            {
                Vector3 world = water.transform.TransformPoint(vertex);
                if (!rows.TryGetValue(world.z, out var row)) row = (world, world);
                if (world.x < row.left.x) row.left = world;
                if (world.x > row.right.x) row.right = world;
                rows[world.z] = row;
            }
            if (rows.Count < 2 || rows.Values.Any(row => row.right.x - row.left.x < 10 ||
                Mathf.Abs(row.right.y - row.left.y) > .01f))
                throw new InvalidOperationException("V3 river requires a continuous water ribbon with paired banks.");
            left = rows.Values.Select(row => row.left).ToArray();
            right = rows.Values.Select(row => row.right).ToArray();
            distances = new float[left.Length];
            for (int i = 1; i < distances.Length; i++)
                distances[i] = distances[i - 1] + Vector3.Distance(Center(i - 1), Center(i));

            var colliders = river.GetComponentsInChildren<Collider>();
            LowerDock = colliders.Single(collider => collider.name == "LowerDock");
            UpperDock = colliders.Single(collider => collider.name == "UpperDock");
            var markers = river.GetComponentsInChildren<LevelMarker>();
            LowerCheckpoint = markers.Single(marker => marker.id == "Valley.V3.LowerCheckpoint").transform.position;
            UpperCheckpoint = markers.Single(marker => marker.id == "Valley.V3.UpperCheckpoint").transform.position;
            firstPlayableRow = Array.BinarySearch(left, new Vector3(0, 0, Start), ZOrder.Instance);
            lastPlayableRow = Array.BinarySearch(left, new Vector3(0, 0, End), ZOrder.Instance);
            if (firstPlayableRow < 0 || lastPlayableRow <= firstPlayableRow)
                throw new InvalidOperationException("The playable river endpoints must match rows in the saved water geometry.");
            distanceAtStart = distances[firstPlayableRow];
            Length = distances[lastPlayableRow] - distanceAtStart;
            if (LowerDock.bounds.center.z >= UpperDock.bounds.center.z ||
                Sample(LowerDock.bounds.center.z).center.y >= Sample(UpperDock.bounds.center.z).center.y)
                throw new InvalidOperationException("V3 river dock direction must be lower to upper along +Z.");
        }

        Vector3 Center(int i) => (left[i] + right[i]) * .5f;

        (int index, float fraction) Locate(float z)
        {
            int index = Array.BinarySearch(left, new Vector3(0, 0, z), ZOrder.Instance);
            int i = Mathf.Clamp(index >= 0 ? index : ~index - 1, 0, left.Length - 2);
            float t = Mathf.InverseLerp(left[i].z, left[i + 1].z, z);
            return (i, t);
        }

        public (Vector3 center, float halfWidth, Vector3 upstream, float progress) Sample(float z)
        {
            var (i, t) = Locate(z);
            Vector3 center = Vector3.Lerp(Center(i), Center(i + 1), t);
            float width = Mathf.Lerp(right[i].x - left[i].x, right[i + 1].x - left[i + 1].x, t);
            // Keep the current and steering at the berths independent of visual bends beyond the course.
            bool inCourse = z >= Start && z <= End;
            int lastRow = inCourse ? lastPlayableRow : left.Length - 1;
            int firstRow = inCourse ? firstPlayableRow : 0;
            int tangentRow = Mathf.Min(i, lastRow - 1);
            Vector3 upstream = Center(Mathf.Min(lastRow, tangentRow + 3)) - Center(Mathf.Max(firstRow, tangentRow - 2));
            upstream.y = 0;
            float progress = Mathf.Clamp01((Mathf.Lerp(distances[i], distances[i + 1], t) - distanceAtStart) / Length);
            return (center, width * .5f, upstream.normalized, progress);
        }

        public Vector3 Point(float z, float offset)
        {
            var sample = Sample(z);
            return sample.center + Vector3.right * Mathf.Clamp(offset, -sample.halfWidth, sample.halfWidth);
        }

        sealed class ZOrder : IComparer<Vector3>
        {
            public static readonly ZOrder Instance = new();
            public int Compare(Vector3 a, Vector3 b) => a.z.CompareTo(b.z);
        }
    }
}
