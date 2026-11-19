#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TwinEdge.Gameplay
{
    // Real Relay peers. The only initial placement is outside the castle gate.
    public sealed class CastleRelayReplay : MonoBehaviour
    {
        string output, role, room;
        bool host, failed, screenshots, outboundReconnect;
        int slot;
        Keyboard keyboard;
        CastleGameplay castle;
        GameStateManager game;
        MultiplayerSessionManager sessions;
        NetworkManager manager;
        PlayerMovement player;
        GameplayCharacter character;
        int[] sewerAnswer = new int[12];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            int mode = Array.IndexOf(args, "-castleReplay"), path = Array.IndexOf(args, "-castleEvidence");
            if (mode < 0 || mode + 1 >= args.Length || path < 0 || path + 1 >= args.Length) return;
            var replay = new GameObject(nameof(CastleRelayReplay)).AddComponent<CastleRelayReplay>();
            replay.role = args[mode + 1];
            replay.host = replay.role == "host";
            replay.slot = replay.host ? 0 : 1;
            replay.output = args[path + 1];
            replay.screenshots = args.Contains("-castleScreenshots");
            replay.outboundReconnect = args.Contains("-castleOutboundReconnect");
            Directory.CreateDirectory(replay.output);
        }

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Application.logMessageReceived += WatchLogs;
            InputSystem.settings = Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            manager = FindFirstObjectByType<NetworkManager>();
            game = FindFirstObjectByType<GameStateManager>();
            castle = game.GetComponent<CastleGameplay>();
            sessions = FindFirstObjectByType<MultiplayerSessionManager>();
            yield return Wait(() => castle.IsReady && FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).Length >= 4,
                "castle and V3 markers loaded", 90);
            string roomFile = Path.Combine(output, "room.txt");
            if (host)
            {
                var create = sessions.CreateRoomAsync();
                yield return Wait(() => create.IsCompleted && sessions.HasSession, "Relay room created", 90);
                room = sessions.CurrentJoinCode;
                File.WriteAllText(roomFile + ".tmp", room);
                File.Move(roomFile + ".tmp", roomFile);
            }
            else
            {
                yield return Wait(() => File.Exists(roomFile), "Relay room code available", 100);
                room = File.ReadAllText(roomFile);
                yield return Join();
            }
            yield return Wait(() => game.IsGameReady, "two Relay players", 90);
            if (host) game.StartFoundation();
            yield return Wait(() => game.Phase == FoundationPhase.Outbound, "foundation outbound");
            yield return BindPlayer();
            var position = V3GameplayBootstrap.SafeGround(castle.World(host ? -1 : 2, 0, -19));
            player.PlaceAtOwner(position, Quaternion.identity);
            Debug.Log("CASTLE SETUP placed outside the curtain gate");
            yield return new WaitForSeconds(.5f);
            yield return Walk(1.2f, 45);
            Capture("gate-entry");
            var initial = castle.RoomPlan(0, slot);
            yield return Walk(initial.center.x, 56);
            yield return Walk(initial.center.x, initial.yMin + 3);

            for (int stage = 0; stage < 4; stage++)
            {
                Rect plan = castle.RoomPlan(stage, slot);
                float exit = castle.RoomPlan(Mathf.Min(stage + 1, 3), slot).center.x;
                if (stage == 0)
                {
                    var foreign = Station(CastleGameplay.Action.Pad, 1 - slot, stage, 0);
                    var before = castle.Progress(slot);
                    castle.UseRpc(foreign.Id, stage, before.Revision);
                    castle.UseRpc(-1, stage, before.Revision);
                    yield return new WaitForSeconds(.3f);
                    Require(castle.Progress(slot).Equals(before), "foreign and invalid station requests rejected");
                    yield return Walk(exit, plan.yMax - 2);
                    SetKeys(Key.W);
                    yield return new WaitForSeconds(1);
                    SetKeys();
                    Require(player.transform.position.z < castle.World(0, 0, plan.yMax).z - .35f, "locked stage door blocks CharacterController");
                }

                int code;
                if (slot == stage % 2)
                {
                    yield return Use(Station(CastleGameplay.Action.ReadHint, slot, stage));
                    yield return Wait(() => castle.LocalHint(stage) != 0, $"private hint for stage {stage + 1}");
                    code = castle.LocalHint(stage);
                    if (stage == 0)
                    {
                        yield return Use(Station(CastleGameplay.Action.Signal, slot, stage));
                        yield return Wait(() => CastleGameplay.SignalLength(castle.Signal) == 1, "partial wall message");
                        yield return Use(Station(CastleGameplay.Action.ClearSignal, slot, stage));
                        yield return Wait(() => castle.Signal == 0, "partial signal cancelled");
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        int glyph = ((code >> (i * 2)) & 3) - 1;
                        int count = i + 1;
                        yield return Use(Station(CastleGameplay.Action.Signal, slot, stage, glyph));
                        yield return Wait(() => CastleGameplay.SignalLength(castle.Signal) == count, $"message stone {count} at stage {stage + 1}");
                    }
                }
                else
                {
                    yield return Walk(slot == 0 ? 4 : -4, plan.center.y);
                    Require(castle.LocalHint(stage) == 0, $"no private hint for stage {stage + 1}");
                    yield return Wait(() => CastleGameplay.SignalLength(castle.Signal) == 3, $"partner wall message at stage {stage + 1}", 30);
                    code = castle.Signal;
                }
                Capture($"stage-{stage + 1}-signal");

                if (stage == 0)
                {
                    var wrong = Station(CastleGameplay.Action.Pad, slot, stage, (code & 3) % 3);
                    yield return Use(wrong);
                    yield return Wait(() => castle.Progress(slot).Feedback == -1 && castle.Progress(slot).Steps == 0,
                        "wrong symbol resets only this player's attempt");
                }

                for (int i = 0; i < 3; i++)
                {
                    int glyph = ((code >> (i * 2)) & 3) - 1;
                    sewerAnswer[stage * 3 + i] = glyph;
                    var pad = Station(CastleGameplay.Action.Pad, slot, stage, glyph);
                    int oldRevision = castle.Progress(slot).Revision;
                    yield return Use(pad);
                    yield return Wait(() => castle.Stage > stage || castle.Progress(slot).Revision > oldRevision,
                        $"accepted symbol {i + 1} of stage {stage + 1}");
                    if (i == 0 && stage == 0)
                    {
                        int revision = castle.Progress(slot).Revision;
                        castle.UseRpc(pad.Id, stage, oldRevision);
                        yield return new WaitForSeconds(.25f);
                        Require(castle.Progress(slot).Revision == revision, "stale repeated input rejected");
                    }
                }
                yield return Wait(() => castle.Stage > stage, $"both solved stage {stage + 1}", 35);
                yield return Walk(exit, plan.yMax - 2);
                yield return Walk(exit, plan.yMax + 3);
                Require(castle.AllEarlierDoorsOpen(), "previous stage gate Colliders stay open");
                if (outboundReconnect && stage == 0)
                {
                    yield return CheckOutboundRecovery();
                    yield break;
                }
            }

            Require(!castle.CanUse(Station(CastleGameplay.Action.SewerPad, -1, 4), slot),
                "server keeps sewer input disabled before return");
            yield return Use(Station(CastleGameplay.Action.Collect, slot, 4));
            yield return Wait(() => castle.LocalNumber >= 0, "private four-digit number");
            yield return Use(Station(CastleGameplay.Action.Keypad, slot, 4));
            yield return Wait(() => castle.KeypadForLocal == slot, "local keypad opened");
            int old = castle.Progress(slot).Revision;
            castle.SubmitNumberRpc((castle.LocalNumber + 1) % 10000, old);
            yield return Wait(() => castle.Progress(slot).Revision != old && castle.Progress(slot).Feedback == -1,
                "incorrect four-digit number rejected");
            castle.SubmitNumberRpc(castle.LocalNumber, castle.Progress(slot).Revision);
            yield return Wait(() => castle.DoorOpen, "both four-digit codes open basement", 30);
            yield return Walk(host ? 1.7f : -1.7f, 242.6f);
            if (!host)
            {
                yield return Wait(() => castle.Crossed == 1, "host crossed while client remains door-front");
                Require(!castle.Returning, "door-front proximity does not convert corridor");
            }
            yield return Walk(host ? 1.7f : -1.7f, 248.5f);
            yield return Wait(() => castle.Returning && castle.Crossed == 3 && game.Phase == FoundationPhase.Return,
                "both physically crossed and converted the corridor");
            Require(castle.PartitionsDisabled, "saved removable Collider group disabled");
            Capture("return-corridor");
            CaptureView("return-layout", castle.World(5, 5.5f, 232), castle.World(2, 1, 181));

            if (host) yield return RunTraps();
            else yield return RunSewer();
            yield return Walk(1.2f, 39);
            yield return Walk(host ? -1.7f : 2.6f, -19);
            Capture("gate-exit");
            File.WriteAllText(Path.Combine(output, role + "-pass.txt"), "PASS: Relay, castle stage inputs, physical gate and basement traversal, castle exit");
            yield return Wait(() => File.Exists(Path.Combine(output, (host ? "client" : "host") + "-pass.txt")),
                "both routes completed", 190);
            yield return new WaitForSeconds(1.2f);
            Application.Quit();
        }

        IEnumerator CheckOutboundRecovery()
        {
            string done = Path.Combine(output, "client-rejoin.txt");
            if (host)
            {
                var respawns = game.GetComponent<RespawnManager>();
                respawns.RespawnPlayer(player);
                yield return Wait(() => player != null && Vector3.Distance(player.transform.position, castle.OutboundSpawn(slot)) < 1.5f,
                    "castle outbound death uses upper dock");
                Require(castle.Stage == 1 && castle.AllEarlierDoorsOpen(), "outbound death preserves doors");
                yield return Wait(() => File.Exists(done), "outbound reconnect completed", 100);
            }
            else
            {
                yield return Use(Station(CastleGameplay.Action.Signal, slot, 1));
                yield return Wait(() => CastleGameplay.SignalLength(castle.Signal) == 1, "message in progress before disconnect");
                int message = castle.Signal;
                var leave = sessions.LeaveRoomAsync();
                yield return Wait(() => leave.IsCompleted && !manager.IsListening, "outbound client disconnected", 60);
                yield return new WaitForSeconds(1);
                yield return Join();
                yield return BindPlayer();
                yield return Wait(() => player != null && Vector3.Distance(player.transform.position, castle.OutboundSpawn(slot)) < 1.5f,
                    "outbound reconnect resumes at upper dock");
                Require(castle.Stage == 1 && castle.AllEarlierDoorsOpen() && castle.Signal == message,
                    "outbound reconnect restores doors, stage and partial wall message");
                File.WriteAllText(done, "PASS");
            }
            File.WriteAllText(Path.Combine(output, role + "-pass.txt"), "PASS: outbound death or reconnect and persistence");
            yield return new WaitForSeconds(2);
            Application.Quit();
        }

        IEnumerator Join()
        {
            var join = sessions.JoinRoomAsync(room);
            yield return Wait(() => join.IsCompleted && sessions.HasSession, "Relay room joined", 90);
        }

        IEnumerator BindPlayer()
        {
            yield return Wait(() => manager.IsConnectedClient && manager.LocalClient?.PlayerObject is { IsSpawned: true } &&
                castle.Slot(manager.LocalClientId) == slot,
                "server allocated path " + (slot == 0 ? "A" : "B"));
            player = manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
            character = player.GetComponent<GameplayCharacter>();
            player.GetComponent<GameplayInput>().SimulateCapturedCursor = true;
        }

        IEnumerator RunTraps()
        {
            (float x, float z) = castle.TrapPoint(4);
            yield return Walk(1.4f, 234);
            yield return Walk(x, z);
            yield return Wait(() => castle.Progress(slot).Injured, "first physical trap contact");
            int deaths = character.RespawnCount.Value;
            SetKeys(Key.W, Key.LeftShift);
            yield return new WaitForSeconds(.28f);
            float speed = Vector3.ProjectOnPlane(player.Velocity, Vector3.up).magnitude;
            SetKeys();
            Require(castle.SprintBlocked(player) && castle.SpeedScale(player) < 1 &&
                speed > 1.2f && speed <= 3.5f * castle.SpeedScale(player) + .08f, $"slowed movement while Shift held ({speed:F2} m/s)");
            File.WriteAllText(Path.Combine(output, "host-trap-speed.txt"), "PASS");
            CaptureView("rolling-rock", castle.World(x + 4, 2.7f, z + 7), castle.World(x, 1.3f, z));
            yield return Walk(x, z);
            Require(character.RespawnCount.Value == deaths, "one attack is not double-counted");
            yield return Wait(() => character.RespawnCount.Value > deaths, "second physical contact respawns", 30);
            yield return Wait(() => Vector3.Distance(player.transform.position, castle.World(2.3f, .15f, 249)) < 1,
                "return respawn at basement");
            Require(!castle.Progress(slot).Injured && castle.DoorOpen, "death clears injury and preserves opened gate");
            yield return Walk(2.3f, 233);
            Capture("corridor-traversal");
            for (int i = 4; i >= 0; i--)
            {
                (x, z) = castle.TrapPoint(i);
                yield return Walk(x + 7.1f, z + 5);
                yield return Wait(() => castle.SafeToPassTrap(i), $"trap {i + 1} has a recovery window");
                yield return Walk(x + 7.1f, z - 4.5f, sprint: true);
            }
            yield return Walk(8, 46);
            Require(character.RespawnCount.Value == deaths + 1, "main trap corridor crossed on foot");
        }

        IEnumerator RunSewer()
        {
            var (_, trapZ) = castle.TrapPoint(4);
            yield return Walk(-1.8f, 234);
            yield return Walk(-2.3f, trapZ);
            yield return Wait(() => castle.Progress(slot).Injured, "client made physical trap contact", 30);
            yield return Walk(-2.3f, trapZ + 4.8f);
            yield return Wait(() => File.Exists(Path.Combine(output, "host-trap-speed.txt")), "host measured injured movement");
            yield return Reconnect("after physical trap contact");
            Require(castle.Returning && castle.DoorOpen && castle.Stage == 4 && castle.Progress(slot).Injured,
                "rejoin restores gates, phase, path and physical trap injury");
            yield return GoToSewer();
            int deaths = character.RespawnCount.Value;
            var wrong = new int[12];
            yield return EnterSewer(wrong);
            yield return Wait(() => character.RespawnCount.Value == deaths + 1 && !castle.Progress(slot).Injured,
                "trap and sewer contacts accumulate together");
            yield return GoToSewer();
            yield return Wait(() => castle.Now >= castle.Progress(slot).RetryAt, "retry after mixed damage");
            yield return EnterSewer(wrong);
            yield return Wait(() => castle.Progress(slot).Injured && castle.Progress(slot).RetryAt > castle.Now,
                "wrong sewer sequence causes injury and cooldown");
            var blocked = castle.Progress(slot).Revision;
            castle.UseRpc(Station(CastleGameplay.Action.SewerPad, -1, 4).Id, 4, blocked);
            yield return new WaitForSeconds(.3f);
            Require(castle.Progress(slot).Revision == blocked, "cooldown request ignored");
            yield return Wait(() => castle.Now >= castle.Progress(slot).RetryAt, "retry delay elapsed");
            yield return EnterSewer(wrong);
            yield return Wait(() => character.RespawnCount.Value == deaths + 2, "second wrong sequence respawns");
            yield return GoToSewer();
            yield return Wait(() => castle.Now >= castle.Progress(slot).RetryAt, "sewer retry cooldown elapsed");
            yield return EnterSewer(sewerAnswer);
            yield return Wait(() => castle.SewerOpen, "all twelve outbound glyphs unlock sewer");
            yield return Reconnect("after opening sewer");
            Require(castle.SewerOpen && castle.Returning, "opened sewer restored after reconnect");
            yield return GoToSewer();
            deaths = character.RespawnCount.Value;
            Capture("sewer-entry");
            CaptureView("sewer-ramp", castle.World(-32, 1.35f, 240), castle.World(-32, 6.6f, 218));
            yield return Walk(-32, 242);
            yield return Walk(-32, 46, sprint: true);
            yield return Walk(-32, 41);
            Require(player.IsGrounded, "sewer descends to walkable entrance hall");
            Require(character.RespawnCount.Value == deaths, "sewer traversed on foot with no trap hits");
            Capture("sewer-exit");
        }

        IEnumerator GoToSewer()
        {
            yield return Walk(-1.8f, 241);
            yield return Walk(-12, 241);
        }

        IEnumerator Reconnect(string label)
        {
            var leave = sessions.LeaveRoomAsync();
            yield return Wait(() => leave.IsCompleted && !manager.IsListening, "client disconnect " + label, 60);
            yield return new WaitForSeconds(1);
            yield return Join();
            yield return BindPlayer();
            yield return Wait(() => Vector3.Distance(player.transform.position, castle.World(-2.3f, .15f, 249)) < 1,
                "return reconnect resumes near the basement door");
        }

        IEnumerator EnterSewer(int[] glyphs)
        {
            for (int i = 0; i < glyphs.Length; i++)
            {
                var pad = Station(CastleGameplay.Action.SewerPad, -1, 4, glyphs[i]);
                int revision = castle.Progress(slot).Revision;
                yield return Use(pad);
                yield return Wait(() => castle.Progress(slot).Revision != revision, $"sewer input {i + 1}/12");
            }
        }

        CastleStation Station(CastleGameplay.Action kind, int path, int stage, int value = 0) =>
            castle.stations.Single(s => s.Kind == kind && s.Slot == path && s.Stage == stage && s.Value == value);

        IEnumerator Use(CastleStation station)
        {
            Vector3 p = station.transform.position - castle.World(0, 0, 0);
            yield return Walk(p.x, p.z);
            yield return Wait(() => player.GetComponent<PlayerInteraction>().CurrentTarget is CastleStation candidate && candidate == station,
                $"E target {station.Kind} {station.Value}", 4);
            SetKeys(Key.E);
            yield return new WaitForSeconds(.13f);
            SetKeys();
            yield return new WaitForSeconds(.09f);
        }

        IEnumerator Walk(float x, float z, bool sprint = false)
        {
            Vector3 destination = castle.World(x, 0, z);
            float end = Time.realtimeSinceStartup + 22 + Vector2.Distance(new Vector2(destination.x, destination.z),
                new Vector2(player.transform.position.x, player.transform.position.z)) / 2;
            while (true)
            {
                Vector3 delta = destination - player.transform.position;
                delta.y = 0;
                if (delta.magnitude <= .32f) break;
                Require(Time.realtimeSinceStartup < end, $"walk timeout at {player.transform.position - castle.World(0, 0, 0):F2}, target ({x:F2}, {z:F2})", log: false);
                var camera = Camera.main.transform;
                float forward = Vector3.Dot(delta, Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized);
                float right = Vector3.Dot(delta, Vector3.ProjectOnPlane(camera.right, Vector3.up).normalized);
                float threshold = Mathf.Min(.21f, Mathf.Max(Mathf.Abs(forward), Mathf.Abs(right)) * .45f);
                var keys = new System.Collections.Generic.List<Key>();
                if (Mathf.Abs(forward) > threshold) keys.Add(forward > 0 ? Key.W : Key.S);
                if (Mathf.Abs(right) > threshold) keys.Add(right > 0 ? Key.D : Key.A);
                if (sprint && delta.magnitude > 2) keys.Add(Key.LeftShift);
                SetKeys(keys.ToArray());
                yield return null;
            }
            SetKeys();
            yield return new WaitForSeconds(.08f);
        }

        void Capture(string label)
        {
            if (screenshots) ScreenCapture.CaptureScreenshot(Path.Combine(output, role + "-" + label + ".png"));
        }

        void CaptureView(string label, Vector3 position, Vector3 lookAt)
        {
            if (!screenshots) return;
            Camera camera = Camera.main;
            var savedPosition = camera.transform.position;
            var savedRotation = camera.transform.rotation;
            var previous = RenderTexture.active;
            var render = RenderTexture.GetTemporary(960, 540, 24);
            var texture = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
            try
            {
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = render });
                RenderTexture.active = render;
                texture.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
                File.WriteAllBytes(Path.Combine(output, role + "-" + label + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(render);
                Destroy(texture);
            }
        }

        void SetKeys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        IEnumerator Wait(Func<bool> predicate, string check, float seconds = 20)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < end) yield return null;
            Require(predicate(), check);
        }

        void Require(bool passed, string check, bool log = true)
        {
            if (!passed)
            {
                Fail(check);
                throw new InvalidOperationException(check);
            }
            if (log) Debug.Log("CASTLE PASS " + check);
        }

        void WatchLogs(string message, string stack, LogType type)
        {
            if (type is LogType.Exception or LogType.Assert) Fail(message + "\n" + stack);
        }

        void Fail(string reason)
        {
            if (failed) return;
            failed = true;
            Debug.LogError("CASTLE FAIL " + reason);
            File.WriteAllText(Path.Combine(output, role + "-fail.txt"), reason);
            Application.Quit(1);
        }

        void OnDestroy() => Application.logMessageReceived -= WatchLogs;
    }

    public sealed partial class CastleGameplay
    {
        internal Vector3 OutboundSpawn(int slot) => DockPose(slot).position;
        internal int LocalHint(int index) => hintStage == index ? hintCode : 0;
        internal int LocalNumber => localNumber;
        internal int KeypadForLocal => pendingKeypad;
        internal bool PartitionsDisabled => !partitions.gameObject.activeInHierarchy;
        internal Rect RoomPlan(int index, int slot) => config.rooms.Single(room => room.id == $"{Lane(slot)}{index + 1}").plan;
        internal bool AllEarlierDoorsOpen() => unlockedGroups.Take(Stage).All(gate => !gate.activeInHierarchy &&
            gate.GetComponentsInChildren<Collider>(true).All(collider => !collider.gameObject.activeInHierarchy));
        internal (float x, float z) TrapPoint(int index) => (traps[index].X, traps[index].Z);
        internal bool SafeToPassTrap(int index)
        {
            double elapsed = Now - activations[index];
            return activations[index] >= 0 && elapsed >= trapWarning + trapAttack && elapsed < TrapCycle - 1.7;
        }
    }
}
#endif
