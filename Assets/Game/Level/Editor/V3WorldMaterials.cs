using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

namespace TwinEdge.Levels.Editor
{
    public static class V3WorldMaterials
    {
        const string Derived = "Assets/Game/Level/Data/V3WorldMaterials/";

        internal static Material Top => Load("Terrain1_Test/Mountain/Top/Top.mat");
        internal static Material Middle => Load("Terrain1_Test/Mountain/Middle/Middle.mat");
        internal static Material Bottom => Load("Terrain1_Test/Mountain/Bottom/Bottom.mat");
        const string MountainBlendPath = Derived + "MountainBlend.mat";
        internal static Material Rock => LoadDerived("Rock");
        internal static Material Water => Load("Terrain2_Test/Canyon/Water/Water.mat");
        const string RoofSource = "Terrain3_Test/Castle/Roof/Roof.mat";
        const string FloorSource = "Terrain3_Test/Castle/Floor/Floor.mat";
        const string LavaSource = "Terrain4_Test/Underground/Lava/Lava.mat";
        internal static Material Roof => FindSurface("CastleRoof", RoofSource);
        internal static Material Floor => FindSurface("CastleFloor", FloorSource);
        internal static Material Wall => LoadDerived("Wall");
        internal static Material UndergroundStone => LoadDerived("UndergroundStone");
        internal static Material Lava => FindSurface("CavernLava", LavaSource);

        internal static (Material original, Material surface)[] PrepareFortressSurfaces()
        {
            var roof = PrepareSurface("CastleRoof", RoofSource, new Vector2(.145f, .19f), .72f);
            var floor = PrepareSurface("CastleFloor", FloorSource, new Vector2(.24f, .29f), .52f);
            var lava = PrepareSurface("CavernLava", LavaSource, new Vector2(.063f, .079f), .42f);
            lava.surface.SetColor("_EmissionColor", new Color(.56f, .30f, .12f));
            return new[] { roof, floor, lava };
        }

