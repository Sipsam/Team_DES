#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TwinEdge.Gameplay
{
    public sealed class MountainReturnRelaySmoke : MonoBehaviour
    {
        [Serializable] public class Leg { public string from, to; public Vector3 start, end, initial; public float yaw, pitch, charge; }
        [Serializable] public class Plan { public List<Leg> legs; }
        FullMatchRelayReplay fullMatch;
        bool host, failed, recoveryOnly, concurrentOnly, postOnly, inputOnly;
        string output, planFile, prefix;
        MountainReturnLaunch launch;
        NetworkManager manager;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-returnSmoke"); if (i < 0) return;
            var probe = new GameObject("ReturnRelaySmoke").AddComponent<MountainReturnRelaySmoke>();
            probe.inputOnly = Array.IndexOf(args, "-returnInput") >= 0;
            probe.postOnly = Array.IndexOf(args, "-returnPostRoute") >= 0;
            probe.concurrentOnly = Array.IndexOf(args, "-returnConcurrent") >= 0;
            probe.recoveryOnly = Array.IndexOf(args, "-returnRecovery") >= 0;
            probe.host = args[i + 1] == "host"; probe.prefix = probe.host ? "host" : "client";
            probe.output = args[Array.IndexOf(args, "-returnEvidence") + 1];
            probe.planFile = args[Array.IndexOf(args, "-returnPlan") + 1]; Directory.CreateDirectory(probe.output);
        }
        IEnumerator Start()
        {
            if (fullMatch != null) yield break;
            Application.runInBackground = true; Application.targetFrameRate = 60; Application.logMessageReceived += Log;
            manager = FindFirstObjectByType<NetworkManager>(); var sessions = FindFirstObjectByType<MultiplayerSessionManager>();
            yield return Until(() => FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).Length >= 4, "Level ready", 90);
            if (host)
            {
                var task = sessions.CreateRoomAsync(); yield return Until(() => task.IsCompleted, "Relay create", 90);
                Check(sessions.HasSession, "Relay hosted");
                File.WriteAllText(output + "/room.tmp", sessions.CurrentJoinCode); File.Move(output + "/room.tmp", output + "/room.txt");
            }
            else
            {
                yield return Until(() => File.Exists(output + "/room.txt"), "Room code", 100);
                var task = sessions.JoinRoomAsync(File.ReadAllText(output + "/room.txt")); yield return Until(() => task.IsCompleted, "Relay join", 90);
                Check(sessions.HasSession, "Relay joined");
            }
            var game = FindFirstObjectByType<GameStateManager>();
            yield return RelayMatchStart.Run(game);
            yield return Until(() => game.IsGameReady && manager.LocalClient?.PlayerObject != null, "Two players", 90);
            launch = manager.LocalClient.PlayerObject.GetComponent<MountainReturnLaunch>();
            launch.DevelopmentInputDriver = true;
            yield return Until(() => game.CanPlay, "Outbound ready");
            launch.PrepareRpc(launch.Revision.Value); yield return new WaitForSeconds(.5f);
            Check(launch.State.Value != LaunchState.Charging && launch.State.Value != LaunchState.Flying, "Outbound request rejected");
            File.WriteAllText(output + "/" + prefix + "-outbound.txt", "ready");
            yield return Until(() => File.Exists(output + "/host-outbound.txt") && File.Exists(output + "/client-outbound.txt"), "Both outbound rejection checks complete");
            if (host) { Check(game.AdvanceFoundation(FoundationPhase.SealPending), "Server seal transition"); Check(game.AdvanceFoundation(FoundationPhase.Return), "Server Return transition"); }
            yield return Until(() => launch.ReturnEnabled, "Return replicated");
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(planFile)); Check(plan.legs.Count > 0, "Collider route exists");
            // One initial station teleport only. Every subsequent leg uses production launch and CC.
            var cc = launch.GetComponent<CharacterController>(); cc.enabled = false;
            var start = plan.legs[0].start + (host ? Vector3.zero : Vector3.right * 3);
            launch.GetComponent<OwnerNetworkTransform>().Teleport(start, Quaternion.identity, Vector3.one); cc.enabled = true;
            yield return new WaitForSeconds(1);
            if (inputOnly) { yield return InputCase(); yield break; }
            if (concurrentOnly) { yield return ConcurrentCase("Final"); Application.Quit(0); yield break; }
            if (recoveryOnly) { yield return RecoveryCase(sessions, game); yield break; }
            if (postOnly)
            {
                cc.enabled = false; launch.GetComponent<OwnerNetworkTransform>().Teleport(plan.legs[plan.legs.Count - 1].end + (host ? Vector3.zero : Vector3.right * 3), Quaternion.identity, Vector3.one); cc.enabled = true;
                yield return new WaitForSeconds(1);
            }
            if (!host && !postOnly) yield return Until(() => File.Exists(output + "/host-route.txt"), "Host route then independent client", 360);
            if (!host && !postOnly) yield return WalkTo(plan.legs[0].start);
            if (!postOnly) yield return FlyRoute(plan);
            yield return Until(() => launch.State.Value == LaunchState.Ready, "Post-route ready");
            var center = new Vector3(0, launch.transform.position.y, -225);
            yield return WalkTo(launch.transform.position + Vector3.ProjectOnPlane(center - launch.transform.position, Vector3.up).normalized * (host ? 8 : 4));
            File.WriteAllText(output + "/" + prefix + "-route.txt", (postOnly ? "POST-ROUTE SETUP ONLY, NO TRAVERSAL PROOF: " : "PASS actual owner CC route: ") + count + " legs, real Relay. Sequential peer routes; no human-control claim.");
            yield return Until(() => File.Exists(output + "/host-route.txt") && File.Exists(output + "/client-route.txt"), "Both routes", 360);
            yield return Until(() => launch.State.Value == LaunchState.Ready, "Recovery test ready");
            launch.PrepareRpc(launch.Revision.Value); yield return Until(() => launch.State.Value == LaunchState.Charging, "Cancellation preparation");
            launch.FireRpc(launch.Revision.Value, 0, 45, float.MaxValue); yield return new WaitForSeconds(.15f);
            Check(launch.State.Value == LaunchState.Charging, "Extreme finite strength safely rejected");
            launch.FireRpc(launch.Revision.Value, 0, 45, 1); yield return new WaitForSeconds(.15f);
            Check(launch.State.Value == LaunchState.Charging, "Uncharged full strength rejected");
            launch.CancelRpc(launch.Revision.Value); yield return Until(() => launch.State.Value == LaunchState.Ready, "Cancellation cooldown recovery");
            File.WriteAllText(output + "/" + prefix + "-cancel.txt", "PASS");
            yield return Until(() => File.Exists(output + "/host-cancel.txt") && File.Exists(output + "/client-cancel.txt"), "Cancel barrier");
            // Separate concurrency station AFTER route proofs; not return-traversal evidence.
            cc.enabled = false;
            launch.GetComponent<OwnerNetworkTransform>().Teleport(plan.legs[0].start + (host ? Vector3.zero : Vector3.right * 3), Quaternion.identity, Vector3.one);
            launch.GetComponent<PlayerMovement>().ResetVerticalVelocity(); cc.enabled = true;
            yield return new WaitForSeconds(1);
            yield return ConcurrentCase("Final");
            int respawns = launch.GetComponent<GameplayCharacter>().RespawnCount.Value;
            cc.enabled = false;
            var fall = launch.transform.position + Vector3.right * 400;
            launch.GetComponent<OwnerNetworkTransform>().Teleport(fall, Quaternion.identity, Vector3.one); cc.enabled = true;
            yield return Until(() => launch.GetComponent<GameplayCharacter>().RespawnCount.Value > respawns, "Normal-gravity fall / Final retry", 30);
            yield return Until(() => Vector3.Distance(launch.transform.position, plan.legs[0].start) < 70, "Return Final checkpoint restored");
            File.WriteAllText(output + "/" + prefix + "-recovery.txt", "PASS separate void station setup, actual fall and individual Final respawn");
            yield return Until(() => File.Exists(output + "/host-recovery.txt") && File.Exists(output + "/client-recovery.txt"), "Recovery barrier");
            yield return Until(() => launch.State.Value == LaunchState.Ready, "Respawn cooldown complete before disconnect preparation");
            if (!host)
            {
                launch.PrepareRpc(launch.Revision.Value); yield return Until(() => launch.State.Value == LaunchState.Charging, "Disconnect during preparation");
                File.WriteAllText(output + "/client-disconnecting.txt", "ready");
                var leave = sessions.LeaveRoomAsync(); yield return Until(() => leave.IsCompleted, "Client disconnect");
            }
            else
            {
                yield return Until(() => File.Exists(output + "/client-disconnecting.txt"), "Disconnect station");
                yield return Until(() => !game.IsGameReady, "Disconnect replicated");
                Check(cc.enabled && !launch.ControlsMovement, "Survivor movement available");
                File.WriteAllText(output + "/disconnect-pass.txt", "PASS disconnect during remote charging; foundation paused, survivor unlocked");
            }
            File.WriteAllText(output + "/" + prefix + "-pass.txt", (postOnly ? "PASS post-route subset only; NO route proof. " : "PASS route. ") + "PASS stale/nonfinite/uncharged rejection, cancel, fall/Final retry, concurrent independent hop / remote position and animation. Manual input separate.");
            Application.Quit(0);
        }
        int count;

        internal IEnumerator ReplayFromSummit(FullMatchRelayReplay match, string routeFile)
        {
            fullMatch = match;
            output = match.Output;
            prefix = match.Role;
            launch = match.Player.GetComponent<MountainReturnLaunch>();
            launch.DevelopmentInputDriver = true;
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(routeFile));
            Check(plan.legs.Count > 0, "Collider route exists");
            yield return FlyRoute(plan);
            yield return Until(() => launch.State.Value == LaunchState.Ready, "Controls restored on Start");
        }

        IEnumerator FlyRoute(Plan plan)
        {
            count = 0;
            foreach (var leg in plan.legs)
            {
                yield return Until(() => launch.State.Value == LaunchState.Ready, "Launch available");
                if (fullMatch == null) yield return WalkTo(leg.start);
                else yield return fullMatch.Walk(leg.start, .08f, false);
                yield return new WaitForSeconds(.4f);
                uint revision = launch.Revision.Value;
                launch.PrepareRpc(revision); yield return Until(() => launch.State.Value == LaunchState.Charging, "Charge authorized");
                yield return Until(() => launch.Charge >= leg.charge, "Authoritative charge reached requested strength", 5);
                if (fullMatch == null)
                {
                    launch.FireRpc(revision, float.NaN, leg.pitch, leg.charge);
                    yield return new WaitForSeconds(.1f);
                    Check(launch.State.Value == LaunchState.Charging, "Nonfinite launch rejected");
                }
                var forecast = fullMatch == null
                    ? launch.PredictLanding(launch.config.Velocity(leg.yaw, leg.pitch, leg.charge))
                    : default;
                launch.FireRpc(revision, leg.yaw, leg.pitch, leg.charge);
                yield return Until(() => launch.State.Value == LaunchState.Flying, "Launch accepted");
                if (fullMatch == null) launch.FireRpc(revision, leg.yaw, leg.pitch, leg.charge);
                yield return Until(() => launch.State.Value != LaunchState.Flying, "Actual flight completed", 12);
                Check(launch.LastLanding.Value.ToString() == leg.to, "Actual landing " + leg.to);
                if (fullMatch == null) ReportPrediction(forecast, leg.to, leg.yaw, leg.pitch, leg.charge);
                File.AppendAllText(output + "/" + prefix + "-landings.txt", (++count) + " " + leg.from + " -> " + leg.to + " position=" + launch.transform.position + "\n");
            }
        }

        IEnumerator InputCase()
        {
            InputSystem.settings = Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var input = launch.GetComponent<GameplayInput>(); input.SimulateCapturedCursor = true;
            launch.DevelopmentInputDriver = false;
            yield return Until(() => launch.State.Value == LaunchState.Ready, "Input station ready");
            var before = launch.transform.position; int count = launch.GetComponent<GameplayCharacter>().RespawnCount.Value;
            var camera = Camera.main.GetComponent<ThirdPersonCamera>();
            camera.enabled = false;
            float yaw = host ? -20 : 20;
            void Aim(float direction)
            {
                camera.transform.SetPositionAndRotation(before + Vector3.up * 8 - Quaternion.Euler(0, direction, 0) * Vector3.forward * 9,
                    Quaternion.Euler(22, direction, 0));
            }
            Aim(0);
            bool capture = Array.IndexOf(Environment.GetCommandLineArgs(), "-returnScreenshots") >= 0;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R, Key.W));
            yield return Until(() => launch.State.Value == LaunchState.Charging, "Actual R binding prepares");
            Check(Vector3.ProjectOnPlane(launch.transform.position - before, Vector3.up).magnitude < .15f, "R+W does not leak into normal walking while approval pending");
            yield return Until(() => launch.PredictionVisible && launch.Prediction.Summit == "Final", "Low charge shows supported local contact", 5);
            var early = launch.Prediction.Feet;
            Aim(yaw);
            yield return Until(() => launch.Charge >= .35f && launch.PredictionVisible &&
                Vector3.Distance(launch.Prediction.Feet, early) > 1, "Charge, pitch and camera yaw move the preview", 5);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            File.WriteAllText(output + "/" + prefix + "-preview-ready.txt", "ready");
            yield return Until(() => File.Exists(output + "/host-preview-ready.txt") && File.Exists(output + "/client-preview-ready.txt"),
                "Both peers charging");
            var remote = Array.Find(FindObjectsByType<MountainReturnLaunch>(FindObjectsSortMode.None), p => p.IsSpawned && !p.IsOwner);
            yield return Until(() => remote != null && remote.State.Value == LaunchState.Charging, "Remote charging replicated");
            Check(launch.PredictionVisible && !remote.PredictionVisible, "Only the owner renders their prediction");
            File.WriteAllText(output + "/" + prefix + "-preview-checked.txt", "ready");
            yield return Until(() => File.Exists(output + "/host-preview-checked.txt") && File.Exists(output + "/client-preview-checked.txt"),
                "Both owner previews checked");
            if (capture)
                PoleRuntimeCapture.SaveOwnerView(output + "/" + prefix + "-launch-prediction.png");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Until(() => !launch.PredictionVisible, "Release hides preview");
            yield return Until(() => launch.InFlight, "Actual R release launches");
            if (capture && GameplayEffects.Trace)
                PoleRuntimeCapture.SaveOwnerView(output + "/" + prefix + "-launch-flight.png");
            var v = launch.InitialVelocity.Value;
            float pitch = Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg;
            float charge = Mathf.InverseLerp(launch.config.minSpeed, launch.config.maxSpeed, v.magnitude);
            Check(pitch > 55 && Mathf.Abs(Mathf.DeltaAngle(yaw, Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg)) < .1f,
                "Actual W and camera changed elevation and yaw");
            yield return Until(() => launch.State.Value == LaunchState.Landing, "Input launch landed", 12);
            if (capture && GameplayEffects.Trace)
            {
                yield return new WaitForSeconds(.10f);
                PoleRuntimeCapture.SaveOwnerView(output + "/" + prefix + "-launch-landing.png");
            }
            ReportPrediction(launch.ReleasedPrediction, "Final", yaw, pitch, charge);
            yield return Until(() => launch.State.Value == LaunchState.Ready, "Input launch landed and rearmed", 12);
            Check(launch.LastLanding.Value.ToString() == "Final" && launch.GetComponent<GameplayCharacter>().RespawnCount.Value == count, "Input flight real Final landing without respawn");
            CheckPredictionMatrix();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return Until(() => launch.PredictionVisible, "Second actual R preparation displays preview");
            uint cancelRevision = launch.Revision.Value;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R, Key.Escape));
            yield return Until(() => !launch.PredictionVisible, "Escape hides preview while R is still held");
            yield return Until(() => launch.Revision.Value != cancelRevision && launch.Failure.Value.ToString() == "Cancelled", "Actual Escape cancels");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Until(() => launch.State.Value == LaunchState.Ready && !launch.ControlsMovement, "Actual cancel restores movement");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return Until(() => launch.PredictionVisible, "Preview displayed before respawn");
            File.WriteAllText(output + "/" + prefix + "-respawn-ready.txt", "ready");
            yield return Until(() => File.Exists(output + "/host-respawn-ready.txt") && File.Exists(output + "/client-respawn-ready.txt"),
                "Both owners charging before respawn");
            if (host)
            {
                var respawn = FindFirstObjectByType<RespawnManager>();
                respawn.RespawnPlayer(remote.GetComponent<PlayerMovement>());
                respawn.RespawnPlayer(launch.GetComponent<PlayerMovement>());
            }
            yield return Until(() => launch.GetComponent<GameplayCharacter>().RespawnCount.Value > count && !launch.PredictionVisible,
                "Respawn removes the charging preview");
            File.WriteAllText(output + "/" + prefix + "-input-pass.txt",
                "PASS R/W/release/Escape bindings, owner-only preview, contact/charge/yaw/pitch matrix, actual landing error, cancel and respawn over isolated Relay.");
            yield return Until(() => File.Exists(output + "/host-input-pass.txt") && File.Exists(output + "/client-input-pass.txt"), "Both real input bindings");
            InputSystem.RemoveDevice(keyboard); Application.Quit(0);
        }

        void CheckPredictionMatrix()
        {
            int safe = 0, risky = 0, unknown = 0;
            foreach (float yaw in new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
            foreach (float pitch in new[] { 20f, 45f, 70f })
            foreach (float charge in new[] { 0f, .5f, 1f })
            {
                var result = launch.PredictLanding(launch.config.Velocity(yaw, pitch, charge));
                if (!result.HasContact) unknown++;
                else if (result.Summit == null) risky++;
                else safe++;
            }

            var initial = launch.config.Velocity(0, 45, .3f);
            launch.PredictLanding(initial);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++) launch.PredictLanding(initial);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            string summary = $"Saved Collider predictions: {safe} supported, {risky} risky, {unknown} indeterminate; {bytes} managed bytes in 20 warmed predictions.";
            File.WriteAllText(output + "/" + prefix + "-prediction-matrix.txt", summary);
            Check(safe > 0 && risky > 0 && unknown > 0 && bytes == 0, summary);
        }

        void ReportPrediction(MountainReturnLaunch.LandingPrediction forecast, string summit, float yaw, float pitch, float charge)
        {
            float error = forecast.HasContact ? Vector3.Distance(forecast.Feet, launch.transform.position) : float.NaN;
            File.AppendAllText(output + "/" + prefix + "-prediction-errors.txt",
                $"yaw={yaw:F1} pitch={pitch:F1} charge={charge:F2} predicted={forecast.Summit ?? "none"} expected={summit} prediction={forecast.Feet:F3} actual={launch.transform.position:F3} error={error:F3}m\n");
            Check(forecast.HasContact && forecast.Summit == summit && error < .5f, $"Preview matches actual {summit} within 0.5m ({error:F3}m)");
        }
        IEnumerator ConcurrentCase(string expected)
        {
            yield return Until(() => launch.State.Value == LaunchState.Ready, "Concurrent launch ready");
            int concurrentRespawns = launch.GetComponent<GameplayCharacter>().RespawnCount.Value;
            launch.PrepareRpc(launch.Revision.Value); yield return Until(() => launch.State.Value == LaunchState.Charging, "Concurrent charge authorized");
            yield return new WaitForSeconds(.8f);
            File.WriteAllText(output + "/" + prefix + "-hop-ready.txt", "ready");
            yield return Until(() => File.Exists(output + "/host-hop-ready.txt") && File.Exists(output + "/client-hop-ready.txt"), "Concurrent flight barrier");
            var towardCenter = Vector3.ProjectOnPlane(new Vector3(expected == "Start" ? 0 : 12, 0, expected == "Start" ? -225 : 285) - launch.transform.position, Vector3.up);
            launch.FireRpc(launch.Revision.Value, Mathf.Atan2(towardCenter.x, towardCenter.z) * Mathf.Rad2Deg, 65, .3f);
            yield return Until(() => launch.State.Value == LaunchState.Flying, "Own independent flight");
            var remote = Array.Find(FindObjectsByType<MountainReturnLaunch>(FindObjectsSortMode.None), p => p.IsSpawned && !p.IsOwner);
            yield return Until(() => remote != null && remote.State.Value == LaunchState.Flying, "Concurrent remote flight replicated", 3);
            var remoteBefore = remote.transform.position;
            float observeUntil = Time.realtimeSinceStartup + 2;
            while (Vector3.Distance(remoteBefore, remote.transform.position) <= .1f && Time.realtimeSinceStartup < observeUntil)
            {
                File.AppendAllText(output + "/" + prefix + "-concurrent-trace.txt", $"{Time.realtimeSinceStartup:F3} own={launch.transform.position} remote={remote.transform.position} state={remote.State.Value} velocity={remote.FlightVelocity.Value} anim={remote.GetComponent<GameplayCharacter>().AnimationState} transformEnabled={remote.GetComponent<OwnerNetworkTransform>().enabled}\n");
                yield return null;
            }
            Check(Vector3.Distance(remoteBefore, remote.transform.position) > .1f, "Remote flight position advances after Relay delivery");
            yield return Until(() => remote.GetComponent<GameplayCharacter>().AnimationState == "Jump", "Remote Jump animation replicated after channel delivery", 2);
            yield return Until(() => launch.State.Value == LaunchState.Ready, "Concurrent hop lands and rearms", 12);
            Check(launch.GetComponent<GameplayCharacter>().RespawnCount.Value == concurrentRespawns, "Concurrent landing did not respawn");
            Check(launch.LastLanding.Value.ToString() == expected, "Concurrent hop lands on " + expected);
            File.WriteAllText(output + "/" + prefix + "-concurrent.txt", "PASS simultaneous independent flight, remote state/position/Jump, landing and rearm");
            yield return Until(() => File.Exists(output + "/host-concurrent.txt") && File.Exists(output + "/client-concurrent.txt"), "Concurrent completion barrier");
        }
        IEnumerator RecoveryCase(MultiplayerSessionManager sessions, GameStateManager game)
        {
            int before = launch.GetComponent<GameplayCharacter>().RespawnCount.Value;
            File.WriteAllText(output + "/" + prefix + "-station.txt", "ready");
            yield return Until(() => File.Exists(output + "/host-station.txt") && File.Exists(output + "/client-station.txt"), "Recovery station barrier");
            yield return Hop("first");
            if (host)
            {
                yield return new WaitForSeconds(.25f);
                FindFirstObjectByType<RespawnManager>().RespawnPlayer(launch.GetComponent<PlayerMovement>());
                yield return Until(() => launch.GetComponent<GameplayCharacter>().RespawnCount.Value > before && !launch.ControlsMovement, "Server respawn interrupts flight");
                Check(launch.GetComponent<CharacterController>().enabled, "Respawn controller restored");
            }
            else
            {
                yield return Until(() => launch.State.Value == LaunchState.Ready, "Partner continues independently after host respawn", 12);
                Check(launch.GetComponent<GameplayCharacter>().RespawnCount.Value == before, "Partner not respawned");
            }
            File.WriteAllText(output + "/" + prefix + "-midrespawn.txt", "PASS");
            yield return Until(() => File.Exists(output + "/host-midrespawn.txt") && File.Exists(output + "/client-midrespawn.txt"), "Respawn barrier");
            yield return Until(() => launch.State.Value == LaunchState.Ready, "Flight disconnect ready");
            yield return Hop("disconnect");
            if (host)
            {
                var remote = Array.Find(FindObjectsByType<MountainReturnLaunch>(FindObjectsSortMode.None), p => p.IsSpawned && !p.IsOwner);
                yield return Until(() => remote != null && remote.InFlight && remote.FlightVelocity.Value.magnitude > 1, "Server sees remote airborne motion", 3);
                File.WriteAllText(output + "/host-saw-flight.txt", remote.transform.position.ToString());
                yield return Until(() => !game.IsGameReady, "Airborne disconnect observed");
                yield return Until(() => !launch.ControlsMovement, "Surviving airborne motor releases on phase pause");
                Check(launch.GetComponent<CharacterController>().enabled, "Survivor controller available");
            }
            else
            {
                yield return Until(() => File.Exists(output + "/host-saw-flight.txt"), "Server observed client flight before leave", 3);
                var leave = sessions.LeaveRoomAsync(); yield return Until(() => leave.IsCompleted, "Disconnect while flying");
            }
            File.WriteAllText(output + "/" + prefix + "-pass.txt", "PASS actual Relay mid-flight individual respawn and airborne disconnect; survivor unlocked");
            Application.Quit(0);
        }
        IEnumerator Hop(string station)
        {
            launch.PrepareRpc(launch.Revision.Value); yield return Until(() => launch.State.Value == LaunchState.Charging, station + " charge");
            yield return new WaitForSeconds(.8f);
            File.WriteAllText(output + "/" + prefix + "-" + station + "-ready.txt", "ready");
            yield return Until(() => File.Exists(output + "/host-" + station + "-ready.txt") && File.Exists(output + "/client-" + station + "-ready.txt"), station + " launch barrier");
            launch.FireRpc(launch.Revision.Value, 0, 65, .3f);
            yield return Until(() => launch.InFlight, station + " actual flight");
        }
        IEnumerator WalkTo(Vector3 target)
        {
            float until = Time.realtimeSinceStartup + 40;
            var cc = launch.GetComponent<CharacterController>();
            while (Vector3.ProjectOnPlane(launch.transform.position - target, Vector3.up).magnitude > .08f && Time.realtimeSinceStartup < until)
            {
                cc.Move(Vector3.ClampMagnitude(Vector3.ProjectOnPlane(target - launch.transform.position, Vector3.up), .04f) + Vector3.down * .02f);
                yield return new WaitForFixedUpdate();
            }
            Check(Vector3.ProjectOnPlane(launch.transform.position - target, Vector3.up).magnitude < .1f, "Same-summit CC walking");
        }
        IEnumerator Until(Func<bool> predicate, string label, float timeout = 30)
        { float deadline = Time.realtimeSinceStartup + timeout; while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null; Check(predicate(), label); }
        void Check(bool result, string label) { if (!result) throw new Exception("RETURN_FAIL " + label + (launch != null ? $" state={launch.State.Value} charge={launch.Charge} ticket={launch.Revision.Value} position={launch.transform.position} reason={launch.Failure.Value} lastLanding={launch.LastLanding.Value} respawns={launch.GetComponent<GameplayCharacter>().RespawnCount.Value}" : "")); Debug.Log("RETURN_PASS " + label); }
        void Log(string message, string stack, LogType type)
        {
            if (failed || (type != LogType.Exception && type != LogType.Error)) return;
            failed = true; File.WriteAllText(output + "/" + prefix + "-failure.txt", message + "\n" + stack); Application.Quit(2);
        }
    }
}
#endif
