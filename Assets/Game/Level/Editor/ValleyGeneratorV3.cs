using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace TwinEdge.Levels.Editor
{
    public static class ValleyGeneratorV3
    {
        public static MountainSectionV3 Mountain() => UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MountainGeneratorV3.ScenePath).GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MountainSectionV3>()).Single();
        public const string Notes="Docs/Development/Level/V3";
        const string ConfigPath="Assets/Game/Level/Data/ValleyConfigV3.asset";
        public static ValleyConfigV3 Config() {
            var c=AssetDatabase.LoadAssetAtPath<ValleyConfigV3>(ConfigPath);
            if(c)return c;
            c=ScriptableObject.CreateInstance<ValleyConfigV3>();
            var m=Mountain().config;
            c.bridgeStart=m.bridgeAnchor+Vector3.forward*m.bridgeStubLength;
            c.bridgeEnd.x=c.bridgeStart.x;c.bridgeEnd.y=c.bridgeStart.y;
            AssetDatabase.CreateAsset(c,ConfigPath);return c;
        }
        [MenuItem("Tools/Twin Edge/V3 Bridge River/Create or Regenerate Extensions")]
        public static void BuildAll() {
            MountainGeneratorV3.CreateOrOpen();var mountain=Mountain();
            var c=Config();
            foreach(ValleyPartV3 part in Enum.GetValues(typeof(ValleyPartV3))) {
                var owner=mountain.transform.parent.GetComponentsInChildren<ValleySectionV3>().FirstOrDefault(s=>s.part==part);
                if(!owner) {
                    var go=new GameObject(part.ToString());Undo.RegisterCreatedObjectUndo(go,"Create V3 section");go.transform.SetParent(mountain.transform.parent,false);
                    owner=Undo.AddComponent<ValleySectionV3>(go);owner.part=part;owner.config=c;
                    var section=Undo.AddComponent<LevelSection>(go);section.section=part==ValleyPartV3.Bridge?SectionId.Bridge:SectionId.River;
                    var manual=new GameObject("Manual");manual.transform.SetParent(go.transform,false);
                }
                Generate(owner);
            }
        }
        public static List<Vector3> MountainBoundary() {
            var m=Mountain();
            var mesh=m.generated.GetComponentInChildren<MeshFilter>().sharedMesh;
            var v=mesh.vertices;var t=mesh.triangles;
            var edges=new Dictionary<(Vector3,Vector3),int>();
            Func<Vector3,Vector3,bool> less=(a,b)=>a.x<b.x || a.x==b.x&&(a.z<b.z || a.z==b.z&&a.y<b.y);
            for(int i=0;i<t.Length;i+=3)for(int j=0;j<3;j++) {
                var a=v[t[i+j]];var b=v[t[i+(j+1)%3]];var k=less(a,b)?(a,b):(b,a);
                edges.TryGetValue(k,out int count);edges[k]=count+1;
            }
            var adjacency=new Dictionary<Vector3,List<Vector3>>();
            foreach(var e in edges.Where(e=>e.Value==1&&e.Key.Item1.z>350&&e.Key.Item2.z>350)) {
                var a=e.Key.Item1;var b=e.Key.Item2;
                if(!adjacency.ContainsKey(a))adjacency[a]=new List<Vector3>();if(!adjacency.ContainsKey(b))adjacency[b]=new List<Vector3>();
                adjacency[a].Add(b);adjacency[b].Add(a);
            }
            var result=new List<Vector3>();
            foreach(var entry in adjacency.Where(e=>e.Value.Count==1)) {
                var chain=new List<Vector3>{entry.Key};var prev=new Vector3(float.MaxValue,0,0);var current=entry.Key;
                while(true) {
                    var candidates=adjacency[current].Where(p=>p!=prev).ToArray();if(candidates.Length==0)break;
                    var next=candidates[0];chain.Add(next);prev=current;current=next;if(chain.Count>adjacency.Count)throw new Exception("Boundary cycle");
                }
                if(chain.Count>result.Count)result=chain;
            }
            if(result.Count>0&&result[0].x>result.Last().x)result.Reverse();
            if(result.Count<20)throw new Exception("Incomplete north mountain boundary: chain="+result.Count+" edges="+adjacency.Count+" mesh="+mesh.name+" bounds="+mesh.bounds);return result;
        }
        static float Smooth(float a,float b,float value) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,value));
        public static Vector3 Sample(Vector3[] p,float z) {
            int i=0;while(i<p.Length-2&&z>p[i+1].z)i++;
            float t=Mathf.InverseLerp(p[i].z,p[i+1].z,z);float s=t*t*(3-2*t);
            return new Vector3(Mathf.Lerp(p[i].x,p[i+1].x,s),Mathf.Lerp(p[i].y,p[i+1].y,s),z);
        }
        public static float Width(ValleyConfigV3 c,float z) {
            int i=0;while(i<c.river.Length-2&&z>c.river[i+1].z)i++;
            return Mathf.Lerp(c.widths[i],c.widths[i+1],Smooth(0,1,Mathf.InverseLerp(c.river[i].z,c.river[i+1].z,z)));
        }
        static float WorldX(float x,float z) => x+Smooth(710,790,z)*(x*(.10f*Mathf.Sin(z*.009f)+.045f*Mathf.Cos(z*.021f))+18*Mathf.Sin(z*.014f)*Mathf.Pow(Mathf.Abs(x)/400,3));
        public static float Height(ValleyConfigV3 c,float x,float z) {
            float floor=c.bridgeStart.y-c.canyonDepth;
            float y;
            if(z<=710) {
                float north=Smooth(0,1,Mathf.InverseLerp(565,642,z));
                float distance=Mathf.Abs(x-c.bridgeEnd.x);
                float shoulder=Mathf.Max(0,distance-c.landingRadius);
                y=Mathf.Lerp(floor+12*Mathf.Sin(x*.018f+z*.02f),c.bridgeEnd.y-Mathf.Min(200,shoulder*.72f),north);
                y+=Smooth(0,1,Mathf.InverseLerp(c.landingRadius,100,distance))*c.silhouette*Mathf.Sin(x*.033f+z*.022f)*north;
                if(z>650) {
                    var hp=Sample(c.descent,z);float hd=Mathf.Abs(x-hp.x);
                    float hy=hp.y+Mathf.Max(0,hd-c.walkWidth*.5f)*(x<hp.x?-.48f:c.cliffSlope)+c.silhouette*Mathf.Sin(x*.027f+z*.019f)*Smooth(8,75,hd);
                    y=Mathf.Lerp(y,hy,Smooth(0,1,(z-650)/60));
                }
                var path=Sample(c.descent,z);
                float blend=1-Smooth(c.walkWidth*.5f,c.walkWidth*.5f+32,Mathf.Abs(x-path.x));
                y=Mathf.Lerp(y,path.y,blend*north);
            } else if(z<1150) {
                var p=Sample(c.descent,z);float d=Mathf.Abs(x-p.x);
                float side=Mathf.Sign(x-p.x);
                y=p.y+Mathf.Max(0,d-c.walkWidth*.5f)*(side<0?-.48f:c.cliffSlope);
                y+=c.silhouette*Mathf.Sin(x*.027f+z*.019f)*Smooth(8,75,d);
                if(z>1090)y=Mathf.Lerp(y,RiverHeight(c,x,1150),Smooth(0,1,(z-1090)/60));
                y=Mathf.Lerp(y,p.y,1-Smooth(c.walkWidth*.5f,c.walkWidth*.5f+12,d));
            } else if(z<=1900)y=RiverHeight(c,x,z);
            else {
                float u=Smooth(0,1,Mathf.InverseLerp(1900,2070,z));
                float entrance=RiverHeight(c,x,1900);float dist=Mathf.Abs(x-c.castleEntry.x);
                float site=c.castleEntry.y+Mathf.Max(0,dist-c.castleSite.x*.5f)*.35f;
                y=Mathf.Lerp(entrance,site,u);
                float entryX=c.river.Last().x+Width(c,1900)*.5f+10;
                float routeX=Mathf.Lerp(entryX,c.castleEntry.x,u);
                float route=1-Smooth(c.walkWidth*.5f,c.walkWidth*.5f+12,Mathf.Abs(x-routeX));
                y=Mathf.Lerp(y,Mathf.Lerp(c.river.Last().y+2,c.castleEntry.y,u),route);
            }
            float rim=Smooth(0,1,Mathf.InverseLerp(c.valleyWidth*.75f,c.valleyWidth*1.25f,Mathf.Abs(x)));
            return Mathf.Lerp(y,floor-20,rim);
        }
        static float RiverHeight(ValleyConfigV3 c,float x,float z) {
            var p=Sample(c.river,z);float d=Mathf.Abs(x-p.x);float half=Width(c,z)*.5f;
            // Meander banks vary with curvature, side, longitudinal terraces and rock noses.
            float bend=Sample(c.river,z+8).x-2*p.x+Sample(c.river,z-8).x;
            float outer=.5f-.5f*Mathf.Clamp(bend*.25f*Mathf.Sign(x-p.x),-1,1);
            float slope=Mathf.Lerp(c.innerBankSlope,c.outerBankSlope,outer);
            float edge=d-half;
            float y=edge<-3?p.y-c.bedDepth:edge<5?Mathf.Lerp(p.y-c.bedDepth,p.y+2,(edge+3)/8):p.y+2+(edge-5)*slope;
            y+=Smooth(12,65,edge)*(9*Mathf.Sin(z*.029f+x*.023f)+6*Mathf.Cos(z*.013f-x*.037f));
            // End landing terrain joins the timber docks at exactly water + 2m.
            float dock=Mathf.Max(1-Mathf.Clamp01(Mathf.Abs(z-1150)/30),1-Mathf.Clamp01(Mathf.Abs(z-1900)/30));
            float shelf=1-Smooth(half+12,half+32,d);
            return Mathf.Lerp(y,p.y+2,dock*shelf*Smooth(half-1,half+5,d));
        }
        public static void Check(ValleyConfigV3 c) {
            if(c.plankCount<4||c.plankCount>1000||c.bridgeWidth<1||c.bedDepth<1||c.walkWidth<2||c.widths.Length!=c.river.Length)throw new Exception("Invalid dimensions");
            foreach(var path in new[]{c.descent,c.river})for(int i=1;i<path.Length;i++)if(path[i].z<=path[i-1].z)throw new Exception("Control points must increase Z");
            if(c.widths.Any(w=>w<c.raftLaneReservation*2))throw new Exception("Two reserved raft lanes do not fit");
            if(c.plankGap<0||c.plankGap>=Vector3.Distance(c.bridgeStart,c.bridgeEnd)/c.plankCount)throw new Exception("Invalid plank gap");
            var m=Mountain();
            if(Vector3.Distance(c.bridgeStart,m.config.bridgeAnchor+Vector3.forward*m.config.bridgeStubLength)>.01f)throw new Exception("Bridge start must meet preserved Mountain stub");
            if(c.bridgeEnd!=c.descent[0]||Mathf.Abs(c.descent.Last().y-c.river[0].y-2)>.01f)throw new Exception("Endpoint profiles disagree");
        }
        public static void Generate(ValleySectionV3 owner) {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit mode required");
            Check(owner.config);
            if(owner.transform.position!=Vector3.zero||owner.transform.rotation!=Quaternion.identity||owner.transform.lossyScale!=Vector3.one)throw new Exception("Section requires world identity");
            var boundary=MountainBoundary();var c=owner.config;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Regenerate V3 "+owner.part);
            try {
                string folder="Assets/Game/Level/Data/Generated/ValleyV3_"+owner.part+"_"+Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(folder);AssetDatabase.Refresh();
                var g=new GameObject("Generated");g.transform.SetParent(owner.transform,false);Undo.RegisterCreatedObjectUndo(g,"Generate valley");
                var rock=Mat(folder,"Geology",new Color(.39f,.44f,.40f));var wood=Mat(folder,"Timber",new Color(.48f,.30f,.14f));
                var water=Mat(folder,"Water",new Color(.08f,.39f,.47f));
                int cols=boundary.Count;var arc=new float[cols];for(int i=1;i<cols;i++)arc[i]=arc[i-1]+Vector3.Distance(boundary[i],boundary[i-1]);
                float a=owner.part==ValleyPartV3.Bridge?0:owner.part==ValleyPartV3.CanyonDescent?710:owner.part==ValleyPartV3.River?1150:1900;
                float b=owner.part==ValleyPartV3.Bridge?710:owner.part==ValleyPartV3.CanyonDescent?1150:owner.part==ValleyPartV3.River?1900:2140;
                int rows=owner.part==ValleyPartV3.Bridge?65:Mathf.CeilToInt((b-a)/4)+1;
                var v=new List<Vector3>();var t=new List<int>();
                for(int j=0;j<rows;j++)for(int i=0;i<cols;i++) {
                    float f=j/(float)(rows-1);float x=Mathf.Lerp(-400,400,arc[i]/arc.Last());float z=Mathf.Lerp(a,b,f);float y;
                    if(owner.part==ValleyPartV3.Bridge)continue;
                    x=WorldX(x,z);y=Height(c,x,z);
                    v.Add(new Vector3(x,y,z));
                    if(j>0&&i>0){int k=j*cols+i;t.AddRange(new[]{k-cols-1,k-1,k-cols,k-cols,k-1,k});}
                }
                if(owner.part==ValleyPartV3.Bridge) {
                    v=new List<Vector3>(boundary);
                    for(int i=cols-1;i>=0;i--) {float x=WorldX(Mathf.Lerp(-400,400,arc[i]/arc.Last()),710);v.Add(new Vector3(x,Height(c,x,710),710));}
                    t=Triangulate(v);Refine(c,v,t);
                }
                MeshObject(g.transform,folder,"Terrain",v,t,rock,true);
                if(owner.part==ValleyPartV3.Bridge) {
                    float span=Vector3.Distance(c.bridgeStart,c.bridgeEnd),pitch=span/c.plankCount;
                    for(int i=0;i<c.plankCount;i++) {
                        var p=Vector3.Lerp(c.bridgeStart,c.bridgeEnd,(i+.5f)/c.plankCount);
                        var size=new Vector3(c.bridgeWidth,.4f,pitch-c.plankGap);
                        var plank=Box(g.transform,"Plank_"+i.ToString("000"),p-Vector3.up*.2f,size,wood);
                        plank.transform.rotation=Quaternion.LookRotation(c.bridgeEnd-c.bridgeStart);
                        var id=plank.AddComponent<BridgePlankV3>();id.plankId=i;id.socket=p;id.dimensions=size;
                    }
                    Mark(g.transform,"BridgeEntrance",c.bridgeStart,new Vector3(10,3,10),MarkerKind.BridgeAnchor);
                    Mark(g.transform,"OppositeCliff",c.bridgeEnd,new Vector3(c.landingRadius*2,3,30),MarkerKind.BridgeAnchor);
                    Mark(g.transform,"TimberSupply",c.bridgeStart+new Vector3(12,0,-12),new Vector3(6,3,6),MarkerKind.PuzzlePlacement);
                }
                if(owner.part==ValleyPartV3.CanyonDescent)Route(g.transform,c.descent,RouteKind.Walk,"Ordinary descent; static samples only");
                if(owner.part==ValleyPartV3.River) {
                    var wv=new List<Vector3>();var wt=new List<int>();
                    for(int j=0;j<=375;j++) {
                        float z=Mathf.Lerp(1150,1900,j/375f);var p=Sample(c.river,z);float half=Width(c,z)*.5f-1;
                        wv.Add(p+Vector3.left*half);wv.Add(p+Vector3.right*half);
                        if(j>0){int k=j*2;wt.AddRange(new[]{k-2,k,k-1,k-1,k,k+1});}
                    }
                    MeshObject(g.transform,folder,"WaterSurface",wv,wt,water,false);
                    foreach(bool upper in new[]{true,false}) {
                        var p=upper?c.river.First():c.river.Last();float half=Width(c,p.z)*.5f;
                        Box(g.transform,(upper?"Upper":"Lower")+"Dock",p+new Vector3(half+2,1.4f,upper?8:-8),new Vector3(16,1.2f,16),wood);
                        string name=upper?"Upper":"Lower";
                        Mark(g.transform,name+"Boarding",p+new Vector3(half+3,2,0),new Vector3(12,3,14),MarkerKind.RaftDock);
                        Mark(g.transform,name+"Checkpoint",p+new Vector3(half+10,2,upper?18:-18),new Vector3(8,3,8),MarkerKind.Checkpoint);
                        Mark(g.transform,name+"CombinedRaft",p+new Vector3(0,0,upper?20:-20),new Vector3(c.raftLaneReservation*2,3,16),MarkerKind.PlatformPlacement);
                        for(int lane=-1;lane<=1;lane+=2)Mark(g.transform,name+"IndividualRaft"+lane,p+new Vector3(lane*c.raftLaneReservation*.5f,0,upper?40:-40),new Vector3(c.raftLaneReservation,3,12),MarkerKind.PlatformPlacement);
                    }
                    for(int bend=1;bend<c.river.Length-1;bend++)Mark(g.transform,"TurnReservation_"+bend,c.river[bend],new Vector3(c.turningReservation*2,3,c.turningReservation*2),MarkerKind.PlatformPlacement);
                    Route(g.transform,c.river,RouteKind.Water,"Same waterway outbound/return; currents and rafting unimplemented");
                }
                if(owner.part==ValleyPartV3.CastleApproach) {
                    Mark(g.transform,"CastleEntranceReservation",c.castleEntry,new Vector3(c.castleSite.x,4,c.castleSite.y),MarkerKind.SectionExit);
                    Route(g.transform,new[]{new Vector3(80,31,1900),new Vector3(50,35,1980),c.castleEntry},RouteKind.Walk,"Reserved approach; no castle geometry");
                }
                Undo.RegisterCompleteObjectUndo(owner,"Replace valley generation");if(owner.generated)Undo.DestroyObjectImmediate(owner.generated);
                owner.generated=g;owner.generationAssets=folder;Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
            } catch {Undo.RevertAllDownToGroup(group);throw;}
        }
        static float Cross(Vector3 a,Vector3 b,Vector3 c) => (b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);
        static List<int> Triangulate(List<Vector3> v) {
            var ids=Enumerable.Range(0,v.Count).ToList();var triangles=new List<int>();
            while(ids.Count>3) {
                bool clipped=false;
                for(int k=0;k<ids.Count;k++) {
                    int a=ids[(k+ids.Count-1)%ids.Count],b=ids[k],c=ids[(k+1)%ids.Count];
                    if(Cross(v[a],v[b],v[c])<.00001f)continue;
                    bool contains=ids.Any(i=>i!=a&&i!=b&&i!=c&&Cross(v[a],v[b],v[i])>=-.00001f&&Cross(v[b],v[c],v[i])>=-.00001f&&Cross(v[c],v[a],v[i])>=-.00001f);
                    if(contains)continue;
                    triangles.AddRange(new[]{a,c,b});ids.RemoveAt(k);clipped=true;break;
                }
                if(!clipped)throw new Exception("Cannot triangulate geological boundary");
            }
            triangles.AddRange(new[]{ids[0],ids[2],ids[1]});return triangles;
        }
        static void Refine(ValleyConfigV3 c,List<Vector3> v,List<int> t) {
            for(int iteration=0;iteration<12;iteration++) {
                var split=new Dictionary<(int,int),int>();
                for(int j=0;j<t.Count;j+=3)for(int k=0;k<3;k++) {
                    int a=t[j+k],b=t[j+(k+1)%3];if(a>b){int temp=a;a=b;b=temp;}
                    var key=(a,b);var d=v[a]-v[b];d.y=0;
                    if(d.sqrMagnitude<=144||split.ContainsKey(key))continue;
                    var p=(v[a]+v[b])*.5f;p.y=Height(c,p.x,p.z);split[key]=v.Count;v.Add(p);
                }
                if(split.Count==0)return;
                var next=new List<int>();
                for(int j=0;j<t.Count;j+=3) {
                    int a=t[j],b=t[j+1],d=t[j+2];
                    int ab=split.TryGetValue((Math.Min(a,b),Math.Max(a,b)),out int q)?q:-1;
                    int bd=split.TryGetValue((Math.Min(b,d),Math.Max(b,d)),out q)?q:-1;
                    int da=split.TryGetValue((Math.Min(d,a),Math.Max(d,a)),out q)?q:-1;
                    int count=(ab>=0?1:0)+(bd>=0?1:0)+(da>=0?1:0);
                    if(count==0)next.AddRange(new[]{a,b,d});
                    else if(count==3)next.AddRange(new[]{a,ab,da,ab,b,bd,da,bd,d,ab,bd,da});
                    else if(count==1) {
                        if(ab>=0)next.AddRange(new[]{a,ab,d,ab,b,d});
                        else if(bd>=0)next.AddRange(new[]{b,bd,a,bd,d,a});
                        else next.AddRange(new[]{d,da,b,da,a,b});
                    } else {
                        if(ab<0)next.AddRange(new[]{d,da,bd,a,b,da,b,bd,da});
                        else if(bd<0)next.AddRange(new[]{a,ab,da,b,d,ab,d,da,ab});
                        else next.AddRange(new[]{b,bd,ab,d,a,bd,a,ab,bd});
                    }
                }
                t.Clear();t.AddRange(next);
            }
        }
        static Material Mat(string folder,string name,Color color) {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.05f);AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");return m;
        }
        static GameObject Box(Transform parent,string name,Vector3 pos,Vector3 size,Material mat) {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.position=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;return g;
        }
        static void MeshObject(Transform parent,string folder,string name,List<Vector3> v,List<int> t,Material mat,bool collider) {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};
            // Flat face normals match the existing low-poly Mountain.
            mesh.vertices=t.Select(i=>v[i]).ToArray();mesh.triangles=Enumerable.Range(0,t.Count).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,folder+"/"+name+".asset");var g=new GameObject(name);g.transform.SetParent(parent,false);
            g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collider)g.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        static void Mark(Transform p,string name,Vector3 point,Vector3 size,MarkerKind kind) {
            var g=new GameObject(name);g.transform.SetParent(p,false);g.transform.position=point;var m=g.AddComponent<LevelMarker>();m.id="Valley.V3."+name;m.section=kind==MarkerKind.BridgeAnchor?SectionId.Bridge:SectionId.River;m.kind=kind;m.reservedSize=size;m.notes="Configurable hypothesis; no implemented gimmick";
        }
        static void Route(Transform parent,Vector3[] points,RouteKind kind,string notes) {
            var g=new GameObject("SharedRoute");g.transform.SetParent(parent,false);var r=g.AddComponent<LevelRoute>();r.points=points;r.kind=kind;r.bidirectional=true;r.notes=notes;
        }
    }
    [CustomEditor(typeof(ValleySectionV3))]
    public sealed class ValleySectionInspectorV3 : UnityEditor.Editor {
        public override void OnInspectorGUI(){DrawDefaultInspector();if(GUILayout.Button("Regenerate this section"))ValleyGeneratorV3.Generate((ValleySectionV3)target);}
    }
}

