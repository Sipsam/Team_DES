using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwinEdge.Levels.Editor
{
    [CustomEditor(typeof(MountainSectionV3))]
    public sealed class MountainSectionV3Inspector : UnityEditor.Editor
    {
        LevelViewMode mode=LevelViewMode.Perspective;
        int focus;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var owner=(MountainSectionV3)target;
            EditorGUILayout.HelpBox("Mountain only. Edit the config asset; drag cyan field handles in Scene View. Regeneration preserves Manual. Pole reach and landing balance remain unverified.",MessageType.Info);
            if(GUILayout.Button("Regenerate Mountain (Undo supported)")) MountainGeneratorV3.Generate(owner);
            if(GUILayout.Button("Reseed Field from Count / Width / Depth")) {
                Undo.RecordObject(owner.config,"Reseed peak field");MountainGeneratorV3.Populate(owner.config);EditorUtility.SetDirty(owner.config);
            }
            mode=(LevelViewMode)EditorGUILayout.EnumPopup("View",mode);
            focus=EditorGUILayout.Popup("Frame",focus,new[]{"Whole Mountain","Start Overview","Central Field","Final Peak","Bridge Exit"});
            if(GUILayout.Button("Frame in Scene View")) LevelViewCamera.Frame(FocusBounds(owner,focus),ReviewRotation(focus,mode),mode!=LevelViewMode.Perspective);
            if(GUILayout.Button("Validate Generated Mountain")) {
                Directory.CreateDirectory(MountainGeneratorV3.Notes);
                File.WriteAllLines(MountainGeneratorV3.Notes+"/MountainValidation.txt",Inspect(owner));
            }
        }

        void OnSceneGUI()
        {
            var owner=(MountainSectionV3)target;var c=owner.config;if(c==null)return;
            foreach(var p in c.smallPeaks) {
                var wp=MountainGeneratorV3.WorldPeak(c,p);var pos=new Vector3(wp.position.x,wp.height,wp.position.y);
                Handles.color=Color.cyan;Handles.Label(pos+Vector3.up*4,p.id);
                EditorGUI.BeginChangeCheck();var moved=Handles.PositionHandle(pos,Quaternion.identity);
                if(EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(c,"Move field peak");p.position=new Vector2((moved.x-c.clusterCenter.x)/c.clusterSize.x,(moved.z-c.clusterCenter.y)/c.clusterSize.y);
                    p.height=moved.y;EditorUtility.SetDirty(c);
                }
            }
        }

        internal static Bounds FocusBounds(MountainSectionV3 owner,int target)
        {
            var c=owner.config;
            if(target==0)return LevelVisualizationWindow.SectionBounds(owner.GetComponent<LevelSection>());
            if(target==2)return new Bounds(new Vector3(c.clusterCenter.x,110,c.clusterCenter.y),new Vector3(c.clusterSize.x+50,210,c.clusterSize.y+50));
            if(target==4)return new Bounds(c.bridgeAnchor,new Vector3(55,45,65));
            var p=target==1?c.start:c.destination;
            return new Bounds(new Vector3(p.position.x,p.height-10,p.position.y),new Vector3(p.summitSize.x*2,100,p.summitSize.y*2));
        }

        internal static Quaternion ReviewRotation(int target,LevelViewMode mode) =>
            target==4 && mode==LevelViewMode.Perspective?Quaternion.Euler(48,155,0):LevelViewCamera.Rotation(mode,false);

        public static List<string> Inspect(MountainSectionV3 owner)
        {
            var report=new List<string>{"Unity "+Application.unityVersion,"Mountain structural verification "+DateTime.Now.ToString("O")};
            Action<bool,string> check=(pass,message)=>report.Add((pass?"PASS: ":"FAIL: ")+message);
            var c=owner.config;
            try{MountainGeneratorV3.CheckConfig(c);check(true,"Editable config; terrace rim clearance and original upper-shoulder separation elsewhere");}
            catch(Exception ex){check(false,ex.Message);}
            check(owner.generated!=null && owner.generated.transform.parent==owner.transform,"Owned Generated tree");
            check(owner.transform.Find("Manual")!=null,"Manual sibling exists");
            if(owner.generated==null)return report;
            var markers=owner.generated.GetComponentsInChildren<LevelMarker>();
            check(markers.Count(m=>m.kind==MarkerKind.PlatformPlacement)==c.smallPeakCount,"Distinct small peak candidates: "+c.smallPeakCount);
            check(markers.Any(m=>m.kind==MarkerKind.BridgeAnchor),"Bridge anchor exists; only short exit stub");
            if(c.ridges.Count==0)check(c.boundary.Count>=8,"Enclosing non-route range summits: "+c.boundary.Count);
            else check(c.ridges.Any(r=>r.crestHalfWidth>0),"Ridge profiles with shared rock shelves: "+c.ridges.Count);
            var filter=owner.generated.GetComponentInChildren<MeshFilter>();
            check(filter!=null && filter.sharedMesh!=null,"Persisted geological mesh");
            if(filter==null || filter.sharedMesh==null)return report;
            var mesh=filter.sharedMesh;var v=mesh.vertices;var t=mesh.triangles;
            check(v.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)),"Finite mesh vertices: "+v.Length);
            check(filter.GetComponent<MeshCollider>().sharedMesh==mesh,"Full terrain collider matches rendered mesh");
            var terrainCollider=filter.GetComponent<MeshCollider>();
            bool exitClear=true;
            for(float z=0;z<=c.bridgeStubLength;z+=1) {
                var ray=new Ray(c.bridgeAnchor+new Vector3(0,300,z),Vector3.down);
                if(terrainCollider.Raycast(ray,out var hit,600) && hit.point.y>c.bridgeAnchor.y+.1f)exitClear=false;
            }
            check(exitClear,"Bridge stub deck is exposed above terrain along its centreline");
            // Shared edges give a continuous walking corridor. A corner contact alone cannot
            // support a character moving between faces on the ridgeline mesh.
            var ids=new Dictionary<Vector3,int>();var welded=new List<Vector3>();var map=new int[v.Length];
            for(int i=0;i<v.Length;i++){if(!ids.TryGetValue(v[i],out int id)){id=welded.Count;ids.Add(v[i],id);welded.Add(v[i]);}map[i]=id;}
            bool sharedEdges = c.ridges.Count > 0;
            var parents=Enumerable.Range(0,sharedEdges?t.Length/3:welded.Count).ToArray();
            int Find(int i) {while(parents[i]!=i){parents[i]=parents[parents[i]];i=parents[i];}return i;}
            void Join(int a,int b) => parents[Find(a)]=Find(b);
            var facesByEdge = new Dictionary<(int,int),int>();
            var walkFaceAtVertex = Enumerable.Repeat(-1,welded.Count).ToArray();
            var edges=new Dictionary<(int,int),int>();
            int badNormals=0,degenerate=0,thin=0;float maxEdge=0;
            for(int i=0;i<t.Length;i+=3){
                var a=v[t[i]];var b=v[t[i+1]];var d=v[t[i+2]];var n=Vector3.Cross(b-a,d-a);
                float longest=Mathf.Max((b-a).sqrMagnitude,Mathf.Max((d-b).sqrMagnitude,(a-d).sqrMagnitude));
                maxEdge=Mathf.Max(maxEdge,Mathf.Sqrt(longest));
                if(n.magnitude/Mathf.Max(.0001f,longest)<.015f)thin++;
                if(n.sqrMagnitude<.000001f)degenerate++;
                if(n.y<=0)badNormals++;
                bool walkable = n.normalized.y >= Mathf.Cos(45 * Mathf.Deg2Rad);
                if (walkable && !sharedEdges) {Join(map[t[i]],map[t[i+1]]);Join(map[t[i]],map[t[i+2]]);}
                for(int k=0;k<3;k++) {
                    int u=map[t[i+k]],w=map[t[i+(k+1)%3]];
                    var key=(Math.Min(u,w),Math.Max(u,w));
                    edges.TryGetValue(key,out int count);
                    edges[key]=count+1;
                    if (!walkable || !sharedEdges) continue;
                    int face = i / 3;
                    walkFaceAtVertex[u] = face;
                    if (!facesByEdge.TryAdd(key, face)) Join(face, facesByEdge[key]);
                }
            }
            report.Add("METRIC: triangles="+t.Length/3+" thin faces (area ratio < .015)="+thin+" longest edge="+maxEdge);
            check(mesh.normals.Length==v.Length && mesh.normals.All(n=>float.IsFinite(n.x)&&float.IsFinite(n.y)&&float.IsFinite(n.z)&&n.sqrMagnitude>.9f),"Finite unit mesh normals");
            check(badNormals==0 && degenerate==0,"Upward winding, no degenerate faces");
            check(edges.Values.All(n=>n<=2),"Manifold triangle adjacency");
            // Every unshared edge must lie on the irregular outer perimeter, never inside the terrain.
            Func<Vector3,float> perimeterRadius=p=>{
                float a=Mathf.Atan2((p.z-30)/540,p.x/460);
                return Mathf.Sqrt(p.x*p.x/(460*460)+(p.z-30)*(p.z-30)/(540*540))/(1+.09f*Mathf.Sin(5*a)+.045f*Mathf.Cos(9*a));
            };
            check(edges.Where(e=>e.Value==1).All(e=>perimeterRadius(welded[e.Key.Item1])>1-3*c.meshSpacing/460),"No internal mesh seams / broken shared boundaries");
            Func<MountainPeakV3,int> component=p=>{
                var target=new Vector3(p.position.x,p.height,p.position.y);int nearest=-1;float best=float.MaxValue;
                for(int i=0;i<welded.Count;i++){
                    if (sharedEdges && walkFaceAtVertex[i] < 0) continue;
                    float distance=(welded[i]-target).sqrMagnitude;
                    if(distance<best){best=distance;nearest=i;}
                }
                return nearest < 0 ? -1 : Find(sharedEdges ? walkFaceAtVertex[nearest] : nearest);
            };
            int start=component(c.start),end=component(c.destination);
            check(start!=end,"No ordinary walking connection start-to-final on generated triangles at slope <=45 degrees (no jumping)");
            var candidates=c.smallPeaks.Select(p=>MountainGeneratorV3.WorldPeak(c,p)).ToArray();
            var components=candidates.Select(component).ToArray();
            if(c.ridges.Count==0)
                check(components.Distinct().Count()==c.smallPeakCount && components.All(x=>x!=start && x!=end),"Each small landing is a separate walking component");
            else
                check(components.All(x=>x!=start),"The first pole crossing interrupts ordinary passage from Start to the ridge shelves");
            foreach(var group in candidates.Select((p,i)=>new {p.id,part=components[i]}).GroupBy(x=>x.part).Where(g=>g.Count()>1||g.Key==start||g.Key==end))report.Add("Walking component: "+string.Join(", ",group.Select(x=>x.id))+(group.Key==start?" + Start":"")+(group.Key==end?" + Final":""));
            report.Add("UNVERIFIED: Actual pole reach, character jump/step controller, landing balance, camera collision, two-player gameplay and runtime fall/respawn behavior. Triangle connectivity is a structural test, not mechanic approval.");
            return report;
        }
    }

    public static class MountainBatchV3
    {
        // Explicit composition edit; ordinary regeneration never rewrites authored values.
        public static void RefineSightline()
        {
            MountainGeneratorV3.CreateOrOpen();
            var c=MountainGeneratorV3.Config();
            Undo.RecordObject(c,"Open start sightline");
            foreach(var p in c.smallPeaks) {
                var wp=MountainGeneratorV3.WorldPeak(c,p);
                if(wp.position.y<25 && Mathf.Abs(wp.position.x)<105)
                    p.height=Mathf.Min(p.height,118+(wp.position.y+145)*.14f);
            }
            EditorUtility.SetDirty(c);AssetDatabase.SaveAssets();Run();
        }

        public static void Refine()
        {
            MountainGeneratorV3.CreateOrOpen();
            var owner=UnityEngine.Object.FindFirstObjectByType<MountainSectionV3>();
            var c=owner.config; Undo.RecordObject(c,"Refine field composition");
            var random=new System.Random(c.seed);
            foreach(var p in c.smallPeaks) {
                var original=p.position;
                for(int attempt=0;attempt<120;attempt++) {
                    p.position=original+new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*.19f;
                    try {MountainGeneratorV3.CheckConfig(c);break;}
                    catch(InvalidOperationException){p.position=original;}
                }
            }
            EditorUtility.SetDirty(c);AssetDatabase.SaveAssets();Run();
        }

        public static void Run()
        {
            Directory.CreateDirectory(MountainGeneratorV3.Notes);
            MountainGeneratorV3.CreateOrOpen();
            var owner=UnityEngine.Object.FindFirstObjectByType<MountainSectionV3>();
            var probe=UnityEngine.Object.Instantiate(owner.config);
            MountainGeneratorV3.Populate(probe);MountainGeneratorV3.CheckConfig(probe);
            string deterministic=JsonUtility.ToJson(probe);
            MountainGeneratorV3.Populate(probe);
            if(deterministic!=JsonUtility.ToJson(probe))throw new InvalidOperationException("Reseed is not deterministic");
            UnityEngine.Object.DestroyImmediate(probe);
            var original=owner.generated;
            string originalFolder=owner.generationAssets;
            var manual=new GameObject("Manual_PreservationProbe");manual.transform.SetParent(owner.transform.Find("Manual"),false);
            var unrelated=new GameObject("Unrelated_PreservationProbe");
            MountainGeneratorV3.Generate(owner);
            var regenerated=owner.generated;
            bool retained=manual!=null && unrelated!=null && regenerated!=original;
            Undo.PerformUndo();bool undo=owner.generated!=null && owner.generationAssets==originalFolder && manual!=null && unrelated!=null;
            string undoState="tree="+(owner.generated==null?"null":owner.generated.name)+" assets="+owner.generationAssets;
            Undo.PerformRedo();bool redo=owner.generated!=null && owner.generationAssets!=originalFolder && manual!=null && unrelated!=null;
            UnityEngine.Object.DestroyImmediate(manual);UnityEngine.Object.DestroyImmediate(unrelated);
            EditorSceneManager.SaveScene(owner.gameObject.scene);
            EditorSceneManager.OpenScene(MountainGeneratorV3.ScenePath,UnityEditor.SceneManagement.OpenSceneMode.Single);
            owner=UnityEngine.Object.FindFirstObjectByType<MountainSectionV3>();
            var report=MountainSectionV3Inspector.Inspect(owner);
            report.Add("PASS: Constrained reseed clearance and deterministic same-seed configuration");
            report.Add((retained?"PASS":"FAIL")+": Regeneration preserved Manual and unrelated scene content");
            report.Add((undo&&redo?"PASS":"FAIL")+": Undo and Redo restore generated object references; undo="+undo+" redo="+redo+" "+undoState);
            report.Add("PASS: Saved scene reopened before geometry inspection");
            Capture(owner);
            File.WriteAllLines(MountainGeneratorV3.Notes+"/MountainValidation.txt",report);
            if(report.Any(x=>x.StartsWith("FAIL")))throw new InvalidOperationException(string.Join("\n",report));
            Debug.Log(string.Join("\n",report));
        }

        static void Capture(MountainSectionV3 owner)
        {
            string folder=MountainGeneratorV3.Notes+"/MountainViews";Directory.CreateDirectory(folder);
            var go=new GameObject("ReviewCameraTemporary");var camera=go.AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.14f,.18f);camera.farClipPlane=4000;
            camera.fieldOfView=45;
            var rt=new RenderTexture(1600,1200,24);camera.targetTexture=rt;
            try {
                for(int target=0;target<5;target++) foreach(LevelViewMode mode in Enum.GetValues(typeof(LevelViewMode))) {
                    var bounds=MountainSectionV3Inspector.FocusBounds(owner,target);
                    camera.transform.rotation=MountainSectionV3Inspector.ReviewRotation(target,mode);
                    camera.orthographic=mode!=LevelViewMode.Perspective;
                    camera.orthographicSize=Mathf.Max(bounds.extents.y,Mathf.Max(bounds.extents.x,bounds.extents.z))*1.15f;
                    float distance=bounds.extents.magnitude/Mathf.Sin(camera.fieldOfView*.5f*Mathf.Deg2Rad)*1.15f;
                    camera.transform.position=bounds.center-camera.transform.forward*distance;
                    camera.Render();RenderTexture.active=rt;
                    var texture=new Texture2D(1600,1200,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,1200),0,0);texture.Apply();
                    File.WriteAllBytes(folder+"/"+new[]{"Whole","Start","Field","Final","Exit"}[target]+"_"+mode+".png",texture.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                var c=owner.config;
                var views=new List<(string,Vector3,Vector3)> {
                    ("Start_Player",new Vector3(0,c.start.height+2,-190),new Vector3(12,176,160)),
                    ("Return_Player",new Vector3(12,c.destination.height+2,245),new Vector3(0,136,-160)),
                    ("Central_Reverse",new Vector3(120,205,155),new Vector3(-25,135,-80)),
                    ("Outer_Boundary",new Vector3(650,230,-510),new Vector3(310,20,-200)) };
                foreach(var p in c.smallPeaks.Take(6)) {
                    var wp=MountainGeneratorV3.WorldPeak(c,p);
                    var at=new Vector3(wp.position.x,wp.height-10,wp.position.y);
                    views.Add(("Close_"+p.id,at+new Vector3(70,55,-80),at));
                }
                foreach(var view in views) {
                    camera.orthographic=false;camera.transform.position=view.Item2;camera.transform.LookAt(view.Item3);
                    camera.Render();RenderTexture.active=rt;
                    var texture=new Texture2D(1600,1200,TextureFormat.RGB24,false);
                    texture.ReadPixels(new Rect(0,0,1600,1200),0,0);texture.Apply();
                    File.WriteAllBytes(folder+"/"+view.Item1+".png",texture.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            finally {RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
