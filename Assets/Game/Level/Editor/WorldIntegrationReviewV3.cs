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
    public static class WorldIntegrationReviewV3
    {
        public const string Output="Docs/Development/Level/V3/GeometryRefinement/";
        [MenuItem("Tools/Twin Edge/V3 Fortress/Refine and Validate World")]
        public static void Run() {
            Directory.CreateDirectory(Output);
            FortressReviewV3.ReportDirectory=Output;
            try {
                FortressReviewV3.Run();FortressReviewV3.VerifyPlayerCapsule();Inspect();WorldViewsV3.Capture();
            } finally { FortressReviewV3.ReportDirectory=FortressGeneratorV3.Notes; }
        }
        public static void Cleanup() {
            string legacy=FortressGeneratorV3.Notes+"RefinementCleanup.txt";
            byte[] previous=File.Exists(legacy)?File.ReadAllBytes(legacy):null;
            try { ValleyAssetLifecycleV3.CleanFreshSession();File.Copy(legacy,Output+"Cleanup.txt",true); }
            finally { if(previous!=null)File.WriteAllBytes(legacy,previous); }
        }
        public static void VerifySaved() {
            MountainGeneratorV3.CreateOrOpen();
            var owners=FortressViewsV3.Owners();
            if(owners.Length!=4||owners.Any(o=>!o.config||!o.generated||o.generated.GetComponentsInChildren<MeshFilter>().Any(f=>!f.sharedMesh)||o.generated.GetComponentsInChildren<MeshCollider>().Any(mc=>!mc.sharedMesh)))throw new InvalidOperationException("Cold reload lost a section/config/mesh/collider reference");
            var config=FortressGeneratorV3.Config();
            if(config.rooms.Length!=8||config.platforms.Length!=64)throw new InvalidOperationException("Serialized config changed");
            FortressReviewV3.ReportDirectory=Output;
            try {FortressReviewV3.VerifyPlayerCapsule();} finally {FortressReviewV3.ReportDirectory=FortressGeneratorV3.Notes;}
            Inspect();
            File.WriteAllText(Output+"ColdReload.txt","PASS: cold-session four sections/configs and all mesh/collider references. Eight rooms/64 platforms serialized. Player capsule route probes rerun.\n");
            FortressViewsV3.Capture(Output+"CastleUndergroundViews");WorldViewsV3.Capture();
        }
        public static void Inspect() {
            Directory.CreateDirectory(Output);Physics.SyncTransforms();
            var c=FortressGeneratorV3.Config();var v=ValleyGeneratorV3.Config();var m=ValleyGeneratorV3.Mountain();
            var lines=new List<string>{"Actual saved/generated V3 world inspection "+DateTime.UtcNow.ToString("O")};
            var owners=FortressViewsV3.Owners();
            var cave=owners.Single(o=>o.part==FortressPartV3.UndergroundCavern);
            var floor=cave.generated.transform.Find("CavernFloor").GetComponent<MeshCollider>();
            float area=0,keep=0,curtain=0;
            var bounds=floor.bounds;
            for(float x=bounds.min.x+.5f;x<bounds.max.x;x++)for(float z=bounds.min.z+.5f;z<bounds.max.z;z++) {
                if(!floor.Raycast(new Ray(new Vector3(x,bounds.max.y+1,z),Vector3.down),out var hit,5))continue;
                area++;var q=new Vector2(hit.point.x-c.origin.x,hit.point.z-c.origin.z);
                if(c.keep.Contains(q))keep++;if(FortressGeneratorV3.Inside(q,c.curtain))curtain++;
            }
            lines.Add("MEASURED actual CavernFloor MeshCollider top sampling 1m: area="+area+"m2 under keep="+(keep/area).ToString("P2")+" within curtain="+(curtain/area).ToString("P2"));
            var ceiling=cave.generated.transform.Find("CaveCeiling/Ceiling").GetComponent<MeshFilter>();
            lines.Add("Actual ceiling top worldY="+ceiling.GetComponent<Renderer>().bounds.max.y+" foundation bottom="+(c.origin.y+c.foundationBottom)+" minimum roof rock="+(c.origin.y+c.foundationBottom-ceiling.GetComponent<Renderer>().bounds.max.y));
            Boundary(lines,"Mountain final / bridge",m.config.bridgeAnchor+Vector3.forward*m.config.bridgeStubLength,v.bridgeStart);
            Boundary(lines,"Bridge / descent",v.bridgeEnd,v.descent.First());
            Boundary(lines,"Descent / lower dock water+2",v.descent.Last(),new Vector3(v.descent.Last().x,v.river.First().y+2,v.river.First().z));
            Boundary(lines,"Approach / castle deck",v.castleEntry,c.approach);
            Probe(lines,"Canyon walking",v.descent);
            var approach=Enumerable.Range(0,171).Select(i=>{float u=Mathf.SmoothStep(0,1,i/170f);return new Vector3(Mathf.Lerp(v.river.Last().x+ValleyGeneratorV3.Width(v,1900)*.5f+10,v.castleEntry.x,u),Mathf.Lerp(v.river.Last().y+2,v.castleEntry.y,u),1900+i);}).Append(c.origin).ToArray();
            Probe(lines,"Castle approach curved walking",approach);
            bool direction=v.river.First().y<v.river.Last().y&&v.river.First().z<v.river.Last().z;
            lines.Add((direction?"PASS":"FAIL")+": river outbound lower->upper (+Z/uphill), return upper->lower (-Z/downhill)");
            foreach(var p in c.platforms) {
                var g=cave.generated.transform.Find(p.fixedLanding?"Fixed_"+p.id:"Timed_"+p.id+"_by_"+p.activatedBy+"_25s_PROVISIONAL");
                var surface=g.Find("LandingSurface").GetComponent<Collider>();
                var pt=p.position+c.origin;
                bool support=surface.Raycast(new Ray(pt+Vector3.up,Vector3.down),out var hit,2);
                var blockers=Physics.OverlapCapsule(pt+Vector3.up*.57f,pt+Vector3.up*1.67f,.45f).Where(o=>o!=surface&&!o.isTrigger).ToArray();
                lines.Add((support&&blockers.Length==0?"PASS":"FAIL")+": landing "+p.id+" support/capsule="+support+"/"+(blockers.Length==0)+" blockers="+string.Join(",",blockers.Select(o=>o.name)));
            }
            lines.Add("RESERVED ONLY: pole/elastic movement; bridge construction/collapse; raft shared/individual states; puzzle doors; timers; gem/permanent platforms; sewer shortcut and traps. No success/balance claim.");
            lines.Add("LIMIT: terrain probes project +/-35m from authored polyline and report datum drift and adjacent height steps. This does not certify authored route accuracy. Sampled static probes do not verify controller stepping, swept camera clearance, jump reach, all bypasses or terrain cuts. Mountain/valley were inspected without regeneration.");
            File.WriteAllLines(Output+"FullWorldValidation.txt",lines);
            var perf=new List<string>{"# Actual world geometry and storage", "Unity "+Application.unityVersion};
            foreach(var s in m.transform.parent.GetComponentsInChildren<LevelSection>()) {
                var meshes=s.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh).ToArray();
                perf.Add(s.name+": meshes="+meshes.Length+" triangles="+meshes.Sum(f=>(long)f.sharedMesh.triangles.Length/3)+" MeshColliders="+s.GetComponentsInChildren<MeshCollider>(true).Length);
            }
            var all=m.transform.parent.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh).ToArray();
            perf.Add("World rendered triangle instances="+all.Sum(f=>(long)f.sharedMesh.triangles.Length/3));
            var files=Directory.GetFiles("Assets/Game/Level/Data/Generated","*",SearchOption.AllDirectories);
            perf.Add("Generated files (including meta)="+files.Length+" bytes="+files.Sum(f=>new FileInfo(f).Length));
            perf.Add("Section regeneration timing: see GenerationTimes.txt. Save/reload/Undo: CastleUndergroundValidation.txt. Wall-clock load times and runtime frame profiling not measured.");
            File.WriteAllLines(Output+"WorldPerformanceReport.md",perf);
        }
        static void Boundary(List<string> lines,string name,Vector3 a,Vector3 b) {
            lines.Add((Vector3.Distance(a,b)<.05f?"PASS":"FAIL")+": datum "+name+" "+a+" -> "+b+" delta="+Vector3.Distance(a,b));
        }
        static void Probe(List<string> lines,string label,Vector3[] points) {
            int unsupported=0,blocked=0,total=0,steps=0;float maxDrift=0;Vector3? previous=null;var samples=new List<string>();
            for(int j=1;j<points.Length;j++) {
                int n=Mathf.CeilToInt(Vector3.Distance(points[j-1],points[j])/.5f);
                for(int i=0;i<=n;i++) {
                    total++;var p=Vector3.Lerp(points[j-1],points[j],i/(float)Math.Max(1,n));
                    var hits=Physics.RaycastAll(p+Vector3.up*35,Vector3.down,70).Where(h=>h.normal.y>.75f).OrderBy(h=>Mathf.Abs(h.point.y-p.y)).ToArray();
                    if(hits.Length==0){unsupported++;if(samples.Count<8)samples.Add("unsupported "+p);continue;}
                    maxDrift=Mathf.Max(maxDrift,Mathf.Abs(p.y-hits[0].point.y));p=hits[0].point;
                    if(previous.HasValue&&Vector2.Distance(new Vector2(previous.Value.x,previous.Value.z),new Vector2(p.x,p.z))<1&&Mathf.Abs(p.y-previous.Value.y)>.6f){steps++;if(samples.Count<8)samples.Add("step "+previous.Value+" -> "+p+" surface="+hits[0].collider.name);}
                    previous=p;
                    if(Physics.OverlapCapsule(p+Vector3.up*.57f,p+Vector3.up*1.67f,.45f,~0,QueryTriggerInteraction.Ignore).Length>0){blocked++;if(samples.Count<8)samples.Add("blocked "+p);}
                }
            }
            lines.Add((unsupported==0&&blocked==0&&steps==0?"PASS":"FAIL")+": "+label+" samples="+total+" unsupported="+unsupported+" blocked="+blocked+" vertical steps >0.6m="+steps+" max authored datum drift="+maxDrift+" "+string.Join("; ",samples));
        }
    }
}
