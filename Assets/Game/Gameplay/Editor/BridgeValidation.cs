using System;
using System.Linq;
using TwinEdge.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwinEdge.Gameplay.Editor
{
    public static class BridgeValidation
    {
        internal const string Revised = "Assets/07_Revised_Gimmick_Models/01_Bridge/";
        internal const string Working = "Assets/05_Gimmick_Working_Assets/01_Bridge_Building/";

        public static void ValidateDeck()
        {
            EditorSceneManager.OpenScene(GameplayFoundationSetup.ScenePath);
            var bridge = UnityEngine.Object.FindFirstObjectByType<BridgeGameplay>();
            Require(bridge != null && bridge.GetComponent<Unity.Netcode.NetworkObject>() != null, "Saved bridge network component");
            var references = new SerializedObject(bridge);
            foreach (string field in new[] { "crateModel", "plankModel", "frameModel" })
                Require(references.FindProperty(field).objectReferenceValue != null, "Saved model: " + field);

            var level = EditorSceneManager.OpenScene("Assets/Game/Level/Scenes/LevelBlockoutV3.unity", OpenSceneMode.Additive);
            var planks = UnityEngine.Object.FindObjectsByType<BridgePlankV3>(FindObjectsSortMode.None).OrderBy(p => p.plankId).ToArray();
            bridge.Bind(level);
            Physics.SyncTransforms();
            Require(bridge.PlankCount == 198 && Mathf.Abs(bridge.Length - 311) < .01f, "Saved span and plank count");
            foreach (var plank in planks)
                Require(plank.GetComponentsInChildren<Collider>().All(c => !c.enabled) &&
                    plank.GetComponentsInChildren<Renderer>().All(r => !r.enabled), "Uninstalled deck " + plank.plankId);
            for (float z = 340; z < 645; z += 1)
                for (float x = 8.5f; x <= 15.5f; x += 1)
                    Require(!Physics.Raycast(new Vector3(x, 190.3f, z), Vector3.down, .5f, ~0, QueryTriggerInteraction.Ignore),
                        "Uninstalled surface clear " + x + "," + z);
            foreach (float z in new[] { 327f, 330f, 332f, 334f, 336f, 338f, 644f, 645f, 646f, 647f, 648f, 649f, 650f, 651f, 653f, 656f })
            {
                bool hit = Physics.Raycast(new Vector3(12, 196, z), Vector3.down, out var ground, 20, ~0, QueryTriggerInteraction.Ignore);
                Debug.Log($"BRIDGE_GROUND {z}: {(hit ? ground.point + " " + ground.collider.name : "none")}");
            }
            foreach (string path in new[] { Revised + "FIX_BRIDGE_ARCH_20_02", Revised + "MOV_BRIDGE_ASSEMBLED_20_02",
                     Revised + "MOV_BRIDGE_PLANK_RECT_02", Working + "FIX_BRIDGE_WOOD_CRATE_01", Working + "FIX_BRIDGE_WOOD_PILE_01",
                     Working + "MOV_BRIDGE_PLANK_01" })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab");
                Require(model != null, path);
                var children = model.transform.Cast<Transform>().Select(child => child.name);
                var meshes = model.GetComponentsInChildren<MeshRenderer>(true);
                var bounds = meshes[0].bounds;
                foreach (var mesh in meshes) bounds.Encapsulate(mesh.bounds);
                Debug.Log($"BRIDGE_MODEL {model.name}: {bounds}; meshes={meshes.Length}; children={string.Join(", ", children)}");
            }
            Debug.Log("BRIDGE_DECK_PASS");
        }

        [MenuItem("Tools/Twin Edge/Gameplay/Build Bridge Test Player")]
        public static void BuildBridgePlayer()
        {
            ValidateCollapseGeometry();
            EditorSceneManager.OpenScene(GameplayFoundationSetup.ScenePath);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GameplayFoundationSetup.ScenePath, "Assets/Game/Level/Scenes/LevelBlockoutV3.unity" },
                locationPathName = "Builds/V3Bridge/V3Bridge.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            Require(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded, "Bridge Windows build");
            Debug.Log("BRIDGE_WINDOWS_BUILD_PASS");
        }

        [MenuItem("Tools/Twin Edge/Gameplay/Validate Bridge Collapse Geometry")]
        public static void ValidateCollapseGeometry()
        {
            ValidateDeck();
            var bridge = UnityEngine.Object.FindFirstObjectByType<BridgeGameplay>();
            var deck = UnityEngine.Object.FindObjectsByType<BridgePlankV3>(FindObjectsSortMode.None)
                .OrderBy(plank => plank.plankId).Select(plank => plank.GetComponent<Collider>()).ToArray();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayFoundationSetup.PlayerPath);
            var controllerTemplate = prefab.GetComponent<CharacterController>();
            var motor = new SerializedObject(prefab.GetComponent<PlayerMovement>());
            float gravity = motor.FindProperty("gravity").floatValue;
            float jumpHeight = motor.FindProperty("jumpHeight").floatValue;
            var settings = new SerializedObject(bridge);
            int safeEnds = settings.FindProperty("safeEndPlanks").intValue;
            float hazardFraction = settings.FindProperty("hazardFraction").floatValue;
            float shortest = float.PositiveInfinity;
            float longest = 0;
            int singles = 0, doubles = 0;
            for (int seed = 0; seed < 4096; seed++)
            {
                var removed = BridgeGameplay.SelectHazards(deck.Length, safeEnds, hazardFraction, seed);
                Require(removed.Count(value => value) == Mathf.RoundToInt(deck.Length * hazardFraction), "Hazard count seed " + seed);
                int last = -1;
                for (int i = 0; i < removed.Length; i++)
                {
                    if (removed[i]) continue;
                    Require(i - last <= 3, "Fully collapsed deck path seed " + seed);
                    last = i;
                }
                Require(last == deck.Length - 1 && !removed[0], "Endpoints seed " + seed);
            }

            var obj = new GameObject("Standalone bridge CharacterController probe");
            try
            {
                var controller = AddControllerProbe(obj, controllerTemplate);
                for (int seed = 0; seed < 32; seed++)
                {
                    var removed = BridgeGameplay.SelectHazards(deck.Length, safeEnds, hazardFraction, seed);
                    for (int i = 0; i < deck.Length; i++) deck[i].enabled = !removed[i];
                    Physics.SyncTransforms();
                    for (int high = deck.Length - 1; high > 0; high--)
                    {
                        if (removed[high] || !removed[high - 1]) continue;
                        int low = high - 2;
                        if (removed[low]) low--;
                        bool sprint = high - low == 3;
                        foreach (float step in new[] { 1f / 30, 1f / 60, 1f / 120 })
                        {
                            controller.enabled = false;
                            var origin = deck[high].bounds.center;
                            origin.y = deck[high].bounds.max.y + .10f;
                            origin.z = deck[high].bounds.min.z + .20f;
                            obj.transform.position = origin;
                            controller.enabled = true;
                            Physics.SyncTransforms();
                            controller.Move(Vector3.down * .2f);
                            Require(controller.isGrounded, "Probe starts grounded");
                            float speed = bridge.BridgeJumpSpeed(sprint);
                            float vertical = Mathf.Sqrt(2 * gravity * jumpHeight);
                            bool landed = false;
                            for (float elapsed = 0; elapsed < 1.3f; elapsed += step)
                            {
                                vertical -= gravity * step;
                                var flags = controller.Move((Vector3.back * speed + Vector3.up * vertical) * step);
                                if (vertical < 0 && (flags & CollisionFlags.Below) != 0)
                                {
                                    float distance = origin.z - obj.transform.position.z;
                                    shortest = Mathf.Min(shortest, distance);
                                    longest = Mathf.Max(longest, distance);
                                    var feet = obj.transform.position;
                                    landed = deck[low].bounds.min.z - .3f <= feet.z && feet.z <= deck[low].bounds.max.z + .3f;
                                    break;
                                }
                            }
                            Require(landed, $"Actual CC jump seed={seed} {high}->{low} dt={step:F4} position={obj.transform.position:F3}");
                            if (sprint) doubles++;
                            else singles++;
                        }
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
            Debug.Log($"BRIDGE_COLLAPSE_GEOMETRY_PASS seeds=4096, CC seeds=32 singles={singles} doubles={doubles} movement={shortest:F2}..{longest:F2}m");
        }

        [MenuItem("Tools/Twin Edge/Gameplay/Validate Canyon Stairway")]
        public static void ValidateCanyonStairway()
        {
            EditorSceneManager.OpenScene(TwinEdge.Levels.Editor.MountainGeneratorV3.ScenePath);
            var valley = UnityEngine.Object.FindObjectsByType<ValleySectionV3>(FindObjectsSortMode.None);
            var canyon = valley.Single(part => part.part == ValleyPartV3.CanyonDescent);
            var river = valley.Single(part => part.part == ValleyPartV3.River);
            var route = canyon.generated.GetComponentInChildren<LevelRoute>();
            var layout = canyon.config;
            Require(route.bidirectional && route.points.SequenceEqual(layout.CanyonRoute), "Saved stairs replace the former route");
            var treads = canyon.generated.transform.Find("Canyon Stone Stairway");
            var rails = canyon.generated.transform.Find("Canyon Stair Parapets");
            foreach (var part in new[] { treads, rails })
                Require(part.GetComponent<MeshFilter>().sharedMesh == part.GetComponent<MeshCollider>().sharedMesh,
                    "Rendered mesh supplies collision for " + part.name);

            Physics.SyncTransforms();
            var dock = river.generated.transform.Find("LowerDock").GetComponent<Collider>().bounds;
            Vector3 top = layout.bridgeEnd + Vector3.forward * 3;
            Vector3 bottom = layout.LowerStairLanding;
            Vector3 dockJoint = bottom + Vector3.forward * 2;
            Vector3 board = new(dock.min.x + 2.2f, dock.max.y, dock.center.z);
            for (float z = layout.UpperStairTread.z + 1; z < layout.LowerStairTread.z - .5f; z += 1.1f)
            {
                var axis = layout.CanyonStairProfile(z);
                foreach (float side in new[] { -1f, 1f })
                {
                    var lane = axis + Vector3.right * side * 2.5f;
                    Require(Physics.Raycast(lane + Vector3.up * .6f, Vector3.down, out var floor, .95f) &&
                        floor.collider.transform == treads && floor.normal.y > .99f, $"Exposed stone tread at {lane:F3}");
                    Require(Physics.Raycast(axis + Vector3.up * .7f, Vector3.right * side, out var guard, 4.2f) &&
                        guard.collider.transform == rails, $"Continuous parapet at {axis:F3}");
                }
            }

            // Sample the wet downstream ribbon on the bank next to the stair cut.
            for (float z = 1100; z < layout.LowerStairLanding.z; z += 2)
            {
                var water = TwinEdge.Levels.Editor.ValleyGeneratorV3.RiverSample(layout, z);
                float half = TwinEdge.Levels.Editor.ValleyGeneratorV3.Width(layout, z) * .5f;
                water += Vector3.right * (half - 3f);
                Require(Physics.Raycast(water + Vector3.up * 2f, Vector3.down, out var bed, 15f) &&
                    bed.point.y < water.y - 1f && bed.collider.transform != treads && bed.collider.transform != rails,
                    $"Clear wet downstream bank at {water:F3}");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayFoundationSetup.PlayerPath);
            var motor = new SerializedObject(prefab.GetComponent<PlayerMovement>());
            float gravity = motor.FindProperty("gravity").floatValue;
            float downForce = motor.FindProperty("groundedDownForce").floatValue;
            float moveSpeed = motor.FindProperty("moveSpeed").floatValue;
            var probe = new GameObject("Canyon stair CharacterController");
            try
            {
                var controller = AddControllerProbe(probe, prefab.GetComponent<CharacterController>());
                foreach (bool returning in new[] { false, true })
                foreach (bool sprint in new[] { false, true })
                foreach (int fps in new[] { 30, 60, 120 })
                {
                    var side = Vector3.right * (returning ? 1.2f : -1.2f);
                    var path = new[] { top + side, layout.UpperStairTread + side, bottom + side, dockJoint + side, board };
                    if (returning) Array.Reverse(path);
                    controller.enabled = false;
                    probe.transform.position = path[0] + Vector3.up * .35f;
                    controller.enabled = true;
                    Physics.SyncTransforms();
                    controller.Move(Vector3.down * .55f);
                    Require(controller.isGrounded, $"Probe starts at {path[0]:F3}");

                    float speed = moveSpeed * (sprint ? 1.6f * 1.5f : 1f);
                    float dt = 1f / fps;
                    float vertical = -downForce;
                    float largestError = 0;
                    foreach (var target in path.Skip(1))
                    {
                        float duration = Vector3.Distance(probe.transform.position, target) / speed + 5;
                        Vector3 difference;
                        while ((difference = Vector3.ProjectOnPlane(target - probe.transform.position, Vector3.up)).magnitude > .06f)
                        {
                            duration -= dt;
                            Require(duration > 0, $"Stalled to {target:F3} from {probe.transform.position:F3} at {speed:F1}m/s {fps}fps");
                            if (controller.isGrounded && vertical < 0) vertical = -downForce;
                            vertical -= gravity * dt;
                            controller.Move(Vector3.ClampMagnitude(difference, speed * dt) + Vector3.up * (vertical * dt));
                            var feet = probe.transform.position;
                            largestError = Mathf.Max(largestError, Mathf.Abs(feet.y - layout.CanyonStairProfile(feet.z).y));
                        }
                    }
                    Require(largestError < .65f && Mathf.Abs(probe.transform.position.y - path[^1].y) < .2f,
                        $"End joints returning={returning} speed={speed:F1} fps={fps} maxHeightError={largestError:F3}");
                    Debug.Log($"STAIR_CC_PASS returning={returning} sprint={sprint} speed={speed:F2} fps={fps} maxHeightError={largestError:F3}");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        }

        static CharacterController AddControllerProbe(GameObject obj, CharacterController template)
        {
            var controller = obj.AddComponent<CharacterController>();
            controller.height = template.height;
            controller.radius = template.radius;
            controller.center = template.center;
            controller.skinWidth = template.skinWidth;
            controller.stepOffset = template.stepOffset;
            controller.slopeLimit = template.slopeLimit;
            controller.minMoveDistance = 0;
            return controller;
        }

        static void Require(bool success, string label)
        {
            if (!success) throw new InvalidOperationException(label);
        }
    }
}
