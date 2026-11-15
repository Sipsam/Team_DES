using System;
using UnityEditor;
using UnityEngine;

namespace TwinEdge.Levels.Editor
{
    public static class TerrainBuilder
    {
        private static Vector3[] PlateauNodes(Vector3[] nodes, float radius)
        {
            var result=new System.Collections.Generic.List<Vector3>{nodes[0]};
            for(int i=1;i<nodes.Length;i++)
            {
                Vector3 direction=Vector3.ProjectOnPlane(nodes[i]-nodes[i-1],Vector3.up).normalized;
                result.Add(nodes[i-1]+direction*radius);
                result.Add(nodes[i]-direction*radius);
                result.Add(nodes[i]);
            }
            return result.ToArray();
        }
        public static void Mountain(BlockoutBuild b)
        {
            var c=b.c; var s=c.sections.Find(x=>x.id==SectionId.Mountain);
            var data=new TerrainData { heightmapResolution=c.terrainResolution, size=new Vector3(s.size.x,c.terrainHeight,s.size.z+80) };
            int n=data.heightmapResolution; float[,] h=new float[n,n];
            // Terrain itself cannot rotate. Author its heights in section coordinates, then place
            // the terrain at yaw=0; config validation rejects rotated mountain terrain.
            var offset=new Vector3(-s.size.x/2,-c.terrainDepth,-30);
            for(int z=0;z<n;z++) for(int x=0;x<n;x++)
            {
                var p=offset+new Vector3((float)x/(n-1)*data.size.x,0,(float)z/(n-1)*data.size.z);
                float y=4+Mathf.PerlinNoise((p.x+c.seed)*.025f,p.z*.025f)*12;
                foreach(var peak in c.mountainPath)
                {
                    float distance=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(peak.x,peak.z));
                    float radius=peak.z<130 ? 65 : 20;
                    y=Mathf.Max(y,peak.y-3-distance*(peak.y/radius));
                }
                if(p.z>130 && p.z<250) y=Mathf.Min(y,80-c.gorgeDepth);
                // Lower terrain under the authored walkable route, preserving its full clearance.
                for(int i=1;i<c.mountainPath.Length;i++)
                {
                    Vector3 a=c.mountainPath[i-1],d=c.mountainPath[i]-a;
                    float t=Mathf.Clamp01(((p.x-a.x)*d.x+(p.z-a.z)*d.z)/(d.x*d.x+d.z*d.z));
                    if(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(a.x+d.x*t,a.z+d.z*t))<c.pathWidth)
                        y=Mathf.Min(y,a.y+d.y*t-1);
                }
                h[z,x]=Mathf.Clamp01((y+c.terrainDepth)/c.terrainHeight);
            }
            data.SetHeights(0,0,h); AssetDatabase.CreateAsset(data,b.assetFolder+"/Mountain.asset");
            var terrain=Terrain.CreateTerrainGameObject(data); terrain.name="ENV_MountainTerrain";
            terrain.transform.SetParent(b.root); terrain.transform.position=b.W(offset);
            terrain.GetComponent<Terrain>().materialTemplate=b.Material("Terrain",new Color(.33f,.36f,.3f));
            Vector3[] path=Array.ConvertAll(c.mountainPath,b.W);
            b.Path("Ascent",PlateauNodes(new[]{path[0],path[1],path[2]},12),c.pathWidth,RouteKind.Walk);
            for(int i=2;i<c.mountainPath.Length;i++)
            {
                var p=c.mountainPath[i];
                b.LocalBox("FIX_Peak_"+i,p-Vector3.up*9,new Vector3(16,18,16),Color.gray);
                b.Mark("SpecialMovement_"+i,MarkerKind.PuzzlePlacement,p,new Vector3(12,8,12),"Pole pivot / elastic launch reservation; not a normal jump.");
            }
            b.Path("PoleAndElasticRoute",new[]{path[2],path[3],path[4],path[5]},c.pathWidth,RouteKind.SpecialMovement,false);
            if(c.temporaryTraversal) b.Path("TEST_SpecialMovementBypass",PlateauNodes(new[]{path[2],path[3],path[4],path[5]},12),c.pathWidth,RouteKind.Walk,true,b.temporary);
            b.Mark("SpawnA",MarkerKind.PlayerSpawn,c.mountainPath[0]+new Vector3(-2,.1f,0),Vector3.one);
            b.Mark("SpawnB",MarkerKind.PlayerSpawn,c.mountainPath[0]+new Vector3(2,.1f,0),Vector3.one);
            b.Mark("SummitCheckpoint",MarkerKind.Checkpoint,c.mountainPath[2],new Vector3(8,2,8),"Outbound small-peak failure respawn.");
            b.Mark("Finish",MarkerKind.FinishLine,c.mountainPath[2],new Vector3(12,3,5));
            b.Mark("LargePeakCheckpoint",MarkerKind.Checkpoint,c.mountainPath[c.mountainPath.Length-1],new Vector3(8,2,8),"Outbound bridge / return small-peak respawn.");
            b.Mark("Gorge",MarkerKind.HazardArea,new Vector3(0,5,200),new Vector3(s.size.x,15,110),"Fall reservation only; no kill logic.");
        }
        public static void Bridge(BlockoutBuild b)
        {
            var c=b.c; var end=c.World(SectionId.River,c.riverPath[0]);
            var a=b.W(Vector3.zero); var far=b.W(new Vector3(0,0,c.bridgeSpan));
            b.LocalBox("FIX_BridgeAbutmentNear",new Vector3(0,-4,0),new Vector3(24,8,16),Color.gray);
            b.LocalBox("FIX_BridgeAbutmentFar",new Vector3(0,-4,c.bridgeSpan),new Vector3(24,8,16),Color.gray);
            b.Mark("AnchorNear",MarkerKind.BridgeAnchor,Vector3.zero,new Vector3(c.bridgeWidth,4,1),links:new[]{"Bridge.AnchorFar"});
            b.Mark("AnchorFar",MarkerKind.BridgeAnchor,new Vector3(0,0,c.bridgeSpan),new Vector3(c.bridgeWidth,4,1),links:new[]{"Bridge.AnchorNear"},yaw:180);
            int slots=Mathf.CeilToInt((c.bridgeSpan-16)/1.2f);
            for(int i=0;i<slots;i++) b.Mark("PlankSocket_"+i,MarkerKind.PlatformPlacement,new Vector3(0,0,8+(i+.5f)*(c.bridgeSpan-16)/slots),new Vector3(c.bridgeWidth,.3f,(c.bridgeSpan-16)/slots));
            b.Mark("TimberCrate",MarkerKind.PuzzlePlacement,new Vector3(8,0,0),new Vector3(4,3,4));
            b.Path("BridgeReservation",new[]{a,far},c.bridgeWidth,RouteKind.SpecialMovement,false);
            if(c.temporaryTraversal) b.Path("TEST_BridgeDeck",new[]{a,far},c.bridgeWidth,RouteKind.Walk,true,b.temporary);
            var approach=b.W(new Vector3(0,0,c.bridgeSpan+10));
            var dockApproach=end-Vector3.ProjectOnPlane(end-far,Vector3.up).normalized*10;
            var middle=(approach+dockApproach)/2+Quaternion.Euler(0,c.sections.Find(x=>x.id==b.section).yaw,0)*Vector3.right*25;
            b.Path("DescentToDock",new[]{far,approach,middle,dockApproach,end},c.pathWidth,RouteKind.Walk);
        }
        public static void River(BlockoutBuild b)
        {
            var c=b.c; Vector3[] p=Array.ConvertAll(c.riverPath,b.W);
            b.Path("SharedWaterway",p,c.riverWidth,RouteKind.Water,false);
            for(int i=1;i<p.Length;i++)
            {
                Vector3 a=p[i-1],d=p[i]-a; var rot=Quaternion.LookRotation(d);
                var water=b.Box("ENV_WaterPlaceholder",(a+p[i])/2,new Vector3(c.riverWidth,.08f,d.magnitude+1),new Color(.12f,.45f,.65f),solid:false); water.transform.rotation=rot;
                b.Ramp("RiverBed",a-Vector3.up*c.riverDepth,p[i]-Vector3.up*c.riverDepth,c.riverWidth+2);
                var right=Vector3.Cross(Vector3.up,d).normalized;
                foreach(int side in new[]{-1,1})
                {
                    var offset=right*side*(c.riverWidth+c.bankWidth)/2;
                    b.Ramp("RiverBank",a+offset,p[i]+offset,c.bankWidth,thickness:c.riverDepth+1);
                }
                b.Mark("ObstacleReservation_"+i,MarkerKind.PuzzlePlacement,(c.riverPath[i-1]+c.riverPath[i])/2,new Vector3(c.riverWidth-8,5,12),"Future hazards; retain clear race lanes.");
            }
            for(int i=0;i<c.riverPath.Length;i+=c.riverPath.Length-1)
            {
                var q=c.riverPath[i]; string name=i==0?"Lower":"Upper";
                b.LocalBox("FIX_"+name+"Dock",q-Vector3.up*.5f,new Vector3(c.riverWidth,1,14),new Color(.5f,.4f,.28f));
                b.Mark(name+"Dock",MarkerKind.RaftDock,q,new Vector3(c.riverWidth,4,12));
                b.Mark(name+"Checkpoint",MarkerKind.Checkpoint,q,new Vector3(8,2,8),i==0?"Outbound raft/return bridge respawn":"Return raft/outbound castle respawn");
            }
            b.Path("ApproachCastle",new[]{p[p.Length-1],c.World(SectionId.Castle,Vector3.zero)},c.pathWidth,RouteKind.Walk);
        }
    }
}
