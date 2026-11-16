using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwinEdge.Levels.Editor
{
    public static class ValleyAssetLifecycleV3
    {
        const string Generated = "Assets/Game/Level/Data/Generated";
        const string Notes = "Docs/Development/Level/V3/";
        // Batch entry only: a fresh editor has no previous interactive Undo references.
        // Invoke after saving and normally closing the interactive editor.
        public static void CleanFreshSession()
        {
            if (!Application.isBatchMode || Undo.GetCurrentGroup() != 0)
                throw new InvalidOperationException("Cleanup requires a fresh batch session after normal editor shutdown.");
            Directory.CreateDirectory(Notes);
            var paths = AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(p)).ToArray();
            var roots = paths.Where(p=>!p.StartsWith(Generated+"/")).ToArray();
            var retained = new HashSet<string>(AssetDatabase.GetDependencies(roots,true));
            // Generation directory strings are ownership metadata, not GUID dependencies.
            var text = string.Join("\n", roots.Where(p=>new[]{".cs",".json",".asset",".unity",".prefab"}.Contains(Path.GetExtension(p))).Select(File.ReadAllText));
            var before = Directory.GetFiles(Generated,"*",SearchOption.AllDirectories);
            var log = new List<string>{"Fresh-session dependency cleanup", "Before files="+before.Length+" bytes="+before.Sum(p=>new FileInfo(p).Length)};
            // Only complete known generator-owned directories; preserve any ambiguous file.
            foreach (var folder in Directory.GetDirectories(Generated).Select(p=>p.Replace('\\','/')))
            {
                string name=Path.GetFileName(folder);
                if (!(name.StartsWith("ValleyV3_")||name.StartsWith("Run_")||name.StartsWith("MountainV3_"))) continue;
                var files=Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).ToArray();
                if (files.Any(p=>!new[]{".asset",".mat",".meta"}.Contains(Path.GetExtension(p))) || files.Any(retained.Contains) || text.Contains(folder))
                { log.Add("KEEP "+folder); continue; }
                // Delete only committed old results, or this tool's explicitly recorded candidates.
                var tracked=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", "ls-files -- \""+folder+"\""){RedirectStandardOutput=true,UseShellExecute=false,CreateNoWindow=true});
                var trackedPaths=new HashSet<string>(tracked.StandardOutput.ReadToEnd().Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)); tracked.WaitForExit();
                bool own=File.Exists(Notes+"RefinementGenerations.txt")&&File.ReadAllLines(Notes+"RefinementGenerations.txt").Contains(folder);
                var changed=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", "diff HEAD --name-only -- \""+folder+"\""){RedirectStandardOutput=true,UseShellExecute=false,CreateNoWindow=true});
                string changes=changed.StandardOutput.ReadToEnd();changed.WaitForExit();
                if (tracked.ExitCode!=0 || changed.ExitCode!=0 || !string.IsNullOrWhiteSpace(changes)) { log.Add("KEEP modified or Git error "+folder); continue; }
                if (own) {
                    var hashes=File.Exists(Notes+"RefinementGenerationHashes.txt")?new HashSet<string>(File.ReadAllLines(Notes+"RefinementGenerationHashes.txt")):new HashSet<string>();
                    foreach(var file in files) using(var sha=System.Security.Cryptography.SHA256.Create())
                        if (!hashes.Contains(file+"|"+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))))) own=false;
                }
                if (!own && files.Any(p=>!trackedPaths.Contains(p))) { log.Add("AMBIGUOUS local-only "+folder); continue; }
                log.Add("DELETE candidate "+folder+" files="+files.Length+" bytes="+files.Sum(p=>new FileInfo(p).Length));
                File.WriteAllLines(Notes+"RefinementCleanup.txt",log);
                if (!AssetDatabase.DeleteAsset(folder)) throw new IOException("Delete failed: "+folder);
                log.Add("DELETED "+folder);
                File.WriteAllLines(Notes+"RefinementCleanup.txt",log);
            }
            AssetDatabase.Refresh();
            var after=Directory.GetFiles(Generated,"*",SearchOption.AllDirectories);
            log.Add("After files="+after.Length+" bytes="+after.Sum(p=>new FileInfo(p).Length));
            log.Add("Retained generated dependencies="+retained.Count(p=>p.StartsWith(Generated+"/")));
            File.WriteAllLines(Notes+"RefinementCleanup.txt",log);
            ProjectDependencyAudit.Run();
        }
    }
}
