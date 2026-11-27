#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwinEdge.Gameplay
{
    public sealed partial class BridgeRelaySmoke
    {
        const int FrameCapacity = 768;
        const int EventCapacity = 384;
        static BridgeRelaySmoke trace;
        bool traceEnabled;
        string hazardRoutePath, traceWindow, traceWait, traceKeys;
        NetworkManager traceManager;
        GameplayCharacter[] traceCharacters;
        readonly Queue<string> traceFrames = new();
        readonly Queue<string> traceEvents = new();
        Vector3 traceTarget, traceDirection;
        Vector2 traceStick;
        Collider traceTargetSupport, traceContact;
        int traceContactFrame = -1, fixedSteps, lastFrameFixedSteps;
        Vector3 traceContactPoint;

        internal static bool Tracing => trace != null && trace.traceWindow != null;

        void StartTrace(NetworkManager manager)
        {
            if (!traceEnabled) return;
            trace = this;
            traceManager = manager;
            traceCharacters = FindObjectsByType<GameplayCharacter>(FindObjectsSortMode.None)
                .OrderBy(character => character.OwnerClientId).ToArray();
            File.WriteAllText(Path.Combine(output, peer + "-trace-config.txt"),
                $"unity={Application.unityVersion} graphics={SystemInfo.graphicsDeviceType} window={Screen.width}x{Screen.height} " +
                $"fpsCap={Application.targetFrameRate} vsync={QualitySettings.vSyncCount} fixedDt={Time.fixedDeltaTime:R} " +
                $"maxDt={Time.maximumDeltaTime:R} networkTickRate={manager.NetworkConfig.TickRate} " +
                $"inputUpdate={InputSystem.settings.updateMode} peers={traceCharacters.Length}\n");
        }

        void BeginTrace(string label, Collider targetSupport, Vector3 target)
        {
            if (!traceEnabled) return;
            traceWindow = label;
            traceWait = "approach";
            traceTarget = target;
            traceTargetSupport = targetSupport;
            TraceEvent($"start target={target:F4} expectedSupport={Describe(targetSupport)}");
        }

        void SetTraceTarget(Vector3 target)
        {
            if (!Tracing) return;
            traceTarget = target;
            TraceEvent($"target={target:F4}");
        }

        void FixedUpdate()
        {
            if (traceEnabled) fixedSteps++;
        }

        void LateUpdate()
        {
            if (!traceEnabled) return;
            int steps = fixedSteps - lastFrameFixedSteps;
            lastFrameFixedSteps = fixedSteps;
            if (Tracing) SampleTrace(steps);
        }

        void SampleTrace(int steps)
        {
            if (player == null || bridge == null) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            var load = bridge.Load;
            int slot = bridge.TryGetReturnSlot(player.transform.position, out int current) ? current : -1;
            ulong remote = host ? traceCharacters.FirstOrDefault(character => character != null && !character.IsOwner)?.OwnerClientId ?? 0 : NetworkManager.ServerClientId;
            ulong rtt = traceManager.IsListening ? traceManager.NetworkConfig.NetworkTransport.GetCurrentRtt(remote) : 0;
            string authority = host ? bridge.CarryInputsForTrace() : "";
            var peers = string.Join(" | ", traceCharacters.Select(SamplePlayer));
            var row = Stamp() +
                $" stepsThisFrame={steps} rttMs={rtt} wait={traceWait} target={traceTarget:F4} direction={traceDirection:F4} stick={traceStick:F4} keys={traceKeys}" +
                $" slot={slot} targetSupport={Describe(traceTargetSupport)} collapsed={bridge.CollapseCountForTest}" +
                $" installed={bridge.InstalledCount} completed={bridge.CompletedDeliveries} revision={load.revision} supplied={load.supplied}" +
                $" left={load.left} right={load.right} center={load.center:F4} yaw={load.yaw:R} approvals={load.approvals} {authority} | {peers}";
            double sampleUs = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * (1_000_000.0 / System.Diagnostics.Stopwatch.Frequency);
            Enqueue(traceFrames, FrameCapacity, row + FormattableString.Invariant($" sampleUs={sampleUs:F1}"));
        }

        string SamplePlayer(GameplayCharacter character)
        {
            if (character == null) return "despawned";
            var movement = character.GetComponent<PlayerMovement>();
            var controller = character.GetComponent<CharacterController>();
            var feet = character.transform.position;
            // This is a geometric probe, also on the Host's replica of the Client.
            Physics.Raycast(feet + Vector3.up * .12f, Vector3.down, out var hit, .4f, ~0, QueryTriggerInteraction.Ignore);
            string ownerContact = movement.IsOwner
                ? $" contact={Describe(traceContact)} contactFrame={traceContactFrame} contactPoint={traceContactPoint:F4}"
                : "";
            var visual = character.transform.Find("VisualRoot");
            return $"id={character.OwnerClientId} owner={movement.IsOwner} feet={feet:F4} cc={controller.enabled}/{controller.bounds.center:F4}" +
                $" ccVelocity={movement.Velocity:F4} grounded={movement.IsGrounded} visual={(visual != null ? visual.position : feet):F4}" +
                $" ray={Describe(hit.collider)}{ownerContact} holds={bridge.Holds(character.OwnerClientId)} motion={character.AnimationState} staminaFraction={character.StaminaFraction:R}" +
                $" life={character.RespawnCount.Value} checkpoint={character.CheckpointName.Value}";
        }

        static string Describe(Collider collider) => collider == null ? "none" :
            $"{collider.name}#{collider.GetInstanceID()} enabled={collider.enabled} world={collider.transform.position:F4}" +
            $" bounds={collider.bounds.min:F4}/{collider.bounds.max:F4}";

        internal static void TraceContact(PlayerMovement movement, ControllerColliderHit hit)
        {
            if (!Tracing || movement != trace.player || hit.normal.y <= .4f) return;
            trace.traceContact = hit.collider;
            trace.traceContactFrame = Time.frameCount;
            trace.traceContactPoint = hit.point;
        }

        void TraceSteering(Vector3 direction, Vector2 stick)
        {
            traceDirection = direction;
            traceStick = stick;
            TraceEvent($"steer queued direction={direction:F4} stick={stick:F4}");
        }

        void TraceKeys(Key[] keys)
        {
            traceKeys = string.Join(",", keys);
            TraceEvent($"keys queued: {traceKeys}");
        }

        internal static void TraceEvent(FormattableString message)
        {
            if (Tracing) Enqueue(trace.traceEvents, EventCapacity, trace.Stamp() + " " + FormattableString.Invariant(message));
        }

        string Stamp()
        {
            string clocks = FormattableString.Invariant($"server={traceManager.ServerTime.Time:F6} local={traceManager.LocalTime.Time:F6} realtime={Time.realtimeSinceStartupAsDouble:F6} game={Time.timeAsDouble:F6}");
            string frame = FormattableString.Invariant($"frame={Time.frameCount} fixed={fixedSteps} dt={Time.deltaTime:R} frameDt={Time.unscaledDeltaTime:R} fixedDt={Time.fixedDeltaTime:R}");
            return clocks + " " + frame;
        }

        static void Enqueue(Queue<string> buffer, int limit, string row)
        {
            if (buffer.Count == limit) buffer.Dequeue();
            buffer.Enqueue(row);
        }

        void EndTrace()
        {
            if (!Tracing) return;
            SampleTrace(fixedSteps - lastFrameFixedSteps);
            var prefix = Path.Combine(output, peer + "-" + traceWindow);
            File.WriteAllLines(prefix + "-frames.txt", traceFrames);
            File.WriteAllLines(prefix + "-events.txt", traceEvents);
            traceFrames.Clear();
            traceEvents.Clear();
            traceWindow = null;
        }

        IEnumerator CaptureFailureAndQuit()
        {
            yield return new WaitForEndOfFrame();
            Texture2D texture = null;
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(output, peer + "-failure-screen.png"), texture.EncodeToPNG());
                // Hidden Windows players can return a black backbuffer.
                if (Camera.main is Camera camera) SaveCameraRender(peer + "-failure-camera", camera);
            }
            finally
            {
                Destroy(texture);
                Application.Quit(1);
            }
        }
    }
}
#endif
