using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TwinEdge.Gameplay;
using UnityEditor;
using UnityEngine;
namespace TwinEdge.Levels.Editor {
 public static class MountainPoleContinuousPlan {
  static readonly string[] Primary =
   "Start Peak_02 Peak_05 Peak_08 Peak_11 Peak_14 Peak_17 Peak_20 Peak_23 Final".Split(' ');
  static readonly string[] Western =
   "Start Peak_01 Peak_04 Peak_07 Peak_10 Peak_13 Peak_17 Peak_16 Peak_19 Peak_22 Final".Split(' ');
  static readonly string[] Eastern =
   "Start Peak_03 Peak_06 Peak_09 Peak_12 Peak_15 Peak_18 Peak_21 Peak_24 Final".Split(' ');

  static MountainPoleReachability.Report LoadReport() {
   MountainPoleReachability.Load();
   return JsonUtility.FromJson<MountainPoleReachability.Report>(File.ReadAllText(MountainPoleReachability.Output+"/Tuned.json"));
  }

  public static void Run() {
   var r=LoadReport();
   if(TryResolveRoute(r, Primary, out var route, out _))r.route=route;
   MountainPoleReachability.WithQueries(()=>Build(r));
  }

  public static void VerifyAlternative() {
   var r=LoadReport();
   MountainPoleReachability.WithQueries(()=>BuildNamed(r,"AlternativeContinuous",Western));
  }

  public static void VerifyRepresentativeRoutes() {
   var r=LoadReport();
   MountainPoleReachability.WithQueries(() => {
    BuildNamed(r,"Continuous",Primary);
    BuildNamed(r,"AlternativeContinuous",Western);
    BuildNamed(r,"EasternContinuous",Eastern);
   });
  }

  public static void VerifyNamedRoutes() {
   string source=Environment.GetEnvironmentVariable("TWIN_EDGE_POLE_ROUTE_FILE");
   if(string.IsNullOrWhiteSpace(source))
    throw new InvalidOperationException("Set TWIN_EDGE_POLE_ROUTE_FILE to a text file containing one named route per line.");
   var routes=File.ReadLines(source)
    .Where(line=>!string.IsNullOrWhiteSpace(line))
    .Select(line=>line.Split(new[]{' ', '\t', ','},StringSplitOptions.RemoveEmptyEntries))
    .ToArray();
   if(routes.Length==0 || routes.Select(route=>route[0]).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=routes.Length)
    throw new InvalidOperationException("Provide at least one route and use a unique name for each.");
   var r=LoadReport();
   var failures=new List<string>();
   MountainPoleReachability.WithQueries(() => {
    foreach(var route in routes) {
     try { BuildNamed(r,route[0],route.Skip(1)); }
     catch(Exception error) { failures.Add(route[0]+": "+error.Message); }
    }
   });
   if(failures.Count!=0) throw new InvalidOperationException(string.Join("\n",failures));
  }

  static void BuildNamed(MountainPoleReachability.Report report, string name, IEnumerable<string> summits) {
   if(!name.All(ch=>char.IsLetterOrDigit(ch)||ch=='_'||ch=='-'))
    throw new InvalidOperationException("Invalid route name: "+name);
   if(!TryResolveRoute(report,summits,out var route,out string error))
    throw new InvalidOperationException(error);
   report.route=route;
   Build(report,name);
  }

  static bool TryResolveRoute(MountainPoleReachability.Report report, IEnumerable<string> summits,
   out List<int> route, out string error) {
   var indices=summits.Select(id=>report.nodes.FindIndex(node=>node.id==id)).ToList();
   route=indices;
   error=null;
   if(indices.Count<2||indices[0]!=0||indices[^1]!=report.nodes.Count-1||
     indices.Contains(-1)||indices.Distinct().Count()!=indices.Count) {
    error="Expected distinct known summit IDs from Start to Final.";
    return false;
   }
   for(int i=1;i<indices.Count;i++) {
    int from=indices[i-1], to=indices[i];
    if(report.edges.Any(edge=>edge.clear&&edge.from==from&&edge.to==to))continue;
    error="Missing isolated edge: "+report.nodes[from].id+" -> "+report.nodes[to].id;
    return false;
   }
   return true;
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
  static void Build(MountainPoleReachability.Report r, string reportPrefix = "Continuous") {
   pole=UnityEngine.Object.FindFirstObjectByType<CooperativePole>();plan=new MountainPolePlan {length=pole.length,generationAssets=UnityEngine.Object.FindFirstObjectByType<MountainSectionV3>().generationAssets};
   pole.ConfigureHome(r.nodes[0].center);pivot=V3GameplayBootstrap.SafeGround(r.nodes[0].center-Vector3.right*pole.length*.5f);orbit=V3GameplayBootstrap.SafeGround(r.nodes[0].center+Vector3.right*pole.length*.5f);
   if(r.route.Count==0){File.WriteAllText(MountainPoleReachability.Output+"/"+reportPrefix+"Result.txt","FAIL no isolated route candidate; continuous plan not tested");throw new Exception("No isolated route");}
   string leg="Start";
   try {
    for(int k=0;k<r.route.Count-1;k++) {
     int source=r.route[k],target=r.route[k+1];var n=r.nodes[source];
     leg=n.id+" -> "+r.nodes[target].id;
     if(k>0) {
      bool found=false;
      for(int angle=0;angle<360;angle+=2) {var p=PoleGeometry.Endpoint(pivot,pole.length,angle,0);if(pole.SafeLanding(p,out var id)&&id==n.id&&MountainPoleReachability.Arc(pivot,orbit,p,out _)){ArcTo(p);found=true;break;}}
      if(!found)throw new Exception("Cannot recover both feet on "+n.id);
     }
     var midpoint=(pivot+orbit)*.5f;Carry(new Vector3(n.center.x-midpoint.x,0,n.center.z-midpoint.z));
     var edge=r.edges.First(e=>e.clear&&e.from==source&&e.to==target);
     var desired=Angles(edge.start-edge.pivot);var current=Angles(orbit-pivot);float yawDelta=Mathf.DeltaAngle(current.x,desired.x);
     midpoint=(pivot+orbit)*.5f;
     // During carry the stationary heading puts the orbit foot farther from the centre
     // than at either centred pose. Narrower shelves need shorter heading increments.
     float radius=Mathf.Min(n.summitSize.x,n.summitSize.y)*.5f-1.15f;
     float half=pole.length*.5f;
     float cosine=(pole.length*pole.length+half*half-radius*radius)/(2*pole.length*half);
     float maxTurn=Mathf.Clamp(Mathf.Acos(Mathf.Clamp(cosine,-1,1))*Mathf.Rad2Deg,4,15);
     int turns=Mathf.CeilToInt(Mathf.Abs(yawDelta)/maxTurn);
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
    File.WriteAllText(MountainPoleReachability.Output+"/"+reportPrefix+"Result.txt","PASS continuous production capsule/rod queries, supported carry, rigid arcs and distinct-summit exchange candidates. Relay replay is separate.\n"+
     string.Join(" -> ",r.route.Select(i=>r.nodes[i].id))+"; actions="+plan.actions.Count);
    Debug.Log("MOUNTAIN_CONTINUOUS "+reportPrefix+" "+string.Join(" -> ",r.route.Select(i=>r.nodes[i].id)));
   } catch(Exception ex) {
    string failure=leg+": "+ex.Message+"; actions validated="+plan.actions.Count;
    File.WriteAllText(MountainPoleReachability.Output+"/"+reportPrefix+"Result.txt","FAIL "+failure);
    throw new InvalidOperationException(failure,ex);
   }
  }
 }
}
