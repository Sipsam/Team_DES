#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using TwinEdge.Gameplay;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Opt-in real two-process Relay test. Station teleports are test setup, never route validation.
public sealed partial class V3GameplaySmoke : MonoBehaviour
{
    bool host, failed;
    string evidence;
    NetworkManager manager;
    MultiplayerSessionManager sessions;
    PlayerMovement player;
    Keyboard keyboard;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-v3Smoke");
        if (at < 0) return;
        var test = new GameObject("V3Smoke").AddComponent<V3GameplaySmoke>();
        test.host = args[at + 1] == "host";
        test.evidence = args[Array.IndexOf(args, "-v3Evidence") + 1];
        Directory.CreateDirectory(test.evidence);
    }
    IEnumerator Start()
    {
        Application.runInBackground = true; Application.targetFrameRate = 60;
        // This opt-in process owns a runtime settings clone; hidden-window focus must not discard injected keys.
        InputSystem.settings = Instantiate(InputSystem.settings);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        Application.logMessageReceived += Log;
        manager = FindFirstObjectByType<NetworkManager>(); sessions = FindFirstObjectByType<MultiplayerSessionManager>();
        yield return Until(() => FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).Length >= 4, "V3 load / checkpoint setup");
        if (host)
        {
            var task = sessions.CreateRoomAsync(); yield return Until(() => task.IsCompleted, "Relay create", 80);
            Check(sessions.HasSession, "Host Relay session exists");
            File.WriteAllText(evidence + "/join-code.tmp", sessions.CurrentJoinCode);
            File.Move(evidence + "/join-code.tmp", evidence + "/join-code.txt");
        }
        else
        {
            yield return Until(() => File.Exists(evidence + "/join-code.txt"), "Room code available", 100);
            var task = sessions.JoinRoomAsync(File.ReadAllText(evidence + "/join-code.txt"));
            yield return Until(() => task.IsCompleted, "Relay join", 80); Check(sessions.HasSession, "Joined Relay code");
        }
        var game = FindFirstObjectByType<GameStateManager>();
        yield return Until(() => game.IsGameReady && manager.LocalClient?.PlayerObject != null, "Two players spawned", 80);
        yield return Until(() => FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None).Length == 2, "Two visible players");
        player = manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
        Check(player.IsOwner, "Local ownership");
        Check(Camera.main != null && Camera.main.enabled && Camera.main.GetComponent<ThirdPersonCamera>().enabled, "Owner camera bound and rendering enabled");
        Check(!FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(c => c.enabled && c.name == "MountainPreviewCamera"), "Level preview cannot overwrite owner view: "+string.Join(", ",FindObjectsByType<Camera>(FindObjectsSortMode.None).Select(c=>c.name+" enabled="+c.enabled+" scene="+c.gameObject.scene.name)));
        Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.enabled) == 1, "One audio listener");
        yield return Until(() => game.CanPlay, "Host starts outbound");
        keyboard = InputSystem.AddDevice<Keyboard>();
        Cursor.lockState = CursorLockMode.Locked;
        player.GetComponent<GameplayInput>().SimulateCapturedCursor = true;
        File.WriteAllText(evidence + (host ? "/host-input-ready.txt" : "/client-input-ready.txt"), "ready");
        yield return Until(() => File.Exists(evidence + "/host-input-ready.txt") && File.Exists(evidence + "/client-input-ready.txt"), "Input barrier");
        var remote = FindObjectsByType<GameplayCharacter>(FindObjectsSortMode.None).Single(x => !x.IsOwner);
        if (Environment.GetCommandLineArgs().Contains("-v3CameraControls"))
        {
            yield return RunCameraControls(remote);
            if (host) yield return Until(() => !game.IsGameReady, "Camera controls client left");
            var leave = sessions.LeaveRoomAsync();
            yield return Until(() => leave.IsCompleted && !manager.IsListening, "Camera controls session closed");
            Application.Quit(failed ? 2 : 0);
            yield break;
        }
        bool impaired = Environment.GetCommandLineArgs().Contains("-v3ImpairedRelay");
        if (impaired)
        {
            var transport = (UnityTransport)manager.NetworkConfig.NetworkTransport;
            string peer = host ? "host" : "client";
            ulong beforeRtt = transport.GetCurrentRtt(remote.OwnerClientId);
            transport.GetNetworkDriver().ModifyNetworkSimulatorParameters(new NetworkSimulatorParameter
            {
                SendDelayMS = 60,
                SendJitterMS = 10,
                SendPacketLossPercent = 3
            });
            File.WriteAllText(Path.Combine(evidence, peer + "-impairment.txt"),
                $"Each process: 60 ms send delay, 10 ms jitter, 3% outbound loss. Before RTT: {beforeRtt} ms.");
            yield return Until(() => File.Exists(Path.Combine(evidence, (host ? "client" : "host") + "-impairment.txt")),
                "Both Unity Transport simulators active");
            yield return new WaitForSeconds(4);
            ulong afterRtt = transport.GetCurrentRtt(remote.OwnerClientId);
            File.AppendAllText(Path.Combine(evidence, peer + "-impairment.txt"), $"\nAfter RTT: {afterRtt} ms.");
            // The idle client may still report its last reliable-send RTT until movement starts.
            if (host) Check(afterRtt > beforeRtt + 30, "Measured Relay RTT increased with packet simulation");
        }
        if (Environment.GetCommandLineArgs().Contains("-v3Contest"))
        {
            yield return RunContest(game, remote);
            if (host) yield return Until(() => !game.IsGameReady, "contest client disconnected cleanly");
            var leave = sessions.LeaveRoomAsync();
            yield return Until(() => leave.IsCompleted, "contest session closed");
            Application.Quit(failed ? 2 : 0);
            yield break;
        }
        Vector3 remoteBefore = remote.transform.position;
        Vector3 before = player.transform.position;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
        yield return new WaitForSeconds(1.1f);
        Check(Vector3.Distance(before, player.transform.position) > 2, $"Input System sprint movement: delta={Vector3.Distance(before, player.transform.position)}, cursor={Cursor.lockState}, accepts={player.GetComponent<GameplayInput>().AcceptsInput}, move={player.GetComponent<GameplayInput>().Move.ReadValue<Vector2>()}, sprint={player.GetComponent<GameplayInput>().Sprint.IsPressed()}, canPlay={game.CanPlay}, grounded={player.IsGrounded}");
        var own = player.GetComponent<GameplayCharacter>();
        Check(own.AnimationState == "Run", "Run animation from displacement");
        yield return Until(() => remote.AnimationState == "Run", "Remote Run replicated", 5);
        Check(Vector3.Distance(remoteBefore, remote.transform.position) > 2, "Remote position replicated");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(.5f);
        Check(own.AnimationState == "Idle", "Idle animation after stop");
        Quaternion remoteRotation = remote.transform.rotation;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
        yield return new WaitForSeconds(.7f);
        Check(own.AnimationState == "Walk", "Walk animation from movement");
        yield return Until(() => Quaternion.Angle(remoteRotation, remote.transform.rotation) > 30, "Remote rotation replicated");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(.5f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return Until(() => own.AnimationState == "Jump", "Jump animation airborne", 1.5f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSeconds(1.4f);
        Check(player.IsGrounded, "Landing on V3 collider");
        // Host alone toggles common state; both peers inspect its replicated prompt.
        var target = FindFirstObjectByType<NetworkInteractionTarget>();
        Teleport(target.transform.position + new Vector3(host ? -1f : 1f, 0, -8));
        yield return new WaitForSeconds(.7f);
        if (host) target.Interact(player.GetComponent<PlayerInteraction>());
        yield return new WaitForSeconds(.8f);
        Check(target.Prompt.Contains("[OFF]"), "Out-of-range interaction refused");
        Teleport(target.transform.position + new Vector3(host ? -1f : 1f, 0, -1.2f)); yield return new WaitForSeconds(.7f);
        if (host) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return new WaitForSeconds(.1f); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); }
        yield return Until(() => target.Prompt.Contains("[ON]"), "Interaction replicated");
        if (host)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); yield return new WaitForSeconds(.4f);
            Check(own.AnimationState == "Walk_Carry", "Carrying animation");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }
        File.WriteAllText(evidence + (host ? "/host-interaction-ready.txt" : "/client-interaction-ready.txt"), "ready");
        yield return Until(() => File.Exists(evidence + "/host-interaction-ready.txt") && File.Exists(evidence + "/client-interaction-ready.txt"), "Interaction barrier");
        if (!host)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }
        yield return Until(() => target.Prompt.Contains("[OFF]"), "Client-authorized interaction replicated");
        var cp = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).Single(x => x.name.Contains("Big summit"));
        Teleport(cp.transform.position + Vector3.right * (host ? -1.5f : 1.5f));
        yield return Until(() => own.CheckpointName.Value.ToString().Contains("Big summit"), "Server checkpoint replicated");
        yield return new WaitForSeconds(.7f);
        int count = own.RespawnCount.Value;
        Teleport(player.transform.position + Vector3.down * 25);
        yield return Until(() => own.RespawnCount.Value > count, "Fall respawn counter");
        yield return Until(() => Vector3.Distance(player.transform.position, cp.transform.position) < 4, "Respawn owner teleport replicated");
        File.WriteAllText(evidence + (host ? "/host-pass.txt" : "/client-pass.txt"), "PASS Relay join, two players, owner camera, walk/sprint/idle/jump, remote position/rotation/Run, host and client interaction replication, range refusal, checkpoint, fall respawn. Station teleport smoke; no manual route/camera visual validation.");
        if (host)
        {
            yield return Until(() => File.Exists(evidence + "/client-pass.txt"), "Client complete");
            var transport = (UnityTransport)manager.NetworkConfig.NetworkTransport;
            yield return Until(() => !game.IsGameReady, "Disconnect pauses gameplay", transport.DisconnectTimeoutMS / 1000f + 10);
        }
        var leaveRoom = sessions.LeaveRoomAsync();
        yield return Until(() => leaveRoom.IsCompleted && !manager.IsListening, "Local Relay session closed", 60);
        Application.Quit(failed ? 2 : 0);
    }
    void Teleport(Vector3 position)
    {
        var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
        player.GetComponent<OwnerNetworkTransform>().Teleport(position, player.transform.rotation, player.transform.localScale);
        player.ResetVerticalVelocity(); cc.enabled = true;
    }
    IEnumerator Until(Func<bool> condition, string label, float timeout = 30)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Check(condition(), label);
    }
    void Check(bool success, string label)
    {
        if (!success) { Debug.LogError("V3_SMOKE_FAIL " + label); throw new Exception(label); }
        Debug.Log("V3_SMOKE_PASS " + label);
    }
    void Log(string message, string stack, LogType type)
    {
        if (failed || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        failed = true;
        File.WriteAllText(evidence + (host ? "/host-failure.txt" : "/client-failure.txt"), message + "\n" + stack);
        Application.Quit(2);
    }
}
#endif
