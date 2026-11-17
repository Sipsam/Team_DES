#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
namespace TwinEdge.Gameplay {
 [Serializable] public class MountainPolePlan {
  public float length;
  public string generationAssets;
  public string status="Continuous swept candidate, requires server/Relay replay";
  public List<MountainPoleAction> actions=new();
 }
 [Serializable] public class MountainPoleCollision {public Vector3 pivot,from,to;public float groundHeight;public Vector2 serverIntent;public string snapshot;}
 [Serializable] public class MountainPoleAction {
  public string kind, summit;
  public Vector3 pivot;
  public float yaw,pitch;
 }
}
#endif
