using UnityEngine;

namespace TwinEdge.Levels.Editor
{
    public static class StructureBuilder
    {
        public static float CastleEnd(LevelLayoutConfig c) => 40+4*c.roomLength;
        public static void Castle(BlockoutBuild b)
        {
            var c=b.c; float length=CastleEnd(c), width=c.roomWidth*2;
            var partitions=b.Group("OUTBOUND_Partitions_FutureReturnConversion");
            b.LocalBox("FIX_CastleFloor",new Vector3(0,-.5f,length/2),new Vector3(width,1,length+1),Color.gray);
            b.LocalBox("FIX_CastleCeiling",new Vector3(0,c.roomHeight+.3f,length/2),new Vector3(width,.6f,length),Color.gray);
            // Outer shell includes the sewer below the left edge. Main walls have no closed cap at the gate.
            foreach(int side in new[]{-1,1}) b.LocalBox("ENV_OuterWall",new Vector3(side*(width/2+1),c.roomHeight/2,length/2),new Vector3(2,c.roomHeight,length),Color.gray);
            b.Portal("CastleGate",Vector3.zero,width,c.roomHeight);
            b.LocalBox("FIX_CentralPartition",new Vector3(0,c.roomHeight/2,20+2*c.roomLength),new Vector3(.5f,c.roomHeight,4*c.roomLength),Color.gray,partitions);
            for(int lane=0;lane<2;lane++)
            {
                float x=(lane==0?-1:1)*c.roomWidth/2; string label=lane==0?"A":"B";
                for(int i=0;i<4;i++)
                {
                    float z=20+i*c.roomLength;
                    b.Portal(label+(i+1)+"Door",new Vector3(x,0,z),c.roomWidth,c.roomHeight,partitions);
                    b.Mark(label+(i+1),MarkerKind.PuzzlePlacement,new Vector3(x,0,z+c.roomLength/2),new Vector3(c.roomWidth-2,c.roomHeight-1,c.roomLength-2),"Room interior. Hint/buttons/digits supplied by model and script owners.");
                    b.Mark(label+(i+1)+"Trap",MarkerKind.HazardArea,new Vector3(x,0,z+c.roomLength/2),new Vector3(c.roomWidth-2,4,6),"Return trap reservation on the SAME room floor; partitions require future conversion logic.");
                }
                b.Path(label+"Route",new[]{b.W(Vector3.zero),b.W(new Vector3(x,0,15)),b.W(new Vector3(x,0,length-15)),b.W(new Vector3(0,0,length))},c.doorwayWidth,RouteKind.Walk,false);
            }
            b.Mark("Split",MarkerKind.SectionEntrance,new Vector3(0,0,10),new Vector3(width,3,10),links:new[]{"Castle.A1","Castle.B1"});
            b.Mark("Merge",MarkerKind.SectionExit,new Vector3(0,0,length-10),new Vector3(width,3,10),links:new[]{"Castle.UndergroundDoor"});
            b.Portal("UndergroundDoor",new Vector3(0,0,length),width,c.roomHeight);
            b.Mark("DoorCheckpoint",MarkerKind.Checkpoint,new Vector3(0,0,length-6),new Vector3(8,2,8),"Return trap corridor respawn.");
            b.Path("ReturnTrapCorridorFootprint",new[]{b.W(new Vector3(0,0,length-10)),b.W(new Vector3(0,0,10))},width,RouteKind.Return,false);
            // Optional sewer: a separate lower tunnel with open ramps inside the entrance and merge foyers.
            float sx=-width/2+c.sewerWidth/2+1;
            Vector3[] sewer={new Vector3(sx,0,2),new Vector3(sx,-c.sewerDepth,20),new Vector3(sx,-c.sewerDepth,length-20),new Vector3(sx,0,length-2)};
            // Cut two floor apertures by replacing the broad floor with pieces in the foyers.
            UnityEngine.Object.DestroyImmediate(b.root.Find("FIX_CastleFloor").gameObject);
            b.LocalBox("FIX_RoomFloor",new Vector3(0,-.5f,length/2),new Vector3(width,1,length-40),Color.gray);
            foreach(float z in new[]{10f,length-10})
            {
                float left=-width/2, right=sx+c.sewerWidth/2+.2f;
                b.LocalBox("FIX_FoyerFloor",new Vector3((right+width/2)/2,-.5f,z),new Vector3(width/2-right,1,20),Color.gray);
                if(sx-c.sewerWidth/2>left) b.LocalBox("FIX_FoyerEdge",new Vector3((left+sx-c.sewerWidth/2)/2,-.5f,z),new Vector3(sx-c.sewerWidth/2-left,1,20),Color.gray);
            }
            b.Path("SewerShortcut",System.Array.ConvertAll(sewer,b.W),c.sewerWidth,RouteKind.Walk);
            for(int side=-1;side<=1;side+=2) b.LocalBox("FIX_SewerWall",new Vector3(sx+side*(c.sewerWidth/2+.25f),-c.sewerDepth/2,length/2),new Vector3(.5f,c.sewerDepth,length-40),Color.gray);
            b.Mark("SewerEntry",MarkerKind.DoorOpening,sewer[3],new Vector3(c.sewerWidth,3,2),"12-symbol lock placement; no lock implemented.",new[]{"Castle.SewerExit"},180);
            b.Mark("SewerExit",MarkerKind.SectionExit,sewer[0],new Vector3(c.sewerWidth,3,2));
            var underground=c.World(SectionId.Underground,Vector3.zero);
            b.Path("UndergroundDescent",new[]{b.W(new Vector3(0,0,length)),underground},c.pathWidth,RouteKind.Walk);
        }
        public static void Underground(BlockoutBuild b)
        {
            var c=b.c; var s=c.sections.Find(x=>x.id==SectionId.Underground);
            float length=s.size.z,width=s.size.x;
            b.LocalBox("FIX_Start",new Vector3(0,-.5f,4),new Vector3(width,1,12),Color.gray);
            b.LocalBox("FIX_End",new Vector3(0,-.5f,length-5),new Vector3(width,1,16),Color.gray);
            b.LocalBox("ENV_LavaSpace",new Vector3(0,-6,length/2),new Vector3(width,.1f,length-20),new Color(.8f,.18f,.03f),solid:false);
            b.LocalBox("FIX_CavernBed",new Vector3(0,-8,length/2),new Vector3(width,2,length),Color.gray);
            b.LocalBox("FIX_CavernCeiling",new Vector3(0,c.undergroundHeight,length/2),new Vector3(width,1,length),Color.gray);
            foreach(int side in new[]{-1,1})
            {
                b.LocalBox("FIX_CavernWall",new Vector3(side*(width/2+.5f),c.undergroundHeight/2-3,length/2),new Vector3(1,c.undergroundHeight+6,length),Color.gray);
                float x=side*(width/2-c.jumpLaneWidth/2);
                string lane=side<0?"A":"B";
                b.Path("JumpLane"+lane,new[]{b.W(new Vector3(x,0,8)),b.W(new Vector3(x,0,length-12))},c.jumpLaneWidth,RouteKind.Jump,false);
                int count=Mathf.CeilToInt((length-24)/(c.jumpPlatformLength+c.jumpGap));
                float step=(length-24)/count;
                for(int i=0;i<=count;i++)
                {
                    float z=10+i*step;
                    b.Mark("Platform"+lane+i,MarkerKind.PlatformPlacement,new Vector3(x,0,z),new Vector3(c.jumpLaneWidth,.4f,c.jumpPlatformLength),"Future timed main-route platform; permanent on return. TEST geometry only.");
                    if(c.temporaryTraversal) b.LocalBox("TEST_Platform"+lane+i,new Vector3(x,-.3f,z),new Vector3(c.jumpLaneWidth,.6f,c.jumpPlatformLength),new Color(.45f,.48f,.42f),b.temporary);
                    if(i%3==0) b.Mark("Switch"+lane+i,MarkerKind.PuzzlePlacement,new Vector3(x+side*(c.jumpLaneWidth/2-.3f),1,z),new Vector3(1,2,1),"Reserve opposite-lane platform group connection.");
                }
            }
            b.Mark("JumpStartCheckpoint",MarkerKind.Checkpoint,new Vector3(0,0,4),new Vector3(8,2,8));
            b.Mark("LavaHazard",MarkerKind.HazardArea,new Vector3(0,-6,length/2),new Vector3(width,5,length-20));
            b.Mark("GemSeal",MarkerKind.GemSealLocation,new Vector3(0,0,length-5),new Vector3(8,5,8),"Two-player merge / seal interaction reservation.");
            b.Mark("ReturnStart",MarkerKind.ReturnStart,new Vector3(0,0,length-8),new Vector3(8,2,5),yaw:180);
            b.Mark("GemCheckpoint",MarkerKind.Checkpoint,new Vector3(0,0,length-5),new Vector3(8,2,8),"Return lava respawn.");
        }
    }
}
