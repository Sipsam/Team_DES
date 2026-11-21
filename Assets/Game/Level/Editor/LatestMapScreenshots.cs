using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Levels.Editor
{
    [InitializeOnLoad]
    public static class LatestMapScreenshots
    {
        const int Width = 1800, Height = 1200;
        const string Version = "v3";
        const string TerrainPath = MountainGeneratorV3.ScenePath;
        static readonly string[] Regions = { "World", "Mountain", "Bridge", "Canyon", "River", "Castle", "Underground" };

        const string Request = "Docs/Development/Level/V3/LatestScreenshots.request";
        static double nextRequestCheck;

        static LatestMapScreenshots()
        {
            if (!Application.isBatchMode) EditorApplication.update += CheckRequestedCapture;
        }

        static void CheckRequestedCapture()
        {
            if (EditorApplication.timeSinceStartup < nextRequestCheck) return;
            nextRequestCheck = EditorApplication.timeSinceStartup + 2;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request))
                return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return;
            try
            {
                string requested = File.ReadAllText(Request).Trim();
                if (!Regex.IsMatch(requested, "^[0-9a-f]{40}$")) return;
                File.Delete(Request);
                if (Git("rev-parse HEAD") == requested) Capture();
            }
            catch (Exception error)
            {
                UnityEngine.Debug.LogWarning("Latest map screenshots were kept: " + error.Message);
            }
        }

        [MenuItem("Tools/Twin Edge/Capture Latest Map Screenshots")]
        public static void Capture()
        {
            string commit = CommittedTree();
            CaptureSet(Path.GetFullPath("Screenshots"), commit, true);
        }

        public static void CaptureBaseline()
        {
            CaptureSet(Path.GetFullPath("Docs/Development/Level/V3/WorldBaseline"), Git("rev-parse HEAD")[..7], false);
        }

        public static void CaptureWorkingMap()
        {
            CaptureSet(Path.GetFullPath("Docs/Development/Level/V3/WorldWorking"), "working", false);
        }

        static string Git(string arguments)
        {
            using var git = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = Path.GetDirectoryName(Application.dataPath),
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
            });
            string output = git.StandardOutput.ReadToEnd().Trim();
            git.WaitForExit();
            if (git.ExitCode != 0) throw new IOException("Git failed: " + arguments);
            return output;
        }

        static string CommittedTree()
        {
            if (Git("status --porcelain --untracked-files=no").Length != 0 ||
                Git("ls-files --others --exclude-standard -- Assets Packages ProjectSettings").Length != 0)
                throw new InvalidOperationException("Commit the map and project changes before capturing latest screenshots.");
            return Git("rev-parse HEAD")[..7];
        }

        static void CaptureSet(string destination, string commit, bool publish)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Capture requires Edit mode.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before capturing the map.");

            if (Directory.Exists(destination) && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("The capture directory cannot be a filesystem link.");
            string staging = Path.Combine(destination, ".capture-" + Guid.NewGuid().ToString("N"));
            string previous = Path.Combine(staging, "previous");
            Directory.CreateDirectory(previous);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(TerrainPath, OpenSceneMode.Single);
                Render(scene, staging, commit);
                if (publish && CommittedTree() != commit)
                    throw new InvalidOperationException("The committed project changed during capture.");
                ReplaceSet(destination, staging, previous);
            }
            finally
            {
                if (setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                if (!Directory.EnumerateFileSystemEntries(previous).Any())
                {
                    Directory.Delete(previous);
                    foreach (string png in Directory.GetFiles(staging, "*.png")) File.Delete(png);
                    Directory.Delete(staging);
                }
            }
        }

        static void ReplaceSet(string destination, string staging, string previous)
        {
            string[] fresh = Directory.GetFiles(staging, "*.png");
            if (fresh.Length != Regions.Length * 2) throw new IOException("Expected 14 verified screenshots.");
            Directory.CreateDirectory(destination);
            var retired = new List<string>();
            var installed = new List<string>();
            var name = new Regex("^(World|Mountain|Bridge|Canyon|River|Castle|Underground)_(top|perspective)_v[0-9]+_(?:[0-9a-f]{7}|working)\\.png$");
            try
            {
                foreach (string file in Directory.GetFiles(destination, "*.png").Where(p => name.IsMatch(Path.GetFileName(p))))
                {
                    File.Move(file, Path.Combine(previous, Path.GetFileName(file)));
                    retired.Add(file);
                }
                foreach (string file in fresh)
                {
                    string target = Path.Combine(destination, Path.GetFileName(file));
                    File.Move(file, target);
                    installed.Add(target);
                }
            }
            catch
            {
                foreach (string file in installed) File.Delete(file);
                foreach (string file in retired) File.Move(Path.Combine(previous, Path.GetFileName(file)), file);
                throw;
            }
            foreach (string file in retired) File.Delete(Path.Combine(previous, Path.GetFileName(file)));
        }

        static void Render(Scene scene, string staging, string commit, string onlyRegion = null)
        {
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>())
                .Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            bool IsRange(Renderer renderer) => renderer.transform.root.name == "V3 Distant Terrain";
            bool InsideCave(Renderer renderer)
            {
                var section = renderer.GetComponentInParent<FortressSectionV3>();
                if (!section || (section.part != FortressPartV3.UndergroundAccess && section.part != FortressPartV3.UndergroundCavern))
                    return false;
                string path = AnimationUtility.CalculateTransformPath(renderer.transform, section.transform);
                return !path.Contains("/CaveCeiling/") && !path.Contains("/BedrockRoof/") && renderer.name != "Foundation";
            }

            var bounds = new Dictionary<string, Renderer[]>
            {
                ["World"] = all.Where(r => !IsRange(r)).ToArray(),
                ["Mountain"] = all.Where(r => r.GetComponentInParent<MountainSectionV3>()).ToArray(),
                ["Bridge"] = all.Where(r => r.GetComponentInParent<BridgePlankV3>() || r.name == "Bridge_StartStub_Only").ToArray(),
                ["Canyon"] = all.Where(r => r.GetComponentInParent<ValleySectionV3>()?.part == ValleyPartV3.CanyonDescent).ToArray(),
                ["River"] = all.Where(r => r.GetComponentInParent<ValleySectionV3>()?.part == ValleyPartV3.River &&
                    (r.name != "Terrain")).ToArray(),
                ["Castle"] = all.Where(r => r.GetComponentInParent<FortressSectionV3>() is { } section &&
                    (section.part == FortressPartV3.CastleExterior || section.part == FortressPartV3.CastleInterior)).ToArray(),
                ["Underground"] = all.Where(InsideCave).ToArray()
            };
            var camera = new GameObject("Map capture camera").AddComponent<Camera>();
            var target = new RenderTexture(Width, Height, 24);
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var before = RenderTexture.active;
            bool fog = RenderSettings.fog;
            var hidden = new List<Renderer>();
            try
            {
                RenderSettings.fog = false;
                camera.enabled = false;
                camera.targetTexture = target;
                camera.aspect = (float)Width / Height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 20000;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.08f, .11f, .14f);
                camera.fieldOfView = 42;
                foreach (string region in Regions)
                {
                    if (onlyRegion != null && onlyRegion != region) continue;
                    var contents = bounds[region];
                    if (contents.Length == 0) throw new InvalidOperationException("Missing capture geometry: " + region);
                    foreach (var renderer in hidden) renderer.forceRenderingOff = false;
                    hidden.Clear();
                    var visible = region is "Underground" or "Mountain" ? new HashSet<Renderer>(contents) : null;
                    foreach (var renderer in all)
                    {
                        if (renderer.forceRenderingOff || !(IsRange(renderer) || visible != null && !visible.Contains(renderer))) continue;
                        renderer.forceRenderingOff = true;
                        hidden.Add(renderer);
                    }
                    var box = contents[0].bounds;
                    foreach (var r in contents) box.Encapsulate(r.bounds);
                    foreach (bool top in new[] { true, false })
                    {
                        camera.orthographic = top;
                        camera.transform.rotation = LevelViewCamera.Rotation(top ? LevelViewMode.Top : LevelViewMode.Perspective, false);
                        float distance = Frame(camera, box);
                        if (top)
                        {
                            // A narrow target such as water can sit below adjacent canyon walls.
                            float ceiling = all.Where(renderer => !renderer.forceRenderingOff)
                                .Max(renderer => renderer.bounds.max.y);
                            distance = Mathf.Max(distance, ceiling - box.center.y + 5);
                        }
                        camera.transform.position = box.center - camera.transform.forward * distance;
                        camera.farClipPlane = distance + box.extents.magnitude * 1.25f + 10;
                        camera.Render();
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                        image.Apply();
                        var pixels = image.GetPixels32();
                        if (pixels.Count(pixel => !pixel.Equals(pixels[0])) < Width * Height / 200)
                            throw new IOException("Blank capture: " + region);
                        var png = image.EncodeToPNG();
                        if (png == null || png.Length < 256) throw new IOException("Invalid PNG: " + region);
                        string path = Path.Combine(staging, $"{region}_{(top ? "top" : "perspective")}_{Version}_{commit}.png");
                        File.WriteAllBytes(path, png);
                        if (!image.LoadImage(File.ReadAllBytes(path)) || image.width != Width || image.height != Height)
                            throw new IOException("Cannot decode the saved PNG: " + region);
                    }
                }
            }
            finally
            {
                foreach (var renderer in hidden) renderer.forceRenderingOff = false;
                RenderSettings.fog = fog;
                RenderTexture.active = before;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }

        public static void CompareUndergroundWall()
        {
            string output = Path.GetFullPath("Docs/Development/Level/V3/MaterialComparisons");
            Directory.CreateDirectory(output);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            Material masonry = null;
            try
            {
                var scene = EditorSceneManager.OpenScene(TerrainPath, OpenSceneMode.Single);
                var rock = V3WorldMaterials.UndergroundStone;
                var walls = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>())
                    .Where(renderer => renderer.sharedMaterial == rock).ToArray();
                if (walls.Length == 0) throw new InvalidOperationException("Missing saved underground stone.");
                Render(scene, output, "rock", "Underground");
                masonry = new Material(V3WorldMaterials.Wall);
                masonry.SetColor("_BaseColor", rock.GetColor("_BaseColor"));
                masonry.SetFloat("_BumpScale", rock.GetFloat("_BumpScale"));
                masonry.SetTextureScale("_BaseMap", rock.GetTextureScale("_BaseMap"));
                foreach (var wall in walls) wall.sharedMaterial = masonry;
                try { Render(scene, output, "masonry", "Underground"); }
                finally { foreach (var wall in walls) wall.sharedMaterial = rock; }
            }
            finally
            {
                if (masonry != null) UnityEngine.Object.DestroyImmediate(masonry);
                if (setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        public static void CompareHorizon()
        {
            string output = Path.GetFullPath("Docs/Development/Level/V3/HorizonComparison");
            Directory.CreateDirectory(output);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var active = RenderTexture.active;
            RenderTexture target = null;
            Texture2D image = null;
            Camera camera = null;
            try
            {
                EditorSceneManager.OpenScene(TwinEdge.Gameplay.Editor.GameplayFoundationSetup.ScenePath, OpenSceneMode.Single);
                var level = EditorSceneManager.OpenScene(TerrainPath, OpenSceneMode.Additive);
                camera = UnityEngine.Object.FindFirstObjectByType<TwinEdge.Gameplay.V3GameplayBootstrap>().standbyCamera;
                target = new RenderTexture(1024, 720, 24);
                image = new Texture2D(1024, 720, TextureFormat.RGB24, false);
                var range = level.GetRootGameObjects().Single(root => root.name == "V3 Distant Terrain");
                var terrain = ValleyGeneratorV3.Config();
                var markers = level.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelMarker>())
                    .ToDictionary(marker => marker.id, marker => marker.transform.position);
                var planks = UnityEngine.Object.FindObjectsByType<BridgePlankV3>(FindObjectsSortMode.None)
                    .OrderBy(plank => plank.plankId).ToArray();
                Physics.SyncTransforms();
                var regions = new (string name, Vector3 feet, float yaw)[]
                {
                    ("Mountain", markers["Mountain.V3.Start"], 20),
                    ("Summit", markers["Mountain.V3.Final"], 12),
                    ("Bridge", planks[planks.Length / 2].socket, 40),
                    ("Canyon", terrain.descent[3], -120),
                    ("River", markers["Valley.V3.LowerCheckpoint"], 178),
                    ("Castle", terrain.castleEntry, -78),
                    ("UndergroundAccess", FortressGeneratorV3.Config().origin + FortressGeneratorV3.Config().start, 90)
                };
                camera.targetTexture = target;
                var comparison = new List<string>
                {
                    "Unity Editor, paired 1024x720 camera render + GPU readback ms, 3 warmups and 9 samples.",
                    "Frustum-intersecting range renderers and triangles are potential submission, not measured GPU draws."
                };
                foreach (var region in regions)
                {
                    // Use the gameplay camera and its ordinary orbit height at fixed, supported viewpoints.
                    Vector3 look = TwinEdge.Gameplay.V3GameplayBootstrap.SafeGround(region.feet) + Vector3.up * 1.65f;
                    camera.transform.rotation = Quaternion.Euler(15, region.yaw, 0);
                    Vector3 back = -camera.transform.forward;
                    float distance = Physics.SphereCast(look, .22f, back, out var hit, 5, ~0, QueryTriggerInteraction.Ignore)
                        ? Mathf.Max(.15f, hit.distance - .1f) : 5;
                    camera.transform.position = look + back * distance;
                    foreach (bool revised in new[] { false, true })
                    {
                        range.SetActive(revised);
                        RenderSettings.fog = revised;
                        camera.farClipPlane = revised ? 2600 : 4000;
                        var times = new double[9];
                        for (int sample = -3; sample < times.Length; sample++)
                        {
                            long start = Stopwatch.GetTimestamp();
                            camera.Render();
                            RenderTexture.active = target;
                            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                            if (sample >= 0) times[sample] = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                        }
                        Array.Sort(times);
                        var planes = GeometryUtility.CalculateFrustumPlanes(camera);
                        var rangeMeshes = range.GetComponentsInChildren<MeshFilter>()
                            .Where(filter => filter.gameObject.activeInHierarchy &&
                                GeometryUtility.TestPlanesAABB(planes, filter.GetComponent<Renderer>().bounds)).ToArray();
                        long triangles = rangeMeshes.Sum(filter => (long)filter.sharedMesh.GetIndexCount(0) / 3);
                        string variant = revised ? "revised" : "original-settings";
                        comparison.Add(FormattableString.Invariant($"{region.name} {variant}: median={times[4]:F2}ms, min={times[0]:F2}ms, max={times[^1]:F2}ms, range meshes={rangeMeshes.Length}, triangles={triangles}"));
                        image.Apply();
                        File.WriteAllBytes(Path.Combine(output, $"{region.name}_{variant}.png"), image.EncodeToPNG());
                    }
                }
                File.WriteAllLines(Path.Combine(output, "CameraRenderCost.txt"), comparison);
            }
            finally
            {
                if (camera) camera.targetTexture = null;
                RenderTexture.active = active ? active : null;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
                if (setup.Length > 0 && setup.All(scene => !string.IsNullOrEmpty(scene.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        static float Frame(Camera camera, Bounds box)
        {
            var local = Quaternion.Inverse(camera.transform.rotation);
            float size = 0, distance = 0;
            float tan = Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            foreach (int x in new[] { -1, 1 })
                foreach (int y in new[] { -1, 1 })
                    foreach (int z in new[] { -1, 1 })
                    {
                        var point = local * Vector3.Scale(box.extents, new Vector3(x, y, z));
                        float extent = Mathf.Max(Mathf.Abs(point.y), Mathf.Abs(point.x) / camera.aspect) * 1.1f;
                        size = Mathf.Max(size, extent);
                        distance = Mathf.Max(distance, (camera.orthographic ? 0 : extent / tan) - point.z);
                    }
            camera.orthographicSize = size;
            return distance + 2;
        }
    }
}
