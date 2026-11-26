using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwinEdge.Levels.Editor
{
    public static partial class ValleyGeneratorV3
    {
        const string StairTreads = "Canyon Stone Stairway";
        const string StairWalls = "Canyon Stair Parapets";

        [MenuItem("Tools/Twin Edge/V3 Bridge River/Refresh Canyon Stairway")]
        public static void RefreshCanyonStairway()
        {
            var scene = EditorSceneManager.OpenScene(MountainGeneratorV3.ScenePath);
            var sections = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ValleySectionV3>()).ToArray();
            var canyon = sections.Single(section => section.part == ValleyPartV3.CanyonDescent);
            var bridge = sections.Single(section => section.part == ValleyPartV3.Bridge);
            var c = canyon.config;
            Check(c);

            // Keep the saved collar topology and all bridge objects. Reshape only its Canyon end.
            var bridgeMesh = bridge.generated.transform.Find("Terrain").GetComponent<MeshCollider>().sharedMesh;
            var collar = bridgeMesh.vertices;
            for (int i = 0; i < collar.Length; i++)
                if (collar[i].z > c.bridgeEnd.z)
                    collar[i].y = Height(c, collar[i].x, collar[i].z);
            UpdateTerrain(bridge, collar);

            var canyonMesh = canyon.generated.transform.Find("Terrain").GetComponent<MeshCollider>().sharedMesh;
            var slope = canyonMesh.vertices;
            for (int i = 0; i < slope.Length; i++)
                slope[i].y = Height(c, slope[i].x, slope[i].z);
            UpdateTerrain(canyon, slope);

            BuildCanyonStairs(canyon.generated.transform, canyon.generationAssets, c);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the Canyon stairway.");
            AssetDatabase.SaveAssets();
        }

        static void UpdateTerrain(ValleySectionV3 section, Vector3[] vertices)
        {
            var terrain = section.generated.transform.Find("Terrain").GetComponent<MeshFilter>();
            var collider = terrain.GetComponent<MeshCollider>();
            var mesh = collider.sharedMesh;
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
            EditorUtility.SetDirty(mesh);
            V3EnvironmentPolish.RefreshValleySurface(section.config, section.part, terrain);
        }

        static void BuildCanyonStairs(Transform parent, string folder, ValleyConfigV3 config)
        {
            var profile = config.CanyonRoute;
            var upper = config.UpperStairTread;
            var lower = config.LowerStairTread;
            var down = Vector3.ProjectOnPlane(lower - upper, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, down);
            var half = right * (ValleyConfigV3.StairWidth * .5f);
            int count = Mathf.CeilToInt((upper.y - lower.y) / ValleyConfigV3.MaxStairRise);

            var stairs = new StairMesh();
            var walls = new StairMesh();
            stairs.Tread(profile[0], upper, half);
            for (int i = 0; i < count; i++)
            {
                var a = Vector3.Lerp(upper, lower, i / (float)count);
                var b = Vector3.Lerp(upper, lower, (i + 1f) / count);
                var lip = new Vector3(a.x, b.y, a.z);
                stairs.Tread(lip, b, half);
                stairs.Quad(a + half, a - half, lip - half, lip + half);
            }
            stairs.Tread(lower, profile[^1], half);

            // Sloped masonry caps form a continuous guard and the lower courses cover the tread edges.
            var guardStart = upper - down * 2f;
            foreach (float side in new[] { -1f, 1f })
            {
                var inset = (half - right * .25f) * side;
                var outset = (half + right * .23f) * side;
                walls.Parapet(guardStart, upper, inset, outset, side);
                int sections = Mathf.CeilToInt(Vector3.Distance(upper, lower) / 8f);
                for (int i = 0; i < sections; i++)
                    walls.Parapet(Vector3.Lerp(upper, lower, i / (float)sections),
                        Vector3.Lerp(upper, lower, (i + 1f) / sections), inset, outset, side);
                walls.Cap(guardStart, inset, outset, side > 0);
                walls.Cap(lower, inset, outset, side < 0);
            }

            stairs.Save(parent, folder, StairTreads, V3WorldMaterials.Floor);
            walls.Save(parent, folder, StairWalls, V3WorldMaterials.Wall);
            var route = parent.Find("SharedRoute");
            const string notes = "Bidirectional stone stairway, from the bridge landing to the Lower dock approach.";
            if (route == null) Route(parent, profile, RouteKind.Walk, notes);
            else
            {
                var metadata = route.GetComponent<LevelRoute>();
                metadata.points = profile;
                metadata.notes = notes;
            }
        }

        sealed class StairMesh
        {
            readonly List<Vector3> vertices = new();
            readonly List<int> indices = new();
            static readonly Vector3 Footing = Vector3.down * 3f;
            static readonly Vector3 Guard = Vector3.up * 1.5f;

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            public void Tread(Vector3 start, Vector3 end, Vector3 half)
            {
                Quad(start - half, end - half, end + half, start + half);
                Quad(start - half + Footing, end - half + Footing, end - half, start - half);
                Quad(start + half, end + half, end + half + Footing, start + half + Footing);
            }

            public void Parapet(Vector3 start, Vector3 end, Vector3 inside, Vector3 outside, float side)
            {
                var left = side > 0 ? inside : outside;
                var right = side > 0 ? outside : inside;
                Quad(start + left + Guard, end + left + Guard, end + right + Guard, start + right + Guard);
                Quad(start + left + Footing, end + left + Footing, end + left + Guard, start + left + Guard);
                Quad(start + right + Guard, end + right + Guard, end + right + Footing, start + right + Footing);
            }

            public void Cap(Vector3 end, Vector3 inside, Vector3 outside, bool faceUpstairs)
            {
                var left = faceUpstairs ? inside : outside;
                var right = faceUpstairs ? outside : inside;
                Quad(end + left + Guard, end + right + Guard, end + right + Footing, end + left + Footing);
            }

            public void Save(Transform parent, string folder, string name, Material material)
            {
                var previous = parent.Find(name);
                if (previous == null)
                {
                    MeshObject(parent, folder, name, vertices, indices, material, true);
                    return;
                }
                var filter = previous.GetComponent<MeshFilter>();
                var collider = previous.GetComponent<MeshCollider>();
                var mesh = filter.sharedMesh;
                mesh.Clear();
                mesh.SetVertices(vertices);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                collider.sharedMesh = null;
                collider.sharedMesh = mesh;
                EditorUtility.SetDirty(mesh);
            }
        }
    }
}
