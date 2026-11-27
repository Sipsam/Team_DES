using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TwinEdge.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static TwinEdge.Presentation.CourseMapData;

namespace TwinEdge.Presentation.Editor
{
    public static class CourseMapBake
    {
        const string ScenePath = "Assets/Game/Level/Scenes/LevelBlockoutV3.unity";
        const string Output = "Assets/Game/UI/Resources/";
        const string AtlasPath = Output + "V3CourseMap.asset";

        [MenuItem("Tools/Twin Edge/Presentation/Bake Public Course Map")]
        public static void Bake()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var mountain = roots.SelectMany(r => r.GetComponentsInChildren<MountainSectionV3>()).Single();
            var valley = roots.SelectMany(r => r.GetComponentsInChildren<ValleySectionV3>()).ToDictionary(s => s.part);
            var fortress = roots.SelectMany(r => r.GetComponentsInChildren<FortressSectionV3>()).ToDictionary(s => s.part);
            var river = valley[ValleyPartV3.River];
            var castle = fortress[FortressPartV3.CastleExterior];
            var config = castle.config;
            var markers = roots.SelectMany(r => r.GetComponentsInChildren<LevelMarker>()).ToDictionary(m => m.id);
            var data = AssetDatabase.LoadAssetAtPath<CourseMapData>(AtlasPath);
            if (!data) data = ScriptableObject.CreateInstance<CourseMapData>();
            Vector3 Marker(string id) => markers[id].transform.position;

            data.Finish = Marker("Mountain.V3.ReturnFinish");
            data.FirstSummit = Marker("Mountain.V3.Start");
            data.LastSummit = Marker("Mountain.V3.Final");
            data.BridgeStart = valley[ValleyPartV3.Bridge].config.bridgeStart;
            data.BridgeEnd = valley[ValleyPartV3.Bridge].config.bridgeEnd;
            data.LowerDock = Marker("Valley.V3.LowerBoarding");
            data.UpperDock = Marker("Valley.V3.UpperBoarding");
            data.CastleGate = config.origin;
            data.StairTop = config.origin + config.undergroundDoor;
            data.CaveStart = config.origin + config.start;
            data.Seal = config.origin + config.seal;
            data.CavernCeiling = config.origin.y + config.ceiling;

            Directory.CreateDirectory(Output);
            var landscape = new List<Surface>();
            Take(landscape, mountain.transform.Find("Generated"), n => n == "Mountain_ConnectedGeology" || n == "Bridge_StartStub_Only", 0);
            Take(landscape, valley[ValleyPartV3.Bridge].generated.transform, n => n.StartsWith("Plank_", StringComparison.Ordinal) || n == "OppositeCliff", 1);
            Take(landscape, valley[ValleyPartV3.CanyonDescent].generated.transform, n => n is "Terrain" or "Canyon Stone Stairway", 0);
            Take(landscape, river.generated.transform, n => n is "Terrain" or "LowerDock" or "UpperDock", 0);
            Take(landscape, river.generated.transform, n => n == "WaterSurface", 2);
            Take(landscape, valley[ValleyPartV3.CastleApproach].generated.transform, n => n == "Terrain", 0);
            Take(landscape, castle.generated.transform, n => n == "Foundation" || n.StartsWith("Curtain_", StringComparison.Ordinal) ||
                n.StartsWith("Tower_", StringComparison.Ordinal) || n == "OuterRockEscarpment", 1);
            Rect whole = BoundsOf(landscape, 10);
            Texture2D outdoor = Raster("V3MapOutdoor", whole, landscape, 1.6f);
            data.Overview = new Sheet { Bounds = whole, TextureBounds = whole, Texture = outdoor, Radius = 120 };

