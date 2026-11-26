using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TwinEdge.Levels;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwinEdge.Gameplay
{
    // Surface ribbons follow the sampled edge of usable footing on the saved rock.
    // They own no colliders and never participate in the authoritative landing check.
    internal sealed class SummitBoundaries : MonoBehaviour
    {
        const float Grid = .8f;
        const float HalfWidth = .13f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        readonly List<Mesh> meshes = new();
        internal bool Ready { get; private set; }

        internal IEnumerator Build(CooperativePole pole, MountainSectionV3 section, NetworkObject playerPrefab)
        {
            var terrain = section.generated.GetComponentsInChildren<MeshCollider>()
                .Single(collider => collider.name == "Mountain_ConnectedGeology");
            var material = pole.support.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
            var color = new MaterialPropertyBlock();
            color.SetColor(BaseColor, new Color(.94f, .75f, .47f, .65f));
            foreach (var peak in PoleGeometry.Summits(pole.mountain))
            {
                var mesh = SampleBoundary(pole, terrain, peak, playerPrefab);
                if (mesh != null)
                {
                    meshes.Add(mesh);
                    var marker = new GameObject(peak.id);
                    marker.transform.SetParent(transform, false);
                    marker.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = marker.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.SetPropertyBlock(color);
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                yield return null;
            }
            Ready = true;
        }

        static Mesh SampleBoundary(CooperativePole pole, MeshCollider terrain, MountainPeakV3 peak, NetworkObject playerPrefab)
        {
            Vector2 center = PoleGeometry.SummitCenter(pole.mountain, peak);
            int radius = Mathf.CeilToInt(Mathf.Max(peak.summitSize.x, peak.summitSize.y) * .5f / Grid) + 1;
            int count = radius * 2 + 1;
            var valid = new bool[count, count];
            Vector3 origin = new Vector3(center.x - radius * Grid, peak.height, center.y - radius * Grid);
            Vector3 Point(float x, float z) => origin + new Vector3(x * Grid, 0, z * Grid);
            bool Rock(Vector3 point, out RaycastHit hit) =>
                terrain.Raycast(new Ray(point + Vector3.up * 6f, Vector3.down), out hit, 12f);

            for (int x = 0; x < count; x++)
            for (int z = 0; z < count; z++)
            {
                var point = Point(x, z);
                if (PoleGeometry.Summit(point, pole.mountain, .9f) != peak.id || !Rock(point, out var hit)) continue;
                var feet = hit.point + Vector3.up * .12f;
                valid[x, z] = pole.SafeLanding(feet, out var id) && id == peak.id && pole.ClearPath(feet, feet, playerPrefab);
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var crossings = new Vector3[4];
            for (int x = 0; x < count - 1; x++)
            for (int z = 0; z < count - 1; z++)
            {
                int edgeCount = 0;
                bool a = valid[x, z], b = valid[x + 1, z], c = valid[x + 1, z + 1], d = valid[x, z + 1];
                if (a != b) crossings[edgeCount++] = Point(x + .5f, z);
                if (b != c) crossings[edgeCount++] = Point(x + 1, z + .5f);
                if (c != d) crossings[edgeCount++] = Point(x + .5f, z + 1);
                if (d != a) crossings[edgeCount++] = Point(x, z + .5f);
                // At a diagonal ambiguity, keep islands of valid footing separate.
                if (edgeCount == 4 && a)
                {
                    AddRibbon(crossings[3], crossings[0]);
                    AddRibbon(crossings[1], crossings[2]);
                }
                else
                    for (int i = 0; i < edgeCount; i += 2) AddRibbon(crossings[i], crossings[i + 1]);
            }

            void AddRibbon(Vector3 start, Vector3 end)
            {
                Vector3 side = Vector3.Cross(Vector3.up, end - start).normalized * HalfWidth;
                if (!Rock(start + side, out var a) || !Rock(start - side, out var b) ||
                    !Rock(end + side, out var c) || !Rock(end - side, out var d)) return;
                int index = vertices.Count;
                Vector3 lift = Vector3.up * .035f;
                vertices.Add(a.point + lift);
                vertices.Add(b.point + lift);
                vertices.Add(c.point + lift);
                vertices.Add(d.point + lift);
                triangles.Add(index);
                triangles.Add(index + 1);
                triangles.Add(index + 2);
                triangles.Add(index + 1);
                triangles.Add(index + 3);
                triangles.Add(index + 2);
            }

            if (vertices.Count == 0) return null;
            var mesh = new Mesh { name = peak.id + " landing boundary" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        void OnDestroy()
        {
            foreach (var mesh in meshes) Destroy(mesh);
        }
    }
}
