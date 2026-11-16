using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace TwinEdge.Levels.Testing
{
    [Serializable] public class TraversalPreset
    {
        public string name;
        public string[] ids;
        public Mesh mesh;
        public Material[] materials;
    }
    [Serializable] public class TraversalAttempt
    {
        public string utc, preset, from, to, result, evidence = "Human-triggered simplified arc; not official pole mechanic";
        public float horizontal, heightDifference, elapsed, simulatedElapsed;
        public Vector3 expected, actual;
        public bool success, fell, wallCollision;
        public TraversalParameters parameters;
    }
    [Serializable] public class TraversalSession
    {
        public string utc, scene, configHash, geometryHash, version="V3 traversal prototype 1", visibility="Not measured", complexity="Not measured";
        public float elapsed;
        public int attempts, successes;
        public List<TraversalAttempt> records;
        public List<TraversalSnapshot> comparisons;
        public List<TraversalStatistic> statistics;
    }
    [Serializable] public class TraversalStatistic
    {
        public string preset, route, target, failure;
        public int attempts, successes;
        public float totalSeconds;
    }
    [Serializable] public class TraversalSnapshot
    {
        public string preset;
        public TraversalParameters parameters;
        public TraversalGraph graph;
        public List<TraversalNode> surfaces;
    }
    public sealed class MountainTraversalTest : MonoBehaviour
    {
        public MountainConfigV3 sourceConfig;
        public string configHash, geometryHash;
        public MeshFilter terrainFilter;
        public MeshCollider terrainCollider;
        public MeshRenderer terrainRenderer;
        public TraversalPreset[] presets;
        public TraversalParameters parameters = new TraversalParameters();
        public int presetIndex=2;
        public bool drawConnections=true, drawLandings=true, drawOccupancy=true;
        public List<TraversalNode> nodes=new List<TraversalNode>();
        public TraversalGraph graph;
        public List<TraversalAttempt> records=new List<TraversalAttempt>();
        public List<TraversalSnapshot> comparisons=new List<TraversalSnapshot>();
        public string status="Ready", savedPath;
        public int targetIndex=1, sourceIndex;
        CharacterController player;
        [SerializeField] Camera view;
        public Camera RuntimeCamera => view;
        public bool Initialized { get; private set; }
        string initializationError;
        Transform body;
        LineRenderer line;
        float yaw,pitch=15,vertical,flightTime,flightStart,sessionStart,unsupported;
        bool firstPerson,flight,wall,fall,locked;
        Vector3 launch,end;
        TraversalAttempt current;
        int safeIndex;
        Vector2 scroll;
        CursorLockMode oldLock;
        bool oldVisible;
        public string PresetName => presets[presetIndex].name;

        void Awake()
        {
            try {
                if(view==null)view=GetComponentInChildren<Camera>(true);
                if(view==null) {
                    var go=new GameObject("TestCamera");go.transform.SetParent(transform,false);
                    view=go.AddComponent<Camera>();
                }
                view.gameObject.SetActive(true);view.enabled=true;view.tag="MainCamera";
                view.cullingMask=~0;view.targetTexture=null;view.targetDisplay=0;view.rect=new Rect(0,0,1,1);
                view.nearClipPlane=.1f;view.farClipPlane=3000;
                var data=view.GetUniversalAdditionalCameraData();data.renderType=CameraRenderType.Base;data.SetRenderer(-1);
                if(view.GetComponent<AudioListener>()==null)view.gameObject.AddComponent<AudioListener>();
                if(!view.isActiveAndEnabled)throw new InvalidOperationException("Test camera is inactive");
            }catch(Exception e){ReportInitializationFailure(e);}
        }
        void ReportInitializationFailure(Exception e)
        {
            Initialized=false;initializationError="Traversal initialization failed: "+e.Message;
            status=initializationError;Debug.LogError(initializationError+"\n"+e,this);
        }
        void Start()
        {
            oldLock=Cursor.lockState;oldVisible=Cursor.visible;sessionStart=Time.realtimeSinceStartup;
            if(initializationError!=null)return;
            try {
            if(view==null||!view.isActiveAndEnabled)throw new InvalidOperationException("No active runtime camera");
            var go=new GameObject("TestPlayer_2m_x_0.9m");go.transform.SetParent(transform);go.layer=2;
            player=go.AddComponent<CharacterController>();ApplyCapsule();
            var visual=GameObject.CreatePrimitive(PrimitiveType.Capsule);visual.name="Scale reference";visual.transform.SetParent(go.transform,false);visual.transform.localPosition=Vector3.up;visual.transform.localScale=new Vector3(parameters.radius*2,parameters.height/2,parameters.radius*2);Destroy(visual.GetComponent<Collider>());visual.layer=2;body=visual.transform;
            var path=new GameObject("HypothesisArc");path.transform.SetParent(transform);line=path.AddComponent<LineRenderer>();line.widthMultiplier=.16f;line.material=new Material(Shader.Find("Sprites/Default"));line.startColor=Color.yellow;line.endColor=Color.cyan;
            SetPreset(presetIndex);
            if(!nodes[0].hasLanding)throw new InvalidOperationException("Start summit has no safe spawn");
            Initialized=true;LateUpdate();Lock(true);
            }catch(Exception e){ReportInitializationFailure(e);}
        }
        void ApplyCapsule()
        {
            parameters.radius=Mathf.Max(.1f,parameters.radius);parameters.height=Mathf.Max(parameters.radius*2,parameters.height);
            player.height=parameters.height;player.radius=parameters.radius;player.center=Vector3.up*parameters.height/2;player.slopeLimit=parameters.slope;player.stepOffset=.25f;player.skinWidth=.04f;
        }
        public void SetPreset(int index)
        {
            if(flight)Finish("PresetChanged");
            presetIndex=Mathf.Clamp(index,0,presets.Length-1);var preset=presets[presetIndex];
            terrainCollider.sharedMesh=null;terrainFilter.sharedMesh=preset.mesh;terrainCollider.sharedMesh=preset.mesh;terrainRenderer.sharedMaterials=preset.materials;Physics.SyncTransforms();
            nodes.Clear();AddNode(sourceConfig.start,false);AddNode(sourceConfig.destination,false);
            foreach(var p in sourceConfig.smallPeaks)if(preset.ids.Contains(p.id))AddNode(p,true);
            Reanalyze();safeIndex=0;sourceIndex=0;targetIndex=1;if(player!=null)Respawn(0);
        }
        void AddNode(MountainPeakV3 p,bool normalized)
        {
            var xz=normalized?sourceConfig.clusterCenter+Vector2.Scale(p.position,sourceConfig.clusterSize):p.position;
            nodes.Add(new TraversalNode{id=p.id,authored=new Vector3(xz.x,p.height,xz.y),size=p.summitSize});
        }
        public void Reanalyze()
        {
            foreach(var n in nodes)TraversalSurface.Sample(n,terrainCollider,parameters);
            graph=TraversalGraph.Analyze(nodes,parameters);
            comparisons.Add(new TraversalSnapshot{preset=PresetName,parameters=parameters.Copy(),graph=graph,surfaces=JsonUtility.FromJson<NodeList>(JsonUtility.ToJson(new NodeList{values=nodes})).values});
        }
        [Serializable] class NodeList { public List<TraversalNode> values; }
        public void Respawn(int index)
        {
            if(flight)Finish("ManualReset");
            var n=nodes[index];if(!n.hasLanding){status="No sampled safe spawn on "+n.id;return;}
            player.enabled=false;player.transform.position=n.landing+Vector3.up*.12f;player.enabled=true;vertical=0;unsupported=0;safeIndex=index;sourceIndex=index;status="Respawn: "+n.id;
        }
        int GroundNode(out RaycastHit hit)
        {
            if(!terrainCollider.Raycast(new Ray(player.transform.position+Vector3.up*.3f,Vector3.down),out hit,.65f))return -1;
            Vector3 point=hit.point; for(int i=0;i<nodes.Count;i++)if(nodes[i].hasLanding&&nodes[i].safe.Any(v=>TraversalParameters.Horizontal(v,point)<2 && Mathf.Abs(v.y-point.y)<.7f))return i;
            return -1;
        }
        public void Launch()
        {
            int ground=GroundNode(out _);
            if(flight||ground!=sourceIndex||sourceIndex==targetIndex||!nodes[targetIndex].hasLanding){status="Stand on selected source; target needs a sampled safe landing";return;}
            launch=player.transform.position;end=nodes[targetIndex].landing+Vector3.up*.08f;
            if(!parameters.Allows(launch,end)){status="Outside hypothesis envelope";return;}
            flightTime=0;flightStart=Time.realtimeSinceStartup;wall=false;fall=false;flight=true;vertical=0;
            current=new TraversalAttempt{utc=DateTime.UtcNow.ToString("O"),preset=PresetName,from=nodes[ground].id,to=nodes[targetIndex].id,horizontal=TraversalParameters.Horizontal(launch,end),heightDifference=end.y-launch.y,expected=end,parameters=parameters.Copy()};
            status="Experimental parabola running";
        }
        void Finish(string reason)
        {
            if(!flight)return;
            if(reason=="Landed"&&!TraversalSurface.Safe(terrainCollider,player.transform.position,current.parameters,out _,out _))reason="UnsafeLanding";
            flight=false;current.actual=player.transform.position;current.elapsed=Time.realtimeSinceStartup-flightStart;current.simulatedElapsed=flightTime;current.result=reason;current.success=reason=="Landed";current.wallCollision=wall;current.fell=fall;records.Add(current);status=reason;Save();
        }
        void Update()
        {
            if(!Initialized)return;
            var kb=Keyboard.current;var mouse=Mouse.current;
            if(kb!=null) {
                if(kb.escapeKey.wasPressedThisFrame)Lock(false);
                if(kb.qKey.wasPressedThisFrame)firstPerson=!firstPerson;
                if(kb.rKey.wasPressedThisFrame)Respawn(kb.shiftKey.isPressed?0:safeIndex);
                if(kb.enterKey.wasPressedThisFrame)Launch();
                if(kb.tabKey.wasPressedThisFrame)targetIndex=(targetIndex+1)%nodes.Count;
                if(kb.f5Key.wasPressedThisFrame)Save();
            }
            if(mouse!=null && mouse.rightButton.wasPressedThisFrame)Lock(true);
            if(locked&&mouse!=null){var delta=mouse.delta.ReadValue();yaw+=delta.x*.15f;pitch=Mathf.Clamp(pitch-delta.y*.15f,-70,80);}
            float dt=Mathf.Min(Time.deltaTime,.05f);
            if(flight) {
                flightTime+=dt;float t=Mathf.Clamp01(flightTime/Mathf.Max(.2f,current.parameters.duration));
                Vector3 desired=flightTime>current.parameters.duration?player.transform.position:current.parameters.Arc(launch,end,t);
                Vector3 delta=desired-player.transform.position;
                int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.2f));
                for(int step=0;step<steps;step++) {
                    var flags=player.Move(delta/steps);if((flags&CollisionFlags.Sides)!=0){wall=true;Finish("WallCollision");break;}
                    if((flags&CollisionFlags.Above)!=0){wall=true;Finish("CeilingCollision");break;}
                    if((flags&CollisionFlags.Below)!=0 && t>.05f) {
                        int landed=GroundNode(out _);Finish(landed==targetIndex?"Landed":"WrongSurface");break;
                    }
                }
                if(flight&&t>=1) {
                    player.Move(Vector3.down*current.parameters.gravity*dt);
                    int landed=GroundNode(out _);
                    if(player.isGrounded)Finish(landed==targetIndex?"Landed":"WrongSurface");
                    else if(flightTime>current.parameters.duration+2){fall=true;Finish("MissedLanding");}

                }
            } else {
                Vector2 input=Vector2.zero;
                if(locked&&kb!=null)input=new Vector2((kb.dKey.isPressed?1:0)-(kb.aKey.isPressed?1:0),(kb.wKey.isPressed?1:0)-(kb.sKey.isPressed?1:0));
                if(player.isGrounded&&vertical<0)vertical=-2;
                if(locked&&kb!=null&&kb.spaceKey.wasPressedThisFrame&&player.isGrounded)vertical=Mathf.Sqrt(2*parameters.gravity*parameters.jumpHeight);
                vertical-=parameters.gravity*dt;
                player.Move((Quaternion.Euler(0,yaw,0)*new Vector3(input.normalized.x,0,input.normalized.y)*parameters.speed+Vector3.up*vertical)*dt);
            }
            int node=GroundNode(out _);
            if(node>=0&&!flight) {
                if(TraversalSurface.Safe(terrainCollider,player.transform.position,parameters,out _,out _))safeIndex=node;
                unsupported=0;
            } else unsupported+=dt;
            // Summit-relative kill threshold prevents the low connected geology becoming a bypass.
            float cutoff=nodes.Min(n=>n.authored.y)-22;
            if(player.transform.position.y<cutoff || (!flight&&player.isGrounded&&node<0&&unsupported>1.5f)) {
                if(flight){fall=true;Finish("Fell");}status="Fall / non-traversal surface";Respawn(safeIndex);
            }
            Preview();
        }
        void LateUpdate()
        {
            if(!Initialized||view==null||player==null||body==null)return;body.gameObject.SetActive(!firstPerson);
            var rotation=Quaternion.Euler(pitch,yaw,0);var eye=player.transform.position+Vector3.up*(parameters.height*.85f);
            var position=eye;
            if(!firstPerson) {
                Vector3 dir=-(rotation*Vector3.forward);float distance=5;
                if(Physics.SphereCast(eye,.2f,dir,out var hit,5,~(1<<2),QueryTriggerInteraction.Ignore))distance=Mathf.Max(0,hit.distance-.1f);
                position=eye+dir*distance;
            }
            view.transform.SetPositionAndRotation(position,rotation);
        }
        void Preview()
        {
            line.positionCount=41;var a=player.transform.position;var b=nodes[targetIndex].landing+Vector3.up*.08f;
            for(int i=0;i<=40;i++)line.SetPosition(i,parameters.Arc(a,b,i/40f));
        }
        void Lock(bool value){locked=value;Cursor.lockState=value?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!value;}
        public void Save()
        {
            try {
                string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Development/Level/V3/TraversalTests"));Directory.CreateDirectory(directory);
                var session=new TraversalSession{utc=DateTime.UtcNow.ToString("O"),scene=gameObject.scene.path,configHash=configHash,geometryHash=geometryHash,elapsed=Time.realtimeSinceStartup-sessionStart,attempts=records.Count,successes=records.Count(r=>r.success),records=records,comparisons=comparisons,statistics=records.GroupBy(r=>new{r.preset,r.from,r.to,r.result}).Select(g=>new TraversalStatistic{preset=g.Key.preset,route=g.Key.from+" > "+g.Key.to,target=g.Key.to,failure=g.Key.result,attempts=g.Count(),successes=g.Count(r=>r.success),totalSeconds=g.Sum(r=>r.elapsed)}).ToList()};
                if(string.IsNullOrEmpty(savedPath))savedPath=Path.Combine(directory,"Session-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".json");
                File.WriteAllText(savedPath,JsonUtility.ToJson(session,true));
            }catch(Exception e){status="Save failed: "+e.Message;Debug.LogException(e);}
        }
        void OnDisable(){if(player!=null){if(flight)Finish("TestStopped");Save();Cursor.lockState=oldLock;Cursor.visible=oldVisible;}if(line!=null)Destroy(line.material);}
        void OnGUI()
        {
            if(initializationError!=null){GUI.Box(new Rect(10,10,700,65),initializationError);return;}
            if(!Initialized)return;
            GUILayout.BeginArea(new Rect(10,10,440,Screen.height-20),GUI.skin.box);scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("V3 Mountain / "+PresetName+" / HYPOTHESIS ONLY");
            GUILayout.Label("WASD / Space / Mouse / Q view / Esc menu / RMB capture\nTab target / Enter arc / R safe / Shift+R start / F5 save");
            GUILayout.Label($"Position {player.transform.position:F1} | current: {CurrentId()}\nCapsule {parameters.height:F2}m / radius {parameters.radius:F2}m");
            GUILayout.BeginHorizontal();for(int i=0;i<presets.Length;i++){int next=i;if(GUILayout.Button(presets[i].name))SetPreset(next);}GUILayout.EndHorizontal();
            GUILayout.Label("Source (setup respawn excluded from attempts)");
            sourceIndex=Selection(sourceIndex);if(GUILayout.Button("Setup at source"))Respawn(sourceIndex);
            GUILayout.Label("Target");targetIndex=Selection(targetIndex);
            var a=nodes[sourceIndex].authored;var b=nodes[targetIndex].authored;
            GUILayout.Label($"Distance {TraversalParameters.Horizontal(a,b):F1}m | rise {b.y-a.y:F1}m\nTarget area sample {nodes[targetIndex].sampledArea:F1}m² | 2 capsules {nodes[targetIndex].twoCapsules}");
            parameters.reach=Slider("Reach",parameters.reach,20,250);parameters.maxRise=Slider("Rise",parameters.maxRise,0,120);parameters.maxDrop=Slider("Drop",parameters.maxDrop,0,160);
            parameters.arcHeight=Slider("Arc lift",parameters.arcHeight,5,120);parameters.duration=Slider("Duration",parameters.duration,.5f,8);
            parameters.speed=Slider("Walk speed",parameters.speed,1,15);parameters.jumpHeight=Slider("Jump height",parameters.jumpHeight,.3f,5);
            parameters.radius=Slider("Capsule radius",parameters.radius,.2f,1);parameters.height=Slider("Capsule height",parameters.height,1,3);
            if(GUILayout.Button("Apply capsule / recompute graph and samples")){ApplyCapsule();body.localPosition=Vector3.up*parameters.height/2;body.localScale=new Vector3(parameters.radius*2,parameters.height/2,parameters.radius*2);Reanalyze();}
            if(GUILayout.Button("Run experimental arc"))Launch();
            GUILayout.Label(status+"\n"+ $"Attempts {records.Count} | successes {records.Count(r=>r.success)}");
            GUILayout.Label("Nearby coordinate candidates: "+string.Join(", ",nodes.Where(n=>n.id!=CurrentId()&&parameters.Allows(player.transform.position,n.authored)).OrderBy(n=>Vector3.Distance(n.authored,player.transform.position)).Take(6).Select(n=>n.id)));
            GUILayout.Label($"Static: edges {graph.edges}, branches (outdegree ≥3) {graph.branches}\nIsolated: {string.Join(",",graph.isolated)}\nUnreachable: {string.Join(",",graph.unreachable)}\nFewest: {string.Join(" > ",graph.fewestMoves)}\nConservative heuristic: {string.Join(" > ",graph.conservative)}\nHeight preference: {string.Join(" > ",graph.highVariation)}\nLeft: {string.Join(" > ",graph.left)}\nRight: {string.Join(" > ",graph.right)}\nReturn: {string.Join(" > ",graph.returnRoute)}");
            foreach(var record in records.Skip(Mathf.Max(0,records.Count-5)))GUILayout.Label(record.from+" > "+record.to+": "+record.result);
            if(GUILayout.Button("Save JSON"))Save();if(!string.IsNullOrEmpty(savedPath))GUILayout.Label(savedPath);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        string CurrentId(){int i=GroundNode(out _);return i<0?"air / non-landing":nodes[i].id;}
        int Selection(int index){GUILayout.BeginHorizontal();if(GUILayout.Button("<",GUILayout.Width(35)))index=(index+nodes.Count-1)%nodes.Count;GUILayout.Label(nodes[index].id);if(GUILayout.Button(">",GUILayout.Width(35)))index=(index+1)%nodes.Count;GUILayout.EndHorizontal();return index;}
        static float Slider(string label,float value,float min,float max){GUILayout.Label(label+": "+value.ToString("F1")+" (hypothesis)");return GUILayout.HorizontalSlider(value,min,max);}
        void OnDrawGizmos()
        {
            if(nodes==null)return;
            for(int i=0;i<nodes.Count;i++) {
                var n=nodes[i];Gizmos.color=i==0?Color.green:i==1?Color.magenta:Color.cyan;Gizmos.DrawWireSphere(n.authored,2);
                if(drawLandings){Gizmos.color=new Color(0,1,0,.5f);foreach(var v in n.safe)Gizmos.DrawCube(v,new Vector3(.4f,.08f,.4f));}
                if(drawOccupancy&&n.hasLanding){DrawCapsule(n.landing);if(n.twoCapsules)DrawCapsule(n.second);}
                if(drawConnections)for(int j=0;j<nodes.Count;j++)if(i!=j){bool allowed=parameters.Allows(n.authored,nodes[j].authored);if(allowed||i==sourceIndex){Gizmos.color=allowed?new Color(0,1,1,.3f):new Color(1,0,0,.2f);Gizmos.DrawLine(n.authored,nodes[j].authored);}}
#if UNITY_EDITOR
                UnityEditor.Handles.Label(n.authored+Vector3.up*3,n.id+" / "+(presets!=null&&presets.Length>presetIndex?PresetName:""));
#endif
            }
        }
        void DrawCapsule(Vector3 foot){Gizmos.color=Color.yellow;Gizmos.DrawWireSphere(foot+Vector3.up*parameters.radius,parameters.radius);Gizmos.DrawWireSphere(foot+Vector3.up*(parameters.height-parameters.radius),parameters.radius);Gizmos.DrawLine(foot,foot+Vector3.up*parameters.height);}
    }
}

