using UnityEditor;
using UnityEngine;
namespace TwinEdge.Levels.Editor {
 public sealed class MountainPoleReviewWindow : EditorWindow {
  bool safe=true, failed; int selected,from,to;string[] summitNames;
  MountainPoleReachability.Report cached;
  readonly System.Collections.Generic.Dictionary<MountainPoleReachability.Node,Vector3[]> crosses=new();
  Vector3[] reachableLines,rejectedLines;
  void Cache(MountainPoleReachability.Report r){
   if(cached==r)return;cached=r;crosses.Clear();
   summitNames=r.nodes.ConvertAll(n=>n.id).ToArray();selected=Mathf.Max(0,r.edges.FindIndex(e=>e.clear));from=r.edges[selected].from;to=r.edges[selected].to;
   foreach(var n in r.nodes){var lines=new Vector3[n.safe.Count*4];for(int i=0;i<n.safe.Count;i++){var p=n.safe[i];lines[i*4]=p-Vector3.right*.22f;lines[i*4+1]=p+Vector3.right*.22f;lines[i*4+2]=p-Vector3.forward*.22f;lines[i*4+3]=p+Vector3.forward*.22f;}crosses[n]=lines;}
   var good=new System.Collections.Generic.List<Vector3>();var bad=new System.Collections.Generic.List<Vector3>();
   foreach(var e in r.edges){var lines=e.clear?good:bad;lines.Add(r.nodes[e.from].center);lines.Add(r.nodes[e.to].center);}
   reachableLines=good.ToArray();rejectedLines=bad.ToArray();
  }
  [MenuItem("Tools/Twin Edge/V3 Mountain/Pole Reachability Review")]
  public static void Open()=>GetWindow<MountainPoleReviewWindow>("Mountain Pole");
  void OnEnable()=>SceneView.duringSceneGui+=Draw;
  void OnDisable(){SceneView.duringSceneGui-=Draw;SceneView.RepaintAll();}
  void OnGUI(){
   EditorGUILayout.HelpBox("Collider-tested isolated arc candidates. Yellow graph routes do not prove chained roles or Relay play. Re-analyze after terrain/config changes.",MessageType.Info);
   if(GUILayout.Button("Analyze loaded Gameplay + V3 colliders")) MountainPoleReachability.AnalyzeLoaded();
   safe=EditorGUILayout.Toggle("Actual supported samples (1-2m grid)",safe);failed=EditorGUILayout.Toggle("Rejected connections",failed);
   var r=MountainPoleReachability.Latest;if(r==null)return;Cache(r);
   GUILayout.Label($"{r.nodes.Count} summits | {r.reachableEdges} directed candidates | {r.direct} from Start | {r.shortest} candidate hops | {r.simpleCandidateRoutes} candidate routes");
   from=EditorGUILayout.Popup("From summit",from,summitNames);to=EditorGUILayout.Popup("To summit",to,summitNames);
   if(from==to){EditorGUILayout.HelpBox("Select two different summits.",MessageType.Info);return;}
   selected=r.edges.FindIndex(e=>e.from==from&&e.to==to);
   var e=r.edges[selected];GUILayout.Label($"{r.nodes[e.from].id} -> {r.nodes[e.to].id}\n{e.reason}\nR={r.length}m, yaw={e.yaw:F1}, pitch={e.pitch:F1}, rise={e.heightDelta:F1}m");
   if(GUILayout.Button("Mountain Top"))Frame(true);if(GUILayout.Button("Mountain Perspective"))Frame(false);
   SceneView.RepaintAll();
  }
  void Frame(bool top){var o=Object.FindFirstObjectByType<MountainSectionV3>();if(o)LevelViewCamera.Frame(MountainSectionV3Inspector.FocusBounds(o,0),Quaternion.Euler(top?90:38,-38,0),top);}
  void Draw(SceneView view){
   var r=MountainPoleReachability.Latest;if(r==null||!view.drawGizmos)return;Cache(r);
   foreach(var n in r.nodes){Handles.color=Color.white;Handles.Label(n.center+Vector3.up*4,n.id);if(safe){Handles.color=Color.green;if(crosses[n].Length>0)Handles.DrawLines(crosses[n]);}}
   Handles.color=Color.cyan;if(reachableLines.Length>0)Handles.DrawLines(reachableLines);if(failed&&rejectedLines.Length>0){Handles.color=new Color(1,.2f,.2f,.15f);Handles.DrawLines(rejectedLines);}
   Handles.color=Color.yellow;for(int i=1;i<r.route.Count;i++)Handles.DrawDottedLine(r.nodes[r.route[i-1]].center+Vector3.up*5,r.nodes[r.route[i]].center+Vector3.up*5,3);
   if(from==to)return;var edge=r.edges[Mathf.Min(selected,r.edges.Count-1)];if(edge.start==Vector3.zero)return;
   Handles.color=Color.magenta;Handles.DrawWireDisc(edge.pivot,Vector3.up,r.length);Handles.Label(edge.pivot,"Candidate pivot / exchange (unverified)");
   Handles.DrawLine(edge.pivot,edge.end);
   Vector3 a=edge.start-edge.pivot,b=edge.end-edge.pivot;
   float startYaw=Mathf.Atan2(a.x,a.z)*Mathf.Rad2Deg,endYaw=startYaw+Mathf.DeltaAngle(startYaw,Mathf.Atan2(b.x,b.z)*Mathf.Rad2Deg);
   float startPitch=Mathf.Asin(a.normalized.y)*Mathf.Rad2Deg,endPitch=Mathf.Asin(b.normalized.y)*Mathf.Rad2Deg;
   var angles=new[]{new Vector2(startYaw,startPitch),new Vector2(startYaw,r.maxPitch),new Vector2(endYaw,r.maxPitch),new Vector2(endYaw,endPitch)};
   Handles.color=Color.cyan;
   for(int leg=1;leg<angles.Length;leg++) {
    var points=new Vector3[65];for(int i=0;i<points.Length;i++){var angle=Vector2.Lerp(angles[leg-1],angles[leg],i/64f);points[i]=TwinEdge.Gameplay.PoleGeometry.Endpoint(edge.pivot,r.length,angle.x,angle.y);}
    Handles.DrawAAPolyLine(2,points);
   }
   Handles.color=Color.green;Handles.SphereHandleCap(0,edge.end,Quaternion.identity,.7f,EventType.Repaint);
   Handles.Label(edge.end,$"Candidate exchange; rise {edge.heightDelta:F1}m / pitch {edge.pitch:F1}");
   if(!edge.clear&&edge.collision!=Vector3.zero){Handles.color=Color.red;Handles.SphereHandleCap(0,edge.collision,Quaternion.identity,1,EventType.Repaint);}
  }
 }
}
