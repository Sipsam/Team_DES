#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
namespace TwinEdge.Gameplay {
 // Opt-in development input replay. Only the initial grip setup uses station placement.
 [DefaultExecutionOrder(11000)]
 public sealed class MountainPoleRelayReplay:MonoBehaviour {
  bool host,failed,firstExchangeOnly; FullMatchRelayReplay fullMatch;string output,planPath,prefix;NetworkManager manager;CooperativePole pole;Vector2 steering;bool send;MountainPolePlan plan;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install(){
   var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-mountainPoleRun");if(i<0)return;
   var test=new GameObject("MountainPoleRelayReplay").AddComponent<MountainPoleRelayReplay>();test.host=a[i+1]=="host";test.prefix=test.host?"host":"client";
   test.firstExchangeOnly=Array.IndexOf(a,"-mountainFirstExchange")>=0;
   test.output=a[Array.IndexOf(a,"-mountainPoleEvidence")+1];test.planPath=a[Array.IndexOf(a,"-mountainPolePlan")+1];Directory.CreateDirectory(test.output);
  }
  void LateUpdate(){
   if(fullMatch==null&&send&&pole&&pole.IsSpawned&&pole.Snapshot.Paired){
    var player=manager.LocalClient?.PlayerObject;var s=pole.Snapshot;
    if(player&&player.GetComponent<PoleMotor>().Attached)Check(Vector3.Distance(player.transform.position,s.left==player.OwnerClientId?s.a:s.b)<.01f,"LateUpdate endpoint agrees with received snapshot");
    pole.SteerRpc(s.revision,steering);
   }
  }
  IEnumerator Start(){
   if(fullMatch!=null)yield break;
   Application.runInBackground=true;Application.targetFrameRate=60;Application.logMessageReceived+=Log;
   plan=JsonUtility.FromJson<MountainPolePlan>(File.ReadAllText(planPath));
   manager=FindFirstObjectByType<NetworkManager>();var sessions=FindFirstObjectByType<MultiplayerSessionManager>();
   yield return Until(()=>FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).Length>=4,"V3 loaded",90);
   if(host){var t=sessions.CreateRoomAsync();yield return Until(()=>t.IsCompleted,"Relay create",90);Check(sessions.HasSession,"Relay created");File.WriteAllText(output+"/room.tmp",sessions.CurrentJoinCode);File.Move(output+"/room.tmp",output+"/room.txt");}
   else{yield return Until(()=>File.Exists(output+"/room.txt"),"Host published session",120);var t=sessions.JoinRoomAsync(File.ReadAllText(output+"/room.txt"));yield return Until(()=>t.IsCompleted,"Relay join",90);Check(sessions.HasSession,"Relay joined");}
   var game=FindFirstObjectByType<GameStateManager>();
   yield return RelayMatchStart.Run(game);
   yield return Until(()=>game.IsGameReady&&manager.LocalClient?.PlayerObject&&CooperativePole.Current,"Both players spawned",90);
   yield return Until(()=>game.CanPlay,"Outbound",30);pole=CooperativePole.Current;Check(Mathf.Abs(plan.length-pole.length)<.0001f&&FindFirstObjectByType<TwinEdge.Levels.MountainSectionV3>().generationAssets==plan.generationAssets,"Plan matches saved terrain / length");
   if(host)pole.CollisionRejected+=(pivot,from,to)=>{
    float ground=-10000;
    foreach(var hit in Physics.RaycastAll(to+Vector3.up*2,Vector3.down,4,~0,QueryTriggerInteraction.Ignore))if(hit.collider!=pole.supportCollider&&!hit.collider.transform.IsChildOf(pole.transform)&&hit.collider.GetComponentInParent<PlayerMovement>()==null)ground=Mathf.Max(ground,hit.point.y);
    var trace=new MountainPoleCollision{pivot=pivot,from=from,to=to,groundHeight=ground,snapshot=JsonUtility.ToJson(pole.Snapshot),serverIntent=(Vector2)typeof(CooperativePole).GetField("intent",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(pole)};
    File.AppendAllText(output+"/server-collisions.jsonl",JsonUtility.ToJson(trace)+"\n");
   };
   var player=manager.LocalClient.PlayerObject;var cc=player.GetComponent<CharacterController>();cc.enabled=false;
   player.GetComponent<OwnerNetworkTransform>().Teleport(host?pole.Snapshot.a:pole.Snapshot.b,Quaternion.identity,Vector3.one);player.GetComponent<PlayerMovement>().ResetVerticalVelocity();cc.enabled=true;
   yield return new WaitForSeconds(1);if(!host)yield return Until(()=>pole.Snapshot.phase==PolePhase.OneAttached,"Host grip",30);
   pole.UseRpc(pole.Snapshot.revision);yield return Until(()=>pole.Snapshot.Paired,"Both grips",30);yield return new WaitForSeconds(.4f);
   yield return WalkPoleRoute(player);
   if(firstExchangeOnly){
    Check(swaps==1,"First supported role exchange completed");
    if(host)pole.CancelRpc(pole.Snapshot.revision);
    yield return Until(()=>pole.Snapshot.phase==PolePhase.Idle&&!player.GetComponent<PoleMotor>().Attached&&cc.enabled,
     "First exchange release restores ordinary movement",30);
    File.WriteAllText(output+"/"+prefix+"-exchange-pass.txt","PASS supported first role exchange and release");
    string other=host?"client":"host";
    yield return Until(()=>File.Exists(output+"/"+other+"-exchange-pass.txt"),"Peer verified first exchange and release",30);
    Application.Quit(0);
    yield break;
   }
   var own=player.GetComponent<GameplayCharacter>();
   var hostCharacter=FindObjectsByType<GameplayCharacter>(FindObjectsSortMode.None).First(c=>c.OwnerClientId==NetworkManager.ServerClientId);
   string partner=host?"client":"host";
   int hostRespawns=hostCharacter.RespawnCount.Value,ownRespawns=own.RespawnCount.Value;
   if(host)pole.CancelRpc(pole.Snapshot.revision);
   yield return Until(()=>pole.Snapshot.phase==PolePhase.Idle&&!player.GetComponent<PoleMotor>().Attached&&cc.enabled&&player.GetComponent<OwnerNetworkTransform>().enabled,"Final cancel restores normal movement",30);
   File.WriteAllText(output+"/"+prefix+"-cancelled.txt","released");
   yield return Until(()=>File.Exists(output+"/"+partner+"-cancelled.txt"),"Cancel peer barrier",30);
   if(host){
    // Fall-test setup ONLY, after both real Final arrivals were recorded. Never a traversal success.
    var c=pole.mountain;cc.enabled=false;player.GetComponent<OwnerNetworkTransform>().Teleport(new Vector3(c.destination.position.x+1200,c.destination.height+1,c.destination.position.y),Quaternion.identity,Vector3.one);player.GetComponent<PlayerMovement>().ResetVerticalVelocity();cc.enabled=true;
   }
   Vector3 finalCenter=new Vector3(pole.mountain.destination.position.x,pole.mountain.destination.height,pole.mountain.destination.position.y);
   yield return Until(()=>hostCharacter.RespawnCount.Value>hostRespawns&&Vector3.Distance(hostCharacter.transform.position,finalCenter)<4,"Normal gravity fall respawns at existing Final checkpoint / replicated",60);
   if(!host)Check(own.RespawnCount.Value==ownRespawns,"Partner not forced to respawn");
   Check(own.CheckpointName.Value.ToString().Contains("Big summit"),"Final checkpoint retained");
   File.WriteAllText(output+"/"+prefix+"-recovered.txt","PASS Final cancellation, normal gravity fall and checkpoint respawn; hostCount="+hostCharacter.RespawnCount.Value+" ownCount="+own.RespawnCount.Value);
   yield return Until(()=>File.Exists(output+"/"+partner+"-recovered.txt"),"Recovery peer barrier",30);
   File.WriteAllText(output+"/"+prefix+"-pass.txt","PASS real Relay input/RPC route after initial grip setup; both participants actually on Final; exchanges="+swaps+"; actual arrival saved separately. PASS Final cancel and normal gravity / existing Final checkpoint respawn. Fall setup teleport after arrival is NOT traversal evidence.");
   yield return new WaitForSeconds(1);
   if(!host){yield return Until(()=>File.Exists(output+"/host-pass.txt"),"Host final result",30);var t=sessions.LeaveRoomAsync();yield return Until(()=>t.IsCompleted,"Disconnect",30);}else{yield return Until(()=>!game.IsGameReady&&pole.Snapshot.phase==PolePhase.Idle,"Disconnect after Final recovery",30);File.WriteAllText(output+"/disconnect-pass.txt","PASS disconnect after Final cancellation/recovery; active attached motion disconnect is a separate RecoveryRegression scenario");}
   Application.Quit(0);
  }
  int swaps;
  internal IEnumerator ReplayFromSpawn(FullMatchRelayReplay match, string routePath){
   fullMatch=match;host=match.Host;prefix=match.Role;output=match.Output;manager=match.Manager;
   plan=JsonUtility.FromJson<MountainPolePlan>(File.ReadAllText(routePath));
   pole=CooperativePole.Current;
   Check(Mathf.Abs(plan.length-pole.length)<.0001f&&FindFirstObjectByType<TwinEdge.Levels.MountainSectionV3>().generationAssets==plan.generationAssets,
    "Plan matches saved terrain / length");
   var grip=host?pole.Snapshot.a:pole.Snapshot.b;
   yield return match.Walk(grip,.09f,false);
   yield return match.Barrier("pole-grips-in-reach");
   match.Capture("mountain-first-grip");
   if(!host)yield return Until(()=>pole.Snapshot.phase==PolePhase.OneAttached,"Host grip",30);
   yield return match.Tap(UnityEngine.InputSystem.Key.E);
   yield return Until(()=>pole.Snapshot.Paired,"Both grips from normal spawn",15);
   yield return WalkPoleRoute(match.Player.NetworkObject);
   if(host)yield return match.Tap(UnityEngine.InputSystem.Key.Escape);
   yield return Until(()=>pole.Snapshot.phase==PolePhase.Idle&&!match.Player.GetComponent<PoleMotor>().Attached&&
    match.Player.GetComponent<CharacterController>().enabled&&match.Player.GetComponent<OwnerNetworkTransform>().enabled,"Pole release restores walking",15);
  }
  IEnumerator WalkPoleRoute(NetworkObject player){
   swaps=0;ulong local=player.OwnerClientId, firstPivot=pole.Snapshot.Pivot, firstOrbit=pole.Snapshot.Orbit;
   BoxCollider landingObstruction=null;
   for(int i=0;i<plan.actions.Count;i++){
    var action=plan.actions[i];float deadline=Time.realtimeSinceStartup+60;bool done=false;float stableSince=-1, diagnosticAt=Time.realtimeSinceStartup+3;
    if(fullMatch==null&&swaps==0&&action.kind=="Rotate"&&i+1<plan.actions.Count&&plan.actions[i+1].kind=="Swap")
    {
     // Only the final shaft span meets this collider. The real mountain supplies all footing.
     var end=PoleGeometry.Endpoint(action.pivot,pole.length,action.yaw,action.pitch);
     landingObstruction=new GameObject("Landing midspan rock fixture").AddComponent<BoxCollider>();
     landingObstruction.transform.position=(action.pivot+end)*.5f+Vector3.up*1.1f;
     landingObstruction.size=new Vector3(2,2,2);
     Physics.SyncTransforms();
    }
    string hostStep=Path.Combine(output,"host-step-"+i+".txt");
    // The client keeps driving while the host checks the authoritative pose. A briefly
    // matching delayed snapshot cannot leave the host waiting with all input released.
    while(!done&&Time.realtimeSinceStartup<deadline){
     var s=pole.Snapshot;Check(s.Paired,"Pair remains attached at action "+i+" failure="+s.failure);
     bool pivotRole=s.Pivot==local;steering=Vector2.zero;send=true;
     if(action.kind=="Carry"){
      var delta=action.pivot-s.PivotPoint;delta.y=0;done=delta.magnitude<.05f;
      if(pivotRole&&!done)steering=Vector2.ClampMagnitude(new Vector2(delta.x,delta.z)/(player.GetComponent<PlayerMovement>().GroundMoveSpeed(false)*.34f),1);
      if(host&&!done&&Time.realtimeSinceStartup>=diagnosticAt){diagnosticAt=Time.realtimeSinceStartup+3;TraceCarry(i,s);}
     }else if(action.kind=="Rotate"){
      Vector3 wanted=PoleGeometry.Endpoint(action.pivot,pole.length,action.yaw,action.pitch)-s.PivotPoint;
      float targetYaw=Mathf.Atan2(wanted.x,wanted.z)*Mathf.Rad2Deg;
      float targetPitch=Mathf.Clamp(Mathf.Asin(Mathf.Clamp(wanted.y/pole.length,-1,1))*Mathf.Rad2Deg,-pole.maxPitch,pole.maxPitch);
      float yaw=Mathf.DeltaAngle(s.yaw,targetYaw),pitch=targetPitch-s.pitch;done=Mathf.Abs(yaw)<.08f&&Mathf.Abs(pitch)<.08f;
      if(!pivotRole&&!done)steering=Vector2.ClampMagnitude(new Vector2(yaw/(pole.angularSpeed*.6f),pitch/(pole.pitchSpeed*.6f)),1);
     }else if(action.kind=="Complete"){
      done=pole.SafeLanding(s.a,out var a)&&a==action.summit&&pole.SafeLanding(s.b,out var b)&&b==action.summit;
      if(!done&&!pivotRole){
       var ground=Physics.RaycastAll(s.OrbitPoint+Vector3.up*2,Vector3.down,6,~0,QueryTriggerInteraction.Ignore)
        .Where(h=>h.collider!=pole.supportCollider&&!h.collider.transform.IsChildOf(pole.transform)&&h.collider.GetComponentInParent<PlayerMovement>()==null&&h.normal.y>=.85f).OrderBy(h=>h.distance).ToArray();
       if(ground.Length>0){float desired=Mathf.Asin(Mathf.Clamp((ground[0].point.y+.12f-s.PivotPoint.y)/pole.length,-1,1))*Mathf.Rad2Deg;steering.y=Mathf.Clamp((desired-s.pitch)/(pole.pitchSpeed*.6f),-.05f,.05f);}
      }
     }else{
      if(fullMatch==null && swaps==0 && s.phase==PolePhase.Landed &&
       !File.Exists(output+"/"+prefix+"-cross-summit-carry.json"))
      {
       yield return VerifyCrossSummitCarry(landingObstruction);
       s=pole.Snapshot;
       pivotRole=s.Pivot==local;
      }
      ulong expectedPivot=swaps%2==0?firstOrbit:firstPivot;
      done=s.Pivot==expectedPivot;
      if(done)Check(pole.SafeLanding(s.PivotPoint,out var actualSummit)&&actualSummit==action.summit,"Expected pivot on "+action.summit);
      if(!done&&s.phase==PolePhase.Landed&&!pivotRole){
       Check(pole.SafeLanding(s.OrbitPoint,out var destination)&&destination==action.summit,"Expected landing before exchange");
       if(fullMatch==null) pole.UseRpc(s.revision);
       else {
        fullMatch.SteerPole(Vector2.zero);
        yield return fullMatch.Tap(UnityEngine.InputSystem.Key.E);
       }
      }
     }
     if(done){
      if(stableSince<0)stableSince=Time.realtimeSinceStartup;
      done=Time.realtimeSinceStartup-stableSince>=.5f&&(host||File.Exists(hostStep));
     }else stableSince=-1;
     if(fullMatch!=null)fullMatch.SteerPole(steering);
     yield return null;
    }
    steering=Vector2.zero;if(fullMatch!=null)fullMatch.SteerPole(Vector2.zero);File.AppendAllText(output+"/"+prefix+"-states.jsonl",i+" "+JsonUtility.ToJson(pole.Snapshot)+"\n");Check(done,"Action "+i+" "+action.kind+" expected yaw="+action.yaw+" pitch="+action.pitch+" actual="+JsonUtility.ToJson(pole.Snapshot));
    if(action.kind=="Complete"){
     if(fullMatch!=null)fullMatch.Capture("mountain-both-final");
     else {
      ScreenCapture.CaptureScreenshot(output+"/"+prefix+"-both-final.png");
      PoleRuntimeCapture.Save(pole,output+"/"+prefix+"-pole-both-final.png");
     }
    }
    if(action.kind=="Swap"){
     swaps++;
     if(swaps==1&&landingObstruction!=null)
     {
      Check(CooperativePoleRelaySmoke.LegacyRodIntersects(pole.Snapshot,landingObstruction),
       "Roles exchanged while the shaft still intersects the obstruction");
      Destroy(landingObstruction.gameObject);
     }
     if(fullMatch!=null){
      if(swaps is 1 or 4 or 7)fullMatch.Capture("mountain-landing-"+swaps);
      if(action.summit is "Peak_25" or "Peak_26" or "Peak_27")fullMatch.Capture("mountain-side-ridge-"+action.summit);
     }else{
      ScreenCapture.CaptureScreenshot(output+"/"+prefix+"-landing-"+swaps+".png");
      PoleRuntimeCapture.Save(pole,output+"/"+prefix+"-pole-"+swaps+".png");
      if(swaps==1)
      {
       var boundaries=FindFirstObjectByType<SummitBoundaries>();
       yield return Until(()=>boundaries!=null&&boundaries.Ready,"Landing boundaries generated",10);
       Check(boundaries.GetComponentsInChildren<MeshRenderer>().Length==pole.mountain.smallPeaks.Count+2 &&
        boundaries.GetComponentsInChildren<Collider>().Length==0,"Each saved summit has a visual boundary with no collider");
       PoleRuntimeCapture.SaveOwnerView(output+"/"+prefix+"-summit-boundary.png");
      }
     }
    }
    File.WriteAllText(output+"/"+prefix+"-step-"+i+".txt",i.ToString());
    string other=host?"client":"host";yield return Until(()=>File.Exists(output+"/"+other+"-step-"+i+".txt"),"Peer step "+i,30);
    if(firstExchangeOnly&&swaps==1){send=false;yield break;}
   }
   send=false;if(fullMatch!=null)fullMatch.SteerPole(Vector2.zero);Check(pole.SafeLanding(pole.Snapshot.a,out var finalA)&&finalA=="Final"&&pole.SafeLanding(pole.Snapshot.b,out var finalB)&&finalB=="Final","Both participants really on Final");
   var own=player.GetComponent<GameplayCharacter>();
   yield return Until(()=>own.CheckpointName.Value.ToString().Contains("Big summit"),"Actual Final summit checkpoint registered",30);
   File.WriteAllText(output+"/"+prefix+"-arrival.json",JsonUtility.ToJson(pole.Snapshot));
   File.WriteAllText(output+"/"+prefix+"-arrived.txt",own.CheckpointName.Value.ToString());
   string partner=host?"client":"host";
   yield return Until(()=>File.Exists(output+"/"+partner+"-arrived.txt"),"Both actual Final proofs",30);
  }
  IEnumerator VerifyCrossSummitCarry(Collider obstruction)
  {
   var start=pole.Snapshot;
   bool pivotSupported=pole.SafeLanding(start.PivotPoint,out var origin);
   bool orbitSupported=pole.SafeLanding(start.OrbitPoint,out var destination);
   Check(pivotSupported && orbitSupported && origin!=destination &&
    Mathf.Abs(start.PivotPoint.y-start.OrbitPoint.y)>.5f,"Different supported summits and elevations");
   Check(obstruction!=null&&CooperativePoleRelaySmoke.LegacyRodIntersects(start,obstruction),
    "Landed while the old shaft cast still detects the obstruction");
   const float stride=.14f;
   if(host)
   {
    var pivot=manager.ConnectedClients[start.Pivot].PlayerObject;
    var orbit=manager.ConnectedClients[start.Orbit].PlayerObject;
    Vector3 direction=Vector3.zero;
    for(int angle=0;angle<360;angle+=15)
    {
     var test=start;
     var axis=Quaternion.Euler(0,angle,0)*Vector3.forward;
     if(!pole.TryCarry(ref test,axis*(stride*.5f),origin,pivot,orbit) ||
      !pole.TryCarry(ref test,axis*(stride*.5f),origin,pivot,orbit) ||
      !pole.SafeLanding(test.OrbitPoint,out var landing) || landing!=destination) continue;
     direction=axis;
     break;
    }
    Check(direction!=Vector3.zero,"A supported cross-summit pivot step exists at the first landing");
    var goal=start.PivotPoint+direction*stride;
    yield return DrivePivot(goal,pivot);
    var moved=pole.Snapshot;
    Check(pole.SafeLanding(moved.PivotPoint,out var pivotId) && pivotId==origin &&
     pole.SafeLanding(moved.OrbitPoint,out var orbitId) && orbitId==destination &&
     Vector3.Distance(moved.OrbitPoint-start.OrbitPoint,moved.PivotPoint-start.PivotPoint)<.002f &&
     Mathf.Abs(Vector3.Distance(moved.a,moved.b)-pole.length)<.002f,"Cross-summit translation kept both support and 32m span");
    Check(CooperativePoleRelaySmoke.LegacyRodIntersects(moved,obstruction),
     "Both endpoints moved while the shaft intersects the obstruction");
    WriteCrossSummit("host-moved",moved);
    yield return Until(()=>File.Exists(output+"/client-cross-summit-carry.json"),"Client observed translated endpoints",8);
    yield return DrivePivot(start.PivotPoint,pivot);
    yield return Until(()=>pole.Snapshot.phase==PolePhase.Landed,"Landing dwell is valid before exchange",8);
   }
   else
   {
    yield return Until(()=>File.Exists(output+"/host-moved-cross-summit-carry.json"),"Server reached cross-summit step",8);
    var moved=JsonUtility.FromJson<PoleSnapshot>(File.ReadAllText(output+"/host-moved-cross-summit-carry.json"));
    yield return Until(()=>Vector3.Distance(moved.a,pole.Snapshot.a)<.01f && Vector3.Distance(moved.b,pole.Snapshot.b)<.01f,
     "Client snapshot agrees with translated server endpoints",8);
    WriteCrossSummit(prefix,pole.Snapshot);
    yield return Until(()=>File.Exists(output+"/host-cross-summit-carry.json"),"Pivot returned to the planned landing pose",8);
   }
   if(host) WriteCrossSummit(prefix,pole.Snapshot);
  }
  IEnumerator DrivePivot(Vector3 goal,NetworkObject pivot)
  {
   float deadline=Time.realtimeSinceStartup+5f;
   Vector3 remaining;
   while(Time.realtimeSinceStartup<deadline && pole.Snapshot.Paired)
   {
    remaining=goal-pole.Snapshot.PivotPoint;
    remaining.y=0;
    if(remaining.magnitude<.005f) break;
    float speed=pivot.GetComponent<PlayerMovement>().GroundMoveSpeed(false);
    // Keep the precision probe above the normal steer dead zone.
    var direction=new Vector2(remaining.x,remaining.z).normalized;
    steering=direction*Mathf.Clamp(remaining.magnitude/(speed*.35f),.04f,1);
    yield return null;
   }
   steering=Vector2.zero;
   yield return new WaitForSeconds(.3f);
   remaining=goal-pole.Snapshot.PivotPoint;
   remaining.y=0;
   Check(remaining.magnitude<.008f,"Pivot reached supported cross-summit waypoint");
  }
  void WriteCrossSummit(string role,PoleSnapshot snapshot)
  {
   string path=output+"/"+role+"-cross-summit-carry.json";
   File.WriteAllText(path+".tmp",JsonUtility.ToJson(snapshot));
   File.Move(path+".tmp",path);
  }
  IEnumerator Until(Func<bool> f,string name,float timeout){float until=Time.realtimeSinceStartup+timeout;while(!f()&&Time.realtimeSinceStartup<until)yield return null;Check(f(),name);}
  void TraceCarry(int step,PoleSnapshot s){
   const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
   object Field(string key)=>typeof(CooperativePole).GetField(key,flags).GetValue(pole);
   var pivot=manager.ConnectedClients[s.Pivot].PlayerObject;
   var orbit=manager.ConnectedClients[s.Orbit].PlayerObject;
   var move=(Vector2)Field("pivotIntent");var delta=new Vector3(move.x,0,move.y)*(pivot.GetComponent<PlayerMovement>().GroundMoveSpeed(false)*Time.fixedDeltaTime);
   var control=(Vector2)Field("intent");double now=manager.ServerTime.Time;
   bool before=pole.SafeLanding(s.OrbitPoint,out var current),a=pole.SafeLanding(s.a+delta,out var nextA),b=pole.SafeLanding(s.b+delta,out var nextB);
   var pose=s;
   bool canCarry=pole.TryCarry(ref pose,delta,(string)Field("pivotSummit"),pivot,orbit);
   File.AppendAllText(output+"/server-carry.jsonl",$"step={step} t={now:F3} inputAge={now-(double)Field("inputAt"):F4} pivotAge={now-(double)Field("pivotInputAt"):F4} control={control:F5} move={move:F5} summit={Field("pivotSummit")} orbitSupport={before}/{current} next={a}/{nextA},{b}/{nextB} canCarry={canCarry} state={JsonUtility.ToJson(s)}\n");
  }
  void Check(bool ok,string label){if(!ok)throw new Exception("MOUNTAIN_REPLAY_FAIL "+label);}
  void Log(string message,string stack,LogType type){if(failed||(type!=LogType.Exception&&type!=LogType.Error&&type!=LogType.Assert))return;failed=true;File.WriteAllText(output+"/"+prefix+"-failure.txt",message+"\n"+stack);Application.Quit(2);}
 }
}
#endif
