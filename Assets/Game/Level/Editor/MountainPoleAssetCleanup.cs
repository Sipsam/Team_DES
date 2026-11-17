using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace TwinEdge.Levels.Editor {
 public static class MountainPoleAssetCleanup {
  // Explicit fresh-process entry; never deletes tracked generations or another task's files.
  public static void Run(){
   if(!Application.isBatchMode||Undo.GetCurrentGroup()!=0)throw new InvalidOperationException("Fresh batch session required");
   string output=MountainPoleReachability.Output;Directory.CreateDirectory(output);
   var roots=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(p)&&!p.StartsWith("Assets/Game/Level/Data/Generated/")).ToArray();
   var references=new HashSet<string>(AssetDatabase.GetDependencies(roots,true));var log=new List<string>();
   foreach(var folder in File.ReadAllLines(output+"/TaskGenerations.txt")){
    if(!folder.StartsWith("Assets/Game/Level/Data/Generated/MountainV3_")||folder.Contains(".."))throw new Exception("Unexpected cleanup scope");
    string absolute=Path.GetFullPath(folder),allowed=Path.GetFullPath("Assets/Game/Level/Data/Generated");
    if(!string.Equals(Path.GetDirectoryName(absolute),allowed,StringComparison.OrdinalIgnoreCase)||!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(absolute),"^MountainV3_[0-9a-f]{32}$"))throw new Exception("Resolved cleanup path outside owned generation root");
    if(!Directory.Exists(folder))continue;
    var files=Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Select(f=>f.Replace('\\','/')).ToArray();long bytes=files.Sum(f=>new FileInfo(f).Length);
    var info=new System.Diagnostics.ProcessStartInfo("git","ls-files -- \""+folder+"\""){RedirectStandardOutput=true,UseShellExecute=false,CreateNoWindow=true};var p=System.Diagnostics.Process.Start(info);string tracked=p.StandardOutput.ReadToEnd();p.WaitForExit();
    int count=files.Count(references.Contains);
    bool ambiguous=files.Any(f=>!new[]{".asset",".mat",".meta"}.Contains(Path.GetExtension(f)));
    bool ownership=roots.Where(f=>new[]{".unity",".prefab",".asset",".cs"}.Contains(Path.GetExtension(f))).Any(f=>File.ReadAllText(f).Contains(folder));
    if(p.ExitCode!=0||tracked.Length>0||count>0||ambiguous||ownership){log.Add($"KEEP {folder} bytes={bytes} referencedFiles={count} tracked={tracked.Length>0} ambiguous={ambiguous} ownershipMetadata={ownership}");continue;}
    log.Add($"DELETE task-owned unreferenced generation {folder} bytes={bytes} referencedFiles=0");
    if(!AssetDatabase.DeleteAsset(folder))throw new IOException("Cleanup failed "+folder);
   }
   AssetDatabase.SaveAssets();File.WriteAllLines(output+"/AssetCleanup.txt",log);
   MountainPoleReachability.Load();
   foreach(var f in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))if(!f.sharedMesh)throw new Exception("Missing saved mesh: "+f.name);
   foreach(var c in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))if(!c.sharedMesh)throw new Exception("Missing saved collider: "+c.name);
   var structural=MountainSectionV3Inspector.Inspect(UnityEngine.Object.FindFirstObjectByType<MountainSectionV3>());File.WriteAllLines(output+"/Structural.txt",structural);
   if(structural.Any(line=>line.StartsWith("FAIL")))throw new Exception("Mountain structural verification failed; see Structural.txt");
   MountainPoleReachability.VerifyCapturedLanding();
   MountainPoleContinuousPlan.Run();MountainPoleContinuousPlan.VerifyAlternative();
   bool dirtyBefore=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty;
   int objectsBefore=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
   MountainPoleReachability.Analyze("FinalAudit");
   if(objectsBefore!=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length||dirtyBefore!=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("Analysis changed scene objects or dirty state");
   File.WriteAllText(output+"/FinalAudit.txt","PASS compile, fresh saved scene/config/collider references, structural checks, captured-contact regression, primary and alternative continuous plans, read-only analysis object count / dirty state. Network results are separate.");
   File.AppendAllText(output+"/AssetCleanup.txt","\nPASS fresh reload: all active saved mesh/collider references present.\n");
  }
 }
}
