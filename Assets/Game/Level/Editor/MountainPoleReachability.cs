using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using TwinEdge.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace TwinEdge.Levels.Editor
{
    // Uses the production landing and character capsule queries.
    [InitializeOnLoad]
    public static class MountainPoleReachability
    {
        public const string Output = "Docs/Development/Gameplay/V1/MountainPole";
        [Serializable] public class Node { public string id; public bool terrace; public Vector3 center; public Vector2 summitSize; public float rotation; public List<Vector3> safe = new(); }
        [Serializable] public class Edge { public int from, to; public Vector3 pivot, start, end, collision; public float yaw, pitch, heightDelta, centerHorizontalDistance, centerHeightDelta, sampledLandingGap = -1; public bool clear; public string reason; }
        [Serializable] public class Report { public string status = "Collider-tested candidates; NOT continuous gameplay or Relay proof"; public float length, maxPitch; public string search="1m terrace / 2m other support grid, up to 80 nearest pivots / 80 radius-nearest targets / 8 supported departures; rejected pairs may be search-limited"; public List<Node> nodes = new(); public List<Edge> edges = new(); public int direct, shortest = -1, reachableEdges, simpleCandidateRoutes; public bool routeCountCapped; public List<int> route = new(); }
        public static Report Latest;
        static bool pending;
        static MountainPoleReachability() {
            Undo.undoRedoPerformed+=()=>Latest=null;
            MountainGeneratorV3.Regenerated += owner=> {
                Latest=null;
                if(Application.isBatchMode||pending)return;
                pending=true;
                EditorApplication.delayCall+=()=>{pending=false;if(owner&&owner.generated&&!EditorApplication.isPlayingOrWillChangePlaymode)AnalyzeLoaded();};
            };
        }
        static CooperativePole pole;
        static Unity.Netcode.NetworkObject character;
        static bool Clear(Vector3 a, Vector3 b) => pole.ClearPath(a, b, character);
        public static Vector3 Ground(Vector3 p) {
            var hits = Physics.RaycastAll(p + Vector3.up * 100, Vector3.down, 200, ~0, QueryTriggerInteraction.Ignore)
                .Where(h=>!h.collider.GetComponentInParent<PlayerMovement>() && !h.collider.transform.IsChildOf(pole.transform)).OrderBy(h=>h.distance).ToArray();
            return hits.Length > 0 ? hits[0].point : p + Vector3.down * 100;
        }
        static Vector2 Angles(Vector3 d) => new Vector2(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg, Mathf.Asin(Mathf.Clamp(d.normalized.y,-1,1))*Mathf.Rad2Deg);
        public static bool Arc(Vector3 pivot, Vector3 from, Vector3 end, out Vector3 collision) {
            collision = from;
            var a = Angles(from-pivot); var b = Angles(end-pivot);
            if(Mathf.Abs(b.y)>pole.maxPitch) return false;
            // Raise above shoulders, yaw on the actual sphere, then lower to real ground.
            var waypoints = new[] {a, new Vector2(a.x,pole.maxPitch), new Vector2(a.x+Mathf.DeltaAngle(a.x,b.x),pole.maxPitch), new Vector2(a.x+Mathf.DeltaAngle(a.x,b.x),b.y)};
            Vector3 last=from;
            for(int k=1;k<waypoints.Length;k++) {
                int steps=Mathf.CeilToInt((Mathf.Abs(waypoints[k].x-waypoints[k-1].x)+Mathf.Abs(waypoints[k].y-waypoints[k-1].y))*Mathf.Deg2Rad*pole.length/.12f);
                for(int i=1;i<=steps;i++) {
                    Vector2 angle=Vector2.Lerp(waypoints[k-1],waypoints[k],(float)i/steps);
                    Vector3 p=PoleGeometry.Endpoint(pivot,pole.length,angle.x,angle.y);
                    collision=p;
                    if(!Clear(last,p)) return false;
                    last=p;
                }
            }
            return true;
        }
        public static bool StepClear(Vector3 a,Vector3 b)=>Clear(a,b);
        public static bool CarryStep(ref PoleSnapshot pose, Vector3 step, string summit)
            => pole.TryCarry(ref pose, step, summit, character, character);
        public static void WithQueries(Action action) {
            pole=Object.FindFirstObjectByType<CooperativePole>();
            var probe=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Gameplay/Prefabs/V3NetworkPlayer.prefab");
            character=probe.GetComponent<Unity.Netcode.NetworkObject>();Physics.SyncTransforms();
            action();
        }
        public static void InspectCapturedCarry() {
            Load();
            string failure=File.ReadAllText(Output+"/RelayRun9/client-failure.txt");
            string json=failure.Substring(failure.IndexOf("actual=",StringComparison.Ordinal)+7).Split('\n')[0];
            var s=JsonUtility.FromJson<PoleSnapshot>(json);
            var plan=JsonUtility.FromJson<MountainPolePlan>(File.ReadAllText(Output+"/RelayRun9/Plan.json"));
            var goal=plan.actions[63].pivot;var delta=goal-s.PivotPoint;delta.y=0;
            var input=Vector2.ClampMagnitude(new Vector2(delta.x,delta.z)/1.2f,1);
            var step=new Vector3(input.x,0,input.y)*2*Time.fixedDeltaTime;
            WithQueries(()=>{
                var lines=new List<string>{"Captured carry goal="+goal+" a="+s.a+" b="+s.b+" step="+step};
                lines.Add("Before support: a="+pole.SafeLanding(s.a,out var ai)+" "+ai+" b="+pole.SafeLanding(s.b,out var bi)+" "+bi);
                lines.Add("After support: a="+pole.SafeLanding(s.a+step,out ai)+" "+ai+" b="+pole.SafeLanding(s.b+step,out bi)+" "+bi);
                lines.Add("Sweep a="+Clear(s.a,s.a+step)+" b="+Clear(s.b,s.b+step));
                foreach(var point in new[]{s.a,s.b})foreach(var offset in new[]{Vector3.zero,Vector3.right*.45f,Vector3.left*.45f,Vector3.forward*.45f,Vector3.back*.45f}){
                    var hits=Physics.RaycastAll(point+offset+Vector3.up*.35f,Vector3.down,.65f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider!=pole.supportCollider&&!h.collider.transform.IsChildOf(pole.transform)&&h.collider.GetComponentInParent<PlayerMovement>()==null).OrderBy(h=>h.distance).ToArray();
                    lines.Add("Ray "+point+" + "+offset+" => "+(hits.Length==0?"none":hits[0].point+" normal="+hits[0].normal+" collider="+hits[0].collider.name));
                }
                File.WriteAllLines(Output+"/CapturedCarry.txt",lines);
            });
        }
        public static void VerifyCapturedLanding() {
            Load();
            var trace=JsonUtility.FromJson<MountainPoleCollision>(File.ReadAllLines(Output+"/RelayRun6/server-collisions.jsonl")[0]);
            WithQueries(()=> {
                bool blocked=!Clear(trace.from,trace.to);
                bool support=pole.SafeLanding(trace.from,out var id);
                bool contact=(bool)typeof(CooperativePole).GetMethod("LandingContact",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pole,new object[]{trace.from,trace.to,character});
                if(!blocked||!support||id!="Peak_04"||!contact)throw new Exception("Captured landing regression failed");
                File.WriteAllText(Output+"/CapturedLandingRegression.txt",$"PASS actual RelayRun6 rejected coordinates: capsule blocked={blocked}, five-point support={support} on {id}, production walkable landing contact={contact}. Previous authoritative pose retained; radius error={Mathf.Abs(Vector3.Distance(trace.pivot,trace.from)-pole.length)}m.");
            });
        }
        [MenuItem("Tools/Twin Edge/V3 Mountain/Analyze Pole Reachability")]
        public static void AnalyzeLoaded() { Analyze("Current"); }
        public static void Baseline() { Load(); Analyze("Baseline"); }
        public static void Load() {
            EditorSceneManager.OpenScene("Assets/Game/Gameplay/Scenes/V3Gameplay.unity");
            EditorSceneManager.OpenScene(MountainGeneratorV3.ScenePath,OpenSceneMode.Additive);
            Physics.SyncTransforms();
        }
        public static Report Analyze(string name) {
            Directory.CreateDirectory(Output);
            pole=Object.FindFirstObjectByType<CooperativePole>();
            if(!pole) {
                if(!Object.FindFirstObjectByType<MountainSectionV3>())throw new InvalidOperationException("Open LevelBlockoutV3 first.");
                pole=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Gameplay/Prefabs/V3CooperativePole.prefab").GetComponent<CooperativePole>();
            }
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Gameplay/Prefabs/V3NetworkPlayer.prefab");
            character=prefab.GetComponent<Unity.Netcode.NetworkObject>();
            Physics.SyncTransforms();
            try {
                var r=new Report {length=pole.length,maxPitch=pole.maxPitch}; var c=pole.mountain;
                foreach(var p in new[]{c.start}.Concat(c.smallPeaks).Concat(new[]{c.destination})) {
                    Vector2 xz=p==c.start||p==c.destination?p.position:c.clusterCenter+Vector2.Scale(p.position,c.clusterSize);
                    var n=new Node {id=p.id,terrace=p.landingTerrace,center=new Vector3(xz.x,p.height,xz.y),summitSize=p.summitSize,rotation=p.rotation};
                    float radius=Mathf.Max(p.summitSize.x,p.summitSize.y)*.5f;
                    float grid=p.landingTerrace?1:2;
                    for(float x=-radius;x<=radius;x+=grid) for(float z=-radius;z<=radius;z+=grid) {
                        var feet=Ground(n.center+new Vector3(x,0,z))+Vector3.up*.12f;
                        if((!p.landingTerrace||(Mathf.Abs(feet.y-p.height-.12f)<.05f&&new Vector2(x,z).magnitude<=17f))&&pole.SafeLanding(feet,out var id)&&id==p.id&&Clear(feet,feet)) n.safe.Add(feet);
                    }
                    r.nodes.Add(n);
                }
                var departures = new Dictionary<Vector3, List<Vector3>>[r.nodes.Count];
                for (int i = 0; i < departures.Length; i++) departures[i] = new Dictionary<Vector3, List<Vector3>>();
                for(int i=0;i<r.nodes.Count;i++) for(int j=0;j<r.nodes.Count;j++) {
                    if(i==j) continue;
                    if(!Application.isBatchMode&&EditorUtility.DisplayCancelableProgressBar("Mountain pole collider candidates",r.nodes[i].id+" -> "+r.nodes[j].id,(float)(i*r.nodes.Count+j)/(r.nodes.Count*r.nodes.Count)))throw new OperationCanceledException("Reachability analysis cancelled; no scene edits.");
                    var e=new Edge {from=i,to=j,reason="No rigid-radius safe landing pair", centerHorizontalDistance=Vector2.Distance(new Vector2(r.nodes[i].center.x,r.nodes[i].center.z),new Vector2(r.nodes[j].center.x,r.nodes[j].center.z)),centerHeightDelta=r.nodes[j].center.y-r.nodes[i].center.y};
                    if(Vector3.Distance(r.nodes[i].center,r.nodes[j].center)>pole.length+(r.nodes[i].summitSize.magnitude+r.nodes[j].summitSize.magnitude)*.5f) {r.edges.Add(e);continue;}
                    float minGap=float.PositiveInfinity;
                    foreach(var a in r.nodes[i].safe)foreach(var b in r.nodes[j].safe)minGap=Mathf.Min(minGap,new Vector2(a.x-b.x,a.z-b.z).sqrMagnitude);
                    e.sampledLandingGap=float.IsPositiveInfinity(minGap)?-1:Mathf.Sqrt(minGap);
                    foreach(var pivot in r.nodes[i].safe.OrderBy(p=>(p-r.nodes[j].center).sqrMagnitude).Take(80)) {
                        if (!departures[i].TryGetValue(pivot, out var starts))
                        {
                            starts = new List<Vector3>();
                            foreach (var candidate in r.nodes[i].safe.Where(p=>Mathf.Abs((p-pivot).magnitude-pole.length)<1.2f))
                            {
                                var angle = Angles(candidate-pivot);
                                if (r.nodes[i].terrace) angle.y = 0;
                                var point = PoleGeometry.Endpoint(pivot,pole.length,angle.x,angle.y);
                                if (!pole.SafeLanding(point,out var sid) || sid!=r.nodes[i].id ||
                                    !Clear(point,point) || !Arc(pivot,point,PoleGeometry.Endpoint(pivot,pole.length,angle.x,pole.maxPitch),out _))
                                    continue;
                                starts.Add(point);
                                if (starts.Count == 8) break;
                            }
                            departures[i].Add(pivot, starts);
                        }
                        if(starts.Count==0){e.reason="No supported departure endpoint and clear ascent";continue;}
                        foreach(var target in r.nodes[j].safe.Where(p=>Mathf.Abs((p-pivot).magnitude-pole.length)<1.2f&&(!r.nodes[j].terrace||Vector2.Distance(new Vector2(p.x,p.z),new Vector2(r.nodes[j].center.x,r.nodes[j].center.z))>15)).OrderBy(p=>Mathf.Abs((p-pivot).magnitude-pole.length)).Take(80)) {
                            var angles=Angles(target-pivot);
                            // Fix the intended elevation before projecting onto the rigid sphere.
                            // Scaling a diagonal sample to length can accumulate a vertical offset after exchanges.
                            angles.y=Mathf.Asin(Mathf.Clamp((target.y-pivot.y)/pole.length,-1,1))*Mathf.Rad2Deg;
                            var end=PoleGeometry.Endpoint(pivot,pole.length,angles.x,angles.y);
                            if(Mathf.Abs(angles.y)>pole.maxPitch){e.reason="Pitch limit";continue;}
                            if(!pole.SafeLanding(end,out var id)||id!=r.nodes[j].id){e.reason="Target five-point support / summit gate";continue;}
                            if(!Clear(end,end)){e.reason="Target capsule overlap";continue;}
                            if(r.nodes[j].terrace) {
                                float arrivalRadius=Vector2.Distance(new Vector2(end.x,end.z),new Vector2(r.nodes[j].center.x,r.nodes[j].center.z));
                                // A central arrival strands the pivot: a full second endpoint cannot fit on this summit.
                                if(arrivalRadius<16.2f||arrivalRadius>16.9f)continue;
                                float clearance=end.y-Ground(end).y;
                                if(clearance<.10f||clearance>.16f)continue;
                            }
                            e.pivot=pivot;e.end=end;e.pitch=angles.y;e.heightDelta=end.y-pivot.y;
                            if(!Arc(pivot,PoleGeometry.Endpoint(pivot,pole.length,angles.x,pole.maxPitch),end,out e.collision)){e.reason="Arrival capsule collision";continue;}
                            e.reason="No supported source endpoint / clear departure arc";
                            // Source endpoint must itself have real support. Edge candidates do not assume an airborne start.
                            foreach(var from in starts) {
                                var s=Angles(from-pivot);
                                e.start=from;e.yaw=Mathf.DeltaAngle(s.x,angles.x);
                                if(Arc(pivot,from,end,out e.collision)) {e.clear=true;e.reason="Collider-tested isolated arc; chained arrival not proven";break;}
                                e.reason="Capsule collision along sphere";
                            }
                            if(e.clear)break;
                        }
                        if(e.clear)break;
                    }
                    if(e.clear)e.collision=Vector3.zero;
                    r.edges.Add(e);
                    if(e.clear)Debug.Log("POLE_EDGE "+r.nodes[i].id+" -> "+r.nodes[j].id);
                }
                var parents=Enumerable.Repeat(-1,r.nodes.Count).ToArray();var q=new Queue<int>();q.Enqueue(0);parents[0]=0;
                while(q.Count>0) {int i=q.Dequeue();foreach(var e in r.edges.Where(e=>e.clear&&e.from==i)) if(parents[e.to]<0) {parents[e.to]=i;q.Enqueue(e.to);}}
                if(parents[^1]>=0) {int i=r.nodes.Count-1;while(i!=0){r.route.Insert(0,i);i=parents[i];}r.route.Insert(0,0);r.shortest=r.route.Count-1;}
                void CountRoutes(int node,HashSet<int> seen) { if(r.simpleCandidateRoutes>=1000){r.routeCountCapped=true;return;}if(node==r.nodes.Count-1){r.simpleCandidateRoutes++;return;}foreach(var e in r.edges.Where(e=>e.clear&&e.from==node))if(seen.Add(e.to)){CountRoutes(e.to,seen);seen.Remove(e.to);} }
                CountRoutes(0,new HashSet<int>{0});
                r.direct=r.edges.Count(e=>e.from==0&&e.clear);r.reachableEdges=r.edges.Count(e=>e.clear);
                Latest=r;File.WriteAllText(Output+"/"+name+".json",JsonUtility.ToJson(r,true));
                Debug.Log($"MOUNTAIN_POLE {name}: nodes={r.nodes.Count}, edges={r.reachableEdges}, direct={r.direct}, candidate hops={r.shortest}");
                return r;
            } finally {EditorUtility.ClearProgressBar();}
        }
    }
}
