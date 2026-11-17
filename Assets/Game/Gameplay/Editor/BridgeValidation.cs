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
                var controller = obj.AddComponent<CharacterController>();
                controller.height = controllerTemplate.height;
                controller.radius = controllerTemplate.radius;
                controller.center = controllerTemplate.center;
                controller.skinWidth = controllerTemplate.skinWidth;
                controller.stepOffset = controllerTemplate.stepOffset;
                controller.slopeLimit = controllerTemplate.slopeLimit;
                controller.minMoveDistance = 0;
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

        static void Require(bool success, string label)
        {
            if (!success) throw new InvalidOperationException(label);
        }
    }
}
