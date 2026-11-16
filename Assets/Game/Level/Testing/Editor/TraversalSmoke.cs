using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwinEdge.Levels.Testing.Editor
{
    // Automated runtime smoke is deliberately marked separately from human play.
    [InitializeOnLoad]
    public static class TraversalSmoke
    {
        static double deadline;
        static int stage;
        static MountainTraversalTest test;
        static TraversalSmoke(){if(SessionState.GetBool("TraversalSmoke",false))EditorApplication.update+=Tick;}
        [MenuItem("Tools/Twin Edge/V3 Traversal/Run Automated Play Smoke")]
        public static void Run()
        {
            Directory.CreateDirectory("Docs/Development/Level/V3/TraversalTests");
            EditorSceneManager.OpenScene(TraversalSetup.ScenePath);
            stage=0;test=null;deadline=0;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            SessionState.SetBool("TraversalSmoke",true);EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try {
                if(deadline==0)deadline=EditorApplication.timeSinceStartup+30;
                if(test==null)test=UnityEngine.Object.FindFirstObjectByType<MountainTraversalTest>();
                if(test==null||!test.Initialized){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Traversal initialization timed out (including runtime camera)");return;}
                var camera=test.RuntimeCamera;
                Require(camera!=null&&camera.isActiveAndEnabled&&camera==Camera.main&&camera.cullingMask!=0&&camera.targetTexture==null,"Active MainCamera renders to Game View");
                var player=test.GetComponentInChildren<CharacterController>();Require(player!=null&&player.enabled&&player.gameObject.activeInHierarchy,"Active test player");
                if(stage==0) {
                    if(!player.isGrounded){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Spawn did not ground");return;}
                    test.SetPreset(0);Require(test.nodes.Count==17,"Sparse count");test.SetPreset(1);Require(test.nodes.Count==22,"Balanced count");test.SetPreset(2);Require(test.nodes.Count==29,"Dense count");
                    player.enabled=false;player.transform.position=new Vector3(0,-100,0);player.enabled=true;stage=1;deadline=EditorApplication.timeSinceStartup+8;return;
                }
                if(stage==1) {
                    if(player.transform.position.y<100){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Fall respawn failed");return;}
                    if(!player.isGrounded)return;
                    test.parameters.reach=160;test.Reanalyze();int nearest=-1;float d=float.MaxValue;
                    for(int i=2;i<test.nodes.Count;i++)if(test.nodes[i].hasLanding&&test.parameters.Allows(player.transform.position,test.nodes[i].landing)) {float x=Vector3.Distance(player.transform.position,test.nodes[i].landing);if(x<d){d=x;nearest=i;}}
                    Require(nearest>=0,"Launch candidate");test.sourceIndex=0;test.targetIndex=nearest;test.Launch();stage=2;deadline=EditorApplication.timeSinceStartup+12;return;
                }
                if(stage==2) {
                    if(test.records.Count==0){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Arc did not terminate");return;}
                    if(test.records[0].success)Require(player.isGrounded,"Physical controller grounding on success");
                    Require(test.records[0].elapsed>0,"Non-teleport attempt duration");Require(File.Exists(test.savedPath),"Runtime JSON saved");
                    // Do not pass automated attempts off as human data.
                    foreach(var record in test.records)record.evidence="Automated runtime smoke; not human play";test.Save();
                    File.WriteAllText("Docs/Development/Level/V3/TraversalTests/RuntimeSmoke.txt","PASS: runtime spawn grounding, preset 15/20/27, fall respawn, collision-controller arc termination, JSON persistence.\nArc result: "+test.records[0].result+"\nNo human input, camera usability, official mechanic, multiplayer or balance acceptance.\n");
                    Capture(test);SessionState.SetBool("TraversalSmoke",false);EditorApplication.update-=Tick;Complete(0);
                }
            }catch(Exception e){SessionState.SetBool("TraversalSmoke",false);Debug.LogException(e);File.WriteAllText("Docs/Development/Level/V3/TraversalTests/RuntimeSmoke.txt","FAIL: "+e);Complete(1);}
        }
        static void Complete(int code)
        {
            SessionState.SetBool("TraversalSmoke",false);EditorApplication.update-=Tick;
            if(Application.isBatchMode)EditorApplication.Exit(code);
            else EditorApplication.isPlaying=false;
        }
        static void Capture(MountainTraversalTest test)
        {
            var camera=test.GetComponentInChildren<Camera>();var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;camera.Render();
            var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes("Docs/Development/Level/V3/TraversalTests/RuntimeCamera.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
        }
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    }
}
