using System;
using System.Collections.Generic;
using System.Linq;
using TwinEdge.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwinEdge.Levels.Editor
{
    public static class V3WorldRevision
    {
        // Updates the existing generated objects and mesh assets, retaining their GUIDs and manual children.
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(MountainGeneratorV3.ScenePath, OpenSceneMode.Single);
            var mountain = ValleyGeneratorV3.Mountain();
            var trail = mountain.config.entryTrail;
            var terrain = mountain.generated.GetComponentsInChildren<MeshFilter>().Single(f => f.name == "Mountain_ConnectedGeology");
            if (mountain.generated.transform.Find("InitialSpawn"))
                throw new InvalidOperationException("The saved mountain already contains the V3 approach.");
            var mesh = terrain.sharedMesh;
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i];
                vertices[i].y = MountainGeneratorV3.TrailHeight(mountain.config, v.x, v.z, v.y);
            }
            mesh.vertices = vertices;
            Refresh(terrain.sharedMesh, terrain.GetComponent<MeshCollider>());
            MountainGeneratorV3.AddTrailMarkers(mountain.config, mountain.generated.transform);

            RefreshValleyMeshes(scene);
            ValidateTrail(trail);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the V3 world.");
            AssetDatabase.SaveAssets();
        }

        public static void RefreshRiverGeometry()
        {
            var scene = EditorSceneManager.OpenScene(MountainGeneratorV3.ScenePath, OpenSceneMode.Single);
            var config = ValleyGeneratorV3.Config();
            ValleyGeneratorV3.Check(config);
            RefreshValleyMeshes(scene);
            ValidateCastleApproach(config);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the V3 river geometry.");
            AssetDatabase.SaveAssets();
        }

        internal static void ValidateCastleApproach(ValleyConfigV3 config)
        {
            Physics.SyncTransforms();
            // Sample both lanes from the east bank to the castle apron using the saved collision mesh.
            for (int lane = -1; lane <= 1; lane += 2)
            {
                Vector3 previous = default;
                for (int z = 1907; z <= 2070; z++)
                {
                    float x = Mathf.Lerp(88, config.castleEntry.x, Mathf.InverseLerp(1907, 1950, z)) + lane * 1.5f;
                    Vector3 surface = ValleyGeneratorV3.RiverSample(config, z);
                    var feet = V3GameplayBootstrap.SafeGround(new Vector3(x, config.river[^1].y + 3, z));
                    bool inRibbon = z <= config.upstreamEnd.z && Mathf.Abs(x - surface.x) < ValleyGeneratorV3.Width(config, z) * .5f;
                    if ((inRibbon && feet.y <= surface.y + .15f) || (z > 1907 && Mathf.Abs(feet.y - previous.y) > .7f))
                        throw new InvalidOperationException("Castle approach crosses water or a steep step at " + feet);
                    previous = feet;
                }
            }
            Debug.Log("V3 CASTLE APPROACH PASS: 328 collision samples across two dry lanes.");
        }

        static void RefreshValleyMeshes(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var section in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ValleySectionV3>()))
            {
                if (section.part == ValleyPartV3.Bridge) continue;
                var filters = section.generated.GetComponentsInChildren<MeshFilter>();
                var terrain = filters.Single(f => f.name == "Terrain");
                Replace(terrain,
                    (v, t) => ValleyGeneratorV3.TerrainGeometry(section.config, section.part, v, t));
                V3EnvironmentPolish.RefreshValleySurface(section.config, section.part, terrain);
                if (section.part == ValleyPartV3.River)
                {
                    var water = filters.Single(f => f.name == "WaterSurface");
                    Replace(water, (v, t) => ValleyGeneratorV3.WaterGeometry(section.config, v, t));
                    V3WorldMaterials.SetRiverUV(water.sharedMesh);
                }
            }
        }

        public static void RefreshMountainMesh()
        {
            var scene = EditorSceneManager.OpenScene(MountainGeneratorV3.ScenePath, OpenSceneMode.Single);
            var owner = ValleyGeneratorV3.Mountain();
            var config = owner.config;
            MountainGeneratorV3.CheckConfig(config);
            var peaks = new[] { config.start, config.destination }
                .Concat(config.smallPeaks.Select(peak => MountainGeneratorV3.WorldPeak(config, peak))).ToArray();
            var filter = owner.generated.GetComponentsInChildren<MeshFilter>()
                .Single(meshFilter => meshFilter.name == "Mountain_ConnectedGeology");
            var mesh = MountainGeneratorV3.BuildTerrainMesh(config, peaks);
            EditorUtility.CopySerialized(mesh, filter.sharedMesh);
            UnityEngine.Object.DestroyImmediate(mesh);
            Refresh(filter.sharedMesh, filter.GetComponent<MeshCollider>());
            filter.GetComponent<MeshRenderer>().sharedMaterial = V3WorldMaterials.MountainBlend();
            foreach (var peak in peaks)
                owner.generated.transform.Find(peak.id).position = new Vector3(peak.position.x, peak.height, peak.position.y);
            if (config.entryTrail.Length > 0)
            {
                var spawn = owner.generated.transform.Find("InitialSpawn");
                spawn.position = config.entryTrail[1];
                MountainGeneratorV3.AlignTrailSpawn(config.entryTrail, spawn);
                owner.generated.transform.Find("TrailEntrance").position = config.entryTrail[^1];
                owner.generated.transform.Find("ReturnFinish").position = config.entryTrail[^2];
            }
            if (config.entryTrail.Length > 0) ValidateTrail(config.entryTrail);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the V3 mountain.");
            AssetDatabase.SaveAssets();
        }

        public static void RefreshAndAnalyzeMountain()
        {
            RefreshMountainMesh();
            MountainPoleReachability.Load();
            MountainPoleReachability.Analyze("Tuned");
            MountainPoleTuning.InspectCurrent();
        }

        // Far range silhouettes remain outside navigation space and cast no shadows.
        public static void ApplyHorizon()
        {
            var scene = EditorSceneManager.OpenScene(MountainGeneratorV3.ScenePath, OpenSceneMode.Single);
            const string folder = "Assets/Game/Level/Data/Generated/V3Horizon";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Game/Level/Data/Generated", "V3Horizon");
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/HorizonRock.mat");
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "HorizonRock" };
                material.SetColor("_BaseColor", new Color(.33f, .41f, .46f));
                material.SetFloat("_Smoothness", 0);
                AssetDatabase.CreateAsset(material, folder + "/HorizonRock.mat");
            }
            var root = scene.GetRootGameObjects().SingleOrDefault(o => o.name == "V3 Distant Terrain");
            if (!root) root = new GameObject("V3 Distant Terrain");
            // Independent strips allow ordinary camera frustum culling of the opposite skyline.
            const int strips = 8, columns = 21;
            for (int strip = 0; strip < strips; strip++)
            {
                var vertices = new List<Vector3>();
                var indices = new List<int>();
                for (int i = 0; i < columns; i++)
                {
                    float a = (strip + i / (float)(columns - 1)) * Mathf.PI * 2 / strips;
                    var radial = new Vector3(Mathf.Cos(a) * 1180, 0, Mathf.Sin(a) * 1980);
                    float peak = 245 + 136 * Mathf.Pow(.5f + .5f * Mathf.Sin(a * 8 + .7f), 2f) +
                        75 * Mathf.Sin(a * 17) + 37 * Mathf.Sin(a * 33);
                    var center = new Vector3(0, 0, 820);
                    vertices.Add(center + radial * .80f + Vector3.down * 170);
                    vertices.Add(center + radial * .92f + Vector3.up * (peak * .34f));
                    vertices.Add(center + radial * 1.04f + Vector3.up * peak);
                    vertices.Add(center + radial * 1.17f + Vector3.down * 170);
                    if (i == 0) continue;
                    for (int row = 0; row < 3; row++)
                    {
                        int k = i * 4 + row;
                        indices.AddRange(new[] { k - 4, k, k - 3, k - 3, k, k + 1 });
                    }
                }
                string name = "Range " + strip;
                var filter = root.transform.Find(name)?.GetComponent<MeshFilter>();
                if (!filter)
                {
                    var part = new GameObject(name);
                    part.transform.SetParent(root.transform, false);
                    filter = part.AddComponent<MeshFilter>();
                    var renderer = part.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                string path = folder + "/Range" + strip + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!mesh)
                {
                    mesh = new Mesh { name = name };
                    AssetDatabase.CreateAsset(mesh, path);
                }
                filter.sharedMesh = mesh;
                Replace(filter, (v, t) => { v.AddRange(vertices); t.AddRange(indices); });
            }
            ConfigureFog();
            EditorSceneManager.SaveScene(scene);
            var gameplay = EditorSceneManager.OpenScene(TwinEdge.Gameplay.Editor.GameplayFoundationSetup.ScenePath);
            ConfigureFog();
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<V3GameplayBootstrap>();
            bootstrap.standbyCamera.farClipPlane = 2600;
            EditorSceneManager.SaveScene(gameplay);
            AssetDatabase.SaveAssets();
        }

        static void ConfigureFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.54f, .64f, .72f);
            RenderSettings.fogStartDistance = 235;
            RenderSettings.fogEndDistance = 1750;
        }

        static void ValidateTrail(Vector3[] trail)
        {
            Physics.SyncTransforms();
            float maxStep = 0, length = 0;
            for (int i = 2; i < trail.Length; i++) length += Vector3.Distance(trail[i - 1], trail[i]);
            Vector3 previous = default;
            for (float z = trail[1].z; z <= trail[^1].z; z++)
            {
                Vector3 point = MountainGeneratorV3.SampleTrail(trail, z);
                var ground = V3GameplayBootstrap.SafeGround(point);
                if (Mathf.Abs(ground.y - .12f - point.y) > .5f)
                    throw new InvalidOperationException("Approach ground diverges from its authored grade at " + point);
                foreach (float lane in new[] { -2f, 2f })
                    if (Mathf.Abs(V3GameplayBootstrap.SafeGround(point + Vector3.right * lane).y - ground.y) > .55f)
                        throw new InvalidOperationException("Approach lane lacks continuous support at " + point);
                if (z > trail[1].z) maxStep = Mathf.Max(maxStep, Mathf.Abs(ground.y - previous.y));
                previous = ground;
            }
            if (maxStep > .8f) throw new InvalidOperationException("Approach too steep: " + maxStep);
            Debug.Log($"V3 TRAIL {length:F1}m, maximum 1m Z-sample height change {maxStep:F2}m. Spawn {V3GameplayBootstrap.SafeGround(trail[1]):F2}, finish {V3GameplayBootstrap.SafeGround(trail[^2]):F2}.");
        }

        static void Replace(MeshFilter filter, Action<List<Vector3>, List<int>> generate)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            generate(vertices, indices);
            var mesh = filter.TryGetComponent<MeshCollider>(out var collider) ? collider.sharedMesh : filter.sharedMesh;
            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            Refresh(mesh, collider);
        }

        static void Refresh(Mesh mesh, MeshCollider collider)
        {
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            if (collider)
            {
                collider.sharedMesh = null;
                collider.sharedMesh = mesh;
            }
        }
    }
}
