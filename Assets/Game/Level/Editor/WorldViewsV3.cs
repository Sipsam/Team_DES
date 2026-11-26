using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace TwinEdge.Levels.Editor
{
    public sealed class WorldViewsV3:EditorWindow
    {
        static readonly string[] Names={"Whole World Top","Whole World Perspective","Whole World Side X","Whole World Side Z","Elevation Profile"};
        int index;
        [MenuItem("Tools/Twin Edge/V3 Fortress/Whole World Views")]
        public static void Open()=>GetWindow<WorldViewsV3>("V3 Whole World");
        static Bounds WorldBounds() {
            var renderers=ValleyGeneratorV3.Mountain().transform.parent.GetComponentsInChildren<Renderer>();
            var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;
        }
        static Quaternion Rotation(int i)=>i==0?Quaternion.Euler(90,0,0):i==2?Quaternion.Euler(0,90,0):i==3?Quaternion.identity:Quaternion.Euler(35,-35,0);
        void OnGUI() {
            index=EditorGUILayout.Popup("View",index,Names);
            if(index<4&&GUILayout.Button("Frame"))LevelViewCamera.Frame(WorldBounds(),Rotation(index),index!=1);
            if(GUILayout.Button("Castle / underground views"))FortressViewsV3.Open();
            EditorGUILayout.HelpBox("Cyan solid: walking hypothesis. Amber dotted: pole, bridge, raft or platform mechanism reservation. Heights use current V3 settings; this diagram is not a traversability result.",MessageType.Info);
            if(index!=4)return;
            var m=ValleyGeneratorV3.Mountain().config;var v=ValleyGeneratorV3.Config();var c=FortressGeneratorV3.Config();
            var pts=new[]{new Vector3(m.start.position.x,m.start.height,m.start.position.y),new Vector3(m.destination.position.x,m.destination.height,m.destination.position.y),v.bridgeStart,v.bridgeEnd}.Concat(v.CanyonRoute).Concat(v.river).Append(v.castleEntry).Append(c.origin).Concat(c.stairs.Select(p=>p+c.origin)).Append(c.start+c.origin).Append(c.seal+c.origin).ToArray();
            Rect rect=GUILayoutUtility.GetRect(200,300);if(Event.current.type!=EventType.Repaint)return;
            float lo=pts.Min(p=>p.y)-10,hi=pts.Max(p=>p.y)+10;float length=0;var distances=new float[pts.Length];
            for(int i=1;i<pts.Length;i++){length+=Vector3.Distance(pts[i-1],pts[i]);distances[i]=length;}
            Handles.BeginGUI();
            for(int i=1;i<pts.Length;i++) {
                Vector3 a=new Vector3(rect.x+rect.width*distances[i-1]/length,rect.yMax-rect.height*(pts[i-1].y-lo)/(hi-lo));
                Vector3 b=new Vector3(rect.x+rect.width*distances[i]/length,rect.yMax-rect.height*(pts[i].y-lo)/(hi-lo));
                bool mechanism=i==1||i==3||(i>=v.CanyonRoute.Length+4&&i<v.CanyonRoute.Length+4+v.river.Length)||i==pts.Length-1;
                Handles.color=mechanism?new Color(1,.55f,.1f):Color.cyan;
                if(mechanism)Handles.DrawDottedLine(a,b,5);else Handles.DrawLine(a,b);
            }
            Handles.EndGUI();GUI.Label(new Rect(rect.x,rect.y,300,25),"Y "+lo.ToString("F0")+".."+hi.ToString("F0")+"m; schematic distance "+length.ToString("F0")+"m");
        }
        public static void Capture() {
            string folder=WorldIntegrationReviewV3.Output+"WorldViews";Directory.CreateDirectory(folder);
            var go=new GameObject("TemporaryWorldCamera");var cam=go.AddComponent<Camera>();var rt=new RenderTexture(1800,1200,24);var old=RenderTexture.active;
            try {
                cam.targetTexture=rt;cam.farClipPlane=20000;cam.nearClipPlane=.1f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.08f,.11f,.14f);cam.fieldOfView=45;
                var b=WorldBounds();
                for(int i=0;i<4;i++) {
                    cam.transform.rotation=Rotation(i);cam.orthographic=i!=1;cam.orthographicSize=b.extents.magnitude;
                    cam.transform.position=b.center-cam.transform.forward*b.extents.magnitude*3;
                    cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1800,1200,TextureFormat.RGB24,false);
                    tex.ReadPixels(new Rect(0,0,1800,1200),0,0);tex.Apply();File.WriteAllBytes(folder+"/"+Names[i]+".png",tex.EncodeToPNG());DestroyImmediate(tex);
                }
            } finally {RenderTexture.active=old;cam.targetTexture=null;DestroyImmediate(rt);DestroyImmediate(go);}
        }
    }
}