        static Material FindSurface(string name, string source)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Derived + name + ".mat");
            return material ? material : Load(source);
        }

        static (Material original, Material surface) PrepareSurface(string name, string source, Vector2 scale,
            float normalStrength)
        {
            var original = Load(source);
            string path = Derived + name + ".mat";
            var surface = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!surface)
            {
                surface = new Material(original) { name = name };
                AssetDatabase.CreateAsset(surface, path);
            }
            surface.SetTextureScale("_BaseMap", scale);
            surface.SetFloat("_BumpScale", normalStrength);
            EditorUtility.SetDirty(surface);
            return (original, surface);
        }

        static Material Load(string path) => LoadPath("Assets/" + path);
        static Material LoadDerived(string name) => LoadPath(Derived + name + ".mat");

        static Material LoadPath(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) throw new InvalidOperationException("Missing V3 world material: " + path);
            return material;
        }

        internal static Material MountainBlend()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MountainBlendPath);
            if (material) return material;
            material = new Material(Middle) { name = "MountainBlend" };
            material.SetFloat("_MountainLayers", 1);
            material.EnableKeyword("_MOUNTAIN_LAYERS");
            SetMountainLayer(material, Top, "_Top");
            SetMountainLayer(material, Bottom, "_Bottom");
            AssetDatabase.CreateAsset(material, MountainBlendPath);
            return material;
        }

        static void SetMountainLayer(Material target, Material source, string layer)
        {
            target.SetTexture(layer + "Map", source.GetTexture("_BaseMap"));
            target.SetTextureScale(layer + "Map", source.GetTextureScale("_BaseMap"));
            target.SetTextureOffset(layer + "Map", source.GetTextureOffset("_BaseMap"));
            target.SetTexture(layer + "Normal", source.GetTexture("_BumpMap"));
            target.SetFloat(layer + "NormalScale", source.GetFloat("_BumpScale"));
            target.SetColor(layer + "Tint", source.GetColor("_BaseColor"));
        }

        internal static void SetRiverUV(Mesh mesh)
        {
            // The ribbon stores a pair of left and right vertices per cross-section.
            // Measure V along the centreline including the two visible, non-playable extensions.
            var vertices = mesh.vertices;
            if (vertices.Length < 4 || vertices.Length % 2 != 0)
                throw new InvalidOperationException("Invalid V3 river ribbon.");
            var uv = new Vector2[vertices.Length];
            float distance = 0;
            Vector3 previous = (vertices[0] + vertices[1]) * .5f;
            for (int i = 0; i < vertices.Length; i += 2)
            {
                Vector3 centre = (vertices[i] + vertices[i + 1]) * .5f;
                distance += Vector3.Distance(previous, centre);
                float halfWidth = Vector3.Distance(vertices[i], vertices[i + 1]) * .5f;
                uv[i] = new Vector2(-halfWidth, distance);
                uv[i + 1] = new Vector2(halfWidth, distance);
                previous = centre;
            }
            mesh.uv = uv;
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
        }

        static MeshRenderer SavedWater()
        {
            var river = UnityEngine.Object.FindObjectsByType<ValleySectionV3>(FindObjectsSortMode.None)
                .Single(section => section.part == ValleyPartV3.River);
            return river.generated.transform.Find("WaterSurface").GetComponent<MeshRenderer>();
        }

        [MenuItem("Tools/Twin Edge/V3 River/Update Saved Water UVs")]
        public static void UpdateSavedWaterUV()
        {
            EditorSceneManager.OpenScene(MountainGeneratorV3.ScenePath);
            SetRiverUV(SavedWater().GetComponent<MeshFilter>().sharedMesh);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Twin Edge/Gameplay/Validate Saved World Materials")]
        public static void Validate()
        {
            EditorSceneManager.OpenScene(MountainGeneratorV3.ScenePath);
            var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            var mountain = renderers.Single(renderer => renderer.name == "Mountain_ConnectedGeology");
            var blend = mountain.sharedMaterial;
            if (!blend || !blend.IsKeywordEnabled("_MOUNTAIN_LAYERS") || !blend.IsKeywordEnabled("_TRIPLANAR"))
                throw new InvalidOperationException("The mountain renderer requires the three-layer surface.");
            var mountainMaps = new[] { (Top, "_TopMap", "_TopNormal"), (Middle, "_BaseMap", "_BumpMap"), (Bottom, "_BottomMap", "_BottomNormal") };
            foreach (var (source, colorMap, normalMap) in mountainMaps)
                if (blend.GetTexture(colorMap) != source.GetTexture("_BaseMap") ||
                    blend.GetTexture(normalMap) != source.GetTexture("_BumpMap"))
                    throw new InvalidOperationException("Mountain blend is missing maps from " + source.name);
            var materials = new[] { Top, Middle, Bottom, Rock, Water, Roof, Floor, Wall, Lava, UndergroundStone };
            var textures = new System.Collections.Generic.HashSet<Texture>();
            foreach (var material in materials)
            {
                int references = renderers.Count(renderer => renderer.sharedMaterials.Contains(material)) +
                    (mountainMaps.Any(layer => layer.Item1 == material) ? 1 : 0);
                var normal = material.GetTexture("_BumpMap");
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
                if (references == 0 || material.shader.name != "Twin Edge/V3 World Surface" ||
                    !material.GetTexture("_BaseMap") || importer == null || importer.textureType != TextureImporterType.NormalMap)
                    throw new InvalidOperationException("Incomplete saved material: " + material.name);
                foreach (string property in material.GetTexturePropertyNames())
                    if (material.GetTexture(property) is Texture texture) textures.Add(texture);
                Debug.Log($"V3_MATERIAL {material.name}: {references} saved renderer(s), normal {normal.name}");
            }
            var mesh = SavedWater().GetComponent<MeshFilter>().sharedMesh;
            if (SavedWater().sharedMaterial != Water || !Water.IsKeywordEnabled("_RIVER_FLOW") ||
                mesh.uv.Length != mesh.vertexCount || mesh.tangents.Length != mesh.vertexCount ||
                mesh.uv[^1].y < 1000)
                throw new InvalidOperationException("The saved water requires downstream ribbon UVs and tangents.");
            if (!Lava.IsKeywordEnabled("_EMISSION") || !Lava.GetTexture("_EmissionMap"))
                throw new InvalidOperationException("The saved lava requires its emissive map.");
            Debug.Log($"V3_MATERIAL_PASS {textures.Count} maps; Editor asset memory " +
                $"{textures.Sum(Profiler.GetRuntimeMemorySizeLong) / (1024f * 1024f):F1} MiB.");
        }

        public static void PrepareAndBuildDevelopment()
        {
            UpdateSavedWaterUV();
            Validate();
            TwinEdge.Gameplay.Editor.GameplayFoundationSetup.Validate();
            TwinEdge.Gameplay.Editor.RiverValidation.Validate();
            TwinEdge.Gameplay.Editor.CastleGameplaySetup.Validate();
            TwinEdge.Gameplay.Editor.UndergroundGameplaySetup.Validate();
            TwinEdge.Gameplay.Editor.GameplayFoundationSetup.BuildFullMatchPlayer();
        }
    }
}
