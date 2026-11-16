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
    public static class FortressReviewV3
    {
        static readonly List<string> report=new List<string>();
        static void Check(bool ok,string text){report.Add((ok?"PASS: ":"FAIL: ")+text);}
        public static void Run() {
            Directory.CreateDirectory(FortressGeneratorV3.Notes);
            try { Execute(); }catch(Exception e){File.WriteAllText(FortressGeneratorV3.Notes+"CastleUndergroundError.txt",e.ToString());throw;}
        }
        public static void VerifySaved() {
            Directory.CreateDirectory(FortressGeneratorV3.Notes);
            MountainGeneratorV3.CreateOrOpen();
            var owners=FortressViewsV3.Owners();
            if(owners.Length!=4||owners.Any(o=>!o.config||!o.generated||o.generated.GetComponentsInChildren<MeshFilter>().Any(f=>!f.sharedMesh)))throw new Exception("Saved fortress references missing after cleanup: "+string.Join("; ",owners.Select(o=>o.part+" config="+(bool)o.config+" meshes="+o.generated.GetComponentsInChildren<MeshFilter>().Count(f=>!f.sharedMesh))));
            var config=FortressGeneratorV3.Config();EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
            FortressViewsV3.Capture();
            File.WriteAllText(FortressGeneratorV3.Notes+"CastleUndergroundPostCleanup.txt","PASS: fresh Unity session loads four section/config/mesh references after cleanup.\nPASS: "+FortressViewsV3.Names.Length+" actual Unity views recaptured from saved scene.\n");
            ProjectDependencyAudit.Run();
        }
        public static void VerifyFinalSaved() { VerifySaved(); VerifyPlayerCapsule(); }
        public static void NormalizeConfigAsset() {
            MountainGeneratorV3.CreateOrOpen();var c=FortressGeneratorV3.Config();
            if(!c)throw new Exception("Config could not load");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c,out string guid,out long id);
            File.WriteAllText(FortressGeneratorV3.Notes+"CastleConfigImport.txt","Before config localId="+id+" guid="+guid+" rooms="+c.rooms.Length+"\n");
            EditorUtility.SetDirty(c);AssetDatabase.SaveAssets();AssetDatabase.ForceReserializeAssets(new[]{AssetDatabase.GetAssetPath(c)});
            foreach(var o in FortressViewsV3.Owners()){o.config=c;EditorUtility.SetDirty(o);}
            var scene=ValleyGeneratorV3.Mountain().gameObject.scene;EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c,out guid,out id);
            File.AppendAllText(FortressGeneratorV3.Notes+"CastleConfigImport.txt","After config localId="+id+" guid="+guid+"\n");
        }
        public static void VerifyPlayerCapsule() {
            MountainGeneratorV3.CreateOrOpen();report.Clear();var c=FortressGeneratorV3.Config();
            var colliders=FortressViewsV3.Owners().SelectMany(o=>o.generated.GetComponentsInChildren<Collider>()).ToArray();Physics.SyncTransforms();
            ValidatePath("Approach to keep",new[]{c.approach-c.origin,new Vector3(0,0,-14),new Vector3(0,0,5),new Vector3(0,0,40),new Vector3(14,0,45)},colliders,true);
            ValidatePath("H to U0",new[]{c.undergroundDoor}.Concat(c.stairs).Append(c.start).ToArray(),colliders,true);
            foreach(string side in new[]{"A","B"}) {
                var rooms=c.rooms.Where(r=>r.id.StartsWith(side)).OrderBy(r=>r.id).ToArray();var path=new List<Vector3>{new Vector3(rooms[0].plan.center.x,0,50)};
                for(int i=0;i<rooms.Length;i++){var r=rooms[i].plan;path.Add(new Vector3(r.center.x,0,r.yMin+4));path.Add(new Vector3(i<rooms.Length-1?rooms[i+1].plan.center.x:r.center.x,0,r.yMax-4));}
                path.Add(new Vector3(rooms.Last().plan.center.x,0,220));path.Add(c.undergroundDoor);ValidatePath(side+" rooms",path.ToArray(),colliders,true);
            }
            var partitions=FortressViewsV3.Owners().Single(o=>o.part==FortressPartV3.CastleInterior).generated.transform.Find("RemovablePartitions").gameObject;
            try {partitions.SetActive(false);Physics.SyncTransforms();ValidatePath("Return corridor",new[]{new Vector3(0,0,245),new Vector3(0,0,42)},colliders,true);}
            finally {partitions.SetActive(true);Physics.SyncTransforms();}
            Directory.CreateDirectory(FortressGeneratorV3.Notes);File.WriteAllLines(FortressGeneratorV3.Notes+"CastleUndergroundPlayerCapsule.txt",report);
            if(report.Any(l=>l.StartsWith("FAIL")))throw new Exception("Player capsule clearance failed");
        }
        static void Execute() {
            report.Clear();report.Add("Unity "+Application.unityVersion+" "+DateTime.UtcNow.ToString("O"));
            MountainGeneratorV3.CreateOrOpen();
            var mountain=ValleyGeneratorV3.Mountain();var valleys=mountain.transform.parent.GetComponentsInChildren<ValleySectionV3>();
            var preserved=valleys.Select(v=>v.generated).Append(mountain.generated).ToArray();
            var counts=preserved.Select(g=>g.GetComponentsInChildren<Transform>(true).Length).ToArray();
            FortressGeneratorV3.BuildAll();
            var owners=FortressViewsV3.Owners();Check(owners.Length==4,"Four independently owned sections");
            for(int i=0;i<preserved.Length;i++)Check(preserved[i]&&preserved[i].GetComponentsInChildren<Transform>(true).Length==counts[i],"Preserved existing generated subtree "+preserved[i].transform.parent.name);
            // All four regenerate independently; actual scene Undo/Redo is exercised, including Manual preservation.
            foreach(var owner in owners) {
                var manual=new GameObject("ValidationManualSentinel");manual.transform.SetParent(owner.transform.Find("Manual"),false);
                var previous=owner.generated;FortressGeneratorV3.Generate(owner);var next=owner.generated;
                Check(next&&next!=previous&&manual,"Regenerate and Manual preservation "+owner.part);
                int previousId=previous?previous.GetInstanceID():0;
                Undo.PerformUndo();Check(owner.generated&&owner.generated!=next&&manual,"Undo "+owner.part+" restored="+(owner.generated?owner.generated.GetInstanceID():0)+" previous="+previousId);
                Undo.PerformRedo();Check(owner.generated==next&&next&&manual,"Redo "+owner.part);
                Object.DestroyImmediate(manual);
            }
            var c=FortressGeneratorV3.Config();report.Add("Loaded keep="+c.keep+" shaft="+c.stairVoid);var interior=owners.Single(o=>o.part==FortressPartV3.CastleInterior);
            Check(c.rooms.Count(r=>r.id.StartsWith("A"))==4&&c.rooms.Count(r=>r.id.StartsWith("B"))==4,"A/B four rooms each");
            Check(c.rooms.All(r=>c.keep.Contains(r.plan.min+Vector2.one*.01f)&&c.keep.Contains(r.plan.max-Vector2.one*.01f)),"All eight room plans inside Keep");
            Check(interior.generated.transform.Find("RemovablePartitions/CentralSharedWall"),"Central removable shared wall exists");
            Check(c.rooms.Where(r=>r.hint).Select(r=>r.id).SequenceEqual(new[]{"A1","B2","A3","B4"}),"Alternating opposite-player hint sources preserved");
            float area=0,under=0,within=0;
            for(float x=c.cave.Min(q=>q.x);x<c.cave.Max(q=>q.x);x++)for(float z=c.cave.Min(q=>q.y);z<c.cave.Max(q=>q.y);z++) {
                var p=new Vector2(x+.5f,z+.5f);if(!FortressGeneratorV3.Inside(p,c.cave))continue;
                area++;if(c.keep.Contains(p))under++;if(FortressGeneratorV3.Inside(p,c.curtain))within++;
            }
            Check(under/area>.65f&&within/area>.95f,"Cave area "+area+"m2; under keep="+(under/area).ToString("P2")+" within curtain="+(within/area).ToString("P2"));
            Check(c.foundationBottom-c.ceiling>=20,"20m bedrock between foundation and ceiling (shaft excluded)");
            Check(c.origin.z+c.cave.Min(q=>q.y)>2140,"Cavern beyond preserved Approach terrain edge; no old terrain across chamber");
            Physics.SyncTransforms();
            var colliders=owners.SelectMany(o=>o.generated.GetComponentsInChildren<Collider>()).ToArray();
            foreach(var o in owners)Check(o.generated.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh&&AssetDatabase.Contains(f.sharedMesh)),"Persisted mesh references "+o.part);
            Check(colliders.OfType<MeshCollider>().All(m=>m.sharedMesh),"No missing collider meshes");
            for(int i=0;i<c.stairs.Length-1;i++) {
                var a=c.stairs[i];var b=c.stairs[i+1];float horizontal=new Vector2(b.x-a.x,b.z-a.z).magnitude;
                Check(horizontal>0&&Mathf.Atan2(Mathf.Abs(b.y-a.y),horizontal)*Mathf.Rad2Deg<=30,"Stair ramp slope "+i);
            }
            ValidatePath("Castle approach to keep",new[]{c.approach-c.origin,new Vector3(0,0,-14),new Vector3(0,0,5),new Vector3(0,0,40),new Vector3(14,0,45)},colliders,true);
            ValidatePath("H to U0",new[]{c.undergroundDoor}.Concat(c.stairs).Append(c.start).ToArray(),colliders,true);
            foreach(string side in new[]{"A","B"}) {
                var rooms=c.rooms.Where(r=>r.id.StartsWith(side)).OrderBy(r=>r.id).ToArray();var path=new List<Vector3>();
                path.Add(new Vector3(rooms[0].plan.center.x,0,50));
                for(int i=0;i<rooms.Length;i++) {
                    var r=rooms[i].plan;path.Add(new Vector3(r.center.x,0,r.yMin+4));
                    path.Add(new Vector3(i<rooms.Length-1?rooms[i+1].plan.center.x:r.center.x,0,r.yMax-4));
                }
                path.Add(new Vector3(rooms.Last().plan.center.x,0,220));path.Add(c.undergroundDoor);
                ValidatePath(side+" four-room route",path.ToArray(),colliders,true);
            }
            // Preview physical corridor state, restored before save.
            var partitions=interior.generated.transform.Find("RemovablePartitions").gameObject;partitions.SetActive(false);Physics.SyncTransforms();
            ValidatePath("Return corridor walls removed",new[]{new Vector3(0,0,245),new Vector3(0,0,42)},colliders,true);
            partitions.SetActive(true);Physics.SyncTransforms();
            Check(c.platforms.Count(p=>p.fixedLanding)==8&&c.platforms.Count(p=>!p.fixedLanding)==56,"Eight fixed switch landings and 56 provisional timed platforms");
            Check(c.platforms.Where(p=>!p.fixedLanding).All(p=>p.position.y>c.lava+10&&p.activatedBy[0]!=p.id[0]&&p.seconds>=20&&p.seconds<=30),"Opposite switch links, 20–30s metadata, lava clearance");
            Check(c.platforms.All(p=>FortressGeneratorV3.Inside(new Vector2(p.position.x,p.position.z),c.cave)),"Platform centers inside cave");
            var scene=mountain.gameObject.scene;EditorSceneManager.MarkSceneDirty(scene);Check(EditorSceneManager.SaveScene(scene),"Save V3");AssetDatabase.SaveAssets();
            EditorSceneManager.CloseScene(scene,true);var reopened=EditorSceneManager.OpenScene(MountainGeneratorV3.ScenePath,OpenSceneMode.Single);
            Check(FortressViewsV3.Owners().Length==4,"Reload four sections");
            Check(FortressViewsV3.Owners().SelectMany(o=>o.generated.GetComponentsInChildren<MeshFilter>()).All(f=>f.sharedMesh),"Reload mesh references");
            var test=EditorSceneManager.OpenScene("Assets/Game/Level/Testing/MountainTraversalTest.unity",OpenSceneMode.Additive);
            Check(test.IsValid()&&test.isLoaded,"Retained traversal test loads");EditorSceneManager.CloseScene(test,true);
            FortressViewsV3.Capture();Check(Directory.GetFiles(FortressGeneratorV3.Notes+"CastleUndergroundViews","*.png").Length==FortressViewsV3.Names.Length,"Unity-rendered review views: "+FortressViewsV3.Names.Length);
            report.Add("NOT VERIFIED: actual controller/camera traversal, jump reach, pole mechanics, timer/gameplay/network; visual approval recorded separately after image inspection.");
            File.WriteAllLines(FortressGeneratorV3.Notes+"CastleUndergroundValidation.txt",report);
            if(report.Any(l=>l.StartsWith("FAIL")))throw new Exception("Fortress validation failures; inspect local report");
        }
        static void ValidatePath(string label,Vector3[] points,Collider[] colliders,bool capsule) {
            var c=FortressGeneratorV3.Config();bool floor=true,clear=true;float worst=0;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/NetworkPlayer.prefab");
            var player=prefab.GetComponentInChildren<CharacterController>();float radius=player.radius,height=player.height;
            for(int j=0;j<points.Length-1;j++) {
                int steps=Mathf.CeilToInt(Vector3.Distance(points[j],points[j+1])/.5f);
                for(int i=0;i<=steps;i++) {
                    var p=Vector3.Lerp(points[j],points[j+1],i/(float)Mathf.Max(1,steps))+c.origin;
                    var hits=Physics.RaycastAll(p+Vector3.up*1,Vector3.down,2,~0,QueryTriggerInteraction.Ignore);
                    var decks=hits.Where(h=>h.normal.y>.8f).OrderBy(h=>h.distance).ToArray();
                    if(decks.Length==0)floor=false;
                    else p=decks[0].point;
                    // Exact prefab height/radius; slight foot lift avoids treating a walkable slope as an obstacle.
                    var obstacles=Physics.OverlapCapsule(p+Vector3.up*(radius+.12f),p+Vector3.up*(height-radius+.12f),radius,~0,QueryTriggerInteraction.Ignore);
                    var blocked=obstacles.Where(o=>colliders.Contains(o)&&o.enabled&&o.gameObject.activeInHierarchy).ToArray();
                    if(blocked.Length>0) {if(clear)report.Add("BLOCKED "+label+" local="+(p-c.origin)+" by "+string.Join(",",blocked.Select(o=>o.name)));clear=false;worst=j;}
                }
            }
            Check(floor,label+" floor at <=0.5m intervals");Check(clear,label+" capsule radius="+radius+" height="+height+" foot tolerance=0.12m; last blocked segment="+worst);
        }
    }
}
