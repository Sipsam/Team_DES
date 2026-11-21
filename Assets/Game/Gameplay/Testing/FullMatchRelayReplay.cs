#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TwinEdge.Levels;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Unity.Profiling;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TwinEdge.Gameplay
{
    // One session from the ordinary start spawn. Isolated fixtures have their own entry points.
    public sealed class FullMatchRelayReplay : MonoBehaviour
    {
        public bool Host => Role == "host";
        public string Role { get; private set; }
        public string Output { get; private set; }
        public NetworkManager Manager { get; private set; }
        public GameStateManager Game { get; private set; }
        public PlayerMovement Player { get; private set; }
        public Keyboard Keyboard { get; private set; }

        string Partner => Host ? "client" : "host";
        string polePlan, returnPlan, stage, isolatedLink;
        bool screenshots, failed;
        Gamepad gamepad;
        GameplayCharacter character;
        bool started;
        float nextSample, peakFrame;
        double frameSeconds;
        int frames;
        long totalGcBytes, peakGcBytes;
        ProfilerRecorder gcBytes, drawCalls;
        readonly FrameTiming[] frameTiming = new FrameTiming[1];
        ValleyConfigV3 valley;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            string role = Arg(args, "-fullMatchRelay");
            if (role == null) return;
            string output = Arg(args, "-fullMatchEvidence");
            string pole = Arg(args, "-mountainPolePlan");
            string back = Arg(args, "-returnPlan");
            if (role is not ("host" or "client") || string.IsNullOrWhiteSpace(output) ||
                !Path.IsPathFullyQualified(output) || !File.Exists(pole) || !File.Exists(back))
            {
                Debug.LogError("Use -fullMatchRelay host|client, -fullMatchEvidence <absolute directory>, -mountainPolePlan <plan>, and -returnPlan <plan>.");
                Application.Quit(2);
                return;
            }
            Directory.CreateDirectory(output);
            var replay = new GameObject(nameof(FullMatchRelayReplay)).AddComponent<FullMatchRelayReplay>();
            replay.Role = role;
            replay.Output = output;
            replay.polePlan = pole;
            replay.returnPlan = back;
            replay.isolatedLink = Arg(args, "-fullMatchLink");
            replay.screenshots = args.Contains("-fullMatchScreenshots");
        }

        static string Arg(string[] args, string key)
        {
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Application.logMessageReceived += ObserveLog;
            InputSystem.settings = Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            gamepad = InputSystem.AddDevice<Gamepad>();
            Keyboard = InputSystem.AddDevice<Keyboard>();
            var bootstrap = FindFirstObjectByType<V3GameplayBootstrap>();
            Manager = bootstrap.manager;
            Game = bootstrap.state;
            stage = "load V3";
            File.WriteAllText(Evidence("environment.txt"),
                $"{Application.unityVersion} | {SystemInfo.operatingSystem} | {SystemInfo.processorType} | " +
                $"{SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceName} | {Screen.width}x{Screen.height}\n");
            var finishObject = default(GameObject);
            yield return Until(() => (finishObject = GameObject.Find("V3 Summit Finish")) != null, "level loaded", 90);
            var finish = finishObject.GetComponent<BoxCollider>();
            valley = FindObjectsByType<ValleySectionV3>(FindObjectsSortMode.None).First(s => s.part == ValleyPartV3.Bridge).config;

            stage = "Relay room";
            string roomFile = Path.Combine(Output, "room.txt");
            if (Host)
            {
                var task = bootstrap.sessions.CreateRoomAsync();
                yield return Until(() => task.IsCompleted, "Relay create request completed", 90);
                Require(bootstrap.sessions.HasSession && Game.Phase == FoundationPhase.WaitingForPlayers,
                    "Host relay lobby is waiting for the second player");
                File.WriteAllText(roomFile + ".tmp", bootstrap.sessions.CurrentJoinCode);
                File.Move(roomFile + ".tmp", roomFile);
            }
            else
            {
                yield return Until(() => File.Exists(roomFile), "Host published room code", 105);
                var task = bootstrap.sessions.JoinRoomAsync(File.ReadAllText(roomFile));
                yield return Until(() => task.IsCompleted, "Relay join request completed", 90);
                Require(bootstrap.sessions.HasSession, "Client joined Relay");
            }

            yield return Until(() => Manager.LocalClient?.PlayerObject != null && Game.CanPlay &&
                Game.Phase == FoundationPhase.Outbound && CooperativePole.Current != null, "both peers ready and automatically started", 90);
            Player = Manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
            character = Player.GetComponent<GameplayCharacter>();
            Player.GetComponent<GameplayInput>().SimulateCapturedCursor = true;
            var trail = FindFirstObjectByType<MountainSectionV3>().config.entryTrail;
            Require(Vector3.Distance(Player.transform.position, trail[1]) < 3f, "player spawned at the trailhead");
            Require(!finish.bounds.Contains(Player.transform.position + Vector3.up * .3f), "initial spawn is outside the finish");
            Require(character.RespawnCount.Value == 0, "ordinary spawn required no respawn");
            yield return Barrier("normal-spawns");
            Capture("normal-spawn");
            gcBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            started = true;

            if (isolatedLink != null)
            {
                yield return ReplayLink(finish);
                started = false;
                File.WriteAllText(Evidence("isolated-link-pass.txt"), State());
                yield return Barrier("isolated-pass");
                Application.Quit(0);
                yield break;
            }

            stage = "mountain-entry-trail";
            Record("begin");
            var trailLane = Vector3.right * (Host ? -1.5f : 1.5f);
            for (int i = 2; i < trail.Length; i++)
            {
                Vector3 from = trail[i - 1], to = trail[i];
                // Keyboard diagonals move both axes at the same speed. Keep each leg short
                // so players stay in their lane on the steep, curved approach.
                int steps = Mathf.CeilToInt(Vector3.Distance(from, to) / 3f);
                for (int step = 1; step <= steps; step++)
                    yield return Walk(Vector3.Lerp(from, to, step / (float)steps) + trailLane, .55f, false);
            }
            Require(Game.Phase == FoundationPhase.Outbound && !Game.HasWinner, "passing the finish on the outbound trail cannot end the match");
            Record("end");
            yield return Segment("mountain-outbound", CreateDriver<MountainPoleRelayReplay>().ReplayFromSpawn(this, polePlan));
            Require(character.CheckpointName.Value.ToString().EndsWith("Big summit", StringComparison.Ordinal),
                "summit checkpoint captured");
            var bridge = CreateDriver<BridgeRelaySmoke>();
            yield return Segment("bridge-outbound", bridge.ReplayOutbound(this));
            yield return ReplayMiddleCourse();
            yield return Segment("bridge-return", bridge.ReplayReturn());
            yield return ReplayFinish(finish);
            started = false;
            File.WriteAllText(Evidence("full-match-pass.txt"), State());
            yield return Barrier("both-pass");
            Application.Quit(0);
        }

        IEnumerator ReplayFinish(BoxCollider finish)
        {
            bool carrier = GemOwnership.Current.Owner == Player.OwnerClientId;
            if (carrier) yield return Until(() => File.Exists(Path.Combine(Output, Partner + "-noncarrier-in-finish.txt")),
                "non-carrier entered finish first", 450);
            stage = "mountain-return";
            Record("begin");
            yield return CreateDriver<MountainReturnRelaySmoke>().ReplayFromSummit(this, returnPlan);
            Record("end", true);

            stage = "summit-finish";
            // The demo interaction cube sits on the direct approach from Peak_04.
            Vector3 finishApproach = finish.transform.position + Vector3.right * (carrier ? -3 : 3);
            yield return Walk(finishApproach, .35f, false);
            yield return Walk(finish.transform.position, .45f, false,
                () => finish.bounds.Contains(Player.transform.position + Vector3.up * .3f));
            Require(finish.bounds.Contains(Player.transform.position + Vector3.up * .3f), "actual feet in finish volume");
            if (!carrier)
            {
                yield return new WaitForSeconds(.8f);
                Require(!Game.HasWinner && Game.Phase == FoundationPhase.Return, "finish denied without gem");
                File.WriteAllText(Evidence("noncarrier-in-finish.txt"), "Return");
                yield return Walk(finishApproach);
            }
            yield return Until(() => Game.Phase == FoundationPhase.Finished && Game.HasWinner, "server finalized winner", 465);
            var elapsed = Game.ElapsedSeconds;
            var winner = Game.WinnerClientId;
            Require(winner == GemOwnership.Current.Owner && !Game.CanPlay, "winner is the gem owner and play is closed");
            // The river's two-second hull rebuild must complete without moving either finalist.
            yield return new WaitForSeconds(3);
            Require(Game.Phase == FoundationPhase.Finished && Game.CurrentState == GameState.Finished &&
                Game.ElapsedSeconds == elapsed && Game.WinnerClientId == winner, "phase, timer and winner stable after Finished");
            Record("finish", true);
            File.WriteAllText(Evidence("result.txt"), FormattableString.Invariant($"{winner},{elapsed:R}"));
            File.WriteAllText(Evidence("outcome.txt"), $"player={Player.OwnerClientId} outcome={(winner == Player.OwnerClientId ? "Victory" : "Defeat")}");
            yield return Barrier("winner-replicated");
            Require(File.ReadAllText(Evidence("result.txt")) == File.ReadAllText(Path.Combine(Output, Partner + "-result.txt")),
                "same winner and final timer on both peers");
            started = false;
            yield return Barrier("finish-verified");
        }

        IEnumerator Segment(string label, IEnumerator action)
        {
            stage = label;
            Record("begin");
            yield return action;
            yield return Barrier(label);
            Record("end", true);
        }

        T CreateDriver<T>() where T : MonoBehaviour
        {
            var driver = gameObject.AddComponent<T>();
            // The coordinator runs these coroutines. On their own, Start opens an isolated Relay room.
            driver.enabled = false;
            return driver;
        }

        IEnumerator ReplayMiddleCourse(bool fromDock = false)
        {
            Vector3 lower = RiverGameplay.Current.Landing(false, Host ? 0 : 1).position;
            Vector3 upper = RiverGameplay.Current.Landing(true, Host ? 0 : 1).position;
            var river = CreateDriver<RiverRelayReplay>();
            if (!fromDock) yield return Segment("canyon-descent", Canyon(false, lower));
            yield return Segment("river-outbound", river.ReplayCrossing(this, false));

            var castle = CastleGameplay.Current;
            yield return Segment("upper-dock-to-castle", CastleApproach(upper, castle, false));
            yield return Segment("castle-and-underground", CreateDriver<CastleRelayReplay>().ReplayFromGate(this));
            Require(Game.Phase == FoundationPhase.Return && GemOwnership.Current.State == GemState.Carried, "normal Return with a carrier");

            yield return Segment("castle-to-upper-dock", CastleApproach(upper, castle, true));
            yield return Segment("river-return", river.ReplayCrossing(this, true));
            yield return Segment("canyon-ascent", Canyon(true, lower));
        }

        IEnumerator ReplayLink(BoxCollider finish)
        {
            int slot = Host ? 0 : 1;
            var river = RiverGameplay.Current;
            var bridge = BridgeGameplay.Current;
            var castle = CastleGameplay.Current;
            Vector3 side = Vector3.right * (Host ? -1.2f : 1.2f);
            stage = "isolated-" + isolatedLink;
            Pose setup;
            switch (isolatedLink)
            {
                case "canyon":
                case "middle":
                    setup = new Pose(V3GameplayBootstrap.SafeGround(bridge.EndPoint + Vector3.forward * 3 + side),
                        Quaternion.identity);
                    break;
                case "approach":
                    setup = river.Landing(true, slot);
                    break;
                case "river":
                    setup = river.Landing(false, slot);
                    break;
                case "summit":
                case "finish":
                    var plan = JsonUtility.FromJson<MountainPolePlan>(File.ReadAllText(polePlan));
                    var final = plan.actions.Last(action => action.kind == "Rotate");
                    bool hostPivot = plan.actions.Count(action => action.kind == "Swap") % 2 == 0;
                    Vector3 landing = Host == hostPivot ? final.pivot :
                        PoleGeometry.Endpoint(final.pivot, CooperativePole.Current.length, final.yaw, final.pitch);
                    setup = new Pose(V3GameplayBootstrap.SafeGround(landing), Quaternion.identity);
                    break;
                case "castle":
                    setup = new Pose(V3GameplayBootstrap.SafeGround(castle.World(Host ? -1 : 2, 0, -19)),
                        Quaternion.identity);
                    break;
                default:
                    throw new ArgumentException("Isolated link must be canyon, approach, summit, castle, river, middle or finish.");
            }
            // Explicit isolated setup. The normal match never enters this code path.
            Player.PlaceAtOwner(setup.position, setup.rotation);
            yield return Barrier("isolated-placement");
            switch (isolatedLink)
            {
                case "canyon":
                    var lower = river.Landing(false, slot).position;
                    yield return Segment("isolated-canyon-descent", Canyon(false, lower));
                    Require(Mathf.Abs(Player.transform.position.y - lower.y) < .4f && Player.IsGrounded, "stood on lower dock");
                    yield return Segment("isolated-canyon-ascent", Canyon(true, lower));
                    Require(Mathf.Abs(Player.transform.position.y - bridge.EndPoint.y) < .4f && Player.IsGrounded,
                        "stood at canyon end of bridge");
                    break;
                case "approach":
                    var upper = river.Landing(true, slot).position;
                    yield return Segment("isolated-upper-to-castle", CastleApproach(upper, castle, false));
                    Require(Mathf.Abs(Player.transform.position.y - castle.World(0, 0, -19).y) < 1 && Player.IsGrounded,
                        "stood near the castle gate");
                    yield return Segment("isolated-castle-to-upper", CastleApproach(upper, castle, true));
                    Require(Mathf.Abs(Player.transform.position.y - upper.y) < .4f && Player.IsGrounded, "stood on upper dock");
                    break;
                case "summit":
                    yield return Segment("isolated-summit-through-bridge", CreateDriver<BridgeRelaySmoke>().ReplayOutbound(this));
                    break;
                case "finish":
                    yield return ReplayIsolatedFinish(finish);
                    break;
                case "castle":
                    yield return Segment("isolated-castle-and-underground", CreateDriver<CastleRelayReplay>().ReplayFromGate(this));
                    Require(Game.Phase == FoundationPhase.Return && GemOwnership.Current.State == GemState.Carried,
                        "real seal, pickup and castle exit");
                    break;
                case "middle":
                case "river":
                    yield return ReplayMiddleCourse(isolatedLink == "river");
                    break;
            }
        }

        IEnumerator ReplayIsolatedFinish(BoxCollider finish)
        {
            var bridge = CreateDriver<BridgeRelaySmoke>();
            yield return Segment("isolated-bridge-outbound", bridge.ReplayOutbound(this));
            stage = "isolated-gem-setup";
            var gem = GemOwnership.Current;
            if (Host)
            {
                Require(Game.AdvanceFoundation(FoundationPhase.SealPending), "isolated seal setup");
                gem.UnlockServer(Player.transform.position + Vector3.forward * .8f + Vector3.up * .65f);
                yield return new WaitForSeconds(.25f);
                yield return Tap(Key.E);
            }
            yield return Until(() => Game.Phase == FoundationPhase.Return && gem.State == GemState.Carried,
                "E took the fixture gem at Canyon");
            yield return Segment("isolated-bridge-return", bridge.ReplayReturn());
            yield return ReplayFinish(finish);
        }

        IEnumerator Canyon(bool returning, Vector3 dock)
        {
            Vector3 lane = Vector3.right * (Host ? -1.2f : 1.2f);
            var start = BridgeGameplay.Current.EndPoint + Vector3.forward * 3 + lane;
            if (returning) yield return Walk(dock);
            // The saved path curves between its control points; follow its centre instead of cutting the banks.
            var path = new List<Vector3>();
            for (int i = 1; i < valley.descent.Length; i++)
            {
                Vector3 a = valley.descent[i - 1], b = valley.descent[i];
                int samples = Mathf.CeilToInt(Vector3.Distance(a, b) / 12f);
                for (int j = 1; j <= samples; j++)
                {
                    float t = j / (float)samples;
                    var point = Vector3.Lerp(a, b, Mathf.SmoothStep(0, 1, t));
                    point.z = Mathf.Lerp(a.z, b.z, t);
                    if (point.z > start.z) path.Add(point);
                }
            }
            if (returning) path.Reverse();
            foreach (var point in path) yield return Walk(point + lane, .55f);
            yield return Walk(returning ? start : dock);
        }

        IEnumerator CastleApproach(Vector3 dock, CastleGameplay castle, bool returning)
        {
            Vector3 gate = castle.World(Host ? -1 : 2, 0, -19);
            Vector3 bank = new(dock.x + 9, dock.y, dock.z);
            Vector3 rim = new(dock.x + 9, dock.y, valley.river.Last().z + 6);
            Vector3 approach = new(valley.castleEntry.x, valley.castleEntry.y, 1980);
            var path = returning ? new[] { approach, rim, bank, dock } : new[] { bank, rim, approach, gate };
            foreach (var point in path) yield return Walk(point);
        }

        internal IEnumerator Walk(Vector3 target, float tolerance = .28f, bool sprint = true, Func<bool> arrived = null)
        {
            float deadline = Time.realtimeSinceStartup + 20 +
                Vector3.ProjectOnPlane(target - Player.transform.position, Vector3.up).magnitude / 2;
            do
            {
                var delta = Vector3.ProjectOnPlane(target - Player.transform.position, Vector3.up);
                if (arrived?.Invoke() ?? delta.magnitude <= tolerance) break;
                Require(Time.realtimeSinceStartup < deadline, $"walk to {target:F2}");
                var camera = Camera.main.transform;
                float forward = Vector3.Dot(delta, Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized);
                float right = Vector3.Dot(delta, Vector3.ProjectOnPlane(camera.right, Vector3.up).normalized);
                float threshold = Mathf.Min(tolerance * .7f, Mathf.Max(Mathf.Abs(forward), Mathf.Abs(right)) * .45f);
                var keys = new List<Key>(3);
                if (Mathf.Abs(forward) > threshold) keys.Add(forward > 0 ? Key.W : Key.S);
                if (Mathf.Abs(right) > threshold) keys.Add(right > 0 ? Key.D : Key.A);
                if (sprint && delta.sqrMagnitude > 4) keys.Add(Key.LeftShift);
                Keys(keys.ToArray());
                yield return null;
            } while (true);
            Keys();
            yield return new WaitForSeconds(.08f);
        }

        internal void SteerPole(Vector2 direction) => SteerStick(gamepad, direction);

        internal static void SteerStick(Gamepad input, Vector2 direction)
        {
            // Invert the stick's Input System dead zone so small landing corrections remain expressible.
            var settings = InputSystem.settings;
            var stick = direction.normalized * Mathf.Lerp(settings.defaultDeadzoneMin, settings.defaultDeadzoneMax, direction.magnitude);
            InputSystem.QueueStateEvent(input, new GamepadState { leftStick = stick });
        }

        internal IEnumerator Tap(Key key)
        {
            Keys(key);
            yield return new WaitForSeconds(.12f);
            Keys();
            yield return new WaitForSeconds(.12f);
        }

        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard, new KeyboardState(keys));

        internal IEnumerator Barrier(string label)
        {
            File.WriteAllText(Evidence(label + "-ready.txt"), State());
            yield return Until(() => File.Exists(Path.Combine(Output, Partner + "-" + label + "-ready.txt")), label + " peer barrier", 140);
        }

        IEnumerator Until(Func<bool> condition, string label, float timeout = 20)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                Require(Time.realtimeSinceStartup < deadline, label);
                yield return null;
            }
        }

        void Update()
        {
            if (!started || failed) return;
            Require(character.RespawnCount.Value == 0, "unexpected respawn in the normal route");
            Require(Game.Phase == FoundationPhase.Finished || Manager.IsConnectedClient && Game.IsGameReady,
                "both participants remain connected");
            float seconds = Time.unscaledDeltaTime;
            frameSeconds += seconds;
            frames++;
            peakFrame = Mathf.Max(peakFrame, seconds);
            if (gcBytes.Valid)
            {
                totalGcBytes += gcBytes.LastValue;
                peakGcBytes = Math.Max(peakGcBytes, gcBytes.LastValue);
            }
            if (Time.realtimeSinceStartup < nextSample) return;
            nextSample = Time.realtimeSinceStartup + 5;
            Record("sample");
        }

        string State()
        {
            var peers = Manager != null && Manager.SpawnManager != null
                ? string.Join(" | ", Manager.SpawnManager.SpawnedObjectsList.Where(p => p.IsPlayerObject)
                    .OrderBy(p => p.OwnerClientId).Select(p => $"{p.OwnerClientId}={p.transform.position:F2}"))
                : "";
            var gem = GemOwnership.Current;
            return FormattableString.Invariant(
                $"{Time.realtimeSinceStartup:F2} {stage} phase={Game?.Phase} gameTime={Game?.ElapsedSeconds:F3} winner={Game?.WinnerClientId} gem={gem?.State}/{gem?.Owner} checkpoint={character?.CheckpointName.Value} respawns={character?.RespawnCount.Value} peers={peers}");
        }

        void Record(string moment, bool capture = false)
        {
            ulong remote = Manager.SpawnManager.SpawnedObjectsList.Where(p => p.IsPlayerObject && p.OwnerClientId != Manager.LocalClientId)
                .Select(p => p.OwnerClientId).DefaultIfEmpty(NetworkManager.ServerClientId).First();
            ulong rtt = Manager.NetworkConfig.NetworkTransport.GetCurrentRtt(remote);
            bool timingAvailable = FrameTimingManager.GetLatestTimings(1, frameTiming) != 0;
            File.AppendAllText(Evidence("timeline.txt"), FormattableString.Invariant(
                $"{moment} {State()} frames={frames} meanFps={(frameSeconds > 0 ? frames / frameSeconds : 0):F1} minFps={(peakFrame > 0 ? 1 / peakFrame : 0):F1} allocatedBytes={Profiler.GetTotalAllocatedMemoryLong()} gcBytes={(gcBytes.Valid ? totalGcBytes : -1)} peakGcFrame={(gcBytes.Valid ? peakGcBytes : -1)} networkObjects={Manager.SpawnManager.SpawnedObjectsList.Count} rttMs={rtt} draws={(drawCalls.Valid ? drawCalls.LastValue : -1)} cpuMs={(timingAvailable ? frameTiming[0].cpuFrameTime : -1):F2} gpuMs={(timingAvailable && frameTiming[0].gpuFrameTime > 0 ? frameTiming[0].gpuFrameTime : -1):F2}") + "\n");
            FrameTimingManager.CaptureFrameTimings();
            if (capture) Capture(stage);
        }

        internal void Capture(string label)
        {
            if (!screenshots || Camera.main == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            // Rendering the bound camera to a texture also works for an unfocused player on Windows.
            var target = RenderTexture.GetTemporary(1024, 720, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(Camera.main, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                File.WriteAllBytes(Evidence(label + ".png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Destroy(image);
            }
        }

        string Evidence(string name) => Path.Combine(Output, Role + "-" + name);

        void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException($"FULL_MATCH {label} | {State()}");
        }

        void ObserveLog(string message, string stack, LogType type)
        {
            if (failed || type is not (LogType.Exception or LogType.Assert)) return;
            failed = true;
            if (Keyboard != null) Keys();
            File.WriteAllText(Evidence("full-match-failure.txt"), $"{State()}\n{message}\n{stack}");
            Capture("failure");
            StartCoroutine(QuitAfterCapture());
        }

        IEnumerator QuitAfterCapture()
        {
            yield return new WaitForSecondsRealtime(.5f);
            Application.Quit(2);
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= ObserveLog;
            gcBytes.Dispose();
            drawCalls.Dispose();
        }
    }
}
#endif
