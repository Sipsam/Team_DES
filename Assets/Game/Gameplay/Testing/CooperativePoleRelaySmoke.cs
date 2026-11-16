#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    // Real Relay, two processes. Station positioning is setup ONLY, never evidence of gap traversal.
    public sealed class CooperativePoleRelaySmoke : MonoBehaviour
    {
        bool host, failed;
        string evidence, prefix;
        NetworkManager manager;
        CooperativePole pole;
        PlayerMovement player;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args,"-poleSmoke");
            if (index < 0) return;
            int output = Array.IndexOf(args,"-poleEvidence");
            if (output < 0) throw new ArgumentException("-poleEvidence is required");
            var test = new GameObject("CooperativePoleRelaySmoke").AddComponent<CooperativePoleRelaySmoke>();
            test.host = args[index+1] == "host"; test.evidence = args[output+1];
            test.prefix = test.host ? "host" : "client";
            Directory.CreateDirectory(test.evidence);
        }
        IEnumerator Start()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            Application.logMessageReceived += Log;
            manager = FindFirstObjectByType<NetworkManager>();
            var sessions = FindFirstObjectByType<MultiplayerSessionManager>();
            yield return Until(()=>FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).Length >= 4,"V3 loaded");
            if (host)
            {
                var task = sessions.CreateRoomAsync(); yield return Until(()=>task.IsCompleted,"Relay create",90);
                Check(sessions.HasSession,"Relay session created");
                File.WriteAllText(evidence+"/room.txt",sessions.CurrentJoinCode);
            }
            else
            {
                yield return Until(()=>File.Exists(evidence+"/room.txt"),"Room published",100);
                var task = sessions.JoinRoomAsync(File.ReadAllText(evidence+"/room.txt")); yield return Until(()=>task.IsCompleted,"Relay join",90);
                Check(sessions.HasSession,"Relay joined");
            }
            var game = FindFirstObjectByType<GameStateManager>();
            yield return Until(()=>game.IsGameReady && manager.LocalClient?.PlayerObject != null && CooperativePole.Current != null,"Two players and pole spawned",90);
            if (host) game.StartFoundation();
            yield return Until(()=>game.CanPlay,"Outbound started");
            pole = CooperativePole.Current;
            player = manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
            // Deliberately unauthorized: distant player cannot attach.
            if (host)
            {
                uint rev = pole.Snapshot.revision;
                pole.UseRpc(rev); yield return new WaitForSeconds(.4f);
                Check(!pole.IsAttached(player.OwnerClientId),"Distant attach rejected");
            }
            var target = host ? pole.Snapshot.a : pole.Snapshot.b;
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
            player.GetComponent<OwnerNetworkTransform>().Teleport(target,Quaternion.identity,Vector3.one);
            player.ResetVerticalVelocity(); cc.enabled = true;
            yield return new WaitForSeconds(1);
            if (!host) yield return Until(()=>pole.Snapshot.phase == PolePhase.OneAttached,"First player attached");
            pole.UseRpc(pole.Snapshot.revision);
            yield return Until(()=>pole.IsAttached(player.OwnerClientId),"Own grip attached");
            if (host) yield return Until(()=>pole.Snapshot.Paired,"Two distinct grips");
            else yield return Until(()=>pole.Snapshot.Paired,"Pair replicated");
            var s = pole.Snapshot;
            Check(s.left != s.right,"Unique participants");
            Check(s.Pivot != s.Orbit,"Distinct pivot/orbit");
            yield return new WaitForSeconds(.5f);
            Check(player.GetComponent<PoleMotor>().Attached && !cc.enabled,"Normal controller suspended");
            Check(Vector3.Distance(player.transform.position,host?s.a:s.b)<.05f,"Local endpoint matches snapshot");
            File.WriteAllText(evidence+"/"+prefix+"-paired.txt","ready");
            yield return Until(()=>File.Exists(evidence+"/host-paired.txt") && File.Exists(evidence+"/client-paired.txt"),"Pair barrier");
            if (!host)
            {
                // Stale and non-finite input must be rejected before a valid rotation.
                pole.SteerRpc(s.revision-1,Vector2.one);
                pole.CancelRpc(s.revision-1);
                pole.SteerRpc(s.revision,new Vector2(float.NaN,1));
                yield return new WaitForSeconds(.5f);
                Check(Vector3.Distance(s.b,pole.Snapshot.b)<.01f,"Invalid steering rejected");
                float deadline = Time.realtimeSinceStartup+.8f;
                while (Time.realtimeSinceStartup<deadline) { pole.SteerRpc(pole.Snapshot.revision,new Vector2(.25f,0)); yield return new WaitForSeconds(.05f); }
                pole.SteerRpc(pole.Snapshot.revision,Vector2.zero);
            }
            yield return Until(()=>Mathf.Abs(pole.Snapshot.yaw-s.yaw)>2,"Orbit rotation replicated");
            Check(Mathf.Abs(Vector3.Distance(pole.Snapshot.a,pole.Snapshot.b)-pole.length)<.002f,"Rigid endpoint distance");
            Check(Vector3.Distance(s.PivotPoint,pole.Snapshot.PivotPoint)<.001f,"Pivot remains fixed");
            File.WriteAllText(evidence+"/"+prefix+"-rotation.txt",JsonUtility.ToJson(pole.Snapshot));
            yield return Until(()=>File.Exists(evidence+"/host-rotation.txt") && File.Exists(evidence+"/client-rotation.txt"),"Rotation barrier");
            if (!host) pole.CancelRpc(pole.Snapshot.revision);
            yield return Until(()=>pole.Snapshot.phase == PolePhase.Idle,"Cancel/reset replicated");
            yield return Until(()=>!player.GetComponent<PoleMotor>().Attached && cc.enabled,"Normal controller restored");
            int respawns = player.GetComponent<GameplayCharacter>().RespawnCount.Value;
            yield return AttachAgain("recovery");
            if (!host)
            {
                float deadline = Time.realtimeSinceStartup + .6f;
                while(Time.realtimeSinceStartup < deadline && pole.Snapshot.Paired)
                { pole.SteerRpc(pole.Snapshot.revision,Vector2.down); yield return new WaitForSeconds(.05f); }
            }
            yield return Until(()=>pole.Snapshot.phase == PolePhase.Idle,"Collision failure resets pair");
            Check(pole.Snapshot.failure.ToString().Contains("collision"),"Actual collider caused failure");
            if(!host) yield return Until(()=>player.GetComponent<GameplayCharacter>().RespawnCount.Value>respawns,"Failed orbit respawned");
            else Check(player.GetComponent<GameplayCharacter>().RespawnCount.Value==respawns,"Pivot not forced to respawn");
            yield return Until(()=>!player.GetComponent<PoleMotor>().Attached && cc.enabled,"Recovery unlocks controller");
            yield return AttachAgain("disconnect");
            File.WriteAllText(evidence+"/"+prefix+"-pass.txt","PASS real Relay: pair/grips/roles/rotation/rigid distance/fixed pivot/rejection/cancel/collider failure/individual respawn. NO inter-summit landing, role switch, route completion or human play claim. Disconnect result is logged separately.");
            if (!host)
            {
                yield return Until(()=>File.Exists(evidence+"/host-pass.txt"),"Host completed");
                var task = sessions.LeaveRoomAsync(); yield return Until(()=>task.IsCompleted,"Client leave");
            }
            else
            {
                yield return Until(()=>!game.IsGameReady,"Disconnect pauses foundation");
                yield return Until(()=>pole.Snapshot.phase==PolePhase.Idle && !player.GetComponent<PoleMotor>().Attached,"Attached disconnect releases survivor");
                File.WriteAllText(evidence+"/disconnect-pass.txt","PASS attached client disconnected; server pole idle, survivor movement restored.");
            }
            Application.Quit(0);
        }
        IEnumerator AttachAgain(string stage)
        {
            yield return new WaitForSeconds(.8f);
            var target = host ? pole.Snapshot.a : pole.Snapshot.b;
            var cc = player.GetComponent<CharacterController>(); cc.enabled=false;
            player.GetComponent<OwnerNetworkTransform>().Teleport(target,Quaternion.identity,Vector3.one);
            player.ResetVerticalVelocity(); cc.enabled=true;
            yield return new WaitForSeconds(.8f);
            File.WriteAllText(evidence+"/"+prefix+"-"+stage+"-station.txt","ready");
            yield return Until(()=>File.Exists(evidence+"/host-"+stage+"-station.txt") && File.Exists(evidence+"/client-"+stage+"-station.txt"),stage+" station barrier");
            if(!host) yield return Until(()=>pole.Snapshot.phase==PolePhase.OneAttached,stage+" first attached");
            pole.UseRpc(pole.Snapshot.revision);
            yield return Until(()=>pole.Snapshot.Paired,stage+" pair attached");
            yield return new WaitForSeconds(.3f);
        }
        IEnumerator Until(Func<bool> condition,string label,float timeout=25)
        {
            float deadline=Time.realtimeSinceStartup+timeout;
            while(!condition()&&Time.realtimeSinceStartup<deadline) yield return null;
            Check(condition(),label);
        }
        void Check(bool result,string label)
        {
            if(!result) { Debug.LogError("POLE_SMOKE_FAIL "+label); throw new Exception(label); }
            Debug.Log("POLE_SMOKE_PASS "+label);
        }
        void Log(string message,string stack,LogType type)
        {
            if(failed || (type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)) return;
            failed=true; File.WriteAllText(evidence+"/"+prefix+"-failure.txt",message+"\n"+stack); Application.Quit(2);
        }
    }
}
#endif
