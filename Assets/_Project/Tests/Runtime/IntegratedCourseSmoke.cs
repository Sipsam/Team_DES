#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

// Opt-in, two-process gameplay smoke test. Direct transport by default; -prototypeSmokeRelay uses real sessions.
// Teleports between test stations; actual route traversal and timing remain manual playtests.
public sealed class IntegratedCourseSmoke : MonoBehaviour
{
    private bool host;
    private bool relay;
    private string codePath;
    private bool failed;
    private NetworkManager network;
    private PlayerMovement player;
    private MultiplayerSessionManager session;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-prototypeSmoke");
        if (index < 0 || index + 1 >= args.Length) return;
        IntegratedCourseSmoke test = new GameObject("IntegratedCourseSmoke").AddComponent<IntegratedCourseSmoke>();
        test.host = args[index + 1] == "host";
        test.relay = args.Contains("-prototypeSmokeRelay");
        int pathIndex = Array.IndexOf(args, "-prototypeSmokeCode");
        test.codePath = args[pathIndex + 1];
        DontDestroyOnLoad(test.gameObject);
    }

    private void Start()
    {
        Application.targetFrameRate = 60;
        Application.logMessageReceived += OnLog;
        StartCoroutine(Run());
    }

    private void OnLog(string message, string trace, LogType type)
    {
        if (failed || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        failed = true;
        File.WriteAllText(codePath + (host ? ".hostFailure" : ".clientFailure"), message + "\n" + trace);
        Application.Quit(2);
    }

    private IEnumerator Run()
    {
        network = FindFirstObjectByType<NetworkManager>();
        session = FindFirstObjectByType<MultiplayerSessionManager>();
        if (relay)
        {
            if (host)
            {
                var create = session.CreateRoomAsync();
                yield return Until(() => create.IsCompleted, "Relay session creation", 60f);
                Check(session.HasSession, "Relay session exists");
                File.WriteAllText(codePath + ".joinCode", session.CurrentJoinCode);
            }
            else
            {
                yield return Until(() => File.Exists(codePath + ".joinCode"), "Relay join code", 60f);
                var join = session.JoinRoomAsync(File.ReadAllText(codePath + ".joinCode"));
                yield return Until(() => join.IsCompleted, "Relay session join", 60f);
                Check(session.HasSession, "Joined Relay session");
            }
        }
        else
        {
            network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", 7788, "0.0.0.0");
            Check(host ? network.StartHost() : network.StartClient(), "Start network");
        }
        GameStateManager game = FindFirstObjectByType<GameStateManager>();
        yield return Until(() => game.IsGameReady && network.LocalClient?.PlayerObject != null, "Two spawned players ready");
        player = network.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
        yield return Until(() => FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None).Length == 2, "Two visible player objects");
        Check(player.IsOwner, "Local ownership");
        Step("Ready and two players verified");

        BridgeController bridge = FindFirstObjectByType<BridgeController>();
        BridgeSwitch ownSwitch = FindObjectsByType<BridgeSwitch>(FindObjectsSortMode.None)
            .First(x => x.SwitchId == (host ? BridgeSwitchId.A : BridgeSwitchId.B));
        Teleport(ownSwitch.transform.position + new Vector3(0f, 0f, -1.5f));
        yield return new WaitForSeconds(0.8f);
        bridge.RequestSwitchActivation(host ? BridgeSwitchId.A : BridgeSwitchId.B);
        yield return Until(() => bridge.IsCooperationCompleted, "Bridge shared completion");
        Check(Get<GameObject>(bridge, "cooperationPath").activeSelf, "Bridge open on this peer");
        Step("Bridge completed");

        RaftController raft = FindFirstObjectByType<RaftController>();
        Transform control = Get<Transform>(raft, host ? "leftControlPoint" : "rightControlPoint");
        Teleport(control.position + Vector3.up * 0.15f);
        yield return new WaitForSeconds(0.8f);
        float raftDeadline = Time.realtimeSinceStartup + 55f;
        MethodInfo submit = typeof(RaftController).GetMethod("SubmitControlInputServerRpc", BindingFlags.Instance | BindingFlags.NonPublic);
        while (!raft.IsCooperationCompleted)
        {
            Check(Time.realtimeSinceStartup < raftDeadline, "Cooperative raft arrived");
            raft.RequestControlPointServerRpc(host ? 0 : 1);
            submit.Invoke(raft, new object[] { host ? 0 : 1, 1f, default(RpcParams) });
            yield return new WaitForSeconds(0.1f);
        }
        Step("Raft carried both players and completed");

        DoorPuzzleController door = FindFirstObjectByType<DoorPuzzleController>();
        Teleport(Get<Transform>(door, host ? "infoStation" : "inputStation").position + Vector3.up * 0.15f);
        yield return Until(() => DoorPuzzleUI.IsPanelVisible, "Local station UI");
        if (host)
        {
            DoorPuzzleUI ui = FindFirstObjectByType<DoorPuzzleUI>();
            string code = new string(Get<Text>(ui, "infoCodeText").text.Where(char.IsDigit).ToArray());
            Check(code.Length == 3, "Private info code");
            File.WriteAllText(codePath, code); // Represents the two testers sharing the code verbally.
        }
        else
        {
            Check(Get<string>(door, "answerCode") == string.Empty, "Answer is not broadcast to client");
            yield return Until(() => File.Exists(codePath), "Partner code fixture");
            string code = File.ReadAllText(codePath);
            EnterCode(code == "000" ? "111" : "000");
            yield return new WaitForSeconds(0.8f);
            Check(!door.IsDoorOpen, "Wrong code leaves door locked");
            EnterCode(code);
        }
        yield return Until(() => door.IsDoorOpen, "Door shared completion");
        yield return new WaitForSeconds(0.5f);
        Check(!Get<Collider>(Get<PuzzleDoor>(door, "cooperationDoor"), "blockingCollider").enabled, "Shared door collider open");
        Step("Wrong code and shared door open verified");

        JumpPuzzleController jump = FindFirstObjectByType<JumpPuzzleController>();
        MovingPlatform moving = FindFirstObjectByType<MovingPlatform>();
        if (host)
        {
            Teleport(Get<Transform>(jump, "switchAPoint").position);
            yield return Until(() => jump.IsSwitchBActive, "Partner reached B while A held");
            Check(Get<GameObject>(jump, "cooperativePlatforms").activeSelf, "Cooperative route replicated");
            Teleport(new Vector3(-1.5f, 2.4f, 161f));
        }
        else
        {
            Teleport(moving.transform.position + Vector3.up * 0.3f);
            float startZ = player.transform.position.z;
            yield return Until(() => player.transform.position.z > startZ + 5f, "Moving platform carried local owner", 25f);
            Teleport(Get<Transform>(jump, "switchBPoint").position);
            yield return Until(() => jump.IsSwitchBActive, "B hold accepted");
            yield return Until(() => jump.ArrivalCount == 1, "One end arrival replicated");
            Check(!jump.IsCooperationCompleted, "One arrival does not complete puzzle");
            Teleport(new Vector3(1.5f, 2.4f, 161f));
        }
        yield return Until(() => jump.IsCooperationCompleted, "Jump shared completion");
        Step("Moving platform, hold handoff, two arrivals verified");

        TurnPoint turn = FindFirstObjectByType<TurnPoint>();
        Teleport(new Vector3(host ? -1f : 1f, 2.4f, turn.transform.position.z));
        yield return Until(() => game.CurrentState == GameState.Competition, "Both arrivals start competition");
        yield return new WaitForSeconds(0.5f);
        Check(game.AllGimmicksCompleted, "All four completion flags shared");
        Check(bridge.CurrentState == BridgeState.Competition, "Bridge competition state");
        Check(!Get<GameObject>(bridge, "cooperationPath").activeSelf, "Outbound bridge removed");
        Check(Get<GameObject>(jump, "safeRoute").activeSelf && Get<GameObject>(jump, "riskRoute").activeSelf, "Jump routes active");
        Check(Get<GameObject>(raft, "competitionPlatformRoute").activeSelf, "Raft route active");
        Check(Get<GameObject>(door, "competitionRoute").activeSelf, "Door routes active");
        Step("Competition and routes verified");

        string checkpointName = host ? "CP_Jump_Return" : "CP_Door_Return";
        Checkpoint checkpoint = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).First(x => x.name == checkpointName);
        Teleport(checkpoint.transform.position);
        yield return new WaitForSeconds(0.7f);
        player.RequestCheckpointServerRpc(checkpointName);
        yield return new WaitForSeconds(0.5f);
        KillZone kill = FindFirstObjectByType<KillZone>();
        Teleport(kill.transform.position);
        yield return new WaitForSeconds(0.3f);
        player.RequestKillZoneRespawnServerRpc(kill.name);
        yield return Until(() => Vector3.Distance(player.transform.position, checkpoint.transform.position) < 1.5f,
            "Independent return checkpoint respawn");
        Step("Personal respawn verified");

        if (!host)
        {
            Teleport(Get<Transform>(door, "shortcutStation").position);
            yield return Until(() => DoorPuzzleUI.IsPanelVisible, "Return shortcut input UI");
            EnterCode(File.ReadAllText(codePath));
            yield return Until(() => door.IsLocalPlayerShortcutUnlocked(), "Personal memory shortcut unlocked");
            Check(!Get<Collider>(Get<PuzzleDoor>(door, "shortcutDoor"), "blockingCollider").enabled, "Client shortcut open locally");
            File.WriteAllText(codePath + ".shortcutDone", "pass");
        }
        else
        {
            yield return Until(() => File.Exists(codePath + ".shortcutDone"), "Client memory shortcut test");
            Check(!door.IsLocalPlayerShortcutUnlocked(), "Host shortcut remains locked");
            Check(Get<Collider>(Get<PuzzleDoor>(door, "shortcutDoor"), "blockingCollider").enabled, "Host shortcut collider remains closed");
            Teleport(new Vector3(0f, 0.65f, -2.5f));
        }
        yield return Until(() => game.CurrentState == GameState.Finished, "Finished replicated");
        Check(game.WinnerClientId == 0, "Host first arrival won on both peers");
        if (!host)
        {
            Teleport(new Vector3(0f, 0.65f, -2.5f));
            yield return new WaitForSeconds(1f);
            Check(game.WinnerClientId == 0, "Second arrival did not change winner");
            Step("PROTOTYPE_SMOKE_PASS client");
            if (relay)
            {
                var leave = session.LeaveRoomAsync();
                yield return Until(() => leave.IsCompleted, "Client Relay leave");
            }
            else network.Shutdown();
            yield return new WaitForSeconds(0.5f);
            Application.Quit(0);
            yield break;
        }
        yield return Until(() => !game.IsGameReady, "Disconnect released readiness");
        if (relay)
        {
            var leave = session.LeaveRoomAsync();
            yield return Until(() => leave.IsCompleted, "Host Relay leave");
        }
        else network.Shutdown();
        yield return new WaitForSeconds(1f);
        if (relay)
        {
            var recreate = session.CreateRoomAsync();
            yield return Until(() => recreate.IsCompleted, "Create second Relay session", 60f);
            Check(session.HasSession && network.IsHost, "Restart Relay host");
        }
        else Check(network.StartHost(), "Restart host");
        yield return Until(() => game.IsSpawned, "Scene objects respawned");
        yield return new WaitForSeconds(1f);
        Check(game.CurrentState == GameState.Cooperation && !game.HasWinner && !game.IsGameReady, "Fresh game state");
        Check(!bridge.IsCooperationCompleted && !raft.IsCooperationCompleted && !door.IsDoorOpen && !jump.IsCooperationCompleted,
            "All gimmicks reset for new session");
        Check(Vector3.Distance(raft.transform.position, Get<Transform>(raft, "respawnPoint").position) < 0.1f, "Raft initial pose reset");
        Step("PROTOTYPE_SMOKE_PASS host; restart verified");
        if (relay)
        {
            var leave = session.LeaveRoomAsync();
            yield return Until(() => leave.IsCompleted, "Cleanup second Relay session");
        }
        else network.Shutdown();
        yield return new WaitForSeconds(0.5f);
        Application.Quit(0);
    }

    private void EnterCode(string code)
    {
        DoorPuzzleUI ui = FindFirstObjectByType<DoorPuzzleUI>();
        GameObject panel = Get<GameObject>(ui, "panelRoot");
        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        buttons.First(x => x.name == "ResetButton").onClick.Invoke();
        foreach (char digit in code) buttons.First(x => x.name == "NumberButton_" + digit).onClick.Invoke();
        buttons.First(x => x.name == "SubmitButton").onClick.Invoke();
    }

    private void Teleport(Vector3 position)
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = position;
        player.GetComponent<OwnerNetworkTransform>().Teleport(position, player.transform.rotation, player.transform.localScale);
        player.ResetVerticalVelocity();
        controller.enabled = true;
    }

    private static T Get<T>(object item, string field) =>
        (T)item.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(item);

    private static IEnumerator Until(Func<bool> predicate, string step, float timeout = 30f)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!predicate())
        {
            Check(Time.realtimeSinceStartup < deadline, "Timeout: " + step);
            yield return null;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("PROTOTYPE_SMOKE_FAIL: " + message);
    }
    private void Step(string value) => Debug.Log($"[SMOKE {(host ? "HOST" : "CLIENT")}] {value}");
}
#endif
