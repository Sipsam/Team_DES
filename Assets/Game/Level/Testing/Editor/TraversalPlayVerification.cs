using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwinEdge.Levels.Testing.Editor
{
    // Local opt-in diagnostics. Never installed in a player build.
    [InitializeOnLoad]
    public static class TraversalPlayVerification
    {
        const string Folder="Docs/Development/Level/V3/TraversalTests";
        static TraversalPlayVerification(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            string request=Folder+"/PlayVerification.request";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            string command=File.ReadAllText(request).Trim();File.Delete(request);
            Directory.CreateDirectory(Folder);
            try {
                if(command=="open") {
                    if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
                    if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Save scene changes first");
                    EditorSceneManager.OpenScene(TraversalSetup.ScenePath);return;
                }
                if(command=="smoke"){TraversalSmoke.Run();return;}
                if(command=="inspect") {
                    var test=UnityEngine.Object.FindFirstObjectByType<MountainTraversalTest>();
                    if(test==null||!test.Initialized)throw new Exception("Traversal not initialized");
                    var camera=test.RuntimeCamera;var player=test.GetComponentInChildren<CharacterController>();
                    if(camera==null||!camera.isActiveAndEnabled||camera!=Camera.main||camera.targetTexture!=null||camera.cullingMask==0)throw new Exception("Invalid game camera");
                    if(player==null||!player.enabled||!player.gameObject.activeInHierarchy||!player.isGrounded)throw new Exception("Player not active and grounded");
                    if(Vector3.Distance(player.transform.position,test.nodes[0].landing)>2)throw new Exception("Player not at start");
                    File.WriteAllText(Folder+"/CameraPlayVerification.txt","PASS: non-batch Editor Play; initialized; active MainCamera; screen output; nonzero culling mask; active grounded player at Start. Position: "+player.transform.position+"; camera: "+camera.transform.position+"; scene dirty: "+test.gameObject.scene.isDirty+"\nAutomated state inspection only; no human input acceptance.");return;
                }
                throw new Exception("Unknown verification command");
            }catch(Exception e){File.WriteAllText(Folder+"/CameraPlayVerification.txt","FAIL: "+e);Debug.LogException(e);}
        }
    }
}
