using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace TwinEdge.Levels.Editor
{
    // Positions supplied to this context are world coordinates. The owned root stays at identity.
    public sealed class BlockoutBuild
    {
        public readonly LevelLayoutConfig c;
        public readonly SectionId section;
        public readonly Transform root, markers, temporary;
        public readonly string assetFolder;
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        public BlockoutBuild(LevelLayoutConfig config, SectionId id, Transform parent, string folder)
        {
            c=config; section=id; root=parent; assetFolder=folder;
            markers=Group("Markers"); temporary=Group("TEST_ONLY_Traversal");
        }
        public Vector3 W(Vector3 p) => c.World(section,p);
        public Transform Group(string name, Transform parent=null)
        {
            var g=new GameObject(name); g.transform.SetParent(parent == null ? root : parent,false); return g.transform;
        }
        public Material Material(string key, Color color)
        {
            if(materials.TryGetValue(key,out var m)) return m;
            var shader=Shader.Find(key=="Terrain"?"Universal Render Pipeline/Terrain/Lit":"Universal Render Pipeline/Lit");
            if(shader == null) throw new InvalidOperationException("URP Lit shader unavailable");
            m=new Material(shader){name=key}; m.SetColor("_BaseColor",color);
            AssetDatabase.CreateAsset(m,assetFolder+"/"+section+"_"+key+".mat");
            materials.Add(key,m); return m;
        }
        public GameObject Box(string name, Vector3 center, Vector3 size, Color color, Transform parent=null, bool solid=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=name;
            g.transform.SetParent(parent == null ? root : parent,false);
            g.transform.position=center; g.transform.localScale=size;
            g.GetComponent<Renderer>().sharedMaterial=Material("C"+ColorUtility.ToHtmlStringRGB(color),color);
            if(!solid) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }
        public GameObject LocalBox(string name, Vector3 center, Vector3 size, Color color, Transform parent=null, bool solid=true)
        {
            var g=Box(name,W(center),size,color,parent,solid);
            g.transform.rotation=Quaternion.Euler(0,c.sections.Find(x=>x.id==section).yaw,0); return g;
        }
        // Box top plane passes through both endpoints; .1m longitudinal overlap avoids seam cracks.
        public void Ramp(string name, Vector3 a, Vector3 b, float width, Transform parent=null, float thickness=.6f)
        {
            var d=b-a; var rotation=Quaternion.LookRotation(d,Vector3.up);
            var g=Box(name,(a+b)/2-rotation*Vector3.up*thickness/2,
                new Vector3(width,thickness,d.magnitude+.1f),new Color(.45f,.48f,.42f),parent);
            g.transform.rotation=rotation;
        }
        public void Path(string name, Vector3[] points, float width, RouteKind kind, bool geometry=true, Transform parent=null)
        {
            var g=Group(name,parent); var r=g.gameObject.AddComponent<LevelRoute>(); r.points=points; r.kind=kind;
            if(geometry) for(int i=1;i<points.Length;i++) Ramp(name+"_"+i,points[i-1],points[i],width,g);
        }
        public LevelMarker Mark(string id, MarkerKind kind, Vector3 local, Vector3 size, string notes="", string[] links=null, float yaw=0)
        {
            var edit=c.markerOverrides.Find(x=>x.id==section+"."+id);
            if(edit!=null) { local=edit.localPosition;size=edit.reservedSize;yaw=edit.localYaw; }
            var g=Group(id,markers); g.position=W(local);
            g.rotation=Quaternion.Euler(0,c.sections.Find(x=>x.id==section).yaw+yaw,0);
            var m=g.gameObject.AddComponent<LevelMarker>(); m.id=section+"."+id; m.section=section; m.kind=kind;
            m.reservedSize=size; m.notes=notes; m.connectedIds=links ?? Array.Empty<string>(); return m;
        }
        public void Portal(string name, Vector3 local, float width, float height, Transform parent=null)
        {
            float opening=c.doorwayWidth;
            foreach(int side in new[]{-1,1}) LocalBox(name+"_Jamb",local+new Vector3(side*(width+opening)/4,height/2,0),
                new Vector3((width-opening)/2,height,.6f),Color.gray,parent);
            LocalBox(name+"_Lintel",local+new Vector3(0,(height+3.5f)/2,0),new Vector3(opening,height-3.5f,.6f),Color.gray,parent);
            Mark(name,MarkerKind.DoorOpening,local,new Vector3(opening,3.5f,1));
        }
    }
}
