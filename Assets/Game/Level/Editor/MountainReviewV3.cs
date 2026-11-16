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
            try{MountainGeneratorV3.CheckConfig(c);check(true,"Editable config and conservative upper-shoulder separation");}
            catch(Exception ex){check(false,ex.Message);}
            check(owner.generated!=null && owner.generated.transform.parent==owner.transform,"Owned Generated tree");
            check(owner.transform.Find("Manual")!=null,"Manual sibling exists");
            if(owner.generated==null)return report;
            var markers=owner.generated.GetComponentsInChildren<LevelMarker>();
            check(markers.Count(m=>m.kind==MarkerKind.PlatformPlacement)==c.smallPeakCount,"Distinct small peak candidates: "+c.smallPeakCount);
            check(markers.Any(m=>m.kind==MarkerKind.BridgeAnchor),"Bridge anchor exists; only short exit stub");
            check(c.boundary.Count>=8,"Enclosing non-route range summits: "+c.boundary.Count);
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
            // Weld exact coordinates, then conservatively join all <=45 degree triangle vertices.
            // Vertex-only contacts are also joined, making disconnection a stronger check.
            var ids=new Dictionary<Vector3,int>();var welded=new List<Vector3>();var map=new int[v.Length];
            for(int i=0;i<v.Length;i++){if(!ids.TryGetValue(v[i],out int id)){id=welded.Count;ids.Add(v[i],id);welded.Add(v[i]);}map[i]=id;}
            var parents=Enumerable.Range(0,welded.Count).ToArray();
            Func<int,int> find=null;find=i=>{while(parents[i]!=i){parents[i]=parents[parents[i]];i=parents[i];}return i;};
            Action<int,int> join=(a,b)=>parents[find(a)]=find(b);
            var edges=new Dictionary<(int,int),int>();
            int badNormals=0,degenerate=0;
            for(int i=0;i<t.Length;i+=3){
                var a=v[t[i]];var b=v[t[i+1]];var d=v[t[i+2]];var n=Vector3.Cross(b-a,d-a);
                if(n.sqrMagnitude<.000001f)degenerate++;
                if(n.y<=0)badNormals++;
                if(n.normalized.y>=Mathf.Cos(45*Mathf.Deg2Rad)){join(map[t[i]],map[t[i+1]]);join(map[t[i]],map[t[i+2]]);}
                for(int k=0;k<3;k++) {int u=map[t[i+k]],w=map[t[i+(k+1)%3]];var key=(Math.Min(u,w),Math.Max(u,w));edges.TryGetValue(key,out int count);edges[key]=count+1;}
            }
            check(badNormals==0 && degenerate==0,"Upward winding, no degenerate faces");
            check(edges.Values.All(n=>n<=2),"Manifold triangle adjacency");
            // Every unshared edge must lie on the irregular outer perimeter, never inside the terrain.
            Func<Vector3,float> perimeterRadius=p=>{
                float a=Mathf.Atan2((p.z-30)/540,p.x/460);
                return Mathf.Sqrt(p.x*p.x/(460*460)+(p.z-30)*(p.z-30)/(540*540))/(1+.09f*Mathf.Sin(5*a)+.045f*Mathf.Cos(9*a));
            };
            check(edges.Where(e=>e.Value==1).All(e=>perimeterRadius(welded[e.Key.Item1])>1-3*c.meshSpacing/460),"No internal mesh seams / broken shared boundaries");
            Func<MountainPeakV3,int> component=p=>{
                var target=new Vector3(p.position.x,p.height,p.position.y);int nearest=0;float best=float.MaxValue;
                for(int i=0;i<welded.Count;i++){float distance=(welded[i]-target).sqrMagnitude;if(distance<best){best=distance;nearest=i;}}
                return find(nearest);
            };
            int start=component(c.start),end=component(c.destination);
            check(start!=end,"No ordinary walking connection start-to-final on generated triangles at slope <=45 degrees (no jumping)");
            var candidates=c.smallPeaks.Select(p=>MountainGeneratorV3.WorldPeak(c,p)).ToArray();
            var components=candidates.Select(component).ToArray();
            check(components.Distinct().Count()==c.smallPeakCount && components.All(x=>x!=start && x!=end),"Each small landing is a separate walking component");
            report.Add("UNVERIFIED: Actual pole reach, character jump/step controller, landing balance, camera collision, two-player gameplay and runtime fall/respawn behavior. Triangle connectivity is a structural test, not mechanic approval.");
            return report;
        }
    }

    public static class MountainBatchV3
    {
        public static void Run()
        {
            Directory.CreateDirectory(MountainGeneratorV3.Notes);
            MountainGeneratorV3.CreateOrOpen();
            var owner=UnityEngine.Object.FindFirstObjectByType<MountainSectionV3>();
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
            }
            finally {RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
