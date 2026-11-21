using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TwinEdge.Gameplay;
using UnityEditor;
using UnityEngine;
namespace TwinEdge.Levels.Editor {
 public static class MountainPoleContinuousPlan {
  public static void Run() {
   MountainPoleReachability.Load();
   var r=JsonUtility.FromJson<MountainPoleReachability.Report>(File.ReadAllText(MountainPoleReachability.Output+"/Tuned.json"));
   MountainPoleReachability.WithQueries(()=>Build(r));
  }
  static string reportPrefix="Continuous";
  public static void VerifyAlternative() {
   MountainPoleReachability.Load();var r=JsonUtility.FromJson<MountainPoleReachability.Report>(File.ReadAllText(MountainPoleReachability.Output+"/Tuned.json"));
   // The west branch passes through Peak_26; the east branch uses Peak_21.
   var alternate = new[] {"Start","Peak_04","Peak_03","Peak_10","Peak_16","Peak_15","Peak_20","Peak_26","Peak_27","Final"};
   r.route=alternate.Select(id=>r.nodes.FindIndex(node=>node.id==id)).ToList();
   if(r.route.Contains(-1))throw new Exception("Missing west-branch summit in reachability report");
   for(int k=1;k<r.route.Count;k++)if(!r.edges.Any(e=>e.clear&&e.from==r.route[k-1]&&e.to==r.route[k]))throw new Exception("Alternative route lacks an isolated edge");
   reportPrefix="AlternativeContinuous";try{MountainPoleReachability.WithQueries(()=>Build(r));}finally{reportPrefix="Continuous";}
  }
  static Vector3 pivot,orbit;static CooperativePole pole;static MountainPolePlan plan;
  static Vector2 Angles(Vector3 d)=>new Vector2(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,Mathf.Asin(d.normalized.y)*Mathf.Rad2Deg);
  static void Rotate(float yaw,float pitch) {
   var a=Angles(orbit-pivot);float dy=Mathf.DeltaAngle(a.x,yaw);int count=Mathf.Max(1,Mathf.CeilToInt((Mathf.Abs(dy)+Mathf.Abs(pitch-a.y))*Mathf.Deg2Rad*pole.length/.1f));
   for(int i=1;i<=count;i++) {Vector3 next=PoleGeometry.Endpoint(pivot,pole.length,a.x+dy*i/count,Mathf.Lerp(a.y,pitch,(float)i/count));if(!MountainPoleReachability.StepClear(orbit,next)||!MountainPoleReachability.RodClear(pivot,next))throw new Exception("Continuous rotation collision at "+next);orbit=next;}
   plan.actions.Add(new MountainPoleAction{kind="Rotate",pivot=pivot,yaw=yaw,pitch=pitch});
  }
  static void ArcTo(Vector3 end) {var a=Angles(orbit-pivot);var b=Angles(end-pivot);Rotate(a.x,pole.maxPitch);Rotate(b.x,pole.maxPitch);Rotate(b.x,b.y);}
  static void Carry(Vector3 delta) {
   delta.y=0;var a=pivot;var b=orbit;int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.04f));
   for(int i=1;i<=steps;i++) {Vector3 da=a+delta*i/steps,db=b+delta*i/steps;
    if(!pole.SafeLanding(da,out var sa)||!pole.SafeLanding(db,out var sb)||sa!=sb||!MountainPoleReachability.StepClear(pivot,da)||!MountainPoleReachability.StepClear(orbit,db)||!MountainPoleReachability.RodClear(da,db))throw new Exception("Continuous same-summit carry blocked at "+da+" / "+db+" supported="+pole.SafeLanding(da,out _)+"/"+pole.SafeLanding(db,out _)+" capsule="+MountainPoleReachability.StepClear(pivot,da)+"/"+MountainPoleReachability.StepClear(orbit,db)+" rod="+MountainPoleReachability.RodClear(da,db));
    pivot=da;orbit=db;
   }
   plan.actions.Add(new MountainPoleAction{kind="Carry",pivot=pivot});
  }
  static void Build(MountainPoleReachability.Report r) {
   pole=UnityEngine.Object.FindFirstObjectByType<CooperativePole>();plan=new MountainPolePlan {length=pole.length,generationAssets=UnityEngine.Object.FindFirstObjectByType<MountainSectionV3>().generationAssets};
   pole.ConfigureHome(r.nodes[0].center);pivot=V3GameplayBootstrap.SafeGround(r.nodes[0].center-Vector3.right*pole.length*.5f);orbit=V3GameplayBootstrap.SafeGround(r.nodes[0].center+Vector3.right*pole.length*.5f);
   if(r.route.Count==0){File.WriteAllText(MountainPoleReachability.Output+"/"+reportPrefix+"Result.txt","FAIL no isolated route candidate; continuous plan not tested");throw new Exception("No isolated route");}
   try {
    for(int k=0;k<r.route.Count-1;k++) {
     int source=r.route[k],target=r.route[k+1];var n=r.nodes[source];
     if(k>0) {
      bool found=false;
      for(int angle=0;angle<360;angle+=2) {var p=PoleGeometry.Endpoint(pivot,pole.length,angle,0);if(pole.SafeLanding(p,out var id)&&id==n.id&&MountainPoleReachability.Arc(pivot,orbit,p,out _)){ArcTo(p);found=true;break;}}
      if(!found)throw new Exception("Cannot recover both feet on "+n.id);
     }
     var midpoint=(pivot+orbit)*.5f;Carry(new Vector3(n.center.x-midpoint.x,0,n.center.z-midpoint.z));
     var edge=r.edges.First(e=>e.clear&&e.from==source&&e.to==target);
     var desired=Angles(edge.start-edge.pivot);var current=Angles(orbit-pivot);float yawDelta=Mathf.DeltaAngle(current.x,desired.x);
     midpoint=(pivot+orbit)*.5f;
     int turns=Mathf.CeilToInt(Mathf.Abs(yawDelta)/15);
     for(int turn=1;turn<=turns;turn++) {
      float yaw=current.x+yawDelta*turn/turns;Vector3 d=PoleGeometry.Endpoint(Vector3.zero,pole.length,yaw,0);
      Carry(midpoint-d*.5f-pivot);Rotate(yaw,0);
     }
     // Keep actual arrival height: carry never changes Y. Flat landing terraces make this coherent.
     Carry(edge.pivot-pivot);
     var aim=Angles(edge.end-pivot);var end=PoleGeometry.Endpoint(pivot,pole.length,aim.x,aim.y);
     if(!pole.SafeLanding(end,out var landed)||landed!=r.nodes[target].id)throw new Exception("Arrival radius/height mismatch");
     ArcTo(end);plan.actions.Add(new MountainPoleAction{kind="Swap",summit=landed});
     (pivot,orbit)=(orbit,pivot);
    }
    bool bothFinal=false;
    foreach(var target in r.nodes[^1].safe.OrderBy(p=>(p-r.nodes[^1].center).sqrMagnitude)) {
     var angle=Angles(target-pivot);var end=PoleGeometry.Endpoint(pivot,pole.length,angle.x,angle.y);
     float clearance=end.y-MountainPoleReachability.Ground(end).y;
     if(clearance<.10f||clearance>.17f||Mathf.Abs(angle.y)>pole.maxPitch||!pole.SafeLanding(end,out var id)||id!=r.nodes[^1].id||!MountainPoleReachability.Arc(pivot,orbit,end,out _))continue;
     ArcTo(end);plan.actions.Add(new MountainPoleAction{kind="Complete",summit=id});bothFinal=true;break;
    }
    if(!bothFinal)throw new Exception("Final pivot reached, but partner cannot join Final");
    File.WriteAllText(MountainPoleReachability.Output+"/"+reportPrefix+"Plan.json",JsonUtility.ToJson(plan,true));
    File.WriteAllText(MountainPoleReachability.Output+"/"+reportPrefix+"Result.txt","PASS continuous production capsule/rod queries, supported carry, rigid arcs, distinct-summit exchange candidates. Server replay/Relay NOT established.");
   } catch(Exception ex) {File.WriteAllText(MountainPoleReachability.Output+"/"+reportPrefix+"Result.txt","FAIL "+ex.Message+"; actions validated="+plan.actions.Count);throw;}
  }
 }
}
