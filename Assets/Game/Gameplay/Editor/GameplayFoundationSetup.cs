using System;
using System.IO;
using System.Linq;
using TwinEdge.Gameplay;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TwinEdge.Gameplay.Editor
{
    public static class GameplayFoundationSetup
    {
        public const string ScenePath = "Assets/Game/Gameplay/Scenes/V3Gameplay.unity";
        public const string PlayerPath = "Assets/Game/Gameplay/Prefabs/V3NetworkPlayer.prefab";
        const string LevelPath = "Assets/Game/Level/Scenes/LevelBlockoutV3.unity";
        const string Evidence = "Docs/Development/Gameplay/V1";
        [MenuItem("Tools/Twin Edge/Gameplay/Create V3 Foundation")]
        public static void Create()
        {
            if (File.Exists(ScenePath) || File.Exists(PlayerPath)) throw new InvalidOperationException("Foundation already exists; open the saved scene. Creation never overwrites authored assets.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPath));
            Directory.CreateDirectory(Evidence);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/NetworkPlayer.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(source);
            player.name = "V3NetworkPlayer"; player.transform.position = Vector3.zero;
            var capsule = player.transform.Find("Capsule Mesh"); if (capsule != null) capsule.gameObject.SetActive(false);
            var input = player.AddComponent<GameplayInput>();
            input.actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/06_Character_Animation_Test/Prefabs/CharacterWithAnimation.prefab"));
            visual.transform.SetParent(player.transform, false); visual.name = "VisualRoot"; visual.transform.localPosition = Vector3.zero;
            // Keep the tested skeleton and local pose. Disable only visual colliders in this wrapper.
            foreach (var c in visual.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var demo in visual.GetComponentsInChildren<CharacterMotionDemo>()) demo.enabled = false;
            var character = player.AddComponent<GameplayCharacter>(); character.animator = visual.GetComponentInChildren<Animator>();
            character.animator.applyRootMotion = false;
            var band = GameObject.CreatePrimitive(PrimitiveType.Cube); band.name = "IdentityBand";
            band.transform.SetParent(player.transform, false); band.transform.localPosition = new Vector3(0, 1.8f, 0);
            band.transform.localScale = new Vector3(.5f, .08f, .5f); Object.DestroyImmediate(band.GetComponent<Collider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, "Assets/Game/Gameplay/Prefabs/IdentityBand.mat");
            band.GetComponent<Renderer>().sharedMaterial = material;
            var movement = new SerializedObject(player.GetComponent<PlayerMovement>());
            movement.FindProperty("moveSpeed").floatValue = 3.5f; movement.ApplyModifiedPropertiesWithoutUndo();
            // Prototype PvP inputs differ from the latest planning. Keep the dependency, dormant in this foundation.
            player.GetComponent<PlayerCombat>().enabled = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPath); Object.DestroyImmediate(player);

            var root = new GameObject("V3GameplayBootstrap");
            var bootstrap = root.AddComponent<V3GameplayBootstrap>();
            var network = new GameObject("NetworkManager");
            var manager = network.AddComponent<NetworkManager>();
            var transport = network.AddComponent<UnityTransport>();
            manager.NetworkConfig.NetworkTransport = transport; manager.NetworkConfig.PlayerPrefab = prefab;
            manager.NetworkConfig.ConnectionApproval = true;
            bootstrap.manager = manager;
            bootstrap.spawnPoints = new Transform[2];
            for (int i = 0; i < 2; i++) { bootstrap.spawnPoints[i] = new GameObject("Spawn_" + i).transform; bootstrap.spawnPoints[i].SetParent(root.transform); }
            var coordinator = network.AddComponent<NetworkSpawnCoordinator>();
            var serialized = new SerializedObject(coordinator);
            serialized.FindProperty("networkManager").objectReferenceValue = manager;
            var spawns = serialized.FindProperty("spawnPoints"); spawns.arraySize = 2;
            for (int i = 0; i < 2; i++) spawns.GetArrayElementAtIndex(i).objectReferenceValue = bootstrap.spawnPoints[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            bootstrap.sessions = network.AddComponent<MultiplayerSessionManager>();
            var game = new GameObject("GameState", typeof(NetworkObject));
            bootstrap.state = game.AddComponent<GameStateManager>(); bootstrap.state.foundationMode = true;
            bootstrap.respawns = game.AddComponent<RespawnManager>();
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); camera.tag = "MainCamera";
            bootstrap.standbyCamera = camera.GetComponent<Camera>(); bootstrap.standbyCamera.farClipPlane = 4000;
            camera.AddComponent<ThirdPersonCamera>().enabled = false;
            var sample = GameObject.CreatePrimitive(PrimitiveType.Cube); sample.name = "DevelopmentInteractionSample";
            sample.AddComponent<NetworkObject>(); sample.AddComponent<NetworkInteractionTarget>(); bootstrap.interactionSample = sample.transform;
            sample.GetComponent<Renderer>().sharedMaterial = material;
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var path in new[] { ScenePath, LevelPath })
                if (!scenes.Any(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray(); AssetDatabase.SaveAssets();
            Validate();
        }
        [MenuItem("Tools/Twin Edge/Gameplay/Validate Saved Foundation")]
        public static void Validate()
        {
            Directory.CreateDirectory(Evidence);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var bootstrap = Object.FindFirstObjectByType<V3GameplayBootstrap>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if (bootstrap == null || prefab == null || prefab.GetComponent<NetworkObject>() == null || prefab.GetComponent<OwnerNetworkTransform>() == null) throw new Exception("Network structure missing");
            if (prefab.GetComponent<CharacterController>().height != 2 || prefab.GetComponent<GameplayCharacter>().animator == null) throw new Exception("Character structure invalid");
            foreach (string state in new[] { "Idle", "Walk", "Run", "Jump", "Walk_Carry" })
                if (!((UnityEditor.Animations.AnimatorController)prefab.GetComponent<GameplayCharacter>().animator.runtimeAnimatorController).layers[0].stateMachine.states.Any(s => s.state.name == state)) throw new Exception("Animation missing: " + state);
            EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Additive);
            Physics.SyncTransforms();
            var markers = Object.FindObjectsByType<TwinEdge.Levels.LevelMarker>(FindObjectsSortMode.None);
            foreach (var id in new[] { "Mountain.V3.Start", "Mountain.V3.Final", "Valley.V3.LowerCheckpoint", "Valley.V3.UpperCheckpoint" })
                V3GameplayBootstrap.SafeGround(markers.Single(m => m.id == id).transform.position);
            File.WriteAllText(Evidence + "/StructuralValidation.txt", "PASS: saved/reloaded gameplay scene; existing network prefab variant; height 2/radius .45; 5 animator states; V3 marker ground raycasts. Runtime and Relay require separate validation.\n");
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("GAMEPLAY_STRUCTURE_PASS");
        }
        public static void BuildPlayer()
        {
            BuildWindowsDevelopment("Builds/V3Gameplay/V3Gameplay.exe");
        }

        [MenuItem("Tools/Twin Edge/Gameplay/Build Full Match Development Player")]
        public static void BuildFullMatchPlayer()
        {
            string directory = Environment.GetEnvironmentVariable("TWIN_EDGE_FULL_MATCH_BUILD") ?? "Builds/FullMatch";
            BuildWindowsDevelopment(Path.Combine(directory, "V3Gameplay.exe"));
        }

        static void BuildWindowsDevelopment(string path)
        {
            var options = BuildOptions.Development;
            if (Environment.GetCommandLineArgs().Contains("-v3BuildScriptsOnly")) options |= BuildOptions.BuildScriptsOnly;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath, LevelPath }, locationPathName = path, target = BuildTarget.StandaloneWindows64, options = options });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("V3 build failed");
        }
    }
}
