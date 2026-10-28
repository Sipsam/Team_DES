using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Object = UnityEngine.Object;

// Builds the integrated course from the existing prototype's systems.
// New scene creation always uses a unique path. The repair command never removes geometry.
public static class MainPrototypeSceneBuilder
{
    private const string ScenePath = "Assets/_Project/Scenes/MainPrototype.unity";
    private const string MaterialPath = "Assets/_Project/Materials/Greybox";
    private const string PrefabPath = "Assets/_Project/Prefabs/Greybox";
    private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

    [MenuItem("Tools/Prototype/Create New Main Prototype Scene")]
    public static void CreateNewMainPrototypeScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!Application.isBatchMode && !PrototypeSceneBuilder.SaveModifiedScenesBeforeReplacing()) return;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        LoadMaterials();
        PrototypeSceneBuilder.BuildScene();
        ConfigureCourse();
        CompleteIntegration(true);
        ValidateCurrentScene();
        string path = AssetDatabase.GenerateUniqueAssetPath(ScenePath);
        if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("MainPrototype scene save failed.");
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        foreach (EditorBuildSettingsScene old in scenes) old.enabled = false;
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log($"MAIN PROTOTYPE CREATED: {path}. Original scenes and player prefab preserved.");
    }

    // Command-line entry point also runs structural validation before saving.
    public static void CreateForBatch() => CreateNewMainPrototypeScene();

    public static void ValidateSavedMainPrototype()
    {
        EditorSceneManager.OpenScene(ScenePath);
        ValidateCurrentScene();
    }

    public static void BuildSmokePlayer()
    {
        EditorSceneManager.OpenScene(ScenePath);
        RepairLabels();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        ValidateCurrentScene();
        string output = "Builds/IntegrationSmoke/PrototypeSmoke.exe";
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath }, locationPathName = output,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException("Smoke player build failed: " + report.summary.result);
        Debug.Log("PROTOTYPE SMOKE PLAYER BUILT: " + output);
    }

    private static void RepairLabels()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        foreach (TextMesh text in All<TextMesh>())
        {
            text.font = font;
            text.GetComponent<Renderer>().sharedMaterial = font.material;
        }
    }

    [MenuItem("Tools/Prototype/Add Missing Integration Objects")]
    private static void AddMissingIntegrationObjects()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (All<NetworkManager>().Length != 1 || All<GameStateManager>().Length != 1 ||
            All<BridgeController>().Length != 1 || All<RaftController>().Length != 1 ||
            All<DoorPuzzleController>().Length != 1 || All<JumpPuzzleController>().Length != 1)
        {
            Debug.LogError("Open a scene with one NetworkManager and all four gimmicks. " +
                "Use Create New Main Prototype Scene to create a complete course.");
            return;
        }
        LoadMaterials();
        CompleteIntegration(false);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Missing integration objects and references added. Existing geometry was preserved. Save the scene.");
    }

    private static void ConfigureCourse()
    {
        Transform level = Root("Level").transform;
        Transform start = level.Find("StartArea");
        Remove(start.Find("Ground"));
        for (int i = 1; i <= 3; i++) Remove(start.Find($"JumpPlatform0{i}"));
        Transform floor = start.Find("StartPlatform");
        floor.localPosition = new Vector3(0f, 0.25f, 0f);
        floor.localScale = new Vector3(12f, 0.5f, 10f);
        Segment("StartRamp", start, new Vector3(0f, 0.25f, 4.5f), new Vector3(0f, 2f, 13f), 6f, "Ground");
        Cube("BridgeEntryPlatform", level.Find("BridgeArea"), new Vector3(0f, 2f, 13.5f),
            new Vector3(12f, 0.5f, 5f), "Ground");
        foreach (BridgeSwitch item in All<BridgeSwitch>())
            item.transform.position = new Vector3(item.SwitchId == BridgeSwitchId.A ? -3f : 3f, 2.75f, 13f);

        // Extend the navigated section, leaving the transitions short.
        Transform raft = level.Find("RaftArea");
        raft.Find("RaftEnd").localPosition = new Vector3(0f, 2f, 83f);
        Transform raftExit = raft.Find("RaftExitPlatform");
        raftExit.localPosition = new Vector3(0f, 2f, 84f);
        raftExit.localScale = new Vector3(10f, 0.5f, 6f);
        raft.Find("WaterArea").localPosition = new Vector3(0f, -1f, 59f);
        raft.Find("WaterArea").localScale = new Vector3(3.2f, 1f, 5.8f);
        RaftController raftController = One<RaftController>();
        Field(raftController, "moveSpeed", 2.2f);
        Field(raftController, "reverseDriveInCompetition", true);
        Field(raftController, "competitionSpeedMultiplier", 2.5f);
        Transform raftRoute = raft.Find("CompetitionPlatformRoute");
        Clear(raftRoute);
        Path(raftRoute, new[] { V(0, 35), V(12, 36), V(12, 82), V(0, 84) }, 3.5f, "Safe");
        raftRoute.gameObject.SetActive(false);

        // Downstream authored positions remain editable in their own section roots.
        Transform door = level.Find("DoorPuzzleArea");
        Transform jump = level.Find("JumpPuzzleArea");
        door.localPosition = new Vector3(0f, 0f, 30f);
        jump.localPosition = new Vector3(0f, 0f, 30f);
        ConfigureDoor(door);
        ConfigureJump(jump);

        Remove(level.Find("RaftToDoorPuzzleConnector"));
        Remove(level.Find("DoorPuzzleToJumpConnector"));
        Remove(level.Find("JumpPuzzleToTurnConnector"));
        Transform t1 = Child(level, "Transition_01");
        Transform t2 = Child(level, "Transition_02");
        Transform t3 = Child(level, "Transition_03");
        Cube("Floor", t1, V(0, 33), new Vector3(9f, 0.5f, 4f), "Ground");
        Cube("Floor", t2, V(0, 89), new Vector3(10f, 0.5f, 8f), "Ground");
        Cube("Floor", t3, V(0, 124.5f), new Vector3(12f, 0.5f, 3f), "Ground");
        Transform turnArea = Child(level, "TurnPointArea");
        Transform turnFloor = level.Find("BridgeArea/TurnPlatform");
        turnFloor.SetParent(turnArea, true);
        turnFloor.position = V(0, 175);
        turnFloor.localScale = new Vector3(12f, 0.5f, 8f);
        Transform turnPoint = One<TurnPoint>().transform;
        turnPoint.SetParent(turnArea, true);
        turnPoint.position = new Vector3(0f, 3.1f, 176f);
        turnPoint.GetComponent<BoxCollider>().size = new Vector3(10f, 4f, 3f);
        Cube("Approach", turnArea, V(0, 168), new Vector3(8f, 0.5f, 6f), "Ground");

        foreach (Renderer renderer in All<Renderer>())
        {
            string hierarchy = Hierarchy(renderer.transform);
            string role = hierarchy.Contains("RiskRoute") ? "Risk" :
                hierarchy.Contains("SafeRoute") || hierarchy.Contains("CompetitionPlatformRoute") ? "Safe" :
                hierarchy.Contains("WaterArea") ? "Water" : hierarchy.Contains("Switch") || hierarchy.Contains("Station") ? "Interaction" :
                hierarchy.Contains("CooperationPath") || hierarchy.Contains("CooperativePlatforms") ? "Cooperation" : "Ground";
            renderer.sharedMaterial = materials[role];
        }
    }

    private static void ConfigureDoor(Transform area)
    {
        DoorPuzzleController controller = area.GetComponent<DoorPuzzleController>();
        Transform coopFloor = Child(area, "CooperationFloor");
        area.Find("PuzzleHallwayBeforeDoor").SetParent(coopFloor, true);
        area.Find("PuzzleHallwayAfterDoor").SetParent(coopFloor, true);
        // The original before-door hallway ended short of the threshold.
        Transform before = coopFloor.Find("PuzzleHallwayBeforeDoor");
        before.localPosition = V(0, 70.5f);
        before.localScale = new Vector3(5f, 0.5f, 15f);
        Field(controller, "cooperationFloor", coopFloor.gameObject);
        Cube("DoorThreshold", area, V(0, 78), new Vector3(5f, 0.5f, 4f), "Ground");
        Cube("DoorWall_Left", area, new Vector3(-2.8f, 4f, 78f), new Vector3(0.6f, 4f, 4f), "Ground");
        Cube("DoorWall_Right", area, new Vector3(2.8f, 4f, 78f), new Vector3(0.6f, 4f, 4f), "Ground");

        Transform route = area.Find("CompetitionRoute");
        Remove(route.Find("OpenDoorRoute"));
        Transform normal = Child(route, "OpenDoorRoute");
        Path(normal, new[] { V(0, 91), V(-8, 88), V(-8, 81), V(0, 80),
            V(0, 76), V(-8, 74), V(-8, 67), V(0, 63) }, 3.4f, "Safe");
        Field(controller, "competitionRoute", route.gameObject);

        Transform shortcut = route.Find("MemoryShortcut");
        foreach (Transform child in shortcut.Cast<Transform>().ToArray())
            if (child.name.StartsWith("ShortcutPlatform_")) Remove(child);
        Cube("ShortcutFloor", shortcut, V(10, 77), new Vector3(3f, 0.5f, 28f), "Risk");
        shortcut.Find("ShortcutDoor").localPosition = V(10, 78);
        shortcut.Find("ShortcutPanel").localPosition = new Vector3(10f, 3f, 88f);
        Cube("ShortcutWall_Left", shortcut, new Vector3(8.3f, 4f, 78f), new Vector3(0.5f, 4f, 7f), "Ground");
        Cube("ShortcutWall_Right", shortcut, new Vector3(11.7f, 4f, 78f), new Vector3(0.5f, 4f, 7f), "Ground");
        Transform station = Child(shortcut, "ShortcutInputStation");
        station.localPosition = new Vector3(10f, 2.7f, 88f);
        BoxCollider trigger = station.gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(2.8f, 3f, 2f);
        DoorPuzzleStation input = station.gameObject.AddComponent<DoorPuzzleStation>();
        Field(input, "puzzleController", controller);
        Field(input, "stationType", 1);
        Field(input, "competitionOnly", true);
        Field(controller, "shortcutStation", station);
        area.Find("PuzzleStartPlatform").localScale = new Vector3(24f, 0.5f, 4f);
        Transform exit = area.Find("DoorPuzzleExitPlatform");
        if (exit == null)
        {
            Transform old = Root("Level").transform.Find("DoorPuzzleExitPlatform");
            if (old != null) Remove(old);
            exit = Cube("DoorPuzzleExitPlatform", area, V(0, 91), new Vector3(24f, 0.5f, 4f), "Ground").transform;
        }
        exit.localScale = new Vector3(24f, 0.5f, 4f);
        route.gameObject.SetActive(false);
    }

    private static void ConfigureJump(Transform area)
    {
        Transform safe = area.Find("SafeRoute");
        Clear(safe);
        Path(safe, new[] { V(0, 98), V(8, 100), V(8, 111), V(4, 114),
            V(8, 117), V(8, 128), V(0, 131) }, 3.4f, "Safe");
        safe.gameObject.SetActive(false);
    }

    private static void CompleteIntegration(bool newScene)
    {
        GameStateManager game = One<GameStateManager>();
        RespawnManager respawn = One<RespawnManager>();
        BridgeController bridge = One<BridgeController>();
        RaftController raft = One<RaftController>();
        DoorPuzzleController door = One<DoorPuzzleController>();
        JumpPuzzleController jump = One<JumpPuzzleController>();
        NetworkManager network = One<NetworkManager>();
        Transform systems = Root("GameSystems").transform;
        game.transform.SetParent(systems, true);
        // NGO checks that NetworkManager is a root before moving it to DontDestroyOnLoad.
        network.transform.SetParent(null, true);
        One<NetworkDebugUI>().transform.SetParent(systems, true);
        Field(game, "requireAllGimmicks", true);
        Field(game, "bridge", bridge); Field(game, "raft", raft);
        Field(game, "door", door); Field(game, "jump", jump);
        Field(bridge, "gameStateManager", game); Field(raft, "gameStateManager", game);
        Field(door, "gameStateManager", game); Field(jump, "gameStateManager", game);
        NetworkDebugUI debug = One<NetworkDebugUI>();
        Field(debug, "gameStateManager", game); Field(debug, "bridgeController", bridge);
        Field(debug, "raftController", raft); Field(debug, "doorController", door); Field(debug, "jumpController", jump);

        Transform players = Root("Players").transform;
        Transform spawnRoot = RootOrFind("PlayerSpawnPoints", "SpawnPoints");
        spawnRoot.name = "SpawnPoints";
        spawnRoot.SetParent(players, true);
        Transform[] spawns = spawnRoot.Cast<Transform>().ToArray();
        if (newScene)
        {
            spawns[0].name = "PlayerSpawnPoint_01"; spawns[1].name = "PlayerSpawnPoint_02";
            spawns[0].position = new Vector3(-1.5f, 0.65f, 0f);
            spawns[1].position = new Vector3(1.5f, 0.65f, 0f);
        }
        Refs(One<NetworkSpawnCoordinator>(), "spawnPoints", spawns);

        Transform level = Root("Level").transform;
        Transform turnArea = Child(level, "TurnPointArea");
        Transform turn = One<TurnPoint>().transform;
        Transform raceSpawn = Child(turnArea, "CompetitionRespawnPoint");
        if (newScene || raceSpawn.position == Vector3.zero)
        {
            raceSpawn.position = turn.position + new Vector3(0f, -0.7f, -1.5f);
            raceSpawn.rotation = Quaternion.Euler(0f, 180f, 0f);
        }
        Field(respawn, "competitionStart", raceSpawn);
        Transform goalArea = Root("GoalArea").transform;
        Goal goal = One<Goal>();
        goal.transform.SetParent(goalArea, true);
        Transform goalVisual = Child(goalArea, "GoalMarker");
        if (goalVisual.childCount == 0)
        {
            Cube("Post_Left", goalVisual, new Vector3(-3f, 2f, -2.5f), new Vector3(0.4f, 3f, 0.4f), "Goal");
            Cube("Post_Right", goalVisual, new Vector3(3f, 2f, -2.5f), new Vector3(0.4f, 3f, 0.4f), "Goal");
            Label("GoalLabel", goalVisual, new Vector3(0f, 4.2f, -2.5f), "GOAL", true);
        }
        Field(goal, "goalVisual", goalVisual.gameObject);

        Transform kills = Root("KillZones").transform;
        foreach (KillZone zone in All<KillZone>()) zone.transform.SetParent(kills, true);
        if (newScene)
        {
            KillZone zone = One<KillZone>();
            zone.transform.position = new Vector3(0f, -10f, 85f);
            zone.GetComponent<BoxCollider>().size = new Vector3(500f, 6f, 500f);
            foreach (Checkpoint cp in All<Checkpoint>()) Object.DestroyImmediate(cp.gameObject);
        }
        CreateReusablePrefabs();
        if (newScene) ConvertReusableInstances();
        else
        {
            foreach (Checkpoint checkpoint in All<Checkpoint>())
                if (!checkpoint.name.EndsWith("_Return")) Field(checkpoint, "phase", (int)CheckpointPhase.Cooperation);
        }
        AddCheckpoint("CP_Start", new Vector3(0f, 0.65f, 1f), CheckpointPhase.Cooperation, respawn);
        AddCheckpoint("CP_Bridge", new Vector3(0f, 2.4f, 12.5f), CheckpointPhase.Cooperation, respawn);
        AddCheckpoint("CP_Raft", new Vector3(0f, 2.4f, 33f), CheckpointPhase.Cooperation, respawn);
        AddCheckpoint("CP_Door", door.transform.TransformPoint(new Vector3(0f, 2.4f, 63f)), CheckpointPhase.Cooperation, respawn);
        AddCheckpoint("CP_Jump", level.Find("JumpPuzzleArea").TransformPoint(new Vector3(0f, 2.4f, 97f)), CheckpointPhase.Cooperation, respawn);
        AddCheckpoint("CP_Jump_Return", level.Find("JumpPuzzleArea").TransformPoint(new Vector3(0f, 2.4f, 133.5f)), CheckpointPhase.Competition, respawn);
        AddCheckpoint("CP_Door_Return", door.transform.TransformPoint(new Vector3(0f, 2.4f, 91.5f)), CheckpointPhase.Competition, respawn);
        AddCheckpoint("CP_Raft_Return", new Vector3(0f, 2.4f, newScene ? 86f : 54f), CheckpointPhase.Competition, respawn);
        AddCheckpoint("CP_Bridge_Return", new Vector3(0f, 2.4f, 32f), CheckpointPhase.Competition, respawn);

        PrototypeLevelFlow flow = game.GetComponent<PrototypeLevelFlow>();
        if (flow == null) flow = game.gameObject.AddComponent<PrototypeLevelFlow>();
        Field(flow, "gameStateManager", game);
        Transform start = level.Find("StartArea");
        Transform barrier = start.Find("WaitingBarrier");
        if (barrier == null) barrier = Cube("WaitingBarrier", start, new Vector3(0f, 2.5f, 4f),
            new Vector3(12f, 4f, 0.4f), "Interaction").transform;
        Field(flow, "waitingBarrier", barrier.gameObject);
        List<Object> devices = All<BridgeSwitch>().Select(x => (Object)x.gameObject).ToList();
        devices.AddRange(All<HoldSwitch>().Select(x => (Object)x.gameObject));
        devices.AddRange(All<DoorPuzzleStation>().Where(x => !Hierarchy(x.transform).Contains("Shortcut")).Select(x => (Object)x.gameObject));
        Transform jumpArea = level.Find("JumpPuzzleArea");
        foreach (string name in new[] { "MovingPlatform/Base", "MovingPlatform/RiderTrigger", "ControlSwitchBApproach", "EndArrivalTrigger" })
            if (jumpArea.Find(name) != null) devices.Add(jumpArea.Find(name).gameObject);
        Refs(flow, "cooperationDevices", devices.ToArray());
        Transform returnGuides = Child(turnArea, "ReturnGuides");
        if (returnGuides.childCount == 0)
        {
            Label("ReturnLabel", returnGuides, turn.position + new Vector3(0f, 2f, -1f), "COMPETITION\nRETURN TO GOAL", true, true);
            Arrow("ReturnArrow", returnGuides, turn.position + new Vector3(0f, -0.6f, -3f), true);
        }
        Refs(flow, "returnGuides", returnGuides.gameObject);
        if (newScene) AddCourseLabels(level);
        RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.64f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    }

    private static void AddCheckpoint(string name, Vector3 position, CheckpointPhase phase, RespawnManager respawn)
    {
        Transform root = Root("Checkpoints").transform;
        if (root.Find(name) != null) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Checkpoint.prefab");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
        instance.name = name;
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, phase == CheckpointPhase.Competition ? 180f : 0f, 0f);
        Field(instance.GetComponent<Checkpoint>(), "phase", (int)phase);
        Field(instance.GetComponent<Checkpoint>(), "respawnManager", respawn);
    }

    private static void CreateReusablePrefabs()
    {
        Directory.CreateDirectory(PrefabPath);
        AssetDatabase.Refresh();
        if (!File.Exists(PrefabPath + "/Checkpoint.prefab"))
        {
            GameObject checkpoint = new GameObject("Checkpoint", typeof(BoxCollider), typeof(Checkpoint));
            BoxCollider trigger = checkpoint.GetComponent<BoxCollider>();
            trigger.isTrigger = true; trigger.center = Vector3.up; trigger.size = new Vector3(8f, 3f, 1f);
            GameObject stripe = Cube("Marker", checkpoint.transform, new Vector3(0f, -0.05f, 0f),
                new Vector3(8f, 0.1f, 0.45f), "Checkpoint");
            Object.DestroyImmediate(stripe.GetComponent<Collider>());
            SaveTemplate(checkpoint, "Checkpoint");
            Object.DestroyImmediate(checkpoint);
        }
        if (!File.Exists(PrefabPath + "/SpawnPoint.prefab"))
        {
            GameObject spawn = new GameObject("SpawnPoint");
            SaveTemplate(spawn, "SpawnPoint"); Object.DestroyImmediate(spawn);
        }
        if (!File.Exists(PrefabPath + "/KillZone.prefab"))
        {
            GameObject zone = new GameObject("KillZone", typeof(BoxCollider), typeof(KillZone));
            zone.GetComponent<BoxCollider>().isTrigger = true;
            zone.GetComponent<BoxCollider>().size = new Vector3(20f, 6f, 20f);
            SaveTemplate(zone, "KillZone"); Object.DestroyImmediate(zone);
        }
        if (!File.Exists(PrefabPath + "/BridgeSwitch.prefab"))
        {
            GameObject copy = Object.Instantiate(One<BridgeSwitch>().gameObject);
            Field(copy.GetComponent<BridgeSwitch>(), "bridgeController", null);
            SaveTemplate(copy, "BridgeSwitch"); Object.DestroyImmediate(copy);
        }
        if (!File.Exists(PrefabPath + "/HoldSwitch.prefab"))
        {
            GameObject copy = Object.Instantiate(One<HoldSwitch>().gameObject);
            Field(copy.GetComponent<HoldSwitch>(), "puzzleController", null);
            SaveTemplate(copy, "HoldSwitch"); Object.DestroyImmediate(copy);
        }
        if (!File.Exists(PrefabPath + "/MovingPlatform.prefab"))
        {
            GameObject copy = Object.Instantiate(One<MovingPlatform>().gameObject);
            MovingPlatform moving = copy.GetComponent<MovingPlatform>();
            Field(moving, "puzzleController", null); Field(moving, "startPoint", null); Field(moving, "endPoint", null);
            SaveTemplate(copy, "MovingPlatform"); Object.DestroyImmediate(copy);
        }
    }

    private static void SaveTemplate(GameObject item, string name)
    {
        item.name = name;
        item.transform.SetParent(null, true);
        item.transform.position = Vector3.zero;
        item.transform.rotation = Quaternion.identity;
        PrefabUtility.SaveAsPrefabAsset(item, PrefabPath + "/" + name + ".prefab");
    }

    private static void ConvertReusableInstances()
    {
        Transform spawnRoot = Root("Players").transform.Find("SpawnPoints");
        List<Object> points = new List<Object>();
        foreach (Transform old in spawnRoot.Cast<Transform>().ToArray())
        {
            GameObject point = ReplaceWithPrefab(old.gameObject, "SpawnPoint");
            points.Add(point.transform);
        }
        Refs(One<NetworkSpawnCoordinator>(), "spawnPoints", points.ToArray());
        foreach (BridgeSwitch old in All<BridgeSwitch>())
        {
            int id = (int)old.SwitchId;
            GameObject item = ReplaceWithPrefab(old.gameObject, "BridgeSwitch");
            Field(item.GetComponent<BridgeSwitch>(), "bridgeController", One<BridgeController>());
            Field(item.GetComponent<BridgeSwitch>(), "switchId", id);
            Field(item.GetComponent<BridgeSwitch>(), "inactiveColor", new Color(0.95f, 0.55f, 0.12f));
        }
        foreach (HoldSwitch old in All<HoldSwitch>())
        {
            int id = new SerializedObject(old).FindProperty("switchId").intValue;
            GameObject item = ReplaceWithPrefab(old.gameObject, "HoldSwitch");
            Field(item.GetComponent<HoldSwitch>(), "puzzleController", One<JumpPuzzleController>());
            Field(item.GetComponent<HoldSwitch>(), "switchId", id);
            Field(One<JumpPuzzleController>(), id == 0 ? "switchAPoint" : "switchBPoint", item.transform);
        }
        foreach (KillZone old in All<KillZone>())
        {
            Vector3 size = old.GetComponent<BoxCollider>().size;
            ReplaceWithPrefab(old.gameObject, "KillZone").GetComponent<BoxCollider>().size = size;
        }
    }

    private static GameObject ReplaceWithPrefab(GameObject old, string template)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/" + template + ".prefab");
        GameObject item = (GameObject)PrefabUtility.InstantiatePrefab(asset, old.transform.parent);
        item.name = old.name;
        item.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
        item.transform.localScale = old.transform.localScale;
        Object.DestroyImmediate(old);
        return item;
    }

    private static void AddCourseLabels(Transform level)
    {
        Transform signs = Child(level, "CourseGuides");
        Label("StartGuide", signs, new Vector3(0f, 4.5f, 5f), "COOPERATE OUTWARD\nBRIDGE > RAFT > CODE > JUMP");
        Label("BridgeGuide", signs, new Vector3(0f, 5f, 13f), "BRIDGE\nONE SWITCH EACH - PRESS E");
        Label("RaftGuide", signs, new Vector3(0f, 5f, 34f), "RAFT\nONE CONTROL EACH - W/S\nEMPTY RAFT RETURNS TO START");
        Label("DoorGuide", signs, new Vector3(0f, 5f, 93f), "DOOR CODE\nONE READER / ONE INPUT\nREMEMBER THE CODE FOR THE RETURN");
        Label("JumpGuide", signs, new Vector3(0f, 5.3f, 127f), "JUMP\nA HOLDS - B RIDES\nB HOLDS - A CROSSES");
        Label("TurnGuide", signs, new Vector3(0f, 5.5f, 176f), "TURN POINT\nWAIT FOR YOUR PARTNER");
        Label("JumpReturnGuide", signs, new Vector3(0f, 5f, 161f), "GREEN: SAFE DETOUR\nRED: PRECISE JUMPS", true);
        Label("DoorReturnGuide", signs, new Vector3(0f, 5f, 121f), "GREEN: OPEN DOOR ROUTE\nRED: MEMORY CODE SHORTCUT", true);
        Label("RaftReturnGuide", signs, new Vector3(0f, 5f, 85f), "RAFT: W RETURNS / S REVERSES\nGREEN: PLATFORM DETOUR", true);
        Label("BridgeReturnGuide", signs, new Vector3(0f, 5f, 32f), "GREEN: SAFE DETOUR\nRED: JUMP SHORTCUT", true);
        Transform start = level.Find("StartArea");
        Child(start, "StartZone");
        Arrow("StartArrow", start, new Vector3(0f, 0.58f, 2f), false);
    }

    private static void Label(string name, Transform parent, Vector3 position, string value,
        bool reverse = false, bool worldPosition = false)
    {
        Transform root = Child(parent, name);
        if (worldPosition) root.position = position; else root.localPosition = position;
        root.localRotation = Quaternion.Euler(0f, reverse ? 180f : 0f, 0f);
        TextMesh text = root.gameObject.AddComponent<TextMesh>();
        text.text = value; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.GetComponent<Renderer>().sharedMaterial = text.font.material;
        text.fontSize = 48; text.characterSize = 0.09f; text.color = Color.white;
    }

    private static void Arrow(string name, Transform parent, Vector3 position, bool reverse)
    {
        Transform root = Child(parent, name);
        root.position = position;
        root.rotation = Quaternion.Euler(0f, reverse ? 180f : 0f, 0f);
        GameObject shaft = Cube("Shaft", root, Vector3.zero, new Vector3(0.25f, 0.07f, 1.4f), "Goal");
        GameObject left = Cube("Head_Left", root, new Vector3(-0.22f, 0f, 0.55f), new Vector3(0.22f, 0.07f, 0.7f), "Goal");
        GameObject right = Cube("Head_Right", root, new Vector3(0.22f, 0f, 0.55f), new Vector3(0.22f, 0.07f, 0.7f), "Goal");
        left.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        right.transform.localRotation = Quaternion.Euler(0f, -45f, 0f);
        foreach (GameObject item in new[] { shaft, left, right }) Object.DestroyImmediate(item.GetComponent<Collider>());
    }

    private static void LoadMaterials()
    {
        Directory.CreateDirectory(MaterialPath);
        AssetDatabase.Refresh();
        Material("Ground", new Color(0.4f, 0.44f, 0.49f));
        Material("Safe", new Color(0.15f, 0.65f, 0.4f));
        Material("Risk", new Color(0.86f, 0.2f, 0.14f));
        Material("Interaction", new Color(0.95f, 0.55f, 0.12f));
        Material("Cooperation", new Color(0.17f, 0.65f, 0.82f));
        Material("Checkpoint", new Color(0.15f, 0.4f, 0.95f));
        Material("Goal", new Color(1f, 0.8f, 0.1f));
        Material("Water", new Color(0.1f, 0.28f, 0.48f));
    }

    private static void Material(string name, Color color)
    {
        string path = MaterialPath + "/" + name + ".mat";
        Material asset = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (asset == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            asset = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(asset, path);
        }
        materials[name] = asset;
    }

    private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, string material)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = name; item.transform.SetParent(parent, false);
        item.transform.localPosition = position; item.transform.localScale = scale;
        item.GetComponent<Renderer>().sharedMaterial = materials[material];
        return item;
    }

    private static void Segment(string name, Transform parent, Vector3 a, Vector3 b, float width, string material)
    {
        GameObject item = Cube(name, parent, (a + b) * 0.5f,
            new Vector3(width, 0.5f, Vector3.Distance(a, b) + 0.15f), material);
        item.transform.localRotation = Quaternion.LookRotation(b - a, Vector3.up);
    }

    private static void Path(Transform root, Vector3[] points, float width, string material)
    {
        for (int i = 1; i < points.Length; i++) Segment($"Section_{i:00}", root, points[i - 1], points[i], width, material);
    }

    private static Vector3 V(float x, float z) => new Vector3(x, 2f, z);
    private static void Remove(Transform item) { if (item != null) Object.DestroyImmediate(item.gameObject); }
    private static void Clear(Transform item) { foreach (Transform child in item.Cast<Transform>().ToArray()) Remove(child); }
    private static Transform Child(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;
        child = new GameObject(name).transform; child.SetParent(parent, false); return child;
    }
    private static GameObject Root(string name)
    {
        return SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x => x.name == name) ?? new GameObject(name);
    }
    private static Transform RootOrFind(string firstName, string secondName)
    {
        return All<Transform>().FirstOrDefault(x => x.name == firstName || x.name == secondName) ?? Root(secondName).transform;
    }
    private static string Hierarchy(Transform item) => item.parent == null ? item.name : Hierarchy(item.parent) + "/" + item.name;
    private static T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    private static T One<T>() where T : Object => All<T>().First();

    private static void Field(Object target, string name, object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null) throw new InvalidOperationException($"Missing serialized field {target.GetType().Name}.{name}");
        if (value == null || value is Object) property.objectReferenceValue = value as Object;
        else if (value is bool flag) property.boolValue = flag;
        else if (value is int integer)
        {
            if (property.propertyType == SerializedPropertyType.Enum) property.enumValueIndex = integer;
            else property.intValue = integer;
        }
        else if (value is float number) property.floatValue = number;
        else if (value is Color color) property.colorValue = color;
        else throw new InvalidOperationException("Unsupported serialized value: " + name);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Refs(Object target, string name, params Object[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Prototype/Validate Integrated Scene")]
    public static void ValidateCurrentScene()
    {
        List<string> errors = new List<string>();
        if (All<NetworkManager>().Length != 1) errors.Add("Exactly one NetworkManager is required.");
        if (All<GameStateManager>().Length != 1 || All<RespawnManager>().Length != 1) errors.Add("Game/respawn manager duplicate or missing.");
        if (All<PlayerMovement>().Length != 0) errors.Add("Remove scene players; NetworkManager must spawn the player prefab.");
        foreach (NetworkObject item in All<NetworkObject>())
            if (item.transform.parent != null && item.transform.parent.GetComponentInParent<NetworkObject>() != null)
                errors.Add("Nested NetworkObject: " + Hierarchy(item.transform));
        NetworkManager manager = One<NetworkManager>();
        if (manager.transform.parent != null) errors.Add("NetworkManager must remain a scene root.");
        if (manager.NetworkConfig.PlayerPrefab == null || manager.NetworkConfig.NetworkTransport == null)
            errors.Add("Player prefab or transport missing.");
        if (All<BridgeController>().Length != 1 || All<RaftController>().Length != 1 ||
            All<DoorPuzzleController>().Length != 1 || All<JumpPuzzleController>().Length != 1)
            errors.Add("Each gimmick needs exactly one controller.");
        foreach (Transform item in All<Transform>())
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) > 0)
                errors.Add("Missing script: " + Hierarchy(item));
        foreach (IGrouping<string, Checkpoint> group in All<Checkpoint>().GroupBy(x => x.name))
            if (group.Count() > 1) errors.Add("Duplicate checkpoint name: " + group.Key);
        foreach (Checkpoint checkpoint in All<Checkpoint>())
            if (!checkpoint.GetComponent<Collider>().isTrigger) errors.Add("Checkpoint collider must be a trigger: " + checkpoint.name);
        MovingPlatform moving = One<MovingPlatform>();
        if (moving.transform.lossyScale != Vector3.one) errors.Add("MovingPlatform root must have unit scale.");
        foreach (JumpPuzzleEndTrigger end in All<JumpPuzzleEndTrigger>())
        {
            Vector3 size = end.GetComponent<Collider>().bounds.size;
            if (size.x > 20f || size.z > 15f || size.y < 2f) errors.Add("End trigger inherits incorrect platform scale.");
        }
        CheckReferences(One<GameStateManager>(), errors, "bridge", "raft", "door", "jump");
        CheckReferences(One<JumpPuzzleController>(), errors, "gameStateManager", "switchAPoint", "switchBPoint", "endPlatformTrigger", "cooperativePlatforms", "safeRoute", "riskRoute");
        CheckReferences(One<DoorPuzzleController>(), errors, "gameStateManager", "cooperationDoor", "competitionRoute", "shortcutDoor", "shortcutStation", "cooperationFloor");
        CheckReferences(One<RaftController>(), errors, "gameStateManager", "respawnPoint", "raftEnd", "leftControlPoint", "rightControlPoint");
        CheckReferences(moving, errors, "startPoint", "endPoint", "puzzleController");
        Physics.SyncTransforms();
        foreach (Checkpoint checkpoint in All<Checkpoint>())
        {
            Vector3 origin = checkpoint.transform.position + Vector3.up * 0.7f;
            if (!Physics.Raycast(origin, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
                errors.Add("Checkpoint has no floor below its respawn point: " + checkpoint.name);
        }
        if (errors.Count != 0) throw new InvalidOperationException("Integrated scene validation failed:\n" + string.Join("\n", errors));
        Debug.Log("INTEGRATED SCENE VALIDATION PASSED: managers, ownership prefab, network hierarchy, references, trigger scales, checkpoint floors.");
    }

    private static void CheckReferences(Object target, List<string> errors, params string[] fields)
    {
        SerializedObject serialized = new SerializedObject(target);
        foreach (string field in fields)
            if (serialized.FindProperty(field)?.objectReferenceValue == null) errors.Add(target.GetType().Name + "." + field + " is not assigned.");
    }
}
