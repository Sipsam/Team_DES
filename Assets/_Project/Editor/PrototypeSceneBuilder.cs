using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public static class PrototypeSceneBuilder
{
    private const string SceneDirectory = "Assets/_Project/Scenes";
    private const string ScenePath = SceneDirectory + "/Prototype_01.unity";
    private const string PlayerPrefabDirectory = "Assets/_Project/Prefabs/Player";
    private const string PlayerPrefabPath = PlayerPrefabDirectory + "/NetworkPlayer.prefab";

    [MenuItem("Tools/Prototype/Create Prototype Scene")]
    private static void CreatePrototypeScene()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog(
                "Overwrite Prototype Scene?",
                "Prototype_01 already exists. Do you want to replace it?",
                "Overwrite",
                "Cancel"))
        {
            return;
        }

        if (!SaveModifiedScenesBeforeReplacing())
        {
            return;
        }

        Directory.CreateDirectory(SceneDirectory);
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildScene();
        EditorSceneManager.MarkSceneDirty(scene);

        if (EditorSceneManager.SaveScene(scene, ScenePath))
        {
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"Prototype scene created: {ScenePath}");
        }
        else
        {
            Debug.LogError($"Failed to save prototype scene: {ScenePath}");
        }
    }

    private static bool SaveModifiedScenesBeforeReplacing()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isDirty)
            {
                continue;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "Unsaved Scene",
                $"Save changes to '{scene.name}' before creating the prototype scene?",
                "Save",
                "Cancel",
                "Don't Save");

            if (choice == 1)
            {
                return false;
            }

            if (choice == 2)
            {
                continue;
            }

            bool saved;
            if (string.IsNullOrEmpty(scene.path))
            {
                string savePath = EditorUtility.SaveFilePanelInProject(
                    "Save Scene",
                    scene.name,
                    "unity",
                    "Choose a location for the unsaved scene.");

                if (string.IsNullOrEmpty(savePath))
                {
                    return false;
                }

                saved = EditorSceneManager.SaveScene(scene, savePath);
            }
            else
            {
                saved = EditorSceneManager.SaveScene(scene);
            }

            if (!saved)
            {
                return false;
            }
        }

        return true;
    }

    private static void BuildScene()
    {
        GameObject gameManager = new GameObject("GameManager");
        gameManager.AddComponent<NetworkObject>();
        RespawnManager respawnManager = gameManager.AddComponent<RespawnManager>();
        GameStateManager gameStateManager = gameManager.AddComponent<GameStateManager>();

        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 0.5f, 0f);

        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = 2f;
        characterController.radius = 0.45f;
        characterController.center = new Vector3(0f, 1f, 0f);
        characterController.stepOffset = 0.3f;

        GameObject capsuleMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsuleMesh.name = "Capsule Mesh";
        capsuleMesh.transform.SetParent(player.transform, false);
        capsuleMesh.transform.localPosition = Vector3.up;
        Object.DestroyImmediate(capsuleMesh.GetComponent<Collider>());

        GameObject cameraTarget = new GameObject("CameraTarget");
        cameraTarget.transform.SetParent(player.transform, false);
        cameraTarget.transform.localPosition = new Vector3(0f, 1.4f, 0f);

        player.AddComponent<NetworkObject>();
        OwnerNetworkTransform networkTransform = player.AddComponent<OwnerNetworkTransform>();
        networkTransform.AuthorityMode = Unity.Netcode.Components.NetworkTransform.AuthorityModes.Owner;
        PlayerMovement playerMovement = player.AddComponent<PlayerMovement>();
        player.AddComponent<PlayerInteraction>();

        // A scene camera cannot be referenced from a prefab. The owning client binds it at spawn time.
        SetObjectReference(playerMovement, "cameraTransform", null);

        Directory.CreateDirectory(PlayerPrefabDirectory);
        AssetDatabase.Refresh();
        GameObject networkPlayerPrefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
        if (networkPlayerPrefab == null)
        {
            Debug.LogError($"Failed to save network player prefab: {PlayerPrefabPath}");
            return;
        }
        Object.DestroyImmediate(player);

        GameObject mainCamera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        mainCamera.tag = "MainCamera";
        ThirdPersonCamera thirdPersonCamera = mainCamera.AddComponent<ThirdPersonCamera>();
        thirdPersonCamera.enabled = false;

        GameObject networkManagerObject = new GameObject("NetworkManager");
        NetworkManager networkManager = networkManagerObject.AddComponent<NetworkManager>();
        UnityTransport transport = networkManagerObject.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
        networkManager.NetworkConfig.NetworkTransport = transport;
        networkManager.NetworkConfig.PlayerPrefab = networkPlayerPrefab;
        networkManager.NetworkConfig.ConnectionApproval = true;

        GameObject spawnPointsObject = new GameObject("PlayerSpawnPoints");
        Transform spawnPoint01 = CreateSpawnPoint(spawnPointsObject.transform,
            "SpawnPoint_01", new Vector3(-1.5f, 0.5f, 0f));
        Transform spawnPoint02 = CreateSpawnPoint(spawnPointsObject.transform,
            "SpawnPoint_02", new Vector3(1.5f, 0.5f, 0f));

        NetworkSpawnCoordinator spawnCoordinator = networkManagerObject.AddComponent<NetworkSpawnCoordinator>();
        SetObjectReference(spawnCoordinator, "networkManager", networkManager);
        SetObjectArray(spawnCoordinator, "spawnPoints", spawnPoint01, spawnPoint02);

        GameObject networkDebugUiObject = new GameObject("NetworkDebugUI");
        NetworkDebugUI networkDebugUi = networkDebugUiObject.AddComponent<NetworkDebugUI>();
        SetObjectReference(networkDebugUi, "networkManager", networkManager);

        GameObject level = new GameObject("Level");
        GameObject startArea = new GameObject("StartArea");
        startArea.transform.SetParent(level.transform, false);
        CreateCube("Ground", startArea.transform, new Vector3(0f, -0.5f, 0f), new Vector3(8f, 1f, 8f));
        CreateCube("StartPlatform", startArea.transform, new Vector3(0f, 0.25f, 0f), new Vector3(6f, 0.5f, 6f));
        CreateCube("JumpPlatform01", startArea.transform, new Vector3(0f, 0.8f, 6f), new Vector3(3f, 0.5f, 3f));
        CreateCube("JumpPlatform02", startArea.transform, new Vector3(2f, 1.4f, 10f), new Vector3(3f, 0.5f, 3f));
        CreateCube("JumpPlatform03", startArea.transform, new Vector3(0f, 2f, 14f), new Vector3(3f, 0.5f, 3f));

        GameObject bridgeArea = new GameObject("BridgeArea");
        bridgeArea.transform.SetParent(level.transform, false);

        GameObject bridge = new GameObject("Bridge");
        bridge.transform.SetParent(bridgeArea.transform, false);
        bridge.AddComponent<NetworkObject>();
        BridgeController bridgeController = bridge.AddComponent<BridgeController>();
        SetObjectReference(bridgeController, "gameStateManager", gameStateManager);

        GameObject bridgeFrame = new GameObject("BridgeFrame");
        bridgeFrame.transform.SetParent(bridge.transform, false);
        CreateVisualCube("FrameBeam_Left", bridgeFrame.transform,
            new Vector3(-2.1f, 1.65f, 23f), new Vector3(0.25f, 0.25f, 16f));
        CreateVisualCube("FrameBeam_Right", bridgeFrame.transform,
            new Vector3(2.1f, 1.65f, 23f), new Vector3(0.25f, 0.25f, 16f));

        GameObject cooperationPath = CreatePath("CooperationPath", bridge.transform,
            new[]
            {
                new Vector3(0f, 2f, 16f), new Vector3(0f, 2f, 17.8f),
                new Vector3(0f, 2f, 19.6f), new Vector3(0f, 2f, 21.4f),
                new Vector3(0f, 2f, 23.2f), new Vector3(0f, 2f, 25f),
                new Vector3(0f, 2f, 26.8f), new Vector3(0f, 2f, 28.6f)
            }, new Vector3(3f, 0.5f, 2f));

        GameObject safeRoute = CreatePath("SafeRoute", bridgeArea.transform,
            new[]
            {
                new Vector3(-1.3f, 2f, 16f), new Vector3(-2.3f, 2f, 17.8f),
                new Vector3(-1.3f, 2f, 19.6f), new Vector3(-2.3f, 2f, 21.4f),
                new Vector3(-1.3f, 2f, 23.2f), new Vector3(-2.3f, 2f, 25f),
                new Vector3(-1.3f, 2f, 26.8f), new Vector3(-2.3f, 2f, 28.6f)
            }, new Vector3(2.5f, 0.5f, 2f));

        GameObject riskRoute = CreatePath("RiskRoute", bridgeArea.transform,
            new[]
            {
                new Vector3(1.3f, 2f, 16f), new Vector3(1.3f, 2f, 17.8f),
                new Vector3(1.3f, 2f, 19.6f), new Vector3(1.3f, 2f, 21.4f),
                new Vector3(1.3f, 2f, 23.2f), new Vector3(1.3f, 2f, 25f),
                new Vector3(1.3f, 2f, 26.8f), new Vector3(1.3f, 2f, 28.6f)
            }, new Vector3(0.85f, 0.5f, 2f));

        cooperationPath.SetActive(false);
        safeRoute.SetActive(false);
        riskRoute.SetActive(false);
        SetObjectReference(bridgeController, "bridgeFrame", bridgeFrame);
        SetObjectReference(bridgeController, "cooperationPath", cooperationPath);
        SetObjectReference(bridgeController, "safeRoute", safeRoute);
        SetObjectReference(bridgeController, "riskRoute", riskRoute);

        CreateBridgeSwitch("BridgeSwitch_A", bridgeArea.transform, new Vector3(-2.1f, 0.5f, 1.5f),
            BridgeSwitchId.A, bridgeController);
        CreateBridgeSwitch("BridgeSwitch_B", bridgeArea.transform, new Vector3(2.1f, 0.5f, 1.5f),
            BridgeSwitchId.B, bridgeController);

        CreateCube("TurnPlatform", bridgeArea.transform, new Vector3(0f, 2f, 31f), new Vector3(8f, 0.5f, 6f));
        CreateTrigger("TurnPoint", bridgeArea.transform, new Vector3(0f, 3f, 33.5f),
            new Vector3(7f, 4f, 1f));
        TurnPoint turnPoint = bridgeArea.transform.Find("TurnPoint").gameObject.AddComponent<TurnPoint>();
        SetObjectReference(turnPoint, "gameStateManager", gameStateManager);

        CreateTrigger("Goal", startArea.transform, new Vector3(0f, 1f, -2.5f), new Vector3(5f, 2f, 1f));
        Goal goal = startArea.transform.Find("Goal").gameObject.AddComponent<Goal>();
        SetObjectReference(goal, "gameStateManager", gameStateManager);

        GameObject checkpoints = new GameObject("Checkpoints");
        CreateCheckpoint("CP_Start", checkpoints.transform, new Vector3(0f, 1.4f, 2.2f),
            new Vector3(5f, 2f, 0.25f), respawnManager);
        CreateCheckpoint("CP_01", checkpoints.transform, new Vector3(2f, 2.6f, 10f),
            new Vector3(2.5f, 2f, 0.25f), respawnManager);

        GameObject killZone = new GameObject("KillZone");
        killZone.transform.position = new Vector3(0f, -8f, 5f);
        BoxCollider killZoneCollider = killZone.AddComponent<BoxCollider>();
        killZoneCollider.size = new Vector3(100f, 1f, 100f);
        killZoneCollider.isTrigger = true;
        killZone.AddComponent<KillZone>();

        GameObject directionalLight = new GameObject("Directional Light");
        Light lightComponent = directionalLight.AddComponent<Light>();
        lightComponent.type = LightType.Directional;
        directionalLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static GameObject CreatePath(string pathName, Transform parent, Vector3[] positions, Vector3 pieceSize)
    {
        GameObject path = new GameObject(pathName);
        path.transform.SetParent(parent, false);

        for (int i = 0; i < positions.Length; i++)
        {
            CreateCube($"PathPiece_{i + 1:00}", path.transform, positions[i], pieceSize);
        }

        return path;
    }

    private static GameObject CreateVisualCube(string objectName, Transform parent, Vector3 position, Vector3 scale)
    {
        GameObject visual = CreateCube(objectName, parent, position, scale);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        return visual;
    }

    private static void CreateBridgeSwitch(
        string objectName,
        Transform parent,
        Vector3 position,
        BridgeSwitchId switchId,
        BridgeController bridgeController)
    {
        GameObject switchObject = new GameObject(objectName);
        switchObject.transform.SetParent(parent, false);
        switchObject.transform.localPosition = position;

        SphereCollider trigger = switchObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.1f;

        GameObject switchMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        switchMesh.name = "Switch Mesh";
        switchMesh.transform.SetParent(switchObject.transform, false);
        switchMesh.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        switchMesh.transform.localScale = new Vector3(0.65f, 0.9f, 0.65f);
        Object.DestroyImmediate(switchMesh.GetComponent<Collider>());

        BridgeSwitch bridgeSwitch = switchObject.AddComponent<BridgeSwitch>();
        SetObjectReference(bridgeSwitch, "bridgeController", bridgeController);
        SetEnumValue(bridgeSwitch, "switchId", (int)switchId);
    }

    private static void CreateTrigger(string objectName, Transform parent, Vector3 position, Vector3 size)
    {
        GameObject triggerObject = new GameObject(objectName);
        triggerObject.transform.SetParent(parent, false);
        triggerObject.transform.localPosition = position;

        BoxCollider trigger = triggerObject.AddComponent<BoxCollider>();
        trigger.size = size;
        trigger.isTrigger = true;
    }

    private static Transform CreateSpawnPoint(Transform parent, string objectName, Vector3 position)
    {
        GameObject spawnPoint = new GameObject(objectName);
        spawnPoint.transform.SetParent(parent, false);
        spawnPoint.transform.localPosition = position;
        return spawnPoint.transform;
    }

    private static GameObject CreateCube(string objectName, Transform parent, Vector3 position, Vector3 scale)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        return cube;
    }

    private static void CreateCheckpoint(
        string objectName,
        Transform parent,
        Vector3 position,
        Vector3 triggerSize,
        RespawnManager respawnManager)
    {
        GameObject checkpoint = new GameObject(objectName);
        checkpoint.transform.SetParent(parent, false);
        checkpoint.transform.localPosition = position;

        BoxCollider trigger = checkpoint.AddComponent<BoxCollider>();
        trigger.size = triggerSize;
        trigger.isTrigger = true;

        Checkpoint checkpointScript = checkpoint.AddComponent<Checkpoint>();
        SetObjectReference(checkpointScript, "respawnManager", respawnManager);
    }

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"Serialized field '{propertyName}' was not found on {target.GetType().Name}.");
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnumValue(Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"Serialized field '{propertyName}' was not found on {target.GetType().Name}.");
            return;
        }

        property.enumValueIndex = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectArray(Object target, string propertyName, params Object[] values)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            Debug.LogError($"Serialized array '{propertyName}' was not found on {target.GetType().Name}.");
            return;
        }

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == scenePath)
            {
                scenes.RemoveAt(i);
                break;
            }
        }

        scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
