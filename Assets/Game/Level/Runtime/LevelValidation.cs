using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TwinEdge.Levels
{
    public static class LevelValidation
    {
        public static List<string> ConfigErrors(LevelLayoutConfig c)
        {
            var errors=new List<string>();
            if(c==null) { errors.Add("Missing config"); return errors; }
            foreach(SectionId id in Enum.GetValues(typeof(SectionId)))
                if(c.sections.Count(x=>x.id==id)!=1) errors.Add("Require exactly one layout: "+id);
            if(errors.Count>0) return errors;
            if(c.mountainPath==null || c.mountainPath.Length<6) errors.Add("Mountain needs 6 or more nodes");
            if(c.riverPath==null || c.riverPath.Length<2) errors.Add("River needs 2 or more nodes");
            if(c.spatialV2 && (c.riverWidths==null || c.riverPath==null || c.riverWidths.Length!=c.riverPath.Length || c.riverWidths.Any(w=>w<20))) errors.Add("V2 river needs one width >= 20 per control point");
            if(c.spatialV2 && (c.mountainPath==null || c.mountainPath.Length!=9)) errors.Add("V2 mountain needs 5 ascent + 3 detached + 1 large-peak nodes");
            if(c.sections.Any(x=>x.size.x<=0 || x.size.y<=0 || x.size.z<=0)) errors.Add("Section sizes must be positive");
            if(c.sections.Find(x=>x.id==SectionId.Mountain).yaw!=0) errors.Add("Terrain section yaw must be zero");
            if(c.terrainResolution<33 || c.terrainResolution>2049 || ((c.terrainResolution-1)&(c.terrainResolution-2))!=0) errors.Add("Terrain resolution must be power-of-two + 1 (33..2049)");
            if(c.playerRadius<=0 || c.playerHeight<2*c.playerRadius || c.gravity<=0 || c.jumpHeight<=0 || c.moveSpeed<=0) errors.Add("Invalid player reference");
            if(c.doorwayWidth<2*c.playerRadius+2*c.clearanceMargin || c.roomWidth<=c.doorwayWidth || c.roomHeight<=3.5f) errors.Add("Invalid room / doorway clearance");
            if(c.sewerWidth<2*c.playerRadius+2*c.clearanceMargin || c.sewerWidth>c.roomWidth-3 || c.sewerDepth<c.playerHeight+1) errors.Add("Invalid sewer dimensions");
            if(c.pathWidth<2*c.playerRadius+2*c.clearanceMargin || c.bridgeWidth<=0 || c.riverWidth<12 || c.riverDepth<=0 || c.bankWidth<=0) errors.Add("Invalid route / river width or depth");
            if(c.jumpPlatformLength<=0 || c.jumpGap<0 || c.jumpLaneWidth<2*c.playerRadius+2*c.clearanceMargin || c.undergroundHeight<c.playerHeight+1) errors.Add("Invalid underground dimensions");
            if(c.jumpGap>c.FlatJumpRange*.7f) errors.Add("Jump gap exceeds provisional 70% flat ballistic range");
            if(c.terrainHeight<=0 || c.terrainDepth<0 || c.roomLength<10 || c.bridgeSpan<=16) errors.Add("Invalid terrain / room / bridge dimensions");
            if(c.worldSize.x<=0 || c.worldSize.y<=0 || c.worldSize.z<=0) errors.Add("World bounds must be positive");
            if(c.markerOverrides.GroupBy(x=>x.id).Any(x=>x.Count()>1)) errors.Add("Duplicate marker override ID");
            if(c.sections.Find(x=>x.id==SectionId.Underground).size.z<35 || c.sections.Find(x=>x.id==SectionId.Underground).size.x<2*c.jumpLaneWidth+4) errors.Add("Underground space too small");
            return errors;
        }
        public static List<string> Inspect(GameObject root, LevelLayoutConfig c)
        {
            var report=ConfigErrors(c); if(report.Count>0) return report.Select(x=>"ERROR: "+x).ToList();
            var sections=root.GetComponentsInChildren<LevelSection>(true).Where(x=>x.owner==LevelSection.Owner).ToArray();
            foreach(SectionId id in Enum.GetValues(typeof(SectionId))) if(sections.Count(x=>x.section==id)!=1) report.Add("ERROR: missing/duplicate section "+id);
            var markers=root.GetComponentsInChildren<LevelMarker>(true);
            var ids=new HashSet<string>();
            foreach(var m in markers) if(string.IsNullOrEmpty(m.id) || !ids.Add(m.id)) report.Add("ERROR: duplicate/empty marker "+m.id);
            foreach(var m in markers) foreach(var link in m.connectedIds) if(!ids.Contains(link)) report.Add("ERROR: unresolved marker link "+m.id+" -> "+link);
            if(Vector3.Distance(c.World(SectionId.Mountain,c.mountainPath[c.mountainPath.Length-1]),c.World(SectionId.Bridge,Vector3.zero))>.1f) report.Add("ERROR: mountain / bridge endpoints disconnected");
            if(c.riverPath[c.riverPath.Length-1].y<c.riverPath[0].y) report.Add("ERROR: upstream must not be lower than downstream");
            foreach(var m in markers)
            {
                Vector3 p=m.transform.position-c.origin;
                if(Mathf.Abs(p.x)>c.worldSize.x/2 || Mathf.Abs(p.y)>c.worldSize.y || p.z< -40 || p.z>c.worldSize.z) report.Add("WARNING: marker outside configured world bounds: "+m.id);
            }
            foreach(var required in new[]{"Mountain.SpawnA","Mountain.SpawnB","Mountain.Finish","Mountain.SummitCheckpoint","Mountain.LargePeakCheckpoint","River.LowerDock","River.UpperDock","Castle.UndergroundDoor","Castle.SewerEntry","Underground.GemSeal","Underground.ReturnStart"})
                if(!ids.Contains(required)) report.Add("ERROR: required marker absent "+required);
            foreach(string lane in new[]{"A","B"}) for(int i=1;i<=4;i++) if(!ids.Contains("Castle."+lane+i)) report.Add("ERROR: missing room "+lane+i);
            Physics.SyncTransforms(); int samples=0;
            foreach(var route in root.GetComponentsInChildren<LevelRoute>(true))
            {
                if(route.points==null || route.points.Length<2) { report.Add("ERROR: empty route "+route.name); continue; }
                if(route.kind!=RouteKind.Walk) continue;
                bool floorMissing=false, blocked=false;
                for(int i=1;i<route.points.Length;i++)
                {
                    var a=route.points[i-1];var d=route.points[i]-a;
                    float angle=Mathf.Atan2(Mathf.Abs(d.y),new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg;
                    if(angle>c.slopeLimit) report.Add("ERROR: slope "+route.name+" "+angle.ToString("F1")+" exceeds "+c.slopeLimit);
                    int n=Mathf.CeilToInt(d.magnitude/2);
                    for(int j=0;j<=n;j++)
                    {
                        Vector3 p=a+d*((float)j/Mathf.Max(1,n)); samples++;
                        var hits=Physics.RaycastAll(p+Vector3.up*.4f,Vector3.down,.8f,~0,QueryTriggerInteraction.Ignore);
                        if(!hits.Any(x=>x.collider.transform.IsChildOf(root.transform))) floorMissing=true;
                        var overlaps=Physics.OverlapCapsule(p+Vector3.up*(c.playerRadius+.12f),p+Vector3.up*(c.playerHeight-c.playerRadius),c.playerRadius,~0,QueryTriggerInteraction.Ignore);
                        if(overlaps.Any(x=>x.transform.IsChildOf(root.transform))) blocked=true;
                    }
                }
                if(floorMissing) report.Add("ERROR: floor sampling failed: "+route.name);
                if(blocked) report.Add("WARNING: capsule overlaps geometry (slope/seam or obstruction): "+route.name);
            }
            foreach(var section in sections)
            {
                if(section.transform.Find("Manual")==null || section.transform.Find("Generated")==null) report.Add("ERROR: ownership hierarchy "+section.section);
                if(section.GetComponentsInChildren<Collider>(true).Length==0) report.Add("ERROR: no colliders "+section.section);
            }
            if(!report.Any(x=>x.StartsWith("ERROR"))) report.Add("PASS: sections, marker IDs/links, eight rooms, colliders, walk-route floor samples and slope limits.");
            report.Add("INFO: "+markers.Length+" markers; "+samples+" walk-route samples. Normal flat jump theoretical range: "+c.FlatJumpRange.ToString("F2")+"m.");
            report.Add("UNVERIFIED: actual CharacterController traversal, two-player cameras, special movement, raft, partition conversion, dynamic platforms and gameplay. Static centerline samples do not prove full-volume reachability.");
            return report;
        }
    }
}
