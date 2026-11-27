#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using TwinEdge.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed partial class V3GameplaySmoke
{
    IEnumerator RunCameraControls(GameplayCharacter remote)
    {
        var camera = Camera.main.GetComponent<ThirdPersonCamera>();
        bool capture = Array.IndexOf(Environment.GetCommandLineArgs(), "-v3ControlScreenshots") >= 0;
        bool audioTrace = Array.IndexOf(Environment.GetCommandLineArgs(), "-twinAudioTrace") >= 0;
        int steps = 0, jumps = 0, landings = 0;
        if (audioTrace) Application.logMessageReceived += CountMovementCue;
        // Drive the bound camera transform directly so each sample has a fixed yaw and pitch.
        camera.enabled = false;
        var own = player.GetComponent<GameplayCharacter>();
        var center = new Vector3(host ? -80 : 80, 500, 0);
        Vector3 runDirection = host ? Vector3.forward : Vector3.back;
        Box("Controls floor", center + Vector3.down * .5f, new Vector3(60, 1, 60));
        Box("Remote controls floor", new Vector3(-center.x, center.y - .5f, center.z), new Vector3(60, 1, 60));
        Box("Controls wall", center + runDirection * 18 + Vector3.up * 2, new Vector3(16, 4, .2f));
        GameObject visibleFloor = null;
        Material floorMaterial = null;
        if (capture && GameplayEffects.Trace)
        {
            visibleFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visibleFloor.name = "Movement evidence floor";
            visibleFloor.GetComponent<Collider>().enabled = false;
            visibleFloor.transform.position = center + Vector3.down * .52f;
            visibleFloor.transform.localScale = new Vector3(60, 1, 60);
            floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.32f, .32f, .29f) };
            visibleFloor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
        }
        camera.transform.position = center + Vector3.up * 1.4f - runDirection * 5f;
        camera.transform.rotation = Quaternion.LookRotation(runDirection);
        Teleport(center);
        yield return Until(() => player.IsGrounded, "Grounded on movement fixture");
        // The first horizontal Move after a teleport resolves the capsule's floor skin width.
        SetControlKeys(Key.W);
        yield return new WaitForSeconds(.2f);
        SetControlKeys();
        yield return new WaitForSeconds(.25f);
        var input = player.GetComponent<GameplayInput>();

        var keys = new[] { Key.W, Key.A, Key.S, Key.D };
        var axes = new[] { Vector3.forward, Vector3.left, Vector3.back, Vector3.right };
        foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
        foreach (float pitch in new[] { -25f, 65f })
        {
            var rotation = Quaternion.Euler(0, yaw + (host ? 0 : 180), 0);
            camera.transform.rotation = Quaternion.Euler(pitch, rotation.eulerAngles.y, 0);
            for (int i = 0; i < keys.Length; i++)
            {
                Vector3 start = player.transform.position;
                SetControlKeys(keys[i]);
                yield return new WaitForSeconds(.16f);
                Vector3 delta = player.transform.position - start;
                Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
                string label = $"yaw {rotation.eulerAngles.y:F0} pitch {pitch} {keys[i]} delta {delta:F3}, " +
                    $"stick {input.Move.ReadValue<Vector2>()}, input {input.AcceptsInput}, grounded {player.IsGrounded}";
                Check(horizontal.magnitude > .3f && Vector3.Dot(horizontal.normalized, rotation * axes[i]) > .999f &&
                    Mathf.Abs(delta.y) < .02f, label);
                SetControlKeys();
                yield return null;
            }
        }

        camera.transform.rotation = Quaternion.LookRotation(runDirection);
        Teleport(center);
        yield return Until(() => player.IsGrounded, "Grounded before distance samples");
        int walkSteps = steps;
        yield return MeasureStride(3.5f, Key.W);
        Check(own.AnimationState == "Walk" && Mathf.Approximately(own.animator.speed, 1f), "Walk animation and cadence");
        if (audioTrace) Check(steps > walkSteps, "Actual walk produced footsteps");
        SetControlKeys();
        Teleport(center);
        yield return ControlBarrier("sprint-start");
        yield return Until(() => Vector3.Distance(remote.transform.position, center) > 100, "Remote movement station received");
        Vector3 remoteStart = remote.transform.position;
        SetControlKeys(Key.W, Key.LeftShift);
        yield return Until(() => own.SprintAllowed, "Server sprint lease received");
        float staminaAtStart = own.StaminaFraction;
        float sprintStart = Time.time;
        int runSteps = steps;
        yield return MeasureStride(5.6f * 1.5f, Key.W, Key.LeftShift);
        float drain = (staminaAtStart - own.StaminaFraction) * 100f / (Time.time - sprintStart);
        Check(Mathf.Abs(drain - 2f) < .35f, $"Sprint stamina rate {drain:F2}/s");
        Check(own.AnimationState == "Run" && Mathf.Approximately(own.animator.speed, 1.5f), "Run animation and cadence");
        if (audioTrace) Check(steps > runSteps, "Actual sprint produced footsteps");
        if (capture)
        {
            camera.transform.position = player.transform.position + Vector3.up * 1.15f - runDirection * 4f;
            if (GameplayEffects.Trace) PoleRuntimeCapture.SaveOwnerView(ControlPath("run", host, "png"));
            else ScreenCapture.CaptureScreenshot(ControlPath("run", host, "png"));
        }
        yield return Until(() => remote.AnimationState == "Run" && Mathf.Approximately(remote.animator.speed, 1.5f) &&
            Vector3.Dot(remote.transform.position - remoteStart, -runDirection) > 5f, "Remote run cadence and opposite travel replicated", 5);
        yield return Until(() => Vector3.Dot(player.transform.position - center, runDirection) > 17f &&
            player.Velocity.sqrMagnitude < .03f, "CharacterController stopped at sprint wall", 5);
        yield return new WaitForSeconds(.25f);
        Check(Vector3.Dot(player.transform.position - center, runDirection) < 17.95f && player.IsGrounded,
            "Sprint did not penetrate wall or lose ground");
        if (audioTrace)
        {
            camera.transform.position = player.transform.position + Vector3.up * 1.4f - runDirection * 4;
            int blockedSteps = steps;
            yield return new WaitForSeconds(.55f);
            Check(steps == blockedSteps, "Held input at the wall produced no footsteps within listener range");
        }
        SetControlKeys();
        yield return Until(() => own.StaminaFraction >= .999f && !own.SprintAllowed, "Stamina recovered after sprint", 5);
        yield return ControlBarrier("sprint-end");
        Vector3 settled = player.transform.position;
        string settledPath = ControlPath("settled", host);
        File.WriteAllText(settledPath + ".tmp", JsonUtility.ToJson(settled));
        File.Move(settledPath + ".tmp", settledPath);
        yield return Until(() => File.Exists(ControlPath("settled", !host)), "Peer reported settled position");
        Vector3 remoteSettled = JsonUtility.FromJson<Vector3>(File.ReadAllText(ControlPath("settled", !host)));
        yield return Until(() => Vector3.Distance(remote.transform.position, remoteSettled) < .08f,
            "Remote settled position agrees with owner");
        yield return ControlBarrier("position-verified");

        float ground = player.transform.position.y;
        float apex = ground;
        int beforeJump = jumps, beforeLand = landings, idleSteps = steps;
        SetControlKeys(Key.Space);
        yield return Until(() => !player.IsGrounded, "Normal jump takes off");
        SetControlKeys();
        float jumpTimeout = Time.time + 2f;
        while (!player.IsGrounded && Time.time < jumpTimeout)
        {
            apex = Mathf.Max(apex, player.transform.position.y);
            yield return null;
        }
        Check(player.IsGrounded && apex - ground > 1.33f && apex - ground < 1.55f, $"Normal jump lands, rise {apex - ground:F3}m");
        if (capture && GameplayEffects.Trace)
        {
            yield return new WaitForSeconds(.10f);
            camera.transform.position = player.transform.position + Vector3.up * 1.3f - runDirection * 3f + Vector3.right * 2f;
            camera.transform.LookAt(player.transform.position + Vector3.up * .65f);
            PoleRuntimeCapture.SaveOwnerView(ControlPath("landing", host, "png"));
        }
        if (audioTrace)
        {
            yield return new WaitForSeconds(.2f);
            Check(jumps == beforeJump + 1 && landings == beforeLand + 1 && steps == idleSteps,
                "One jump and one landing cue, with no footfalls in air");
            Application.logMessageReceived -= CountMovementCue;
        }
        if (visibleFloor) Destroy(visibleFloor);
        if (floorMaterial) Destroy(floorMaterial);
        yield return RunPoleCameraControls(camera, capture);
        camera.enabled = true;
        yield return ControlBarrier("finished");
        File.WriteAllText(evidence + (host ? "/host-pass.txt" : "/client-pass.txt"),
            "PASS camera-relative WASD matrix, walk and sprint distance, stamina, jump, sprint collision, animation and peer position, pole pivot and orbit controls.");

        void CountMovementCue(string message, string stack, LogType type)
        {
            if (message.StartsWith("AUDIO_PLAY Step", StringComparison.Ordinal)) steps++;
            if (message.StartsWith("AUDIO_PLAY Jump ", StringComparison.Ordinal)) jumps++;
            if (message.StartsWith("AUDIO_PLAY Land ", StringComparison.Ordinal)) landings++;
        }
    }

    IEnumerator MeasureStride(float expected, params Key[] keys)
    {
        SetControlKeys(keys);
        yield return null;
        Vector3 start = player.transform.position;
        float began = Time.time;
        yield return new WaitForSeconds(1f);
        float distance = Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up).magnitude;
        float elapsed = Time.time - began;
        Check(Mathf.Abs(distance / elapsed - expected) < .08f, $"Stride {distance:F3}m in {elapsed:F3}s = {distance / elapsed:F3}m/s (expected {expected:F1})");
    }

    IEnumerator RunPoleCameraControls(ThirdPersonCamera camera, bool capture)
    {
        var pole = CooperativePole.Current;
        var home = pole.Snapshot;
        Teleport(host ? home.a : home.b);
        yield return ControlBarrier("pole-positioned");
        if (host) pole.UseRpc(pole.Snapshot.revision);
        else
        {
            yield return Until(() => pole.Snapshot.phase == PolePhase.OneAttached, "Host is first pole grip");
            pole.UseRpc(pole.Snapshot.revision);
        }
        yield return Until(() => pole.Snapshot.Paired, "Pole pair replicated");
        var start = pole.Snapshot;
        camera.transform.rotation = Quaternion.Euler(host ? 65 : -25, host ? 90 : 270, 0);
        yield return ControlBarrier("pole-paired");
        yield return PoleKey(pole, "pivot W at yaw 90", host, Key.W,
            () => pole.Snapshot.PivotPoint.x - start.PivotPoint.x > .15f);
        yield return MeasurePoleStride(pole, Key.W, Vector3.right, false);
        yield return MeasurePoleStride(pole, Key.S, Vector3.left, true);
        var sidestep = pole.Snapshot;
        yield return PoleKey(pole, "pivot A at yaw 90", host, Key.A,
            () => pole.Snapshot.PivotPoint.z - sidestep.PivotPoint.z > .15f);
        sidestep = pole.Snapshot;
        yield return PoleKey(pole, "pivot D at yaw 90", host, Key.D,
            () => sidestep.PivotPoint.z - pole.Snapshot.PivotPoint.z > .15f);
        var carried = pole.Snapshot;
        Check(Mathf.Abs(carried.PivotPoint.y - start.PivotPoint.y) < .001f &&
            pole.SafeLanding(carried.PivotPoint, out var startSummit) && startSummit == "Start", "Pivot stays supported on Start");
        yield return PoleKey(pole, "orbit W raises at yaw 270", !host, Key.W,
            () => pole.Snapshot.pitch > carried.pitch + 2f);
        var raised = pole.Snapshot;
        Check(!pole.SafeLanding(raised.OrbitPoint, out _), "Orbit is airborne before pivot translation");
        yield return PoleKey(pole, "pivot moves while orbit is airborne", host, Key.W,
            () => pole.Snapshot.PivotPoint.x - raised.PivotPoint.x > .15f);
        var translated = pole.Snapshot;
        Check(Vector3.Distance(translated.OrbitPoint - raised.OrbitPoint, translated.PivotPoint - raised.PivotPoint) < .002f,
            "Airborne orbit follows the rigid span");
        yield return PoleKey(pole, "orbit D at yaw 270 reverses world yaw", !host, Key.D,
            () => pole.Snapshot.yaw < raised.yaw - 2f);
        var turned = pole.Snapshot;
        if (!host) camera.transform.rotation = Quaternion.Euler(65, 90, 0);
        yield return ControlBarrier("orbit-camera-reversed");
        yield return PoleKey(pole, "orbit D at yaw 90 increases world yaw", !host, Key.D,
            () => pole.Snapshot.yaw > turned.yaw + 2f);
        var returned = pole.Snapshot;
        yield return PoleKey(pole, "orbit S lowers", !host, Key.S, () => pole.Snapshot.pitch < returned.pitch - 1f);
        Check(Vector3.Distance(pole.Snapshot.PivotPoint, translated.PivotPoint) < .002f &&
            Mathf.Abs(Vector3.Distance(pole.Snapshot.a, pole.Snapshot.b) - pole.length) < .002f,
            "Orbit retains fixed pivot and rigid rod length");
        foreach (var peer in FindObjectsByType<GameplayCharacter>(FindObjectsSortMode.None))
        {
            var pose = pole.Snapshot;
            Check(Vector3.Distance(peer.transform.position, peer.OwnerClientId == pose.left ? pose.a : pose.b) < .02f,
                "Both pole motor positions agree with the replicated snapshot");
        }
        if (capture)
        {
            camera.enabled = true;
            camera.FaceSpawn(Quaternion.Euler(0, 30, 0));
            yield return null;
            ScreenCapture.CaptureScreenshot(ControlPath("pole-controls", host, "png"));
            yield return null;
        }
        yield return ControlBarrier("pole-constrained");
        if (host) pole.CancelRpc(pole.Snapshot.revision);
        yield return Until(() => !pole.IsAttached(player.OwnerClientId) && player.GetComponent<CharacterController>().enabled,
            "Leaving pole restores character controller");
    }

    IEnumerator MeasurePoleStride(CooperativePole pole, Key key, Vector3 direction, bool sprint)
    {
        var own = player.GetComponent<GameplayCharacter>();
        if (host)
        {
            var start = pole.Snapshot.PivotPoint;
            SetControlKeys(sprint ? new[] { key, Key.LeftShift } : new[] { key });
            yield return Until(() => Vector3.Dot(pole.Snapshot.PivotPoint - start, direction) > .14f &&
                (!sprint || own.SprintAllowed), "Pole stride starts");
            yield return new WaitForFixedUpdate();
            var before = pole.Snapshot.PivotPoint;
            float started = Time.fixedTime;
            float stamina = own.StaminaFraction;
            while (Time.fixedTime - started < .3f) yield return new WaitForFixedUpdate();
            float speed = Vector3.Dot(pole.Snapshot.PivotPoint - before, direction) / (Time.fixedTime - started);
            Check(Mathf.Abs(speed - (sprint ? 8.4f : 3.5f)) < .05f, $"Pole stride {speed:F3}m/s");
            if (sprint) Check(own.StaminaFraction < stamina, "Pole sprint spends stamina");
            SetControlKeys();
            yield return new WaitForSeconds(.35f);
        }
        yield return ControlBarrier(sprint ? "pole-sprint-measured" : "pole-walk-measured");
    }

    IEnumerator PoleKey(CooperativePole pole, string label, bool drives, Key key, Func<bool> observed)
    {
        if (drives) SetControlKeys(key);
        yield return Until(() => !pole.Snapshot.Paired || observed(), label, 4);
        if (drives) SetControlKeys();
        Check(pole.Snapshot.Paired, label + " kept both grips attached");
        yield return new WaitForSeconds(.35f);
        yield return ControlBarrier(label.Replace(' ', '-'));
    }

    IEnumerator ControlBarrier(string stage)
    {
        File.WriteAllText(ControlPath(stage, host), "ready");
        yield return Until(() => File.Exists(ControlPath(stage, !host)), stage + " peer synchronization");
    }

    string ControlPath(string stage, bool forHost, string extension = "txt") =>
        Path.Combine(evidence, $"{(forHost ? "host" : "client")}-{stage}.{extension}");

    void SetControlKeys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

    static void Box(string name, Vector3 center, Vector3 size)
    {
        var box = new GameObject(name).AddComponent<BoxCollider>();
        box.transform.position = center;
        box.size = size;
    }
}
#endif
