using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace TwinEdge.Levels.Editor {
 public static class MountainPoleTuning {
  public static void InspectCurrent() {MountainPoleReachability.Load();File.WriteAllLines(MountainPoleReachability.Output+"/Structural.txt",MountainSectionV3Inspector.Inspect(Object.FindFirstObjectByType<MountainSectionV3>()));}
  public static void AnalyzePlanAndBuild(){VerifyCurrent();TwinEdge.Gameplay.Editor.GameplayFoundationSetup.BuildPlayer();}
  public static void VerifyCapturedAndBuild(){MountainPoleReachability.VerifyCapturedLanding();BuildValidatedPlayer();}
  public static void BuildValidatedPlayer() {MountainPoleContinuousPlan.Run();TwinEdge.Gameplay.Editor.GameplayFoundationSetup.BuildPlayer();}
  public static void TunePlanAndBuild(){TuneAndPlan();InspectCurrent();MountainPoleContinuousPlan.VerifyAlternative();TwinEdge.Gameplay.Editor.GameplayFoundationSetup.BuildPlayer();}
  public static void TuneAndPlan() {Run();MountainPoleContinuousPlan.Run();}
  public static void Run() {
   Directory.CreateDirectory(MountainPoleReachability.Output);
   MountainPoleReachability.Load();
   var owner=Object.FindFirstObjectByType<MountainSectionV3>();
   var protectedRoots=owner.gameObject.scene.GetRootGameObjects().Where(g=>g!=owner.transform.root.gameObject).Select(g=>EditorJsonUtility.ToJson(g)).ToArray();
   var manual=owner.transform.Find("Manual");var before=manual.GetComponentsInChildren<Transform>(true).Select(t=>t.GetInstanceID()).ToArray();
   string old=owner.generationAssets;
   MountainGeneratorV3.Generate(owner);string current=owner.generationAssets;
   Undo.PerformUndo();bool undo=owner.generationAssets==old && owner.generated;
   Undo.PerformRedo();bool redo=owner.generationAssets==current && owner.generated;
   bool preserved=before.SequenceEqual(manual.GetComponentsInChildren<Transform>(true).Select(t=>t.GetInstanceID()));
   if(!undo||!redo||!preserved)throw new Exception("Generation Undo/Redo/Manual failure");
   EditorSceneManager.SaveScene(owner.gameObject.scene);
   MountainPoleReachability.Load(); owner=Object.FindFirstObjectByType<MountainSectionV3>();
   File.WriteAllLines(MountainPoleReachability.Output+"/Generation.txt",new[]{"PASS mountain-only regeneration, Undo, Redo, Manual identity preservation, save and reload", "Previous generation retained for references / Undo: "+old,"Current generation: "+owner.generationAssets});
   Physics.SyncTransforms();MountainPoleReachability.Analyze("Tuned");Capture(owner);
  }
  public static void VerifyCurrent() {
   MountainPoleReachability.Load();MountainPoleReachability.Analyze("Tuned");
   Capture(Object.FindFirstObjectByType<MountainSectionV3>());
   MountainPoleContinuousPlan.Run();
  }
  public static void Capture(MountainSectionV3 owner) {
   var go=new GameObject("TemporaryMountainPoleCamera");var cam=go.AddComponent<Camera>();cam.farClipPlane=4000;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.1f,.14f,.18f);
   var rt=new RenderTexture(1400,1050,24);cam.targetTexture=rt;
   try {for(int focus=0;focus<5;focus++)foreach(var mode in new[]{LevelViewMode.Top,LevelViewMode.Perspective}) {
    var b=MountainSectionV3Inspector.FocusBounds(owner,focus);cam.transform.rotation=MountainSectionV3Inspector.ReviewRotation(focus,mode);cam.orthographic=mode==LevelViewMode.Top;
    cam.orthographicSize=Mathf.Max(b.extents.x,b.extents.z)*1.1f;cam.transform.position=b.center-cam.transform.forward*b.extents.magnitude/Mathf.Sin(cam.fieldOfView*.5f*Mathf.Deg2Rad)*1.2f;
    cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1400,1050,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1400,1050),0,0);tex.Apply();File.WriteAllBytes(MountainPoleReachability.Output+"/View_"+focus+"_"+mode+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);
   }}finally {RenderTexture.active=null;cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
  }
 }
}
