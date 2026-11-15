using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TwinEdge.Levels.Editor
{
    // Local coordinates throughout; mesh assets are versioned with the existing Undo generation.
    public static class SpatialBlockoutV2
    {
        static readonly Color Rock = new Color(.38f,.43f,.39f), Earth = new Color(.48f,.5f,.38f), Stone = new Color(.55f,.54f,.48f);
        public static LevelLayoutConfig Config()
        {
            const string path = LevelGenerator.BasePath + "/Data/LevelLayoutV2.asset";
            var c = AssetDatabase.LoadAssetAtPath<LevelLayoutConfig>(path);
            if(c != null) return c;
            c = ScriptableObject.CreateInstance<LevelLayoutConfig>();
            c.spatialV2 = true; c.temporaryTraversal = false; c.worldSize = new Vector3(1400,300,1200);
            c.pathWidth = 12; c.bridgeSpan = 64; c.bridgeWidth = 7; c.doorwayWidth = 10;
            c.sections = new List<SectionLayout> {
                new SectionLayout(SectionId.Mountain,new Vector3(-220,0,0),new Vector3(500,180,300)),
                new SectionLayout(SectionId.Bridge,new Vector3(20,126,235),new Vector3(180,160,160)),
                new SectionLayout(SectionId.River,new Vector3(40,0,300),new Vector3(400,90,300)),
                new SectionLayout(SectionId.Castle,new Vector3(240,48,610),new Vector3(110,24,210)){yaw=-90},
                new SectionLayout(SectionId.Underground,new Vector3(20,12,760),new Vector3(96,70,145)) };
            c.mountainPath = new[] {new Vector3(-100,40,0),new Vector3(-140,58,55),new Vector3(-85,78,105),new Vector3(-40,94,80),new Vector3(0,110,140),new Vector3(65,118,175),new Vector3(120,108,135),new Vector3(175,120,190),new Vector3(240,126,235)};
            c.riverPath = new[] {new Vector3(0,28,0),new Vector3(70,29,20),new Vector3(140,30,85),new Vector3(65,31,145),new Vector3(70,33,215),new Vector3(150,35,250),new Vector3(200,36,260)};
            c.riverWidths = new[] {30f,38f,45f,32f,48f,40f,32f};
            AssetDatabase.CreateAsset(c,path); return c;
        }
        public static void Build(BlockoutBuild b)
        {
            switch(b.section) {
                case SectionId.Mountain: Mountain(b); break;
                case SectionId.Bridge: Bridge(b); break;
                case SectionId.River: River(b); break;
                case SectionId.Castle: Castle(b); break;
                case SectionId.Underground: Underground(b); break;
            }
        }
        static void Mesh(BlockoutBuild b,string name,List<Vector3> vertices,List<int> triangles,Color color,bool solid=true,Transform parent=null)
        {
            // Separate vertices per triangle retain low-poly face normals.
            var flat = triangles.Select(i=>b.W(vertices[i])).ToArray();
            var mesh = new UnityEngine.Mesh {name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.vertices=flat; mesh.triangles=Enumerable.Range(0,flat.Length).ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,b.assetFolder+"/"+b.section+"_"+name+".asset");
            var g=b.Group(name,parent).gameObject; g.AddComponent<MeshFilter>().sharedMesh=mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial=b.Material("C"+ColorUtility.ToHtmlStringRGB(color),color);
            if(solid) g.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        static void Quad(List<int> t,int a,int d,int e,int f) { t.AddRange(new[]{a,d,e,a,e,f}); }
        // Broad polygon plateau with cliff ring and flared foot: distinct geological masses.
        static void Mass(BlockoutBuild b,string name,Vector3 top,float rx,float rz,float floor,Color color)
        {
            const int n=12; var v=new List<Vector3>{top}; var t=new List<int>();
            for(int ring=0;ring<3;ring++) for(int i=0;i<n;i++) {
                float a=-i*Mathf.PI*2/n, irregular=1+.09f*Mathf.Sin(i*4.1f+b.c.seed);
                float scale=ring==0?1:ring==1?1.65f:2.2f;
                if(name=="ENV_LargePeak")scale=ring==0?1:ring==1?1.35f:1.7f;
                v.Add(new Vector3(top.x+Mathf.Cos(a)*rx*scale*irregular,ring==0?top.y:ring==1?Mathf.Lerp(top.y,floor,.6f):floor,top.z+Mathf.Sin(a)*rz*scale*irregular));
            }
            for(int i=0;i<n;i++) { int j=(i+1)%n; t.AddRange(new[]{0,1+i,1+j}); for(int ring=0;ring<2;ring++) Quad(t,1+ring*n+i,1+(ring+1)*n+i,1+(ring+1)*n+j,1+ring*n+j); }
            Mesh(b,name,v,t,color);
        }
        // Continuous cross-sections; top shelves extend down to the valley rather than floating roads.
        static void Ribbon(BlockoutBuild b,string name,Vector3[] p,float[] widths,float floor,Color color,bool solid=true,bool sides=true)
        {
            var v=new List<Vector3>(); var t=new List<int>();
            for(int i=0;i<p.Length;i++) {
                var d=p[Mathf.Min(i+1,p.Length-1)]-p[Mathf.Max(0,i-1)]; var r=Vector3.Cross(Vector3.up,d).normalized;
                var l=p[i]-r*widths[i]/2; var q=p[i]+r*widths[i]/2;
                v.Add(l);v.Add(q);v.Add(new Vector3(l.x,floor,l.z));v.Add(new Vector3(q.x,floor,q.z));
                if(i>0) {int a=(i-1)*4,z=i*4; Quad(t,a,z,z+1,a+1); if(sides){Quad(t,a+2,z+2,z,a);Quad(t,a+1,z+1,z+3,a+3);} }
            }
            if(sides) {Quad(t,0,1,3,2);int a=(p.Length-1)*4;Quad(t,a+1,a,a+2,a+3);}
            Mesh(b,name,v,t,color,solid);
        }
        static void LandPath(BlockoutBuild b,string name,Vector3[] local,float width,float floor)
        {
            var points=TerracePath(local,width*.4f);
            for(int i=1;i<points.Length;i++) Ribbon(b,name+"_Landform_"+i,new[]{points[i-1],points[i]},new[]{width,width},floor,Earth);
            for(int i=0;i<local.Length;i++) Mass(b,name+"_TurnTerrace_"+i,local[i],width*.3f,width*.3f,floor,Earth);
            b.Path(name,points.Select(b.W).ToArray(),width,RouteKind.Walk,false);
        }
        static Vector3[] TerracePath(Vector3[] local,float radius)
        {
            var p=new List<Vector3>{local[0]};
            for(int i=1;i<local.Length;i++) {
                var d=Vector3.ProjectOnPlane(local[i]-local[i-1],Vector3.up).normalized;
                float r=Mathf.Min(radius,Vector3.ProjectOnPlane(local[i]-local[i-1],Vector3.up).magnitude*.24f);
                p.Add(local[i-1]+d*r);p.Add(local[i]-d*r);p.Add(local[i]);
            }
            return p.ToArray();
        }
        static void MountainBody(BlockoutBuild b,Vector3[] route)
        {
            var v=new List<Vector3>();var t=new List<int>();const int nx=61,nz=53;
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++) {
                var p=new Vector3(-240+x*5,0,-35+z*5);
                float y=-24;
                foreach(var peak in new[]{new Vector3(-150,113,110),new Vector3(-70,147,180),new Vector3(-25,120,135)}) {
                    float distance=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(peak.x,peak.z));
                    y=Mathf.Max(y,peak.y-distance*1.05f);
                }
                for(int i=1;i<route.Length;i++) {
                    var a=route[i-1];var d=route[i]-a;var horizontal=new Vector2(d.x,d.z);
                    float f=Mathf.Clamp01(Vector2.Dot(new Vector2(p.x-a.x,p.z-a.z),horizontal)/Mathf.Max(.001f,horizontal.sqrMagnitude));
                    float dist=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(a.x+d.x*f,a.z+d.z*f));
                    float target=a.y+d.y*f;
                    if(dist<19)y=Mathf.Min(y,target-3);
                }
                v.Add(new Vector3(p.x,Mathf.Max(-25,y),p.z));
                if(x>0&&z>0){int a=(z-1)*nx+x-1;Quad(t,a,a+nx,a+nx+1,a+1);}
            }
            Mesh(b,"ENV_ConnectedMountainBody",v,t,Rock);
        }
        static void Connections(BlockoutBuild b,Vector3 entry,Vector3 exit,string next)
        {b.Mark("Entry",MarkerKind.SectionEntrance,entry,new Vector3(10,3,3));b.Mark("Exit",MarkerKind.SectionExit,exit,new Vector3(10,3,3),links:next==null?null:new[]{next});}
        static void Mountain(BlockoutBuild b)
        {
            var p=b.c.mountainPath;
            Mesh(b,"ENV_CanyonFloor",new List<Vector3>{new Vector3(-20,-30,50),new Vector3(290,-25,80),new Vector3(320,-32,310),new Vector3(-20,-28,270)},new List<int>{0,3,2,0,2,1},Rock);
            LandPath(b,"Ascent",p.Take(5).ToArray(),24,-25);
            MountainBody(b,TerracePath(p.Take(5).ToArray(),24*.4f));
            for(int i=5;i<8;i++) {
                Mass(b,"ENV_DetachedPeak_"+i,p[i],13,13,-25,Rock);
                b.Mark("SpecialMovement_"+i,MarkerKind.PuzzlePlacement,p[i],new Vector3(20,8,20),"Pole pivot / return elastic landing. Reach and launch strength unconfirmed.");
                b.Mark("ElasticLaunch_"+i,MarkerKind.PuzzlePlacement,p[i]+Vector3.up*3,new Vector3(24,16,24));
            }
            Mass(b,"ENV_LargePeak",new Vector3(p.Last().x,p.Last().y,p.Last().z-25),34,36,-25,Rock);
            b.Path("PoleAndElasticRoute",p.Skip(4).Select(b.W).ToArray(),12,RouteKind.SpecialMovement,false);
            b.Mark("SpawnA",MarkerKind.PlayerSpawn,p[0]+Vector3.left*2,Vector3.one);
            b.Mark("SpawnB",MarkerKind.PlayerSpawn,p[0]+Vector3.right*2,Vector3.one);
            b.Mark("SummitCheckpoint",MarkerKind.Checkpoint,p[4],new Vector3(12,3,12));
            b.Mark("Finish",MarkerKind.FinishLine,p[4],new Vector3(16,3,8));
            b.Mark("LargePeakCheckpoint",MarkerKind.Checkpoint,p.Last(),new Vector3(12,3,12));
            b.Mark("PolePickup",MarkerKind.PuzzlePlacement,p[4]+Vector3.left*8,new Vector3(4,3,4));
            b.Mark("Gorge",MarkerKind.HazardArea,new Vector3(125,-25,180),new Vector3(270,25,120));
            Connections(b,p[0],p.Last(),"Bridge.Entry");
        }
        static void Bridge(BlockoutBuild b)
        {
            float span=b.c.bridgeSpan;
            Mass(b,"ENV_FarCliff",new Vector3(-24,0,span+16),25,26,-151,Rock);
            foreach(float z in new[]{0f,span}) {
                b.LocalBox("FIX_Abutment_"+z,new Vector3(0,-2,z),new Vector3(14,4,8),Stone);
                for(int side=-1;side<=1;side+=2) b.LocalBox("FIX_Frame_"+z+"_"+side,new Vector3(side*(z==0?4:6),1,z),new Vector3(1,6,1),Stone);
            }
            b.Mark("AnchorNear",MarkerKind.BridgeAnchor,Vector3.zero,new Vector3(7,4,2),links:new[]{"Bridge.AnchorFar"});
            b.Mark("AnchorFar",MarkerKind.BridgeAnchor,new Vector3(0,0,span),new Vector3(7,4,2),yaw:180);
            for(int i=0;i<48;i++) b.Mark("PlankSocket_"+i,MarkerKind.PlatformPlacement,new Vector3(0,0,4+(i+.5f)*(span-8)/48),new Vector3(7,.3f,(span-8)/48));
            b.Mark("TimberCrate",MarkerKind.PuzzlePlacement,new Vector3(12,0,-12),new Vector3(6,4,6));
            b.Mark("FallSpace",MarkerKind.HazardArea,new Vector3(0,-151,span/2),new Vector3(40,100,span-20));
            b.Path("BridgeReservation",new[]{b.W(Vector3.zero),b.W(new Vector3(0,0,span))},7,RouteKind.SpecialMovement,false);
            var end=Quaternion.Inverse(Quaternion.Euler(0,b.c.sections.Find(s=>s.id==b.section).yaw,0))*(b.c.World(SectionId.River,b.c.riverPath[0])-b.W(Vector3.zero));
            LandPath(b,"DescentToDock",new[]{new Vector3(0,0,span),new Vector3(12,0,span+5),new Vector3(40,-15,span+25),new Vector3(100,-55,span+55),new Vector3(85,-80,span-25),new Vector3(40,-98,span-10),end},14,-151);
            Connections(b,Vector3.zero,end,"River.Entry");
        }
        static Vector3 Catmull(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
        { return .5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t); }
        static void River(BlockoutBuild b)
        {
            var c=b.c;var p=new List<Vector3>();var widths=new List<float>();
            var castle=b.c.World(SectionId.Castle,Vector3.zero)-b.W(Vector3.zero);
            var approach=new[]{c.riverPath.Last(),castle+new Vector3(30,-4,-45),castle+new Vector3(40,0,0),castle};
            var approachShelf=TerracePath(approach,16*.4f);
            for(int i=0;i<c.riverPath.Length-1;i++) for(int j=0;j<12;j++) {
                float t=j/12f;p.Add(Catmull(c.riverPath[Mathf.Max(0,i-1)],c.riverPath[i],c.riverPath[i+1],c.riverPath[Mathf.Min(c.riverPath.Length-1,i+2)],t));
                widths.Add(Mathf.Lerp(c.riverWidths[i],c.riverWidths[i+1],t));
            }
            p.Add(c.riverPath.Last());widths.Add(c.riverWidths.Last());
            Ribbon(b,"ENV_ContinuousWater",p.ToArray(),widths.ToArray(),0,new Color(.13f,.42f,.57f),false,false);
            Ribbon(b,"ENV_RiverBed",p.Select(x=>x-Vector3.up*7).ToArray(),widths.Select(x=>x+1).ToArray(),-16,Rock);
            // A distance-field basin avoids self-intersecting offset banks on tight meanders.
            var terrainVertices=new List<Vector3>();var terrainTriangles=new List<int>();
            const int cols=77,rows=87;
            for(int z=0;z<rows;z++)for(int x=0;x<cols;x++) {
                var q=new Vector3(-80+x*5,0,-80+z*5);float best=float.MaxValue,level=28,half=15;
                for(int i=1;i<p.Count;i++) {
                    var delta=p[i]-p[i-1];var d=new Vector2(delta.x,delta.z);
                    float f=Mathf.Clamp01(Vector2.Dot(new Vector2(q.x-p[i-1].x,q.z-p[i-1].z),d)/d.sqrMagnitude);
                    var nearest=Vector3.Lerp(p[i-1],p[i],f);float dist=Vector2.Distance(new Vector2(q.x,q.z),new Vector2(nearest.x,nearest.z));
                    if(dist<best){best=dist;level=nearest.y;half=Mathf.Lerp(widths[i-1],widths[i],f)/2;}
                }
                float edge=best-half;
                float y=edge<0?level-7:edge<8?Mathf.Lerp(level-7,level+3,edge/8):level+3+(edge-8)*.42f;
                y=Mathf.Min(y,level+42+4*Mathf.Sin(q.x*.04f));
                var castleLayout=c.sections.Find(s=>s.id==SectionId.Castle);
                var castleLocal=Quaternion.Inverse(Quaternion.Euler(0,castleLayout.yaw,0))*(b.W(q)-c.World(SectionId.Castle,Vector3.zero));
                if(Mathf.Abs(castleLocal.x)<58 && castleLocal.z>-18 && castleLocal.z<225)y=Mathf.Min(y,castleLayout.position.y-4);
                if(castleLocal.x<-20 && castleLocal.x>-55 && castleLocal.z>0 && castleLocal.z<208)y=Mathf.Min(y,castleLayout.position.y-14);
                if(castleLocal.x>-15 && castleLocal.x<85 && castleLocal.z>208)y=Mathf.Min(y,-10);
                // Clear the upper dock-to-gate approach, including the turn terrace.
                if(Mathf.Abs(q.x-c.riverPath.Last().x)<16 && q.z>=c.riverPath.Last().z-8)y=Mathf.Min(y,c.riverPath.Last().y-3);
                for(int i=1;i<approachShelf.Length;i++) {
                    var a=approachShelf[i-1];var d=approachShelf[i]-a;var horizontal=new Vector2(d.x,d.z);
                    float f=Mathf.Clamp01(Vector2.Dot(new Vector2(q.x-a.x,q.z-a.z),horizontal)/Mathf.Max(.001f,horizontal.sqrMagnitude));
                    if(Vector2.Distance(new Vector2(q.x,q.z),new Vector2(a.x+d.x*f,a.z+d.z*f))<20)y=Mathf.Min(y,a.y+d.y*f-4);
                }
                var world=b.W(q);var bridge=c.World(SectionId.Bridge,Vector3.zero);
                if(Mathf.Abs(world.x-bridge.x)<32 && world.z>bridge.z+5 && world.z<bridge.z+c.bridgeSpan-10)y=-25;
                terrainVertices.Add(new Vector3(q.x,y,q.z));
                if(x>0&&z>0){int a=(z-1)*cols+x-1;Quad(terrainTriangles,a,a+cols,a+cols+1,a+1);}
            }
            Mesh(b,"ENV_ContinuousRiverValley",terrainVertices,terrainTriangles,Earth);
            b.Path("SharedWaterway",p.Select(b.W).ToArray(),30,RouteKind.Water,false);
            for(int i=0;i<2;i++) {
                int index=i==0?0:p.Count-1;var q=p[index];string label=i==0?"Lower":"Upper";
                // Decks meet the centerline only at the ends; the shared navigable channel stays open.
                b.LocalBox("FIX_"+label+"Dock",q+new Vector3(0,-.6f,i==0?-6:6),new Vector3(32,1.2f,12),Stone);
                b.Mark(label+"Dock",MarkerKind.RaftDock,q,new Vector3(26,4,12));b.Mark(label+"Checkpoint",MarkerKind.Checkpoint,q,new Vector3(10,3,8));
            }
            for(int i=12;i<p.Count-12;i+=14) b.Mark("ReefReservation_"+i,MarkerKind.PuzzlePlacement,p[i]+Vector3.right*8,new Vector3(6,6,10),"Future obstacle; retain two clear raft lanes.");
            LandPath(b,"ApproachCastle",approach,16,-16);
            Connections(b,p.First(),p.Last(),"Castle.Entry");
        }
        static void Castle(BlockoutBuild b)
        {
            var partitions=b.Group("OUTBOUND_Partitions_FutureReturnConversion");
            // Two broad wings share their central wall; offsets and widths vary by stage.
            b.LocalBox("FIX_GateCourt",new Vector3(7,-1,12),new Vector3(54,2,30),Stone);
            b.Portal("CastleGate",Vector3.zero,68,15);
            b.LocalBox("FIX_MergeHall",new Vector3(10,-1,193),new Vector3(60,2,36),Stone);
            foreach(float z in new[]{14f,193f})b.LocalBox("FIX_SewerLanding_"+z,new Vector3(-24,-.5f,z),new Vector3(8,1,10),Stone);
            b.LocalBox("FIX_MergeThreshold",new Vector3(0,-1,174),new Vector3(72,2,5),Stone);
            var centers=new[]{43f,80f,117f,154f};var widths=new[]{24f,32f,27f,36f};
            foreach(int side in new[]{-1,1}) {
                string lane=side<0?"A":"B";var path=new List<Vector3>{Vector3.zero,new Vector3(side*14,0,20)};
                for(int i=0;i<4;i++) {
                    float z=centers[i],w=widths[i],x=side*w/2;var q=new Vector3(x,0,z);
                    b.LocalBox("FIX_RoomFloor_"+lane+(i+1),q-Vector3.up,new Vector3(w,2,36),Stone);
                    b.LocalBox("ENV_WingWall_"+lane+i,new Vector3(side*(w+1),6,z),new Vector3(2,12,36),Stone);
                    // Horizontal boundary shoulders close the stepped facade.
                    if(i>0) {float delta=Mathf.Abs(w-widths[i-1]);if(delta>0)b.LocalBox("ENV_Shoulder_"+lane+i,new Vector3(side*(w+widths[i-1])/2,6,z-18.5f),new Vector3(delta+2,12,1.5f),Stone);}
                    b.Portal(lane+(i+1)+"Door",new Vector3(x,0,z-18),w,10,partitions);
                    b.Mark(lane+(i+1),MarkerKind.PuzzlePlacement,q,new Vector3(w-4,7,28),"Shared center wall for opposite-room symbol communication.",new[]{i==3?"Castle.Merge":"Castle."+lane+(i+2)});
                    b.Mark(lane+(i+1)+"Trap",MarkerKind.HazardArea,q,new Vector3(w-4,4,8),"Same floor on return; remove OUTBOUND partitions to form one hall.");
                    b.Mark(lane+(i+1)+"HintWall",MarkerKind.PuzzlePlacement,new Vector3(side*1.5f,2,z),new Vector3(1,3,6));
                    path.Add(new Vector3(x,0,z-18));path.Add(new Vector3(x,0,z));
                    // Short connector spans the 1m transition between stage floor slabs.
                    if(i<3)b.LocalBox("FIX_Connector_"+lane+i,new Vector3(side*14,-1,z+18.5f),new Vector3(12,2,2),Stone);
                }
                path.Add(new Vector3(side*14,0,181));path.Add(new Vector3(0,0,193));
                b.Path(lane+"Route",path.Select(b.W).ToArray(),4,RouteKind.Walk,false);
                b.Path("Return"+lane,path.AsEnumerable().Reverse().Select(b.W).ToArray(),16,RouteKind.Return,false);
            }
            b.LocalBox("FIX_SharedWall",new Vector3(0,5,98.5f),new Vector3(1,10,149),Stone,partitions);
            foreach(int side in new[]{-1,1}) {
                b.LocalBox("ENV_CurtainWall_"+side,new Vector3(side*51,8,105),new Vector3(3,16,214),Stone);
                Mass(b,"FIX_GateBastion_"+side,new Vector3(side*45,18,3),8,9,0,Stone);
            }
            b.LocalBox("ENV_RearWallLeft",new Vector3(-28,8,212),new Vector3(42,16,3),Stone);
            b.LocalBox("ENV_RearWallRight",new Vector3(28,8,212),new Vector3(42,16,3),Stone);
            b.Mark("Split",MarkerKind.SectionEntrance,new Vector3(0,0,16),new Vector3(40,3,12),links:new[]{"Castle.A1","Castle.B1"});
            b.Mark("Merge",MarkerKind.SectionExit,new Vector3(0,0,193),new Vector3(40,4,20),links:new[]{"Castle.UndergroundDoor"});
            b.Portal("UndergroundDoor",new Vector3(0,0,210),14,12);
            b.Mark("DoorCheckpoint",MarkerKind.Checkpoint,new Vector3(0,0,202),new Vector3(10,3,8));
            // Separate lower tunnel outside the wing foundations, with open foyer approaches.
            Vector3[] sewer={new Vector3(-28,0,193),new Vector3(-43,-10,178),new Vector3(-43,-10,30),new Vector3(-28,0,14)};
            b.Path("SewerShortcut",sewer.Select(b.W).ToArray(),6,RouteKind.Walk);
            for(int side=-1;side<=1;side+=2)b.LocalBox("FIX_SewerWall_"+side,new Vector3(-43+side*3.5f,-7,104),new Vector3(1,6,148),Stone);
            b.LocalBox("FIX_SewerVault",new Vector3(-43,-3.7f,104),new Vector3(8,.6f,148),Stone);
            b.Mark("SewerEntry",MarkerKind.DoorOpening,sewer[0],new Vector3(6,4,4),"12-symbol lock reservation",new[]{"Castle.SewerExit"});
            b.Mark("SewerExit",MarkerKind.SectionExit,sewer.Last(),new Vector3(6,4,4));
            var u=Quaternion.Inverse(Quaternion.Euler(0,-90,0))*(b.c.World(SectionId.Underground,Vector3.zero)-b.W(Vector3.zero));
            LandPath(b,"UndergroundDescent",new[]{new Vector3(0,0,210),new Vector3(0,-2,227),new Vector3(35,-10,245),new Vector3(70,-22,245),u},12,-70);
            Connections(b,Vector3.zero,new Vector3(0,0,210),"Underground.Entry");
        }
        static void Underground(BlockoutBuild b)
        {
            // Irregular enclosing cavern with exposed cutaway top for review.
            for(int side=-1;side<=1;side+=2) for(int i=0;i<6;i++)
                Mass(b,"ENV_CavernWall_"+side+"_"+i,new Vector3(side*(65+4*Mathf.Sin(i)),38+i%2*6,10+i*25),10,23,-30,Rock);
            Mass(b,"FIX_EntryTerrace",new Vector3(0,0,0),40,14,-30,Rock);
            Mass(b,"FIX_SealTerrace",new Vector3(0,12,140),40,18,-30,Rock);
            b.LocalBox("ENV_LavaBasin",new Vector3(0,-25,72),new Vector3(98,3,165),Rock);
            var lava=new[]{new Vector3(0,-20,10),new Vector3(-5,-20,45),new Vector3(4,-20,85),new Vector3(0,-20,125)};
            Ribbon(b,"ENV_Lava",lava,new[]{72f,86f,78f,70f},-25,new Color(.8f,.22f,.045f),false,false);
            foreach(int side in new[]{-1,1}) {
                string lane=side<0?"A":"B";var points=new List<Vector3>{new Vector3(side*28,0,5)};
                for(int i=0;i<4;i++) {
                    var q=new Vector3(side*(30+3*Mathf.Sin(i*1.8f)),2+i*3+(side>0?1:0),25+i*29);
                    Mass(b,"FIX_Landing_"+lane+i,q,8,7,-25,Rock);points.Add(q);
                    b.Mark("Switch"+lane+i,MarkerKind.PuzzlePlacement,q+new Vector3(side*5,1,0),new Vector3(2,3,2),"Opposite-lane timed platform group.");
                }
                points.Add(new Vector3(side*28,12,137));
                b.Path("JumpLane"+lane,points.Select(b.W).ToArray(),6,RouteKind.Jump,false);
                for(int i=1;i<points.Count;i++) for(int j=1;j<=3;j++) {
                    var q=Vector3.Lerp(points[i-1],points[i],j/4f);
                    b.Mark("Platform"+lane+i+"_"+j,MarkerKind.PlatformPlacement,q,new Vector3(6,.5f,5),"Reservation only. Variable platforms intentionally absent; exact jump arcs require tuning.");
                }
            }
            b.Mark("JumpStartCheckpoint",MarkerKind.Checkpoint,Vector3.zero,new Vector3(12,3,10));
            b.Mark("LavaHazard",MarkerKind.HazardArea,new Vector3(0,-20,70),new Vector3(80,8,120));
            b.Mark("GemSeal",MarkerKind.GemSealLocation,new Vector3(0,12,140),new Vector3(10,6,10));
            b.Mark("ReturnStart",MarkerKind.ReturnStart,new Vector3(0,12,136),new Vector3(12,3,8),yaw:180);
            b.Mark("GemCheckpoint",MarkerKind.Checkpoint,new Vector3(0,12,140),new Vector3(12,3,8));
            Connections(b,Vector3.zero,new Vector3(0,12,140),null);
        }
    }
}