            var indoor = new List<Surface>();
            // Public building footprint only. Omit private room contents, traps, and every sewer surface.
            Floor(indoor, config.origin, config.keep, 3);
            foreach (var room in config.rooms) Floor(indoor, config.origin + Vector3.up * .15f, room.plan, 1);
            var stairs = new List<Surface>();
            Take(stairs, fortress[FortressPartV3.UndergroundAccess].generated.transform,
                n => n.StartsWith("WalkableRamp_", StringComparison.Ordinal) ||
                    n.StartsWith("Landing_", StringComparison.Ordinal) || n is "U0Landing" or "StairToU0", 3);
            var cavern = new List<Surface>();
            Take(cavern, fortress[FortressPartV3.UndergroundCavern].generated.transform,
                n => n is "CavernFloor" or "FacetedCavernWalls", 4);
            Rect mountainPlan = Rect.MinMaxRect(whole.xMin, whole.yMin, whole.xMax, data.BridgeStart.z + 18);
            data.Sheets = new[]
            {
                Outdoor(Zone.Mountain, mountainPlan, 150),
                Outdoor(Zone.Bridge, Rect.MinMaxRect(data.BridgeStart.x - 75, data.BridgeStart.z - 27,
                    data.BridgeEnd.x + 75, data.BridgeEnd.z + 34), 60),
                Outdoor(Zone.Canyon, Rect.MinMaxRect(-190, data.BridgeEnd.z - 28, 170, data.LowerDock.z + 26), 120),
                Outdoor(Zone.River, Rect.MinMaxRect(whole.xMin, data.LowerDock.z - 110, whole.xMax, data.CastleGate.z + 10), 120),
                Outdoor(Zone.CastleExterior, Rect.MinMaxRect(config.origin.x - 138, data.CastleGate.z - 65, config.origin.x + 138,
                    data.CastleGate.z + config.keep.yMax + 70), 60),
                Separate(Zone.CastleInterior, indoor, 40),
                Separate(Zone.Stair, stairs, 50),
                Separate(Zone.Cavern, cavern, 50),
                Outdoor(Zone.Finish, new Rect(Xz(data.Finish) - Vector2.one * 115, Vector2.one * 230), 120)
            };

            var pins = new List<Pin>();
            void Add(string id, Kind kind, Vector3 point) => pins.Add(new Pin { Id = id, Kind = kind, Position = point });
            Add("finish", Kind.Finish, data.Finish);
            Add("pole", Kind.Gimmick, data.FirstSummit);
            Add("timber", Kind.Gimmick, Marker("Valley.V3.TimberSupply"));
            Add("lower-raft", Kind.Gimmick, data.LowerDock);
            Add("upper-raft", Kind.Gimmick, data.UpperDock);
            Add("castle", Kind.Gimmick, data.CastleGate + new Vector3(0, 0, config.keep.yMin + 10));
            Add("seal", Kind.Gimmick, data.Seal);
            Add("cave-start", Kind.Checkpoint, data.CaveStart);
            Add("summit", Kind.Checkpoint, data.LastSummit);
            Add("lower-dock", Kind.Checkpoint, Marker("Valley.V3.LowerCheckpoint"));
            Add("upper-dock", Kind.Checkpoint, Marker("Valley.V3.UpperCheckpoint"));
            foreach (var marker in markers.Values.Where(m => m.id.StartsWith("Mountain.V3.Peak_", StringComparison.Ordinal))
                .OrderBy(m => m.id, StringComparer.Ordinal))
                Add(marker.id, Kind.Peak, marker.transform.position);
            data.Pins = pins.ToArray();

            if (!AssetDatabase.Contains(data)) AssetDatabase.CreateAsset(data, AtlasPath);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"Public course map baked from {scene.path}: {whole}, {landscape.Count} allowed outdoor meshes, {data.Pins.Length} pins.");

            Sheet Outdoor(Zone zone, Rect bounds, float radius) =>
                new() { Zone = zone, Bounds = bounds, TextureBounds = whole, Texture = outdoor, Radius = radius };

