using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TwinEdge.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TwinEdge.Gameplay.Editor
{
    public static class MountainReturnValidation
    {
        [Serializable] public class Leg { public string from, to; public Vector3 start, end, initial; public float yaw, pitch, charge, time, horizontal, heightDelta, landingSpeed; }
        [Serializable] public class Plan { public List<Leg> legs = new(); public string status; }
        public const string Output = "Docs/Development/Gameplay/V1/MountainReturn";
        public static void ValidateCandidatesAndBuild()
        {
            Directory.CreateDirectory(Output);
            var tuning = AssetDatabase.LoadAssetAtPath<MountainLaunchConfig>(MountainReturnSetup.ConfigPath);
            var mountain = AssetDatabase.LoadAssetAtPath<MountainConfigV3>("Assets/Game/Level/Data/MountainConfigV3.asset");
            var route = JsonUtility.FromJson<Plan>(File.ReadAllText(Output + "/Route.json"));
            var results = new List<string>();
            foreach (var leg in route.legs)
            {
                string candidate = MountainReturnLaunch.EstimateCandidate(tuning, mountain, leg.start, leg.initial, .7f);
                results.Add(leg.from + " -> " + leg.to + " predicted=" + candidate);
                if (candidate != leg.to) throw new Exception("Diagnostic candidate disagrees with recorded actual route: " + results.Last());
            }
            File.WriteAllLines(Output + "/CandidateValidation.txt", results.Concat(new[] { "PASS nine diagnostic predictions against recorded actual CC/Relay route. This does not predict arbitrary future collisions." }));
            Debug.Log("MOUNTAIN_RETURN_CANDIDATE_PASS");
            MountainReturnSetup.ValidateAndBuild();
        }
        [MenuItem("Tools/Twin Edge/Gameplay/Validate Mountain Return Route")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/Game/Level/Scenes/LevelBlockoutV3.unity"); Physics.SyncTransforms();
            var mountain = Object.FindFirstObjectByType<MountainSectionV3>().config;
            var config = AssetDatabase.LoadAssetAtPath<MountainLaunchConfig>(MountainReturnSetup.ConfigPath);
            var peaks = new[] { mountain.destination }.Concat(mountain.smallPeaks).Concat(new[] { mountain.start }).ToArray();
            Vector3 Center(MountainPeakV3 p) { var xz = p == mountain.start || p == mountain.destination ? p.position : mountain.clusterCenter + Vector2.Scale(p.position, mountain.clusterSize); return new Vector3(xz.x, p.height, xz.y); }
            Vector3 Ground(Vector3 p) { var hits = Physics.RaycastAll(p + Vector3.up * 6, Vector3.down, 18, ~0, QueryTriggerInteraction.Ignore).Where(h => h.collider is not CharacterController).OrderBy(h => h.distance).ToArray(); return hits.Length > 0 && hits[0].normal.y > .85f ? hits[0].point + Vector3.up * .12f : Vector3.positiveInfinity; }
            var obj = new GameObject("ReturnCapsuleProbe");
            var cc = obj.AddComponent<CharacterController>(); cc.height = 2; cc.radius = .45f; cc.center = Vector3.up; cc.minMoveDistance = 0;
            var arrival = new Dictionary<string, Vector3>(); var path = new Dictionary<string, List<Leg>>(); var queue = new Queue<MountainPeakV3>();
            arrival[peaks[0].id] = Ground(Center(peaks[0])); path[peaks[0].id] = new(); queue.Enqueue(peaks[0]);
            var attempts = new List<string>();
            try
            {
                while (queue.Count > 0 && !arrival.ContainsKey(mountain.start.id))
                {
                    var from = queue.Dequeue(); var arrived = arrival[from.id];
                    var start = arrived;
                    foreach (var to in peaks.Where(p => !arrival.ContainsKey(p.id)).OrderBy(p => Vector3.Distance(start, Center(p))))
                    {
                        start = arrived;
                        if (Vector3.Distance(start, Center(to)) > 125) continue;
                        cc.enabled = false; obj.transform.position = start; cc.enabled = true; Physics.SyncTransforms();
                        for (int walk = 0; walk < 160; walk++)
                        {
                            var dir = Vector3.ProjectOnPlane(Center(to) - obj.transform.position, Vector3.up).normalized;
                            var next = Ground(obj.transform.position + dir * .4f);
                            if (!float.IsFinite(next.y) || PoleGeometry.Summit(next, mountain, 2) != from.id || Mathf.Abs(next.y - obj.transform.position.y) > .5f) break;
                            var flags = cc.Move(dir * .4f + Vector3.down * .1f);
                            if ((flags & CollisionFlags.Sides) != 0) break;
                            start = obj.transform.position;
                        }
                        Leg success = null;
                        foreach (float x in new[] { 0f, -5f, 5f, -10f, 10f, -20f, 20f, -30f, 30f, -40f, 40f })
                        foreach (float z in new[] { 0f, -5f, 5f, -10f, 10f, -20f, 20f, -30f, 30f, -40f, 40f })
                        {
                            if (success != null) break;
                            var target = Ground(Center(to) + new Vector3(x, 0, z));
                            if (!float.IsFinite(target.y) || PoleGeometry.Summit(target, mountain, 1) != to.id) continue;
                            var delta = target - start; float distance = new Vector2(delta.x, delta.z).magnitude;
                            foreach (float angle in new[] { 45f, 55f, 65f, 35f, 25f })
                            {
                                float rad = angle * Mathf.Deg2Rad;
                                float denominator = 2 * Mathf.Cos(rad) * Mathf.Cos(rad) * (distance * Mathf.Tan(rad) - delta.y);
                                if (denominator <= 0) continue;
                                float speed = Mathf.Sqrt(config.gravity * distance * distance / denominator);
                                if (speed < config.minSpeed || speed > config.maxSpeed) continue;
                                float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                                var v = config.Velocity(yaw, angle, (speed - config.minSpeed) / (config.maxSpeed - config.minSpeed)); var initial = v;
                                cc.enabled = false; obj.transform.position = start; cc.enabled = true; Physics.SyncTransforms();
                                bool landed = false; float time = 0; float landingSpeed = 0;
                                for (; time < config.maxFlightSeconds; time += 1f / 120)
                                {
                                    var flags = cc.Move(config.Step(ref v, 1f / 120));
                                    if ((flags & (CollisionFlags.Sides | CollisionFlags.Above)) != 0) break;
                                    if (v.y < 0 && (flags & CollisionFlags.Below) != 0)
                                    { landed = PoleGeometry.Summit(obj.transform.position, mountain, 1) == to.id; landingSpeed = new Vector2(v.x, v.z).magnitude; break; }
                                }
                                if (!landed) continue;
                                success = new Leg { from = from.id, to = to.id, start = start, end = obj.transform.position, initial = initial, yaw = yaw, pitch = angle, charge = (speed - config.minSpeed) / (config.maxSpeed - config.minSpeed), time = time, horizontal = distance, heightDelta = delta.y, landingSpeed = landingSpeed }; break;
                            }
                        }
                        attempts.Add(from.id + " -> " + to.id + (success != null ? " PASS actual CC" : " NO sampled candidate"));
                        if (success == null) continue;
                        arrival[to.id] = success.end; path[to.id] = path[from.id].Concat(new[] { success }).ToList(); queue.Enqueue(to);
                    }
                }
                var plan = new Plan { status = arrival.ContainsKey(mountain.start.id) ? "PASS isolated actual CharacterController chain; Relay and player runtime separate" : "FAIL no sampled continuous chain" };
                if (path.ContainsKey(mountain.start.id)) plan.legs = path[mountain.start.id];
                File.WriteAllText(Output + "/Route.json", JsonUtility.ToJson(plan, true));
                File.WriteAllLines(Output + "/Candidates.txt", attempts);
                Debug.Log("RETURN_ROUTE " + plan.status + " legs=" + plan.legs.Count);
            }
            finally { Object.DestroyImmediate(obj); }
        }
    }
}
