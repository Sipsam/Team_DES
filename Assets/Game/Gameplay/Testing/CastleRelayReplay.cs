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
    public sealed partial class CastleRelayReplay : MonoBehaviour
    {
        string output, role, room;
        bool host, failed, screenshots, outboundReconnect;
        int slot;
        FullMatchRelayReplay fullMatch;
        Keyboard keyboard;
        Gamepad movementPad;
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
            if (fullMatch != null) yield break;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Application.logMessageReceived += WatchLogs;
            InputSystem.settings = Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            movementPad = InputSystem.AddDevice<Gamepad>();
            manager = FindFirstObjectByType<NetworkManager>();
            if (host) manager.OnClientDisconnectCallback += RecordPeerDisconnect;
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
            yield return Wait(() => game.Phase == FoundationPhase.Outbound, "foundation outbound");
            yield return BindPlayer();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-castleCavernProbe") >= 0)
            {
                yield return RunCavernProbe();
                yield break;
            }
            var position = V3GameplayBootstrap.SafeGround(castle.World(host ? -1 : 2, 0, -19));
            player.PlaceAtOwner(position, Quaternion.identity);
            Debug.Log("CASTLE SETUP placed outside the curtain gate");
            yield return new WaitForSeconds(.5f);
            yield return ReplayCourse();
            if (outboundReconnect) yield break;
            File.WriteAllText(Path.Combine(output, role + "-pass.txt"), "PASS: Relay, castle stage inputs, physical gate, underground seal and gem, castle exit");
            yield return Wait(() => File.Exists(Path.Combine(output, (host ? "client" : "host") + "-pass.txt")),
                "both routes completed", 190);
            yield return new WaitForSeconds(1.2f);
            Application.Quit();
        }

        internal IEnumerator ReplayFromGate(FullMatchRelayReplay match, bool walkOnly = false)
        {
            fullMatch = match;
            host = match.Host;
            role = match.Role;
            output = match.Output;
            manager = match.Manager;
            game = match.Game;
            keyboard = match.Keyboard;
            castle = CastleGameplay.Current;
            slot = host ? 0 : 1;
            yield return BindPlayer();
            yield return ReplayCourse(walkOnly);
        }

        IEnumerator ReplayCourse(bool walkOnly = false)
        {
            yield return TraverseGate(false);
            Capture("gate-entry");
            var initial = castle.RoomPlan(0, slot);
            yield return Walk(initial.center.x, host ? 48 : 52);
            yield return Walk(initial.center.x, initial.yMin - 3);
            yield return Walk(initial.center.x, initial.yMin + 3);
            Require(player.IsGrounded, "grounded beyond the first puzzle arch");

            for (int stage = 0; stage < 4; stage++)
            {
                Rect plan = castle.RoomPlan(stage, slot);
                float exit = castle.RoomPlan(Mathf.Min(stage + 1, 3), slot).center.x;
                if (stage == 0 && fullMatch == null)
                {
                    var foreign = Station(CastleGameplay.Action.Pad, 1 - slot, stage, 0);
                    var before = castle.Progress(slot);
                    castle.UseRpc(foreign.Id, stage, before.Revision);
                    castle.UseRpc(-1, stage, before.Revision);
                    yield return new WaitForSeconds(.3f);
                    Require(castle.Progress(slot).Equals(before), "foreign and invalid station requests rejected");
                    yield return Walk(exit, plan.yMax - 2);
                    SteerWorld(Vector3.forward);
                    yield return new WaitForSeconds(1);
                    SteerWorld(Vector3.zero);
                    Require(player.transform.position.z < castle.World(0, 0, plan.yMax).z - .35f, "locked stage door blocks CharacterController");
                }

                int code;
                if (slot == stage % 2)
                {
                    yield return UseAndConfirm(Station(CastleGameplay.Action.ReadHint, slot, stage),
                        () => castle.LocalHint(stage) != 0, $"private hint for stage {stage + 1}");
                    code = castle.LocalHint(stage);
                    if (stage == 0 && fullMatch == null)
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
                        yield return UseAndConfirm(Station(CastleGameplay.Action.Signal, slot, stage, glyph),
                            () => CastleGameplay.SignalLength(castle.Signal) == count, $"message stone {count} at stage {stage + 1}");
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
                if (stage == 0) fullMatch?.Capture("castle-wall-signal");

                if (stage < 2 && fullMatch == null)
                {
                    var room = Station(CastleGameplay.Action.Pad, slot, stage).transform.parent;
                    var labels = room.GetComponentsInChildren<TextMesh>();
                    string guide = slot == stage % 2 ? "WALL TRANSMITTER" : "WALL RECEIVER";
                    Require(labels.Any(label => label.text.StartsWith(guide)) &&
                        labels.Any(label => label.text == "Solve both sides to record") && castle.SolvedCode(stage) == 0,
                        "wall guidance is present without revealing the unsolved sequence");
                    yield return CaptureHeading($"wall-guidance-{stage + 1}", slot == 0 ? 270 : 90);
                }
                if (stage == 0 && fullMatch == null)
                {
                    var first = Station(CastleGameplay.Action.Pad, slot, stage, (code & 3) - 1);
                    yield return UseAndConfirm(first, () => castle.Progress(slot).Steps == 1, "first pad accepted before reset");
                    yield return Rendezvous("castle-first-pads");
                    yield return Wait(() => Enumerable.Range(0, 2).All(lane =>
                        PadIsPressed(Station(CastleGameplay.Action.Pad, lane, stage, first.Value))),
                        "both peers' accepted cap positions are rendered");
                    yield return CaptureDoorPad("door-pad-pressed", first);
                    yield return Rendezvous("castle-first-pads-seen");
                    // Enter the same glyph again: every sequence uses each glyph once.
                    yield return Use(first);
                    yield return Wait(() => castle.Progress(slot).Feedback == -1 && castle.Progress(slot).Steps == 0,
                        "wrong symbol resets only this player's attempt");
                    yield return Wait(() => !PadIsPressed(first), "wrong input raises the previously accepted cap");
                    yield return CaptureDoorPad("door-pad-reset", first);
                }

                for (int i = 0; i < 3; i++)
                {
                    int glyph = ((code >> (i * 2)) & 3) - 1;
                    sewerAnswer[stage * 3 + i] = glyph;
                    var pad = Station(CastleGameplay.Action.Pad, slot, stage, glyph);
                    int oldRevision = castle.Progress(slot).Revision;
                    yield return UseAndConfirm(pad, () => castle.Stage > stage || castle.Progress(slot).Revision > oldRevision,
                        $"accepted symbol {i + 1} of stage {stage + 1}");
                    if (i == 0 && stage == 0 && fullMatch == null)
                    {
                        yield return Wait(() => PadIsPressed(pad), "server-approved glyph lowers the rendered cap");
                        int revision = castle.Progress(slot).Revision;
                        castle.UseRpc(pad.Id, stage, oldRevision);
                        yield return new WaitForSeconds(.25f);
                        Require(castle.Progress(slot).Revision == revision, "stale repeated input rejected");
                    }
                }
                yield return Wait(() => castle.Stage > stage, $"both solved stage {stage + 1}", 35);
                if (fullMatch == null)
                {
                    var room = Station(CastleGameplay.Action.Pad, slot, stage).transform.parent;
                    string recorded = string.Join("  ", sewerAnswer.Skip(stage * 3).Take(3).Select(glyph => CastleGameplay.Glyphs[glyph]));
                    yield return Wait(() => castle.SolvedCode(stage) == code &&
                        room.Find("Sewer record").GetComponentsInChildren<TextMesh>().Any(label => label.text == recorded) &&
                        Enumerable.Range(0, 3).All(glyph => !PadIsPressed(Station(CastleGameplay.Action.Pad, slot, stage, glyph))),
                        "stage advance raises all caps and records the actual sewer glyphs");
                    if (stage == 0 && screenshots)
                    {
                        yield return Walk(slot == 0 ? 4 : -4, plan.yMax - 3.1f);
                        yield return CaptureHeading("sewer-record", slot == 0 ? 270 : 90);
                    }
                }
                yield return Walk(exit, plan.yMax - 2);
                yield return Walk(exit, plan.yMax + 3);
                Require(player.IsGrounded, $"grounded beyond puzzle door {stage + 1}");
                Require(castle.AllEarlierDoorsOpen(), "previous stage gate Colliders stay open");
                if (outboundReconnect && stage == 0)
                {
                    yield return CheckOutboundRecovery();
                    yield break;
                }
            }

            Require(!castle.CanUse(Station(CastleGameplay.Action.SewerPad, -1, 4), slot),
                "server keeps sewer input disabled before return");
            yield return UseAndConfirm(Station(CastleGameplay.Action.Collect, slot, 4),
                () => castle.LocalNumber >= 0, "private four-digit number");
            yield return UseAndConfirm(Station(CastleGameplay.Action.Keypad, slot, 4),
                () => castle.KeypadForLocal == slot, "local keypad opened");
            if (fullMatch == null)
            {
                int old = castle.Progress(slot).Revision;
                castle.SubmitNumberRpc((castle.LocalNumber + 1) % 10000, old);
                yield return Wait(() => castle.Progress(slot).Revision != old && castle.Progress(slot).Feedback == -1,
                    "incorrect four-digit number rejected");
            }
            castle.SubmitNumberRpc(castle.LocalNumber, castle.Progress(slot).Revision);
            yield return Wait(() => castle.DoorOpen, "both four-digit codes open basement", 30);
            yield return Walk(host ? 1.7f : -1.7f, 242.6f);
            if (fullMatch == null)
                yield return CheckBasementRecovery(FoundationPhase.Outbound, 242.75f);
            if (!host)
            {
                yield return Wait(() => castle.Crossed == 1, "host crossed while client remains door-front");
                Require(!castle.Returning, "door-front proximity does not convert corridor");
            }
            yield return Walk(host ? 1.7f : -1.7f, 248.5f);
            yield return Wait(() => castle.PartitionsOpen && castle.Crossed == 3, "both physically crossed the basement door");
            Require(game.Phase == FoundationPhase.Outbound && !castle.Returning && castle.PartitionsDisabled,
                "basement passage opens partitions and keeps outbound rules");
            Require(player.IsGrounded, "grounded beyond the basement door");
            if (fullMatch != null) yield return fullMatch.Barrier("basement-door");
            if (walkOnly) yield break;
            var underground = UndergroundGameplay.Current;
            yield return TraverseUnderground(underground);
            yield return Wait(() => castle.Returning && game.Phase == FoundationPhase.Return, "gem pickup starts the castle return traps");
            Capture("return-corridor");
            CaptureView("return-layout", castle.World(5, 5.5f, 232), castle.World(2, 1, 181));

            if (fullMatch != null)
            {
                // Keep the approach clear until the player entering the glyphs leaves the keypad.
                if (!host) yield return Walk(-2.3f, 239);
                yield return fullMatch.Barrier("sewer-approach-clear");
                if (host)
                {
                    yield return GoToSewer();
                    yield return EnterSewer(sewerAnswer);
                    yield return Walk(-32, 242);
                }
                yield return Wait(() => castle.SewerOpen, "twelve-glyph sewer opened", 40);
                yield return fullMatch.Barrier("sewer-keypad-clear");
                if (!host)
                {
                    yield return GoToSewer();
                    yield return Walk(-32, 242);
                }
                yield return Walk(-32, 46, sprint: true);
                yield return Walk(-32, 41);
            }
            else if (host)
            {
                yield return ObserveSewerPress(sewerAnswer[0], 1);
                yield return Wait(() => castle.SewerOpen && castle.Progress(1).SewerLength == sewerAnswer.Length,
                    "both peers see the twelve-glyph bypass open", 100);
                yield return Rendezvous("sewer-open");
                yield return RunTraps();
            }
            else yield return RunSewer();
            yield return TraverseGate(true);
            Capture("gate-exit");
        }

        IEnumerator CheckBasementRecovery(FoundationPhase phase, float spawnZ)
        {
            string label = $"GameplayCheckpoint_{phase} / Basement entrance";
            var players = FindObjectsByType<GameplayCharacter>(FindObjectsSortMode.None);
            yield return Wait(() => players.Length == 2 && players.All(c => c.CheckpointName.Value.ToString() == label),
                "Basement checkpoint replicated for both players in " + phase);
            var lives = players.Select(c => c.RespawnCount.Value).ToArray();
            var gem = GemOwnership.Current;
            bool carryingGem = gem.State == GemState.Carried;
            yield return Rendezvous("basement-" + phase + "-armed");
            if (host)
            {
                var respawns = game.GetComponent<RespawnManager>();
                foreach (var peer in players)
                    respawns.RespawnPlayer(peer.GetComponent<PlayerMovement>());
            }
            yield return Wait(() => players.Select((c, i) =>
            {
                bool serverPlayer = c.OwnerClientId == NetworkManager.ServerClientId;
                float side = serverPlayer ? -1.3f : 1.3f;
                Vector3 expected = castle.World(phase == FoundationPhase.Outbound ? side : -side, 0, spawnZ);
                return c.RespawnCount.Value > lives[i] && Mathf.Abs(c.transform.position.x - expected.x) < .25f &&
                    Mathf.Abs(c.transform.position.z - expected.z) < .4f;
            }).All(recovered => recovered),
                "Both basement respawns replicated in " + phase);
            Require(Vector3.Distance(players[0].transform.position, players[1].transform.position) > 2 &&
                game.Phase == phase && castle.DoorOpen && castle.Stage == 4, "Separate spawns preserve phase and castle progress");
            if (phase == FoundationPhase.Return)
            {
                Require(castle.PartitionsOpen && UndergroundGameplay.Current.Seal == UndergroundSeal.Unsealed,
                    "Return recovery preserves partitions and unsealed gem");
                if (carryingGem)
                    yield return Wait(() => gem.State == GemState.Relocated && gem.Owner == ulong.MaxValue,
                        "Carrier death uses the existing gem relocation");
            }
            yield return Rendezvous("basement-" + phase + "-recovered");
        }

        IEnumerator TraverseGate(bool returning)
        {
            // The curtain gate is at local Z=-8.8 and the keep portal at Z=35.
            // Keep the same lane across both openings and turn only in the clear halls.
            float x = host ? -1.2f : 2.1f;
            float[] points = { -19, -14, -4, 30, 41 };
            if (returning) Array.Reverse(points);
            foreach (float z in points)
            {
                yield return Walk(x, z);
                Require(player.IsGrounded, $"grounded at gate waypoint ({x:F1}, {z:F1}), feet={player.transform.position:F2}");
                if (!returning && z == 30 && fullMatch == null) Capture("courtyard-floor");
            }
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
                yield return LeaveAndWaitForHost("outbound");
                yield return Join();
                yield return BindPlayer();
                yield return Wait(() => player != null && Vector3.Distance(player.transform.position, castle.OutboundSpawn(slot)) < 1.5f,
                    "outbound reconnect resumes at upper dock");
                Require(castle.Stage == 1 && castle.AllEarlierDoorsOpen() && castle.Signal == message,
                    "outbound reconnect restores doors, stage and partial wall message");
                Require(castle.SolvedCode(0) != 0 && castle.Progress(slot).PressedGlyphs == 0,
                    "outbound reconnect restores the solved record and raised pads");
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
            SetKeys(Key.LeftShift);
            SteerWorld(Vector3.forward);
            yield return new WaitForSeconds(.28f);
            float speed = Vector3.ProjectOnPlane(player.Velocity, Vector3.up).magnitude;
            SetKeys();
            SteerWorld(Vector3.zero);
            Require(castle.SprintBlocked(player) && castle.SpeedScale(player) < 1 &&
                speed > 1.2f && speed <= 3.5f * castle.SpeedScale(player) + .08f, $"slowed movement while Shift held ({speed:F2} m/s)");
            CaptureView("rolling-rock", castle.World(x + 4, 2.7f, z + 7), castle.World(x, 1.3f, z));
            yield return Walk(x, z);
            Require(character.RespawnCount.Value == deaths, "one attack is not double-counted");
            yield return Wait(() => character.RespawnCount.Value > deaths, "second physical contact respawns", 30);
            yield return Wait(() => Vector3.Distance(player.transform.position, castle.World(1.3f, .15f, 246.8f)) < 1,
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
            yield return GoToSewer();
            int deaths = character.RespawnCount.Value;
            CaptureSewerPad("sewer-pad-idle", sewerAnswer[0]);

            for (int i = 0; i < sewerAnswer.Length; i++)
            {
                int glyph = sewerAnswer[i];
                if (i is 1 or 11)
                {
                    int wrongGlyph = (glyph + 1) % CastleGameplay.Glyphs.Length;
                    if (wrongGlyph == sewerAnswer[i - 1]) wrongGlyph = (glyph + 2) % CastleGameplay.Glyphs.Length;
                    var wrong = Station(CastleGameplay.Action.SewerPad, -1, 4, wrongGlyph);
                    var before = castle.Progress(slot);
                    yield return Use(wrong);
                    Require(castle.Progress(slot).Equals(before) && character.RespawnCount.Value == deaths &&
                        castle.SpeedScale(player) == 1 && !castle.SprintBlocked(player) && !castle.SewerOpen &&
                        castle.CanUse(Station(CastleGameplay.Action.SewerPad, -1, 4, glyph), slot),
                        $"wrong sewer glyph preserves {i} correct inputs and allows the next input immediately");
                    Require(!SewerPadIsPressed(wrong), "wrong glyph does not show the accepted press");
                }
                var pad = Station(CastleGameplay.Action.SewerPad, -1, 4, glyph);
                yield return UseAndConfirm(pad, () => castle.Progress(slot).SewerLength == i + 1, $"sewer input {i + 1}/12");
                Require(castle.SewerOpen == (i == sewerAnswer.Length - 1), "only the complete sequence opens the bypass");
                if (i == 0) yield return ObserveSewerPress(glyph, slot);
            }
            yield return Rendezvous("sewer-open");
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

        IEnumerator ObserveSewerPress(int glyph, int inputSlot)
        {
            var pad = Station(CastleGameplay.Action.SewerPad, -1, 4, glyph);
            yield return Wait(() => castle.Progress(inputSlot).SewerLength == 1 && SewerPadIsPressed(pad),
                "approved sewer input lowers and lights the same cap", 35);
            CaptureSewerPad("sewer-pad-accepted", glyph);
            yield return Wait(() => !SewerPadIsPressed(pad), "accepted cap releases");
        }

        static bool SewerPadIsPressed(CastleStation pad)
        {
            var cap = pad.transform.Find("Sewer pad cap");
            if (cap.localPosition.y >= .1f) return false;
            var plate = cap.Find("Sewer press plate").GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            plate.GetPropertyBlock(properties);
            Color tint = properties.GetColor("_BaseColor");
            return tint.g > tint.r;
        }

        void CaptureSewerPad(string label, int glyph)
        {
            var point = Station(CastleGameplay.Action.SewerPad, -1, 4, glyph).transform.position;
            CaptureView(label, point + new Vector3(-2.1f, 2.4f, 3.8f), point + Vector3.up * .35f);
        }

        IEnumerator GoToSewer()
        {
            yield return Walk(-1.8f, 241);
            yield return Walk(-12, 241);
        }

        IEnumerator Reconnect(string label)
        {
            yield return LeaveAndWaitForHost(label);
            yield return Join();
            yield return BindPlayer();
            yield return Wait(() => Vector3.Distance(player.transform.position, castle.World(-2.3f, .15f, 249)) < 1,
                "return reconnect resumes near the basement door");
        }

        IEnumerator LeaveAndWaitForHost(string label)
        {
            ulong departing = manager.LocalClientId;
            var leave = sessions.LeaveRoomAsync();
            yield return Wait(() => leave.IsCompleted && !manager.IsListening, "client disconnect " + label, 60);
            // Relay shutdown can finish locally before the host frees the old two-player slot.
            yield return Wait(() => File.Exists(DisconnectMarker(departing)), "host released the disconnected slot", 65);
        }

        string DisconnectMarker(ulong clientId) => Path.Combine(output, $"host-disconnected-{clientId}.ready");

        void RecordPeerDisconnect(ulong clientId)
        {
            if (clientId != NetworkManager.ServerClientId) File.WriteAllText(DisconnectMarker(clientId), "");
        }

        IEnumerator EnterSewer(int[] glyphs)
        {
            for (int i = 0; i < glyphs.Length; i++)
            {
                var pad = Station(CastleGameplay.Action.SewerPad, -1, 4, glyphs[i]);
                int revision = castle.Progress(slot).Revision;
                yield return UseAndConfirm(pad, () => castle.Progress(slot).Revision != revision, $"sewer input {i + 1}/12");
            }
        }

        CastleStation Station(CastleGameplay.Action kind, int path, int stage, int value = 0) =>
            castle.stations.Single(s => s.Kind == kind && s.Slot == path && s.Stage == stage && s.Value == value);

        static bool PadIsPressed(CastleStation pad) => pad.transform.Find("Door pad cap").localPosition.y < .2f;

        IEnumerator UseAndConfirm(CastleStation station, Func<bool> confirmed, string label)
        {
            yield return Use(station);
            if (fullMatch != null)
            {
                // Owner movement and the station RPC can reach the server on different frames.
                // Give the replicated response time to arrive before sending another ordinary E press.
                for (int retry = 0; retry < 2 && !confirmed(); retry++)
                {
                    float retryAt = Time.realtimeSinceStartup + 1f;
                    while (!confirmed() && Time.realtimeSinceStartup < retryAt) yield return null;
                    if (confirmed()) break;
                    Debug.Log($"CASTLE RETRY {station.Kind} {station.Value}: {label}");
                    yield return Use(station);
                }
            }
            yield return Wait(confirmed, label);
        }

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
            float best = Vector3.ProjectOnPlane(destination - player.transform.position, Vector3.up).magnitude;
            float end = Time.realtimeSinceStartup + 22 + best / 2;
            float progressDeadline = Time.realtimeSinceStartup + 3;
            while (true)
            {
                if (!game.CanPlay)
                {
                    SteerWorld(Vector3.zero);
                    float pausedAt = Time.realtimeSinceStartup;
                    yield return Wait(() => game.CanPlay, "Both peers ready to continue walking", 100);
                    end += Time.realtimeSinceStartup - pausedAt;
                    progressDeadline = Time.realtimeSinceStartup + 3;
                }
                Vector3 delta = Vector3.ProjectOnPlane(destination - player.transform.position, Vector3.up);
                float distance = delta.magnitude;
                if (distance <= .32f) break;
                if (distance < best - .15f)
                {
                    best = distance;
                    progressDeadline = Time.realtimeSinceStartup + 3;
                }
                if (Time.realtimeSinceStartup >= Mathf.Min(end, progressDeadline))
                    Require(false, WalkFailure(destination, delta), log: false);
                SteerWorld(Vector3.ClampMagnitude(delta / .45f, 1));
                if (sprint && distance > 2) SetKeys(Key.LeftShift);
                else SetKeys();
                yield return null;
            }
            SteerWorld(Vector3.zero);
            SetKeys();
            yield return new WaitForSeconds(.08f);
        }

        string WalkFailure(Vector3 target, Vector3 delta)
        {
            var controller = player.GetComponent<CharacterController>();
            Vector3 center = player.transform.TransformPoint(controller.center);
            float halfSegment = controller.height * .5f - controller.radius;
            var direction = delta.normalized;
            bool blocked = Physics.CapsuleCast(center + Vector3.up * halfSegment, center - Vector3.up * halfSegment,
                controller.radius - controller.skinWidth, direction, out var hit, 1, ~0, QueryTriggerInteraction.Ignore);
            var stick = FullMatchRelayReplay.CameraRelativeStick(player, direction);
            player.GetMovementAxes(out var forward, out var right);
            var actual = Vector3.ProjectOnPlane(controller.velocity, Vector3.up);
            var peers = manager.SpawnManager.SpawnedObjectsList.Where(peer => peer.IsPlayerObject)
                .Select(peer => $"{peer.OwnerClientId}={peer.transform.position:F2}");
            return $"walk stalled at {player.transform.position:F2}, target={target:F2}, remaining={delta.magnitude:F2}m, " +
                $"direction={direction:F2}, yaw={Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg:F1}, " +
                $"forward={forward:F2}, right={right:F2}, stick={stick:F2}, velocity={actual:F2}, " +
                $"grounded={player.IsGrounded}, contacts={controller.collisionFlags}, " +
                $"obstruction={(blocked ? $"{hit.collider.name} at {hit.distance:F2}m" : "none within 1m")}, " +
                $"{(manager.IsServer ? "server" : "client")} positions={string.Join(" | ", peers)}";
        }

        void SteerWorld(Vector3 direction)
        {
            if (fullMatch != null) fullMatch.SteerWorld(direction);
            else FullMatchRelayReplay.SteerStick(movementPad, FullMatchRelayReplay.CameraRelativeStick(player, direction));
        }

        void Capture(string label)
        {
            if (!screenshots) return;
            var view = Camera.main.transform;
            CaptureView(label, view.position, view.position + view.forward);
        }

        IEnumerator CaptureHeading(string label, float yaw)
        {
            if (!screenshots) yield break;
            var orbit = Camera.main.GetComponent<ThirdPersonCamera>();
            float previous = orbit.transform.eulerAngles.y;
            orbit.FaceSpawn(Quaternion.Euler(0, yaw, 0));
            yield return null;
            yield return new WaitForEndOfFrame();
            Capture(label);
            orbit.FaceSpawn(Quaternion.Euler(0, previous, 0));
        }

        IEnumerator CaptureDoorPad(string label, CastleStation pad)
        {
            if (!screenshots) yield break;
            Vector3 position = pad.transform.position - castle.World(0, 0, 0);
            yield return Walk(position.x + 1.2f, position.z - 2.7f);
            yield return CaptureHeading(label, 0);
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

        void OnDestroy()
        {
            Application.logMessageReceived -= WatchLogs;
            if (host && manager != null) manager.OnClientDisconnectCallback -= RecordPeerDisconnect;
        }
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
