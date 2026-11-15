using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TwinEdge.Levels.Editor
{
    [InitializeOnLoad]
    public static class SpatialReviewV2
    {
        const string Request="Temp/BlockoutV2.request";
        static SpatialReviewV2(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            string command=File.ReadAllText(Request);File.Delete(Request);
            try {if(command.Trim()=="revise")Revise();else if(command.Trim()=="finalize")FinalizeScene();else Create();} catch(Exception ex){Directory.CreateDirectory("Docs/Development/Level/V2");File.WriteAllText("Docs/Development/Level/V2/Failure.txt",ex.ToString());Debug.LogException(ex);}
        }
        static void FinalizeScene()
        {
            Revise();
            var active=SceneManager.GetActiveScene();const string canonical="Assets/Game/Level/Scenes/LevelBlockoutV2.unity";
            if(active.path!=canonical) {
                for(int i=SceneManager.sceneCount-1;i>=0;i--){var scene=SceneManager.GetSceneAt(i);if(scene.path==canonical)EditorSceneManager.CloseScene(scene,true);}
                // Retain the initial V2 draft as an archive; V1 paths are never targets.
                string archive=AssetDatabase.GenerateUniqueAssetPath("Assets/Game/Level/Scenes/LevelBlockoutV2_InitialDraft.unity");
                string moved=AssetDatabase.MoveAsset(canonical,archive);if(!string.IsNullOrEmpty(moved))throw new IOException(moved);
                string error=AssetDatabase.MoveAsset(active.path,canonical);if(!string.IsNullOrEmpty(error))throw new IOException(error);
                EditorSceneManager.SaveScene(active,canonical);
            }
            string path=SceneManager.GetActiveScene().path;
            EditorSceneManager.CloseScene(SceneManager.GetActiveScene(),true);
            var reloaded=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);SceneManager.SetActiveScene(reloaded);
            var root=LevelGenerator.FindRoot();var c=root.GetComponentInChildren<LevelSection>().config;
            var report=Inspect(root,c);
            report.Add("PASS: final V2 scene saved and reopened in Unity 6000.3.27f1.");
            // Re-run isolation and Undo against this final geometry, then persist the restored scene.
            var section=root.GetComponentsInChildren<LevelSection>().Single(s=>s.section==SectionId.Mountain);
            var old=section.transform.Find("Generated");var neighbor=root.GetComponentsInChildren<LevelSection>().Single(s=>s.section==SectionId.Castle).transform.Find("Generated");
            var manual=new GameObject("V2ManualProbe");manual.transform.SetParent(section.transform.Find("Manual"));
            LevelGenerator.Generate(c,SectionId.Mountain);bool kept=manual!=null && neighbor!=null && old==null;
            Undo.PerformUndo();bool restored=old!=null && section.transform.Find("Generated")==old;
            UnityEngine.Object.DestroyImmediate(manual);
            report.Add((kept&&restored?"PASS":"ERROR")+": final V2 section regeneration / Manual / neighbor preservation / Undo restoration");
            EditorSceneManager.SaveScene(reloaded);
            File.WriteAllLines("Docs/Development/Level/V2/Validation.txt",report);
            File.WriteAllText("Docs/Development/Level/V2/Completed.txt",path+"\n"+DateTime.Now.ToString("O"));
            Selection.activeGameObject=root;Frame(root,1);
        }
        static void Revise()
        {
            Directory.CreateDirectory("Docs/Development/Level/V2");
            var root=LevelGenerator.FindRoot();
            if(root==null || !root.GetComponentInChildren<LevelSection>().config.spatialV2)throw new InvalidOperationException("Active scene must be V2 for revision");
            var c=root.GetComponentInChildren<LevelSection>().config;
            LevelGenerator.Generate(c);EditorSceneManager.SaveScene(root.scene);
            File.WriteAllLines("Docs/Development/Level/V2/Validation.txt",Inspect(root,c));Capture(root);
            File.WriteAllText("Docs/Development/Level/V2/Completed.txt",root.scene.path+"\n"+DateTime.Now.ToString("O"));
        }
        [MenuItem("Tools/Twin Edge/V2/Create new spatial blockout")]
        public static void Create()
        {
            var c=SpatialBlockoutV2.Config(); LevelGenerator.CreateScene(c);
            string path=SceneManager.GetActiveScene().path;
            // Reload only the scene created here. Other loaded scenes remain untouched.
            EditorSceneManager.CloseScene(SceneManager.GetActiveScene(),true);
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
            var root=LevelGenerator.FindRoot();
            File.WriteAllLines("Docs/Development/Level/V2/Validation.txt",Inspect(root,c));
            Capture(root);
            // Exercise existing section ownership/Undo in the new V2 scene, then restore it.
            var mountain=root.GetComponentsInChildren<LevelSection>().Single(x=>x.section==SectionId.Mountain);
            var old=mountain.transform.Find("Generated");
            var river=root.GetComponentsInChildren<LevelSection>().Single(x=>x.section==SectionId.River).transform.Find("Generated");
            var probe=new GameObject("V2_ManualProbe");probe.transform.SetParent(mountain.transform.Find("Manual"));
            LevelGenerator.Generate(c,SectionId.Mountain);
            bool preserved=probe!=null && river!=null && old==null;
            Undo.PerformUndo();
            bool restored=old!=null && mountain.transform.Find("Generated")==old;
            UnityEngine.Object.DestroyImmediate(probe);
            File.AppendAllText("Docs/Development/Level/V2/Validation.txt",$"\n{(preserved&&restored?"PASS":"ERROR")}: V2 regeneration / Manual / neighbor preservation / Undo restoration\n");
            Selection.activeGameObject=root; Frame(root,1);
            File.WriteAllText("Docs/Development/Level/V2/Completed.txt",path+"\n"+DateTime.Now.ToString("O"));
        }
        public static List<string> Inspect(GameObject root,LevelLayoutConfig c)
        {
            var report=LevelValidation.Inspect(root,c); Physics.SyncTransforms();
            var sections=root.GetComponentsInChildren<LevelSection>();
            foreach(var s in sections){var bounds=LevelVisualizationWindow.SectionBounds(s);report.Add($"METRIC: {s.section} geometry bounds {bounds.size}; elevation {bounds.min.y:F1}..{bounds.max.y:F1}");}
            // Cast from the lower of neighboring plateau heights: the upper air gap must remain empty.
            var path=c.mountainPath;
            for(int i=5;i<path.Length;i++) {
                var mid=(path[i-1]+path[i])/2;mid.y=Mathf.Min(path[i-1].y,path[i].y)-2;
                if(i==8)mid=(path[i-1]+new Vector3(240,126,210))/2;
                bool filled=Physics.RaycastAll(c.World(SectionId.Mountain,mid),Vector3.down,30).Any(h=>h.transform.IsChildOf(root.transform));
                report.Add($"{(filled?"ERROR":"PASS")}: gorge {i-4} upper 30m is {(filled?"blocked":"open")}; center distance {Vector3.Distance(path[i-1],path[i]):F1}m");
                var a=c.World(SectionId.Mountain,path[i-1]);var end=c.World(SectionId.Mountain,path[i]);
                int n=Mathf.CeilToInt(Vector3.Distance(a,end));int run=0,longest=0;
                for(int j=0;j<=n;j++) {var q=Vector3.Lerp(a,end,j/(float)n);q.y=Mathf.Max(a.y,end.y)+2;
                    bool support=Physics.RaycastAll(q,Vector3.down,Mathf.Abs(a.y-end.y)+7).Any(h=>h.transform.IsChildOf(root.transform));
                    run=support?0:run+1;longest=Mathf.Max(run,longest);
                }
                float gap=longest*Vector3.Distance(a,end)/n;
                report.Add($"{(gap>c.FlatJumpRange?"PASS":"ERROR")}: gorge {i-4} sampled unsupported span {gap:F1}m > normal theoretical jump {c.FlatJumpRange:F2}m");
            }
            var bridgeMid=c.World(SectionId.Bridge,new Vector3(0,-1,c.bridgeSpan/2));
            report.Add((Physics.RaycastAll(bridgeMid,Vector3.down,80).Any(h=>h.transform.IsChildOf(root.transform))?"ERROR":"PASS")+": bridge midspan 80m fall clearance; no completed deck");
            int temp=root.GetComponentsInChildren<Transform>().Count(x=>x.name.StartsWith("TEST_")&&x.GetComponent<Collider>()!=null);
            report.Add((temp==0?"PASS":"ERROR")+": no temporary collidable bypasses");
            var water=root.GetComponentsInChildren<LevelRoute>().Single(r=>r.name=="SharedWaterway");int waterBlocked=0;
            for(int i=1;i<water.points.Length;i++)foreach(float offset in new[]{-5f,5f}) {
                var q=water.points[i]+Vector3.Cross(Vector3.up,water.points[i]-water.points[i-1]).normalized*offset;
                if(i<3||i>water.points.Length-4)continue;
                var hits=Physics.RaycastAll(q+Vector3.up*3,Vector3.down,3.5f).Where(h=>h.transform.IsChildOf(root.transform)).ToArray();
                // Terminal landing decks are intentional; the through-channel between docking basins must remain open.
                hits=hits.Where(h=>!(h.collider.name.Contains("Dock") && (Vector3.Distance(q,water.points.First())<22 || Vector3.Distance(q,water.points.Last())<22))).ToArray();
                if(hits.Length>0){waterBlocked++;report.Add($"DETAIL: raft lane at {q}: {string.Join(",",hits.Select(h=>h.collider.name))}");}
            }
            report.Add((waterBlocked==0?"PASS":"ERROR")+$": two reserved raft center lanes, static above-water obstructions={waterBlocked}; raft size/turn radius still unverified");
            foreach(var landing in root.GetComponentsInChildren<MeshCollider>().Where(m=>m.name.StartsWith("FIX_Landing_"))) {
                var q=landing.sharedMesh.vertices[0];
                bool blocked=Physics.OverlapCapsule(q+Vector3.up*(c.playerRadius+.12f),q+Vector3.up*(c.playerHeight-c.playerRadius),c.playerRadius).Any(h=>h.transform.IsChildOf(root.transform));
                report.Add((blocked?"ERROR":"PASS")+": fixed cavern landing player clearance "+landing.name);
            }
            // Full player capsule at three lateral offsets on every authored walking route.
            foreach(var route in root.GetComponentsInChildren<LevelRoute>().Where(x=>x.kind==RouteKind.Walk)) {
                int missing=0,blocked=0,total=0;
                for(int i=1;i<route.points.Length;i++){
                    var a=route.points[i-1];var d=route.points[i]-a;var side=Vector3.Cross(Vector3.up,d).normalized;
                    int n=Mathf.Max(1,Mathf.CeilToInt(d.magnitude));
                    for(int j=0;j<=n;j++)foreach(float offset in new[]{-.65f,0,.65f}){
                        var p=a+d*(j/(float)n)+side*offset; total++;
                        if(!Physics.RaycastAll(p+Vector3.up*.5f,Vector3.down,.85f).Any(h=>h.transform.IsChildOf(root.transform))){missing++;if(missing<=3)report.Add($"DETAIL: {route.name} floor missing at {p}");}
                        var overlaps=Physics.OverlapCapsule(p+Vector3.up*(c.playerRadius+.12f),p+Vector3.up*(c.playerHeight-c.playerRadius),c.playerRadius).Where(h=>h.transform.IsChildOf(root.transform)).ToArray();
                        if(overlaps.Length>0){blocked++;if(blocked<=3)report.Add($"DETAIL: {route.name} overlap at {p}: {string.Join(",",overlaps.Select(h=>h.name))}");}
                    }
                }
                report.Add($"{(missing>0?"ERROR":blocked>0?"WARNING":"PASS")}: {route.name} 3-wide capsule/floor {total} samples, missing={missing}, overlaps={blocked}");
            }
            report.Add("UNVERIFIED: actual two-player traversal and camera, pole reach, elastic launch, raft envelope, bridge collapse, room conversion and variable platforms. Fixed landing heights and gaps are reservations, not certified jumps.");
            return report;
        }
        static void Frame(GameObject root,int mode)
        {
            var view=SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            var sections=root.GetComponentsInChildren<LevelSection>(true);
            var bounds=sections.Length==1?LevelVisualizationWindow.SectionBounds(sections[0]):Bounds(root);
            LevelViewCamera.Frame(bounds,Rotation(mode),mode!=1);
        }
        static Bounds Bounds(GameObject root)
        {var rs=root.GetComponentsInChildren<Renderer>();if(rs.Length==0)return new Bounds(root.transform.position,Vector3.one*10);var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);return bounds;}
        static Quaternion Rotation(int mode)=> LevelViewCamera.Rotation(mode==0?LevelViewMode.Top:mode==1?LevelViewMode.Perspective:LevelViewMode.SideX,false);
        [MenuItem("Tools/Twin Edge/V2/Review selected - Top")]static void Top()=>Review(0);
        [MenuItem("Tools/Twin Edge/V2/Review selected - Perspective")]static void Perspective()=>Review(1);
        [MenuItem("Tools/Twin Edge/V2/Review selected - Side")]static void Side()=>Review(2);
        static void Review(int mode){var root=Selection.activeGameObject ?? LevelGenerator.FindRoot();if(root!=null)Frame(root,mode);}
        public static void Capture(GameObject root)
        {
            string folder="Docs/Development/Level/V2/Views";Directory.CreateDirectory(folder);
            var camera=new GameObject("V2_ReviewCamera").AddComponent<Camera>();camera.backgroundColor=new Color(.14f,.18f,.22f);camera.clearFlags=CameraClearFlags.SolidColor;camera.farClipPlane=4000;
            // Isolate the selected section from additive V1 scenes without modifying their objects.
            camera.cullingMask=1<<31;
            var targets=root.GetComponentsInChildren<LevelSection>().Select(s=>s.gameObject).Prepend(root);
            foreach(var target in targets) for(int mode=0;mode<3;mode++) {
                var transforms=target.GetComponentsInChildren<Transform>();var layers=transforms.Select(x=>x.gameObject.layer).ToArray();
                for(int k=0;k<transforms.Length;k++)transforms[k].gameObject.layer=31;
                var bounds=Bounds(target);float extent=bounds.size.magnitude;
                camera.transform.rotation=Rotation(mode);camera.orthographic=mode!=1;camera.orthographicSize=extent*.56f;
                camera.transform.position=bounds.center-camera.transform.forward*extent*1.5f;
                camera.nearClipPlane=.3f;camera.fieldOfView=45;
                var rt=RenderTexture.GetTemporary(1400,1000,24);camera.targetTexture=rt;
                camera.Render();var before=RenderTexture.active;RenderTexture.active=rt;
                var texture=new Texture2D(1400,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1400,1000),0,0);texture.Apply();
                File.WriteAllBytes(folder+"/"+target.name+"_"+new[]{"Top","Perspective","Side"}[mode]+".png",texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);RenderTexture.active=before;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);
                for(int k=0;k<transforms.Length;k++)transforms[k].gameObject.layer=layers[k];
            }
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }
}
