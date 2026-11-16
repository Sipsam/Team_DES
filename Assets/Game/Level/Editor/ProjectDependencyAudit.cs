using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Levels.Editor
{
    // Explicit read-only inspection. Does not generate, save, move, or delete assets.
    [InitializeOnLoad]
    public static class ProjectDependencyAudit
    {
        private const string Request = "Temp/ProjectDependencyAudit.request";
        private const string Output = "Docs/Development/Architecture/V1/Audit";
        private const string Generated = "Assets/Game/Level/Data/Generated/";

        static ProjectDependencyAudit() { EditorApplication.update += Poll; }
        private static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Run();
        }

        [MenuItem("Tools/Twin Edge/Audit project dependencies (read only)")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            var report = new List<string> { "Unity " + Application.unityVersion, DateTime.UtcNow.ToString("O"),
                "Compilation: this Editor assembly loaded and executed after script import.",
                "Unreferenced means review candidate only; dynamic loading and Undo may retain assets." };
            var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var dirty = loaded.Select(s => s.isDirty).ToArray();
            try
            {
                var paths = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !AssetDatabase.IsValidFolder(p)).OrderBy(p => p).ToArray();
                var generated = paths.Where(p => p.StartsWith(Generated, StringComparison.Ordinal)).ToArray();
                // Every retained non-generated asset is a root, including old draft scenes.
                var roots = paths.Except(generated).ToArray();
                var retained = new HashSet<string>(AssetDatabase.GetDependencies(roots, true));
                var reverse = generated.ToDictionary(p => p, p => new List<string>());
                foreach (var path in paths)
                    foreach (var dependency in AssetDatabase.GetDependencies(path, false))
                        if (dependency != path && reverse.TryGetValue(dependency, out var users)) users.Add(path);
                var rows = new List<string> { "path,guid,bytes,retained_dependency,direct_users" };
                foreach (var path in generated)
                    rows.Add(string.Join(",", Quote(path), AssetDatabase.AssetPathToGUID(path), new FileInfo(path).Length.ToString(), retained.Contains(path).ToString(), Quote(string.Join(";", reverse[path]))));
                File.WriteAllLines(Output + "/UnityGeneratedDependencies.csv", rows);
                report.Add($"Generated: {generated.Length}; retained by non-generated roots: {generated.Count(retained.Contains)}; review candidates: {generated.Count(p => !retained.Contains(p))}");
                var unresolved = new List<string> { "source,guid" };
                foreach (var path in paths.Where(p => new[] { ".unity", ".prefab", ".asset", ".mat" }.Contains(Path.GetExtension(p))))
                {
                    // Only text serialization can be checked this way; imported binary dependencies
                    // are covered by GetDependencies above, not this GUID text check.
                    using (var reader = new StreamReader(path))
                        if (reader.ReadLine() != "%YAML 1.1") continue;
                    foreach (var guid in Regex.Matches(File.ReadAllText(path), @"guid:\s*([0-9a-fA-F]{32})").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
                    {
                        if (guid == new string('0', 32) || guid == "0000000000000000e000000000000000" || guid == "0000000000000000f000000000000000") continue;
                        if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))) unresolved.Add(Quote(path) + "," + guid);
                    }
                }
                File.WriteAllLines(Output + "/UnityUnresolvedGuids.csv", unresolved);
                report.Add($"Serialized unresolved GUID references: {unresolved.Count - 1}; see UnityUnresolvedGuids.csv (package GUIDs resolved by AssetDatabase).");
                foreach (var scenePath in paths.Where(p => p.EndsWith(".unity", StringComparison.Ordinal)))
                {
                    var before = Hash(scenePath);
                    var scene = EditorSceneManager.OpenPreviewScene(scenePath);
                    try
                    {
                        var objects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
                        int missing = objects.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                        report.Add($"SCENE {scenePath}: missing scripts={missing}; generated dependencies={AssetDatabase.GetDependencies(scenePath, true).Count(p => p.StartsWith(Generated, StringComparison.Ordinal))}");
                        foreach (var section in objects.Select(t => t.GetComponent<LevelSection>()).Where(s => s != null))
                        {
                            var fortress=section.GetComponent<FortressSectionV3>();
                            var mountain=section.GetComponent<MountainSectionV3>();
                            var valley=section.GetComponent<ValleySectionV3>();
                            UnityEngine.Object config=fortress?fortress.config:mountain?mountain.config:valley?valley.config:(UnityEngine.Object)section.config;
                            report.Add($"SECTION {section.section}: config={AssetDatabase.GetAssetPath(config)}; Generated={section.transform.Find("Generated") != null}; Manual={section.transform.Find("Manual") != null}; markers={section.GetComponentsInChildren<LevelMarker>(true).Length}");
                            if(!config)report.Add("CONFIG ERROR: Missing config");
                            else if(!fortress&&!mountain&&!valley)foreach (var error in LevelValidation.ConfigErrors(section.config)) report.Add("CONFIG ERROR: " + error);
                        }
                        report.Add((scene.isDirty ? "FAIL" : "PASS") + ": preview remains clean " + scenePath);
                    }
                    finally { EditorSceneManager.ClosePreviewScene(scene); }
                    report.Add((before == Hash(scenePath) ? "PASS" : "FAIL") + ": scene SHA256 preserved " + scenePath);
                }
                foreach (var scene in EditorBuildSettings.scenes)
                    report.Add($"BUILD enabled={scene.enabled}; exists={File.Exists(scene.path)}; path={scene.path}");
            }
            catch (Exception ex) { report.Add("FAIL: " + ex); Debug.LogException(ex); }
            finally
            {
                for (int i = 0; i < loaded.Length; i++)
                    report.Add((loaded[i].isLoaded && loaded[i].isDirty == dirty[i] ? "PASS" : "FAIL") + ": loaded scene dirty state preserved " + loaded[i].path + " dirty=" + dirty[i]);
                File.WriteAllLines(Output + "/UnityValidation.txt", report);
            }
        }
        private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        private static string Hash(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }
    }
}
