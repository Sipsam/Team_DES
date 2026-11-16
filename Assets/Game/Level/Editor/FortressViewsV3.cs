using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace TwinEdge.Levels.Editor
{
    public sealed class FortressViewsV3:EditorWindow
    {
        public static readonly string[] Names={"Castle Whole Top","Castle Whole Perspective","Castle Entrance Perspective","Courtyard Perspective","Castle Interior Top","A/B Room Perspective","Return Corridor Preview","Underground Top","Underground Perspective","Castle / Underground Overlay","Castle / Underground Vertical Section","Castle / River Connection"};
        int index;bool roofs=true,partitions=true,ceiling=true;
        readonly List<GameObject> hidden=new List<GameObject>();
        [MenuItem("Tools/Twin Edge/V3 Fortress/Review Views")]
        public static void Open()=>GetWindow<FortressViewsV3>("V3 Fortress Views");
        void OnGUI() {
            EditorGUILayout.HelpBox("Editor inspection only. Scene visibility never changes colliders or gameplay. Close this window to restore objects hidden by it. Return preview hides all removable partitions and doors.",MessageType.Info);
            int next=EditorGUILayout.Popup("View",index,Names);
            if(next!=index){index=next;roofs=index<4;partitions=index!=6;ceiling=index<7;Apply();Frame(index);}
            EditorGUI.BeginChangeCheck();roofs=EditorGUILayout.Toggle("Roofs / room ceilings",roofs);partitions=EditorGUILayout.Toggle("Removable partitions",partitions);ceiling=EditorGUILayout.Toggle("Cave ceiling / bedrock",ceiling);
            if(EditorGUI.EndChangeCheck())Apply();
            if(GUILayout.Button("Frame")){Apply();Frame(index);}
        }
        void OnDisable(){Restore();}
        void Restore(){foreach(var g in hidden)if(g)SceneVisibilityManager.instance.Show(g,false);hidden.Clear();}
        void Apply() {
            Restore();
            foreach(var section in Owners())foreach(var t in section.generated.GetComponentsInChildren<Transform>()) {
                bool hide=(!roofs&&t.name=="Roofs")||(!partitions&&t.name=="RemovablePartitions")||(!ceiling&&(t.name=="CaveCeiling"||t.name=="BedrockRoof"||t.name=="Foundation"));
                if(index==7&&(section.part==FortressPartV3.CastleExterior||section.part==FortressPartV3.CastleInterior))hide=true;
                if(index==10&&(t.name.StartsWith("Curtain_")||t.name=="BedrockAroundCavity"||t.name=="OuterRockEscarpment"||t.name=="FacetedCavernWalls"||t.name=="EastOuter"||t.name.StartsWith("StairTower")||t.name=="RemovablePartitions"||t.name=="EastServiceWing"))hide=true;
                if(hide)foreach(var r in t.GetComponentsInChildren<Renderer>())if(!SceneVisibilityManager.instance.IsHidden(r.gameObject)){SceneVisibilityManager.instance.Hide(r.gameObject,false);hidden.Add(r.gameObject);}
            }
            SceneView.RepaintAll();
        }
        public static FortressSectionV3[] Owners()=>Object.FindObjectsByType<FortressSectionV3>(FindObjectsSortMode.None).Where(o=>o.generated).ToArray();
        public static Bounds BoundsFor(int i) {
            var c=FortressGeneratorV3.Config();Vector3 center;Vector3 size;
            if(i==11){center=new Vector3(0,-10,-50);size=new Vector3(450,250,800);}
            else if(i==2){center=new Vector3(0,14,12);size=new Vector3(110,65,110);}
            else if(i==3){center=new Vector3(0,15,32);size=new Vector3(105,60,85);}
            else if(i==5){center=new Vector3(0,8,91);size=new Vector3(62,30,60);}
            else if(i==4||i==6){center=new Vector3(0,10,145);size=new Vector3(100,40,235);}
            else if(i==7||i==8){center=new Vector3(16,-58,145);size=new Vector3(125,70,260);}
            else {center=new Vector3(15,i>=9?-23:8,145);size=new Vector3(180,i>=9?180:110,340);}
            return new Bounds(c.origin+center,size);
        }
        public static Quaternion Rotation(int i)=>i==0||i==4||i==7||i==9?Quaternion.Euler(90,0,0):i==10?Quaternion.Euler(0,90,0):Quaternion.Euler(i==5?15:30,i==8?145:25,0);
        public static void Frame(int i)=>LevelViewCamera.Frame(BoundsFor(i),Rotation(i),i==0||i==4||i==7||i==9||i==10);
        public static void Capture() {
            string dir=FortressGeneratorV3.Notes+"CastleUndergroundViews";Directory.CreateDirectory(dir);
            var go=new GameObject("TemporaryFortressReviewCamera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.075f,.10f,.13f);cam.farClipPlane=10000;cam.nearClipPlane=.1f;cam.fieldOfView=48;
            var rt=new RenderTexture(1600,1200,24);var old=RenderTexture.active;
            var changed=new List<Renderer>();
            try {
                for(int i=0;i<Names.Length;i++) {
                    foreach(var r in changed)if(r)r.enabled=true;changed.Clear();
                    foreach(var s in Owners())foreach(var r in s.generated.GetComponentsInChildren<Renderer>()) {
                        string path=AnimationUtility.CalculateTransformPath(r.transform,s.generated.transform);
                        bool hide=(i>=4&&i!=8&&i!=11&&(path.Contains("Roofs/")||path.StartsWith("Roofs/")))||(i==6&&path.Contains("RemovablePartitions/"))||(i>=7&&i!=8&&i!=11&&(path.Contains("CaveCeiling/")||path.Contains("BedrockRoof/")||r.name=="Foundation"));
                        if(i==7&&(s.part==FortressPartV3.CastleExterior||s.part==FortressPartV3.CastleInterior))hide=true;
                        // True section: hide near-side opaque rock shell and castle facade only for inspection.
                        if(i==10&&(r.name=="FacetedCavernWalls"||r.name=="BedrockAroundCavity"||r.name=="OuterRockEscarpment"||r.name.Contains("Outer")||r.name.StartsWith("Curtain_")||r.name.StartsWith("StairTower")||path.Contains("RemovablePartitions/")||path.Contains("EastServiceWing/")))hide=true;
                        if(hide&&r.enabled){r.enabled=false;changed.Add(r);}
                    }
                    var b=BoundsFor(i);cam.transform.rotation=Rotation(i);cam.orthographic=i==0||i==4||i==7||i==9||i==10;
                    cam.orthographicSize=Mathf.Max(b.extents.z,b.extents.y)*1.08f;
                    cam.transform.position=b.center-cam.transform.forward*b.extents.magnitude/Mathf.Sin(24*Mathf.Deg2Rad)*1.1f;cam.targetTexture=rt;
                    if(i==8){var c=FortressGeneratorV3.Config();cam.transform.position=c.origin+new Vector3(9,-44,250);cam.transform.LookAt(c.origin+new Vector3(12,-55,85));cam.fieldOfView=70;}
                    else if(i==5){var c=FortressGeneratorV3.Config();cam.transform.position=c.origin+new Vector3(10,4,72);cam.transform.LookAt(c.origin+new Vector3(17,4,95));cam.fieldOfView=80;}
                    else if(i==3){var c=FortressGeneratorV3.Config();cam.transform.position=c.origin+new Vector3(0,5,8);cam.transform.LookAt(c.origin+new Vector3(0,14,42));cam.fieldOfView=80;}
                    else if(i==2){var c=FortressGeneratorV3.Config();cam.transform.position=c.origin+new Vector3(0,5,-45);cam.transform.LookAt(c.origin+new Vector3(0,18,20));cam.fieldOfView=75;}
                    else cam.fieldOfView=48;
                    cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1600,1200,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1200),0,0);tex.Apply();File.WriteAllBytes(dir+"/"+i+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);
                }
            }finally {foreach(var r in changed)if(r)r.enabled=true;RenderTexture.active=old;cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
    }
}
