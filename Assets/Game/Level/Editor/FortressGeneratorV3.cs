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
    public static class FortressGeneratorV3
    {
        public const string Notes="Docs/Development/Level/V3/";
        const string Data="Assets/Game/Level/Data/";
        static FortressConfigV3 c;
        static string folder;
        static Material stone, rock, roof, floor, cyan, amber, lava;
        public static FortressConfigV3 Config()=>AssetDatabase.LoadAssetAtPath<FortressConfigV3>(Data+"FortressConfigV3.asset");
        public static bool Inside(Vector2 p,Vector2[] polygon) {
            bool hit=false;for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++) {
                var a=polygon[i];var b=polygon[j];
                if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)hit=!hit;
            }return hit;
        }
        [MenuItem("Tools/Twin Edge/V3 Fortress/Create or Regenerate All")]
        public static void BuildAll() {
            MountainGeneratorV3.CreateOrOpen(); c=Config();if(!c)throw new InvalidOperationException("Fortress config required");
            var parent=ValleyGeneratorV3.Mountain().transform.parent;
            foreach(FortressPartV3 part in Enum.GetValues(typeof(FortressPartV3))) {
                var owner=parent.GetComponentsInChildren<FortressSectionV3>(true).SingleOrDefault(o=>o.part==part);
                if(!owner) {
                    var go=new GameObject(part.ToString());Undo.RegisterCreatedObjectUndo(go,"Create fortress section");go.transform.SetParent(parent,false);
                    owner=Undo.AddComponent<FortressSectionV3>(go);owner.part=part;owner.config=c;
                    Undo.AddComponent<LevelSection>(go).section=part==FortressPartV3.CastleExterior||part==FortressPartV3.CastleInterior?SectionId.Castle:SectionId.Underground;
                    var manual=new GameObject("Manual");manual.transform.SetParent(go.transform,false);Undo.RegisterCreatedObjectUndo(manual,"Create Manual");
                } owner.config=c;Generate(owner);
            }
        }
        static Material Mat(string name,Color color) {
            string path=Data+"FortressMaterials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")){name=name,color=color};
            m.SetFloat("_Smoothness",.08f);
            if(name=="Lava") {m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*1.3f);}
            AssetDatabase.CreateAsset(m,path);return m;
        }
        static void Materials() {
            Directory.CreateDirectory(Data+"FortressMaterials");AssetDatabase.Refresh();
            stone=Mat("Limestone",new Color(.52f,.53f,.49f));rock=Mat("Bedrock",new Color(.24f,.28f,.29f));
            roof=Mat("Slate",new Color(.16f,.25f,.30f));floor=Mat("Floor",new Color(.40f,.38f,.31f));
            cyan=Mat("RouteA",new Color(.12f,.63f,.69f));amber=Mat("RouteB",new Color(.83f,.55f,.20f));lava=Mat("Lava",new Color(1,.16f,.015f));
        }
        public static void Generate(FortressSectionV3 owner) {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit mode only");
            c=owner.config;if(!c||c.rooms.Length!=8||c.stairs.Length<2||c.cave.Length<3||c.curtain.Length<3)throw new InvalidOperationException("Incomplete config: asset="+(bool)c+" rooms="+(c?c.rooms.Length:-1)+" stairs="+(c?c.stairs.Length:-1)+" cave="+(c?c.cave.Length:-1));
            if(owner.transform.position!=Vector3.zero||owner.transform.rotation!=Quaternion.identity||owner.transform.lossyScale!=Vector3.one)throw new InvalidOperationException("World identity required");
            Materials();Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Regenerate "+owner.part);
            var g=new GameObject("Generated");g.transform.SetParent(owner.transform,false);g.transform.position=c.origin;
            Undo.RegisterCreatedObjectUndo(g,"Generate fortress");
            folder=Data+"Generated/FortressV3_"+owner.part+"_"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            Directory.CreateDirectory(Notes);File.AppendAllText(Notes+"CastleGenerations.txt",folder+Environment.NewLine);
            try {
                switch(owner.part) {
                    case FortressPartV3.CastleExterior:Exterior(g.transform);break;
                    case FortressPartV3.CastleInterior:Interior(g.transform);break;
                    case FortressPartV3.UndergroundAccess:Access(g.transform);break;
                    default:Cavern(g.transform);break;
                }
                Undo.RegisterCompleteObjectUndo(owner,"Publish fortress");if(owner.generated)Undo.DestroyObjectImmediate(owner.generated);owner.generated=g;
                EditorUtility.SetDirty(owner);EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);AssetDatabase.SaveAssets();Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
                using(var sha=System.Security.Cryptography.SHA256.Create())
                    File.AppendAllLines(Notes+"CastleGenerationHashes.txt",Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Select(path=>path.Replace('\\','/')+"|"+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))).ToArray());
            }catch { Undo.RevertAllDownToGroup(group);throw; }
        }
        static Transform Group(Transform p,string name) {var go=new GameObject(name);go.transform.SetParent(p,false);return go.transform;}
        static GameObject Box(Transform p,string name,Vector3 pos,Vector3 size,Material mat) {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(p,false);go.transform.localPosition=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;return go;
        }
        static void Beam(Transform p,string name,Vector3 a,Vector3 b,float width,float thick,Material mat) {
            var go=Box(p,name,(a+b)*.5f,new Vector3(width,thick,Vector3.Distance(a,b)+.04f),mat);go.transform.localRotation=Quaternion.LookRotation(b-a);
        }
        static void Wall(Transform p,string name,Vector2 a,Vector2 b,float bottom,float top,float thickness,Material mat) {
            Beam(p,name,new Vector3(a.x,(bottom+top)*.5f,a.y),new Vector3(b.x,(bottom+top)*.5f,b.y),thickness,top-bottom,mat);
        }
        static void Portal(Transform p,string name,Vector2 a,Vector2 b,float height,float width,float thickness) {
            Vector2 mid=(a+b)*.5f,dir=(b-a).normalized;
            Wall(p,name+"_Left",a,mid-dir*width*.5f,0,height,thickness,stone);
            Wall(p,name+"_Right",mid+dir*width*.5f,b,0,height,thickness,stone);
            Wall(p,name+"_Lintel",mid-dir*width*.5f,mid+dir*width*.5f,7,height,thickness,stone);
            // Segmented arch follows the opening without reducing its central 6m headroom.
            for(int i=0;i<7;i++) {
                float angle=(i+.5f)*Mathf.PI/7;var v=mid+dir*(Mathf.Cos(angle)*width*.5f);
                var go=Box(p,name+"_Voussoir",new Vector3(v.x,5.8f+Mathf.Sin(angle)*width*.23f,v.y),new Vector3(1.5f,1.2f,thickness+.3f),stone);
                go.transform.localRotation=Quaternion.Euler(0,-Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg,0);
            }
        }
        static void Tower(Transform p,FortressTowerV3 t,int index) {
            var g=Group(p,"Tower_"+index);int n=10;
            for(int i=0;i<n;i++) {
                float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;
                Wall(g,"Masonry",t.position+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*t.radius,t.position+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*t.radius,-2,t.height,2.5f,stone);
                Vector2 q=t.position+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*t.radius;
                Box(g,"Merlon",new Vector3(q.x,t.height+1.3f,q.y),new Vector3(2.6f,2.6f,2.6f),stone);
            }
            Box(g,"TowerTop",new Vector3(t.position.x,t.height-1,t.position.y),new Vector3(t.radius*1.6f,1,t.radius*1.6f),floor);
            if(index%2==0)Pyramid(Group(g,"Roofs"),t.position,t.radius*1.8f,t.radius*1.8f,t.height,t.height+9);
        }
        static void Pyramid(Transform p,Vector2 pos,float w,float d,float y,float peak) {
            MeshObject(p,"HippedRoof",new[]{new Vector3(pos.x-w/2,y,pos.y-d/2),new Vector3(pos.x+w/2,y,pos.y-d/2),new Vector3(pos.x+w/2,y,pos.y+d/2),new Vector3(pos.x-w/2,y,pos.y+d/2),new Vector3(pos.x,peak,pos.y)},new[]{0,4,1,1,4,2,2,4,3,3,4,0},roof,true);
        }
        static void Exterior(Transform p) {
            Slab(p,"Foundation",c.curtain,-8,0,stone,true);
            Slab(Group(p,"BedrockRoof"),"RockLayer",c.curtain,c.ceiling,c.foundationBottom,rock,true);
            Slab(p,"BedrockAroundCavity",c.curtain,c.caveFloor-12,c.ceiling,rock,true,true,c.cave);
            var cliffVertices=new List<Vector3>();var cliffTriangles=new List<int>();
            for(int i=0;i<c.curtain.Length;i++) {
                var edgeStart=c.curtain[i];var end=c.curtain[(i+1)%c.curtain.Length];int count=Mathf.CeilToInt(Vector2.Distance(edgeStart,end)/12);
                for(int j=0;j<count;j++) {
                    var q=Vector2.Lerp(edgeStart,end,j/(float)count);var outward=(q-new Vector2(10,145)).normalized;
                    cliffVertices.Add(new Vector3(q.x,c.ceiling,q.y));
                    cliffVertices.Add(new Vector3(q.x+outward.x*(16+i%3*5),c.caveFloor-12-i%2*6,q.y+outward.y*(16+i%3*5)));
                }
            }
            for(int i=0;i<cliffVertices.Count/2;i++) {
                int a=i*2,b=(i+1)%(cliffVertices.Count/2)*2;
                var shaft=new Rect(c.stairVoid.xMin-4,c.stairVoid.yMin-4,c.stairVoid.width+8,c.stairVoid.height+8);
                var va=cliffVertices[a];var vb=cliffVertices[b];var vc=cliffVertices[a+1];var vd=cliffVertices[b+1];
                if(shaft.Overlaps(Rect.MinMaxRect(Mathf.Min(va.x,vb.x,vc.x,vd.x),Mathf.Min(va.z,vb.z,vc.z,vd.z),Mathf.Max(va.x,vb.x,vc.x,vd.x),Mathf.Max(va.z,vb.z,vc.z,vd.z))))continue;
                cliffTriangles.AddRange(new[]{a,b,a+1,a+1,b,b+1,a+1,b,a,b+1,b,a+1});
            }
            MeshObject(p,"OuterRockEscarpment",cliffVertices.ToArray(),cliffTriangles.ToArray(),rock,true);
            // Tapered rock buttresses tie the wall base down into the exposed valley escarpment.
            for(int i=0;i<c.curtain.Length;i++) {
                var a=c.curtain[i];var b=c.curtain[(i+1)%c.curtain.Length];
                if(i==3) {
                    var dir=(b-a).normalized;var gate=Vector2.Lerp(a,b,Mathf.InverseLerp(a.x,b.x,0));
                    Wall(p,"GateFlankLeft",a,gate-dir*c.gateWidth*.5f,0,c.wallHeight,c.wallThickness,stone);
                    Wall(p,"GateFlankRight",gate+dir*c.gateWidth*.5f,b,0,c.wallHeight,c.wallThickness,stone);
                    Wall(p,"GateLintel",gate-dir*c.gateWidth*.5f,gate+dir*c.gateWidth*.5f,c.gateHeight,c.wallHeight,c.wallThickness,stone);
                }
                else Wall(p,"Curtain_"+i,a,b,-3,c.wallHeight,c.wallThickness,stone);
                int count=Mathf.CeilToInt(Vector2.Distance(a,b)/5);
                for(int j=0;j<count;j++) {
                    var q=Vector2.Lerp(a,b,(j+.5f)/count);
                    Box(p,"Battlement",new Vector3(q.x,c.wallHeight+1.2f,q.y),new Vector3(2.5f,2.4f,2.5f),stone);
                }
            }
            for(int i=0;i<c.towers.Length;i++)Tower(p,c.towers[i],i);
            var start=c.approach-c.origin;
            Beam(p,"ApproachConnection",start-new Vector3(0,.3f,0),new Vector3(0,-.3f,3),12,.6f,floor);
            Box(p,"Courtyard",new Vector3(c.courtyard.center.x,-.15f,c.courtyard.center.y),new Vector3(c.courtyard.width,.3f,c.courtyard.height),floor);
        }
        static void Interior(Transform p) {
            Rect k=c.keep;var permanent=Group(p,"PermanentStructure");
            Portal(permanent,"KeepEntrance",new Vector2(k.xMin,k.yMin),new Vector2(k.xMax,k.yMin),c.keepHeight,12,3);
            Wall(permanent,"WestOuter",new Vector2(k.xMin,k.yMin),new Vector2(k.xMin,k.yMax),0,c.keepHeight,3,stone);
            Wall(permanent,"EastOuter",new Vector2(k.xMax,k.yMin),new Vector2(k.xMax,k.yMax),0,c.keepHeight,3,stone);
            // Rear hall opens into the adjoining stair tower, rather than a marker-only door.
            Wall(permanent,"RearWest",new Vector2(k.xMin,k.yMax),new Vector2(16,k.yMax),0,c.keepHeight,3,stone);
            Wall(permanent,"RearEast",new Vector2(24,k.yMax),new Vector2(k.xMax,k.yMax),0,c.keepHeight,3,stone);
            var roofs=Group(p,"Roofs");
            Pyramid(roofs,new Vector2(k.center.x,k.yMin+55),k.width+5,110,c.keepHeight,c.hallHeight+18);
            Pyramid(roofs,new Vector2(k.center.x,k.yMax-55),k.width+5,110,c.keepHeight,c.keepHeight+23);
            // Raised crossing tower and unequal flanking turrets break the keep's long hall silhouette.
            var crossing=Group(roofs,"CrossingTower");
            var corners=new[]{new Vector2(-14,128),new Vector2(14,128),new Vector2(14,161),new Vector2(-14,161)};
            for(int i=0;i<4;i++)Wall(crossing,"UpperMasonry",corners[i],corners[(i+1)%4],c.keepHeight,72,2.5f,stone);
            Pyramid(crossing,new Vector2(0,144.5f),32,37,72,88);
            Tower(p,new FortressTowerV3{position=new Vector2(-35,49),radius=6,height=53},20);
            Tower(p,new FortressTowerV3{position=new Vector2(36,49),radius=6,height=48},21);
            Tower(p,new FortressTowerV3{position=new Vector2(-35,237),radius=7,height=58},22);
            var annex=Group(permanent,"EastServiceWing");
            Wall(annex,"WingSouth",new Vector2(44,85),new Vector2(69,85),0,20,2,stone);
            Wall(annex,"WingEast",new Vector2(69,85),new Vector2(69,130),0,20,2,stone);
            Wall(annex,"WingNorth",new Vector2(69,130),new Vector2(44,130),0,20,2,stone);
            Pyramid(roofs,new Vector2(56.5f,107.5f),29,49,20,36);
            var removable=Group(p,"RemovablePartitions");
            Wall(removable,"CentralSharedWall",new Vector2(c.sharedWallX,58),new Vector2(c.sharedWallX,211),0,16,1.2f,stone);
            foreach(var room in c.rooms) {
                var g=Group(p,room.id);g.localPosition=Vector3.zero;
                var r=room.plan;var tint=room.id.StartsWith("A")?cyan:amber;
                Box(g,"Floor",new Vector3(r.center.x,.03f,r.center.y),new Vector3(r.width-.5f,.06f,r.height-.5f),floor);
                Box(g,"RouteInlay",new Vector3(r.center.x,.08f,r.center.y),new Vector3(.6f,.08f,r.height-3),tint);
                var walls=Group(removable,room.id);
                Portal(walls,"StageArch",new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),room.height,6,1);
                Wall(walls,"Side",new Vector2(room.id.StartsWith("A")?r.xMax:r.xMin,r.yMin),new Vector2(room.id.StartsWith("A")?r.xMax:r.xMin,r.yMax),0,room.height,1,stone);
                Box(Group(roofs,"RoomCeilings"),room.id+"Ceiling",new Vector3(r.center.x,room.height+.4f,r.center.y),new Vector3(r.width,.8f,r.height),floor);
                Box(g,"OppositeHint_"+(room.hint?"Source":"Receiver"),new Vector3(room.id.StartsWith("A")?2:-2,2.5f,r.center.y),new Vector3(.3f,2,3),tint);
                Box(g,"DigitReservation",new Vector3(room.id.StartsWith("A")?r.xMax-1.5f:r.xMin+1.5f,1,r.center.y),new Vector3(1,2,1),tint);
                // Door leaves are parked open and tagged as movable geometry, without puzzle logic.
                Box(Group(removable,"OpenDoors"),room.id+"_Door",new Vector3(r.xMin+2,3,r.yMin+2),new Vector3(2,6,.5f),roof);
            }
            for(int i=0;i<4;i++)Box(Group(p,"TrapReservations"),"Trap_"+i,new Vector3(-30,.2f,80+i*43),new Vector3(8,.4f,8),amber);
            var h=c.undergroundDoor;
            Box(p,"H_TwoFourDigitInputReservation",h+new Vector3(-5,2,0),new Vector3(2,4,2),cyan);
            Beam(p,"HallToStairDoor",h-new Vector3(0,.2f,0),new Vector3(20,-.2f,248),6,.4f,floor);
        }
        static void Access(Transform p) {
            var shaft=c.stairVoid;
            Wall(p,"StairTowerNorth",new Vector2(shaft.xMin,shaft.yMax),new Vector2(shaft.xMax,shaft.yMax),-62,24,2,stone);
            Wall(p,"StairTowerEast",new Vector2(shaft.xMax,shaft.yMin),new Vector2(shaft.xMax,shaft.yMax),-62,24,2,stone);
            Wall(p,"StairTowerWest",new Vector2(shaft.xMin,shaft.yMin),new Vector2(shaft.xMin,shaft.yMax),-62,24,2,stone);
            Pyramid(Group(p,"Roofs"),shaft.center,shaft.width+3,shaft.height+3,24,33);
            for(int i=0;i<c.stairs.Length-1;i++) {
                var a=c.stairs[i];var b=c.stairs[i+1];
                var horizontal=new Vector3(b.x-a.x,0,b.z-a.z).normalized;
                var rampA=Mathf.Abs(b.y-a.y)>.01f?a+horizontal*c.stairWidth*.5f:a;
                var rampB=Mathf.Abs(b.y-a.y)>.01f?b-horizontal*c.stairWidth*.5f:b;
                Beam(p,"WalkableRamp_"+i,rampA-Vector3.up*.3f,rampB-Vector3.up*.3f,c.stairWidth,.6f,floor);
                Box(p,"Landing_"+i,a-Vector3.up*.3f,new Vector3(c.stairWidth,.6f,c.stairWidth),floor);
                // Side parapets follow each slope, leaving the whole 6m deck clear.
                var side=Vector3.Cross(Vector3.up,(b-a).normalized).normalized*(c.stairWidth*.5f+.3f);
                // Open 4m at both ends keeps switchback landings clear of crossing parapets.
                if(Vector3.Distance(a,b)>12)foreach(float sign in new[]{-1f,1f})Beam(p,"RampParapet",Vector3.MoveTowards(a,b,4)+side*sign+Vector3.up*.6f,Vector3.MoveTowards(b,a,4)+side*sign+Vector3.up*.6f,.5f,1.2f,stone);
            }
            var end=c.stairs.Last();Box(p,"U0Landing",c.start-Vector3.up*.5f,new Vector3(16,1,14),floor);
            Beam(p,"StairToU0",end-Vector3.up*.3f,c.start-Vector3.up*.3f,7,.6f,floor);
        }
        static void Cavern(Transform p) {
            Slab(p,"CavernFloor",c.cave,c.caveFloor-3,c.caveFloor,rock,false);
            Slab(Group(p,"CaveCeiling"),"Ceiling",c.cave,c.ceiling-3,c.ceiling,rock,true);
            Slab(p,"Lava",c.cave,c.lava-.5f,c.lava,lava,false,false);
            // Continuous faceted rock shell; bent middle band avoids an extruded polygon silhouette.
            var v=new List<Vector3>();var tris=new List<int>();
            int ring=0;
            for(int i=0;i<c.cave.Length;i++) {
                var a=c.cave[i];var b=c.cave[(i+1)%c.cave.Length];int segments=Mathf.CeilToInt(Vector2.Distance(a,b)/18);
                for(int j=0;j<segments;j++) {
                    var q=Vector2.Lerp(a,b,j/(float)segments);var center=new Vector2(12,145);var inset=(center-q).normalized;
                    float wave=2+3*Mathf.Sin(ring*1.9f);
                    v.Add(new Vector3(q.x,c.caveFloor,q.y));v.Add(new Vector3(q.x+inset.x*wave,c.lava+27+(ring%3)*5,q.y+inset.y*wave));v.Add(new Vector3(q.x,c.ceiling,q.y));ring++;
                }
            }
            for(int i=0;i<ring;i++)for(int band=0;band<2;band++) {
                int a=i*3+band,b=((i+1)%ring)*3+band;
                var shaft=new Rect(c.stairVoid.xMin-3,c.stairVoid.yMin-3,c.stairVoid.width+6,c.stairVoid.height+6);
                float x0=Mathf.Min(v[a].x,v[b].x,v[a+1].x,v[b+1].x),x1=Mathf.Max(v[a].x,v[b].x,v[a+1].x,v[b+1].x);
                float z0=Mathf.Min(v[a].z,v[b].z,v[a+1].z,v[b+1].z),z1=Mathf.Max(v[a].z,v[b].z,v[a+1].z,v[b+1].z);
                if(shaft.Overlaps(Rect.MinMaxRect(x0,z0,x1,z1)))continue;
                tris.AddRange(new[]{a,b,a+1,a+1,b,b+1,a+1,b,a,b+1,b,a+1});
            }
            MeshObject(p,"FacetedCavernWalls",v.ToArray(),tris.ToArray(),rock,true);
            foreach(var platform in c.platforms) {
                var g=Group(p,platform.fixedLanding?"Fixed_"+platform.id:"Timed_"+platform.id+"_by_"+platform.activatedBy+"_25s_PROVISIONAL");
                var tint=platform.id.StartsWith("A")?cyan:amber;
                Box(g,"LandingSurface",platform.position-Vector3.up*.6f,new Vector3(platform.size.x,1.2f,platform.size.y),platform.fixedLanding?stone:tint);
                if(platform.fixedLanding) {
                    Box(g,"OppositeSwitch_"+platform.id,platform.position+new Vector3(0,1,2),new Vector3(1,2,1),tint);
                }
            }
            foreach(string side in new[]{"A","B"}) {
                var first=c.platforms.First(x=>x.id==side+"0");
                var corner=new Vector3(first.position.x,c.start.y,c.start.z);
                Beam(p,"U0Split_"+side,c.start-Vector3.up*.4f,corner-Vector3.up*.4f,7,.8f,stone);
                Beam(p,"StartShelf_"+side,corner-Vector3.up*.4f,first.position-Vector3.up*.4f,7,.8f,stone);
            }
            Box(p,"JointSealLanding",c.seal-Vector3.up*.5f,new Vector3(24,1,22),stone);
            Box(p,"SealInteractionA",c.seal+new Vector3(-4,1.2f,0),new Vector3(1.5f,2.4f,1.5f),cyan);
            Box(p,"SealInteractionB",c.seal+new Vector3(4,1.2f,0),new Vector3(1.5f,2.4f,1.5f),amber);
            Box(p,"GemReservation",c.seal+Vector3.up*2,new Vector3(1.5f,2,1.5f),roof);
            for(int i=0;i<4;i++) {
                var light=new GameObject("CavernBlockoutLight_"+i);light.transform.SetParent(p,false);light.transform.localPosition=new Vector3(12,-48,65+i*52);
                var point=light.AddComponent<Light>();point.type=LightType.Point;point.range=100;point.intensity=6;point.color=new Color(1,.61f,.35f);point.shadows=LightShadows.None;
            }
        }
        static void Slab(Transform p,string name,Vector2[] polygon,float bottom,float top,Material mat,bool cut,bool collider=true,Vector2[] hollow=null) {
            var v=new List<Vector3>();var t=new List<int>();
            float minX=Mathf.Floor(polygon.Min(q=>q.x)/2)*2,maxX=polygon.Max(q=>q.x),minZ=Mathf.Floor(polygon.Min(q=>q.y)/2)*2,maxZ=polygon.Max(q=>q.y);
            var cells=new HashSet<Vector2>();
            for(float x=minX;x<maxX;x+=2)for(float z=minZ;z<maxZ;z+=2) {
                var q=new Vector2(x+1,z+1);if(!Inside(q,polygon))continue;
                if(hollow!=null&&Inside(q,hollow))continue;
                if(cut&&new Rect(c.stairVoid.xMin-2,c.stairVoid.yMin-2,c.stairVoid.width+4,c.stairVoid.height+4).Contains(q))continue;
                cells.Add(new Vector2(x,z));
            }
            foreach(var cell in cells) {
                float x=cell.x,z=cell.y;
                Func<float,float,float> bottomAt=(px,pz)=>name=="Ceiling"?bottom-5*Mathf.PerlinNoise(px*.07f+10,pz*.06f):bottom;
                int n=v.Count;v.AddRange(new[]{new Vector3(x,top,z),new Vector3(x,top,z+2),new Vector3(x+2,top,z+2),new Vector3(x+2,top,z),new Vector3(x,bottomAt(x,z),z),new Vector3(x,bottomAt(x,z+2),z+2),new Vector3(x+2,bottomAt(x+2,z+2),z+2),new Vector3(x+2,bottomAt(x+2,z),z)});
                foreach(int j in new[]{0,1,2,0,2,3,4,6,5,4,7,6})t.Add(n+j);
                var corners=new[]{new Vector2(x,z),new Vector2(x,z+2),new Vector2(x+2,z+2),new Vector2(x+2,z)};
                var neighbors=new[]{new Vector2(-2,0),new Vector2(0,2),new Vector2(2,0),new Vector2(0,-2)};
                for(int side=0;side<4;side++)if(!cells.Contains(cell+neighbors[side])) {
                    var a=corners[side];var b=corners[(side+1)%4];int k=v.Count;
                    v.AddRange(new[]{new Vector3(a.x,top,a.y),new Vector3(a.x,bottom,a.y),new Vector3(b.x,bottom,b.y),new Vector3(b.x,top,b.y)});
                    t.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
                }
            }MeshObject(p,name,v.ToArray(),t.ToArray(),mat,collider);
        }
        static int meshIndex;
        static void MeshObject(Transform p,string name,Vector3[] v,int[] triangles,Material mat,bool collider) {
            var mesh=new Mesh{name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=v;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,folder+"/"+name+"_"+(meshIndex++)+".asset");
            var go=new GameObject(name);go.transform.SetParent(p,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
            if(collider)go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
    }
    [CustomEditor(typeof(FortressSectionV3))]
    public sealed class FortressSectionInspectorV3:UnityEditor.Editor {
        public override void OnInspectorGUI(){DrawDefaultInspector();if(GUILayout.Button("Regenerate this section (Undo)"))FortressGeneratorV3.Generate((FortressSectionV3)target);if(GUILayout.Button("Fortress review views"))FortressViewsV3.Open();}
    }
}