            Sheet Separate(Zone zone, List<Surface> surfaces, float radius)
            {
                var bounds = BoundsOf(surfaces, 3);
                return new Sheet { Zone = zone, Bounds = bounds, TextureBounds = bounds,
                    Radius = radius, Texture = Raster("V3Map" + zone, bounds, surfaces, .65f) };
            }
        }

        readonly struct Surface
        {
            public readonly Vector3[] Vertices;
            public readonly int[] Triangles;
            public readonly int Type;

            public Surface(Vector3[] vertices, int[] triangles, int type)
            {
                Vertices = vertices;
                Triangles = triangles;
                Type = type;
            }
        }

        static void Take(List<Surface> surfaces, Transform generated, Func<string, bool> include, int type)
        {
            foreach (Transform child in generated)
            {
                if (!include(child.name)) continue;
                foreach (var filter in child.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = filter.sharedMesh;
                    if (!mesh) continue;
                    var vertices = mesh.vertices;
                    Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                    for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                    surfaces.Add(new Surface(vertices, mesh.triangles, type));
                }
            }
        }

        static void Floor(List<Surface> surfaces, Vector3 origin, Rect plan, int type)
        {
            surfaces.Add(new Surface(new[]
            {
                origin + new Vector3(plan.xMin, 0, plan.yMin), origin + new Vector3(plan.xMax, 0, plan.yMin),
                origin + new Vector3(plan.xMin, 0, plan.yMax), origin + new Vector3(plan.xMax, 0, plan.yMax)
            }, new[] { 0, 2, 1, 1, 2, 3 }, type));
        }

        static Rect BoundsOf(List<Surface> surfaces, float padding)
        {
            if (surfaces.Count == 0) throw new InvalidOperationException("Public map layer has no mesh sources.");
            Vector2 min = Vector2.one * float.MaxValue, max = -min;
            foreach (var surface in surfaces)
            foreach (var vertex in surface.Vertices)
            {
                Vector2 point = Xz(vertex);
                min = Vector2.Min(point, min);
                max = Vector2.Max(point, max);
            }
            min -= Vector2.one * padding;
            max += Vector2.one * padding;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static Texture2D Raster(string name, Rect bounds, List<Surface> surfaces, float step)
        {
            int width = Mathf.CeilToInt(bounds.width / step);
            int height = Mathf.CeilToInt(bounds.height / step);
            var heights = new float[width * height];
            Array.Fill(heights, float.NegativeInfinity);
            var pixels = new Color32[heights.Length];
            foreach (var surface in surfaces)
            for (int i = 0; i < surface.Triangles.Length; i += 3)
            {
                var a = surface.Vertices[surface.Triangles[i]];
                var b = surface.Vertices[surface.Triangles[i + 1]];
                var c = surface.Vertices[surface.Triangles[i + 2]];
                Vector2 aa = ImagePoint(a), ab = ImagePoint(b), ac = ImagePoint(c);
                float determinant = Cross(ab - aa, ac - aa);
                if (Mathf.Abs(determinant) < .0001f) continue;
                int xMin = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(aa.x, ab.x, ac.x)), 0, width - 1);
                int xMax = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(aa.x, ab.x, ac.x)), 0, width - 1);
                int yMin = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(aa.y, ab.y, ac.y)), 0, height - 1);
                int yMax = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(aa.y, ab.y, ac.y)), 0, height - 1);
                float grade = Mathf.Clamp01(Mathf.Abs(Vector3.Cross(b - a, c - a).normalized.y));
                for (int y = yMin; y <= yMax; y++)
                for (int x = xMin; x <= xMax; x++)
                {
                    Vector2 d = new Vector2(x + .5f, y + .5f) - aa;
                    float u = Cross(d, ac - aa) / determinant, v = Cross(ab - aa, d) / determinant;
                    if (u < 0 || v < 0 || u + v > 1) continue;
                    float elevation = a.y + u * (b.y - a.y) + v * (c.y - a.y);
                    int index = y * width + x;
                    if (elevation <= heights[index]) continue;
                    heights[index] = elevation;
                    pixels[index] = Colour(surface.Type, elevation, grade);
                }
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            string assetPath = Output + name + ".png";
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            Vector2 ImagePoint(Vector3 point) => (Xz(point) - bounds.min) / bounds.size * new Vector2(width, height);
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static Color Colour(int type, float height, float grade)
        {
            if (type == 2) return new Color32(75, 125, 139, 255);
            if (type == 3) return new Color32(100, 111, 111, 255);
            if (type == 4) return new Color32(108, 73, 60, 255);
            if (type == 1) return Color.Lerp(new Color32(114, 93, 69, 255), new Color32(179, 161, 133, 255), grade);
            float band = Mathf.Floor(Mathf.Clamp01((height - 28) / 390) * 11) / 11;
            var color = Color.Lerp(new Color32(69, 101, 83, 255), new Color32(226, 210, 176, 255), band);
            return Color.Lerp(color * .69f, color, grade).gamma.WithAlpha();
        }

        static Color WithAlpha(this Color color) => new(color.r, color.g, color.b, 1);
    }
}
