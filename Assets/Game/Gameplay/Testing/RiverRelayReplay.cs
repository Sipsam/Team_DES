#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TwinEdge.Gameplay
{
    // Two real Relay peers. Only initial placement at the lower boarding pad uses a teleport.
    public sealed partial class RiverRelayReplay : MonoBehaviour
    {
        string output, peer;
        bool host, screenshots, failed, recovery;
        FullMatchRelayReplay fullMatch;
        Keyboard keyboard;
        RiverGameplay river;
        PlayerMovement player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int role = Array.IndexOf(args, "-riverReplay");
            int evidence = Array.IndexOf(args, "-riverEvidence");
            if (role < 0 || evidence < 0 || role + 1 >= args.Length || evidence + 1 >= args.Length) return;
            var replay = new GameObject("RiverRelayReplay").AddComponent<RiverRelayReplay>();
            replay.peer = args[role + 1];
            replay.host = replay.peer == "host";
            replay.output = args[evidence + 1];
            replay.screenshots = Array.IndexOf(args, "-riverScreenshots") >= 0;
            replay.recovery = Array.IndexOf(args, "-riverRecovery") >= 0;
            Directory.CreateDirectory(replay.output);
        }

        IEnumerator Start()
        {
            if (fullMatch != null) yield break;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Application.logMessageReceived += ObserveLog;
            InputSystem.settings = Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var manager = FindFirstObjectByType<NetworkManager>();
            var sessions = FindFirstObjectByType<MultiplayerSessionManager>();
            var game = FindFirstObjectByType<GameStateManager>();
            river = game.GetComponent<RiverGameplay>();
            yield return Wait(() => FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).Length >= 4, "V3 loaded", 90);
            if (host)
            {
                var task = sessions.CreateRoomAsync();
                yield return Wait(() => task.IsCompleted, "Relay create", 90);
                Require(sessions.HasSession, "Relay Host session");
                File.WriteAllText(Path.Combine(output, "room.tmp"), sessions.CurrentJoinCode);
                File.Move(Path.Combine(output, "room.tmp"), Path.Combine(output, "room.txt"));
            }
            else
            {
                yield return Wait(() => File.Exists(Path.Combine(output, "room.txt")), "Relay room code", 110);
                var task = sessions.JoinRoomAsync(File.ReadAllText(Path.Combine(output, "room.txt")));
                yield return Wait(() => task.IsCompleted, "Relay join", 90);
                Require(sessions.HasSession, "Relay Client session");
            }
            yield return Wait(() => game.IsGameReady && manager.LocalClient?.PlayerObject != null, "two Relay peers", 90);
            if (host && recovery) river.EnableRecoveryFixture();
            yield return Wait(() => river.Read(0).Status == RiverRaftStatus.Docked, "linked raft docked");
            player = manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
            player.GetComponent<GameplayInput>().SimulateCapturedCursor = true;
            keyboard = InputSystem.AddDevice<Keyboard>();
            var spawn = river.Landing(false, host ? 0 : 1);
            player.PlaceAtOwner(spawn.position, spawn.rotation);
            yield return new WaitForSeconds(.9f);
            Capture("lower-dock");
            yield return new WaitForSeconds(.35f);
            yield return Press(Key.E);
            yield return Wait(() => river.Read(0).Status == RiverRaftStatus.Sailing, "both seats occupied");
            Require(river.ReplicatedHazardCount == 15, "Outbound replicated 15 hazards");
            var s = river.Read(0);
            Require(s.Left != s.Right && s.Left != RiverRaftSnapshot.Empty && s.Right != RiverRaftSnapshot.Empty, "distinct owners");
            Capture("boarded");
            river.OnContact += (point, cause) => Debug.Log($"RIVER CONTACT {cause} at {point:F1}");
            if (recovery)
            {
                yield return ValidateRecovery(game, manager);
                yield break;
            }

            double actionStart = manager.ServerTime.Time + 1;
            float before = s.Position.z;
            while (manager.ServerTime.Time < actionStart + 1.6) yield return null;
            Require(river.Read(0).Position.z < before - 1, "unpowered downstream drift");
            if (host) SetKeys(Key.W, Key.LeftShift);
            yield return Wait(() =>
            {
                var stroke = river.Read(0);
                return stroke.LeftStamina < .98f && stroke.RightStamina > .99f && stroke.LeftDrive > 1 &&
                    Mathf.Abs(stroke.TurnRate) > 3;
            }, "independent left strong stroke, stamina and differential turn", 3);
            yield return FileBarrier("independent-stroke");
            SetKeys();

            yield return Traverse(0, manager);
            yield return Wait(() => river.Read(0).Status == RiverRaftStatus.Arrived && river.Read(2).Status == RiverRaftStatus.Docked,
                "upstream arrival and split");
            yield return Wait(() => Vector3.Distance(player.transform.position, river.Landing(true, host ? 0 : 1).position) < 3, "disembarkation position");
            Require(Vector3.Distance(player.transform.position, river.Landing(true, host ? 0 : 1).position) < 3,
                "owner placed on upper dock");
            yield return new WaitForSeconds(1.5f);
            Capture("upper-split");
            if (host) game.RequestDevelopmentReturn();
            yield return Wait(() => game.Phase == FoundationPhase.Return, "server Return");
            yield return Press(Key.E);
            yield return Wait(() => river.Read(1).Status == RiverRaftStatus.Sailing && river.Read(2).Status == RiverRaftStatus.Sailing,
                "independent race rafts sailing");
            Require(river.TrySeat(player.OwnerClientId, out int index, out _, out _) && index > 0, "own solo seat");
            Require(river.ReplicatedHazardCount == 15, "Return replicated 15 hazards");
            before = river.Read(index).Position.z;
            yield return new WaitForSeconds(2);
            Require(river.Read(index).Position.z < before - 7 && river.Read(index).Velocity.z < -3,
                "faster downstream drift without input");
            Capture("paired-race", (river.Read(1).Position + river.Read(2).Position) * .5f);
            yield return Traverse(index, manager);
            yield return Wait(() => river.Read(1).Status == RiverRaftStatus.Arrived && river.Read(2).Status == RiverRaftStatus.Arrived,
                "both downstream arrivals", 190);
            Require(river.Read(1).FinishPlace + river.Read(2).FinishPlace == 3, "unique race order");
            Capture("finish");
            yield return new WaitForSeconds(1);
            File.WriteAllText(Path.Combine(output, peer + "-pass.txt"),
                "PASS. Real Relay and keyboard replay, station placement only at lower boarding pad. Both directions completed.");
            Application.Quit();
        }

        internal IEnumerator ReplayCrossing(FullMatchRelayReplay match, bool returning)
        {
            fullMatch = match;
            output = match.Output;
            peer = match.Role;
            river = RiverGameplay.Current;
            player = match.Player;
            keyboard = match.Keyboard;
            yield return match.Walk(river.Landing(returning, match.Host ? 0 : 1).position);
            yield return match.Barrier(returning ? "upper-boarding" : "lower-boarding");
            yield return Press(Key.E);
            int index = returning ? (match.Host ? 1 : 2) : 0;
            yield return Wait(() => river.Read(index).Status == RiverRaftStatus.Sailing &&
                river.TrySeat(player.OwnerClientId, out int seat, out _, out _) && seat == index,
                "local player boarded hull " + index);
            match.Capture(returning ? "solo-raft" : "linked-raft");
            yield return Traverse(index, match.Manager);
            yield return Wait(() => Vector3.Distance(player.transform.position,
                river.Landing(!returning, match.Host ? 0 : 1).position) < 3 &&
                player.GetComponent<CharacterController>().enabled, "arrived and disembarked hull " + index);
            var character = player.GetComponent<GameplayCharacter>();
            string dock = returning ? "Lower dock" : "Upper dock";
            yield return Wait(() => character.CheckpointName.Value.ToString().StartsWith(dock, StringComparison.Ordinal),
                "arrival checkpoint " + dock);
            yield return match.Barrier(returning ? "both-downstream" : "both-upstream");
            if (returning)
                Require(river.Read(1).FinishPlace + river.Read(2).FinishPlace == 3, "unique solo race order");
        }

        IEnumerator Traverse(int index, NetworkManager manager)
        {
            bool outbound = index == 0;
            float deadline = Time.time + (outbound ? 260 : 170);
            float report = 0, nextWarning = 0;
            bool inWhirlpool = false;
            while (river.Read(index).Status == RiverRaftStatus.Sailing && Time.time < deadline)
            {
                var s = river.Read(index);
                var aim = river.ReplayCourse(s.Position.z, outbound, avoidReturnHazards: fullMatch == null);
                float correction = Mathf.Clamp((aim.x - s.Position.x) * 5f - (s.Velocity.x - aim.z * s.Velocity.z) * 2f,
                    -48, 48) * (outbound ? 1 : -1);
                if (!outbound && s.Position.z < 1180)
                    correction = Mathf.Clamp((s.Position.x - aim.x) * 15f + s.Velocity.x * .8f, -48, 48);
                float error = Mathf.DeltaAngle(s.Heading, aim.y + correction);
                bool warned = river.TryNearbyWarning(s.Position, out var warning);
                bool flee = fullMatch != null && warned && Mathf.Abs(error) < 14 &&
                    Vector3.Dot(warning - s.Position, s.Velocity) > 0;
                bool whirlpool = river.InWhirlpool(index);
                if (inWhirlpool != whirlpool)
                {
                    inWhirlpool = whirlpool;
                    Debug.Log($"RIVER WHIRLPOOL {(whirlpool ? "entered" : "left")} {index} at {s.Position:F1}, hits {s.Hits}");
                    if (whirlpool) Capture("whirlpool");
                }
                if (Time.time > nextWarning && warned)
                {
                    nextWarning = Time.time + 3;
                    Debug.Log($"RIVER WARNING {warning:F1}");
                    Capture("warning", warning);
                }
                if (outbound)
                {
                    bool left = s.Left == player.OwnerClientId;
                    bool steerer = error > 0 ? left : !left;
                    bool boosting = Math.Abs(error) > 7 && steerer && (left ? s.LeftStamina : s.RightStamina) > .09f;
                    bool coast = Math.Abs(error) > 16 && !steerer &&
                        (Math.Floor(manager.ServerTime.Time * 5) % 3 == 0);
                    if (boosting || flee) SetKeys(Key.W, Key.LeftShift);
                    else if (coast) SetKeys();
                    else SetKeys(Key.W);
                }
                else
                {
                    if (error > 6) SetKeys(Key.W, Key.D);
                    else if (error < -6) SetKeys(Key.W, Key.A);
                    else if (flee) SetKeys(Key.W, Key.LeftShift);
                    else SetKeys(Key.W);
                }

                if (Time.time >= report)
                {
                    report = Time.time + 4;
                    Debug.Log($"RIVER_REPLAY hull={index} p={s.Position:F1} v={s.Velocity:F1} heading={s.Heading:F0} " +
                        $"desired={aim.y + correction:F0} x={aim.x:F1} hits={s.Hits} contact={s.Contact}");
                    Capture(outbound ? "upstream" : "race");
                }
                yield return new WaitForSeconds(.1f);
            }
            SetKeys();
            var finish = river.Read(index);
            Require(finish.Status == RiverRaftStatus.Arrived,
                $"continuous crossing of hull {index}: {finish.Status}, {finish.Position}, {finish.Hits}, {finish.Recovery}");
        }

        void Capture(string name, Vector3? point = null, Vector3? cameraOffset = null)
        {
            if (!screenshots || Camera.main == null) return;
            var camera = Camera.main;
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            if (point.HasValue)
            {
                camera.transform.position = point.Value + (cameraOffset ?? new Vector3(5, 13, -14));
                camera.transform.LookAt(point.Value);
            }
            var target = RenderTexture.GetTemporary(1024, 720, 24, RenderTextureFormat.ARGB32);
            var saved = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                File.WriteAllBytes(Path.Combine(output, $"{peer}-{name}-{Time.frameCount}.png"), image.EncodeToPNG());
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                RenderTexture.active = saved;
                RenderTexture.ReleaseTemporary(target);
                Destroy(image);
            }
        }

        void SetKeys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        IEnumerator Press(Key key)
        {
            SetKeys(key);
            yield return new WaitForSeconds(.15f);
            SetKeys();
        }

        IEnumerator Wait(Func<bool> condition, string label, float timeout = 15)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Require(condition(), label);
        }

        void Require(bool condition, string label)
        {
            if (condition) { Debug.Log("RIVER PASS " + label); return; }
            Fail(label);
            throw new InvalidOperationException(label);
        }

        void ObserveLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Assert) Fail(message + "\n" + stack);
        }

        void Fail(string reason)
        {
            if (fullMatch != null) return; // The coordinator saves both the exception and current match evidence.
            if (failed) return;
            failed = true;
            Debug.LogError("RIVER FAIL " + reason);
            File.WriteAllText(Path.Combine(output, peer + "-fail.txt"), reason);
            Application.Quit(1);
        }

        void OnDestroy() => Application.logMessageReceived -= ObserveLog;
    }
}
#endif
