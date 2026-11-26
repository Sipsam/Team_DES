using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace TwinEdge.Levels.Editor
{
    public sealed class ValleyViewsV3 : EditorWindow
    {
        public static readonly string[] Names={"Final Summit to Bridge","Bridge to Opposite Cliff","Canyon Descent Overview","Lower River Dock","Entire River Valley","Representative River Bend","Upper River Dock","Castle Approach","Whole World","Bridge Plank Detail","River North Bend","River South Bend"};
        public static Bounds Bounds(int i) {
            switch(i) {
                case 0:return new Bounds(new Vector3(12,130,440),new Vector3(170,200,340));
                case 1:return new Bounds(new Vector3(12,100,610),new Vector3(240,220,210));
                case 2:return new Bounds(new Vector3(0,125,910),new Vector3(400,230,480));
                case 3:return new Bounds(new Vector3(15,45,1170),new Vector3(180,100,150));
                case 4:return new Bounds(new Vector3(0,65,1525),new Vector3(550,200,820));
                case 5:return new Bounds(new Vector3(-10,45,1500),new Vector3(290,140,280));
                case 6:return new Bounds(new Vector3(60,55,1880),new Vector3(180,100,150));
                case 7:return new Bounds(new Vector3(50,45,2040),new Vector3(240,160,300));
                case 9:return new Bounds(new Vector3(12,190,510),new Vector3(24,12,30));
                case 10:return new Bounds(new Vector3(65,55,1360),new Vector3(330,180,230));
                case 11:return new Bounds(new Vector3(-60,55,1700),new Vector3(380,180,260));
                default:return new Bounds(new Vector3(0,50,800),new Vector3(1100,440,2700));
            }
        }
        int target;LevelViewMode mode;
        [MenuItem("Tools/Twin Edge/V3 Bridge River/Views")]
        public static void Open()=>GetWindow<ValleyViewsV3>("V3 Bridge River Views");
        void OnGUI(){target=EditorGUILayout.Popup("Framing",target,Names);mode=(LevelViewMode)EditorGUILayout.EnumPopup("View",mode);if(GUILayout.Button("Frame Scene View"))LevelViewCamera.Frame(Bounds(target),LevelViewCamera.Rotation(mode,false),mode!=LevelViewMode.Perspective);}
        public static void Capture() {
            string folder=ValleyGeneratorV3.Notes+"/BridgeRiverViews";Directory.CreateDirectory(folder);
            var go=new GameObject("ValleyReviewCamera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.17f,.21f);cam.farClipPlane=12000;cam.nearClipPlane=.3f;cam.fieldOfView=45;
            var rt=new RenderTexture(1400,1000,24);cam.targetTexture=rt;var old=RenderTexture.active;
            try {for(int i=0;i<Names.Length;i++)foreach(LevelViewMode mode in Enum.GetValues(typeof(LevelViewMode))) {
                var b=Bounds(i);cam.transform.rotation=LevelViewCamera.Rotation(mode,false);cam.orthographic=mode!=LevelViewMode.Perspective;
                cam.orthographicSize=b.extents.magnitude*1.06f;cam.transform.position=b.center-cam.transform.forward*b.extents.magnitude/Mathf.Sin(22.5f*Mathf.Deg2Rad)*1.12f;
                cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1400,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1400,1000),0,0);tex.Apply();File.WriteAllBytes(folder+"/"+i+"_"+mode+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);
            }}finally {RenderTexture.active=old;cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
    }
    public static class ValleyReviewV3
    {
        static bool CanyonSupportHit(Vector3 p, out RaycastHit hit) {
            hit=default;bool found=false;float best=float.MaxValue;
            var supports = Object.FindObjectsByType<ValleySectionV3>(FindObjectsSortMode.None)
                .SelectMany(section => section.generated.GetComponentsInChildren<MeshCollider>());
            foreach (var col in supports) {
                if(col.Raycast(new Ray(p+Vector3.up*50,Vector3.down),out var next,150)&&next.distance<best) {hit=next;best=next.distance;found=true;}
            }
            return found;
        }
        [MenuItem("Tools/Twin Edge/V3 Bridge River/Validate Save and Capture")]
        public static void Run() {
            Directory.CreateDirectory(ValleyGeneratorV3.Notes);
            var report=new List<string>();var timer=System.Diagnostics.Stopwatch.StartNew();
            ValleyGeneratorV3.BuildAll();
            var mountain=ValleyGeneratorV3.Mountain();var original=mountain.generated;
            foreach(var owner in Object.FindObjectsByType<ValleySectionV3>(FindObjectsSortMode.None)) {
                var manual=new GameObject("ManualProbe");manual.transform.SetParent(owner.transform.Find("Manual"),false);
                string old=owner.generationAssets;ValleyGeneratorV3.Generate(owner);string next=owner.generationAssets;
                bool preserved=manual&&mountain.generated==original;
                Undo.PerformUndo();bool undo=owner.generated&&owner.generationAssets==old;
                Undo.PerformRedo();bool redo=owner.generated&&owner.generationAssets==next;
                report.Add((preserved&&undo&&redo?"PASS":"FAIL")+": "+owner.part+" regeneration Manual Mountain Undo Redo");Object.DestroyImmediate(manual);
            }
            report.Add("Generation + regeneration ms="+timer.ElapsedMilliseconds);
            Physics.SyncTransforms();
            foreach(var s in Object.FindObjectsByType<ValleySectionV3>(FindObjectsSortMode.None)) {
                int tris=0,bad=0,down=0;foreach(var f in s.generated.GetComponentsInChildren<MeshFilter>()) {
                    if(f.name!="Terrain"&&f.name!="WaterSurface")continue;
                    var m=f.sharedMesh;var v=m.vertices;var t=m.triangles;tris+=t.Length/3;
                    for(int i=0;i<t.Length;i+=3) {var cross=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]);if(cross.sqrMagnitude<1e-10f)bad++;if(cross.y<-.0001f)down++;}
                    var col=f.GetComponent<MeshCollider>();if(f.name=="Terrain"&&(!col||col.sharedMesh!=m))report.Add("FAIL: terrain collider mismatch");
                }
                report.Add((bad==0&&down==0?"PASS":"FAIL")+": "+s.part+" triangles="+tris+" degenerate="+bad+" downward="+down+" colliders="+s.generated.GetComponentsInChildren<Collider>().Length);
            }
            var config=ValleyGeneratorV3.Config();float maxSlope=0;int misses=0;float maxError=0;
            for(float z=config.bridgeEnd.z;z<=1150;z+=1) {
                var p=config.CanyonStairProfile(z);
                if(CanyonSupportHit(p,out var hit)) {
                    maxError=Mathf.Max(maxError,Mathf.Abs(hit.point.y-p.y));maxSlope=Mathf.Max(maxSlope,Vector3.Angle(hit.normal,Vector3.up));
                }else misses++;
            }
            report.Add((misses==0&&maxError<.35f&&maxSlope<45?"PASS":"FAIL")+": stair centerline raycasts misses="+misses+" heightError="+maxError+" maxSlope="+maxSlope+" (controller limit45 step.3 radius.45 height2; no play simulation)");
            int widthMisses=0;float widthError=0,widthSlope=0;Vector3 widthWorst=Vector3.zero;
            for(float z=config.bridgeEnd.z;z<=1150;z+=2)foreach(float side in new[]{-.35f,0,.35f}) {
                var p=config.CanyonStairProfile(z)+Vector3.right*(ValleyConfigV3.StairWidth*side);
                if(CanyonSupportHit(p,out var hit)) {
                    widthError=Mathf.Max(widthError,Mathf.Abs(hit.point.y-p.y));float angle=Vector3.Angle(hit.normal,Vector3.up);if(angle>widthSlope){widthSlope=angle;widthWorst=p;}
                }else widthMisses++;
            }
            report.Add((widthMisses==0&&widthError<.35f&&widthSlope<45?"PASS":"FAIL")+": stair central70percent width samples misses="+widthMisses+" heightError="+widthError+" maxSlope="+widthSlope+" worst="+widthWorst);
            var sections=mountain.transform.parent.GetComponentsInChildren<ValleySectionV3>();
            foreach(float z in new[]{710f,1150f,1900f}) {
                var profiles=sections.Select(o=>o.generated.transform.Find("Terrain").GetComponent<MeshFilter>().sharedMesh.vertices.Where(v=>Mathf.Abs(v.z-z)<.001f).Distinct().OrderBy(v=>v.x).ToArray()).Where(v=>v.Length>0).ToArray();
                bool same=profiles.Length==2&&profiles[0].SequenceEqual(profiles[1]);report.Add((same?"PASS":"FAIL")+": shared terrain boundary Z="+z+" profiles="+profiles.Length);
            }
            var terrainCols=sections.Select(o=>o.generated.transform.Find("Terrain").GetComponent<MeshCollider>()).Concat(mountain.generated.GetComponentsInChildren<MeshCollider>()).ToArray();
            int obstructed=0;float clearance=float.MaxValue;
            for(int i=1;i<config.plankCount-1;i++) {
                var p=Vector3.Lerp(config.bridgeStart,config.bridgeEnd,i/(float)config.plankCount);
                foreach(var col in terrainCols)if(col.Raycast(new Ray(p+Vector3.up*100,Vector3.down),out var hit,600)){clearance=Mathf.Min(clearance,p.y-hit.point.y);if(hit.point.y>p.y+.1f)obstructed++;}
            }
            report.Add((obstructed==0?"PASS":"FAIL")+": bridge extension terrain obstruction samples="+obstructed+" minimum terrain clearance="+clearance);
            int wetErrors=0;float minDepth=float.MaxValue;
            var riverCol=sections.Single(o=>o.part==ValleyPartV3.River).generated.transform.Find("Terrain").GetComponent<MeshCollider>();
            for(float z=1152;z<1900;z+=2) {var p=ValleyGeneratorV3.Sample(config.river,z);if(riverCol.Raycast(new Ray(p+Vector3.up*100,Vector3.down),out var hit,200)){minDepth=Mathf.Min(minDepth,p.y-hit.point.y);if(hit.point.y>=p.y)wetErrors++;}else wetErrors++;}
            report.Add((wetErrors==0?"PASS":"FAIL")+": water centerline above bed; errors="+wetErrors+" minimum depth="+minDepth);
            int approachMiss=0;float approachError=0,approachSlope=0;
            var approachCol=sections.Single(o=>o.part==ValleyPartV3.CastleApproach).generated.transform.Find("Terrain").GetComponent<MeshCollider>();
            for(float z=1900;z<=config.castleEntry.z;z+=1) {
                float u=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1900,2070,z));float x=Mathf.Lerp(config.river.Last().x+ValleyGeneratorV3.Width(config,1900)*.5f+10,config.castleEntry.x,u);
                float y=Mathf.Lerp(config.river.Last().y+2,config.castleEntry.y,u);
                if(approachCol.Raycast(new Ray(new Vector3(x,y+100,z),Vector3.down),out var hit,200)) {approachError=Mathf.Max(approachError,Mathf.Abs(hit.point.y-y));approachSlope=Mathf.Max(approachSlope,Vector3.Angle(hit.normal,Vector3.up));}else approachMiss++;
            }
            report.Add((approachMiss==0&&approachError<.3f&&approachSlope<45?"PASS":"FAIL")+": upper dock to castle approach misses="+approachMiss+" heightError="+approachError+" maxSlope="+approachSlope);
            var front=ValleyGeneratorV3.MountainBoundary();var bridgeVertices=new HashSet<Vector3>(sections.Single(o=>o.part==ValleyPartV3.Bridge).generated.transform.Find("Terrain").GetComponent<MeshFilter>().sharedMesh.vertices);
            report.Add((front.All(v=>bridgeVertices.Contains(v))?"PASS":"FAIL")+": exact Mountain north boundary vertices retained="+front.Count);
            report.Add("Combined world mesh filters="+mountain.transform.parent.GetComponentsInChildren<MeshFilter>().Length+" colliders="+mountain.transform.parent.GetComponentsInChildren<Collider>().Length+" triangles="+mountain.transform.parent.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3));
            report.Add("Planks="+Object.FindObjectsByType<BridgePlankV3>(FindObjectsSortMode.None).Length+" pitch="+Vector3.Distance(config.bridgeStart,config.bridgeEnd)/config.plankCount+" gap="+config.plankGap);
            report.Add("NOT VERIFIED: collapse after destruction, jump ranges, rafting, current, network gameplay, runtime frame cost");
            report.Add((config.river.Zip(config.river.Skip(1),(a,b)=>b.y>a.y).All(v=>v)?"PASS":"FAIL")+": outbound upstream +Z, return/current downstream -Z; LowerDock Mountain side Y="+config.river.First().y+" UpperDock Castle side Y="+config.river.Last().y);
            EditorSceneManager.SaveScene(mountain.gameObject.scene);
            // Reopen only the owned scene; other loaded scenes stay loaded.
            var scene=mountain.gameObject.scene;EditorSceneManager.CloseScene(scene,true);EditorSceneManager.OpenScene(MountainGeneratorV3.ScenePath,OpenSceneMode.Additive);
            report.Add("PASS: scene saved and reopened");
            ValleyViewsV3.Capture();
            report.Add("Generated asset folders retained for Undo; no cleanup or GUID replacement");
            report.Add("Total saved generation folders="+Directory.GetDirectories("Assets/Game/Level/Data/Generated","ValleyV3_*").Length);
            File.WriteAllLines(ValleyGeneratorV3.Notes+"/BridgeRiverValidation.txt",report);
            Debug.Log(string.Join("\n",report));
            if(report.Any(line=>line.StartsWith("FAIL")))throw new Exception("Bridge River validation failed; see BridgeRiverValidation.txt");
        }
    }
    [InitializeOnLoad]
    public static class ValleyLocalRequestV3
    {
        static ValleyLocalRequestV3(){EditorApplication.update+=Poll;}
        static void Poll(){
            const string request="Docs/Development/Level/V3/BridgeRiver.request";
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(request))return;
            File.Delete(request);
            try{ValleyReviewV3.Run();}catch(Exception ex){File.WriteAllText(ValleyGeneratorV3.Notes+"/BridgeRiverError.txt",ex.ToString());Debug.LogException(ex);}
        }
    }
}
