using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwinEdge.Gameplay
{
    public enum LaunchState : byte { Ready, Charging, Flying, Landing, Cooldown, Failed }

    // The existing owner transform stream transports movement. Only the server grants launch tickets.
    [DefaultExecutionOrder(-100)]
    public sealed partial class MountainReturnLaunch : NetworkBehaviour
    {
        public MountainLaunchConfig config;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DevelopmentInputDriver;
#endif
        public readonly NetworkVariable<LaunchState> State = new();
        public readonly NetworkVariable<uint> Revision = new();
        readonly NetworkVariable<float> chargeLevel = new();
        public readonly NetworkVariable<Vector3> InitialVelocity = new();
        public readonly NetworkVariable<Vector3> FlightVelocity = new(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<Unity.Collections.FixedString64Bytes> LastLanding = new();
        public readonly NetworkVariable<Unity.Collections.FixedString64Bytes> Failure = new();
        CharacterController controller;
        PlayerMovement movement;
        GameplayCharacter character;
        GameplayInput input;
        GameStateManager game;
        double began;
        readonly NetworkVariable<double> availableAt = new();
        Vector3 velocity, previous;
        bool preparePending;
        float prepareDeadline;
        uint applied;
        float yaw, pitch = 45, nextLandingRequest;
        bool returnCheckpoint;
        bool flight;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool debug;
#endif
        float nextLaunchHint;
        bool launchAvailable;
        string collision;
        public bool ControlsMovement => IsSpawned && (preparePending || State.Value == LaunchState.Charging || State.Value == LaunchState.Flying || State.Value == LaunchState.Landing || flight);
        public bool InFlight => IsSpawned && State.Value == LaunchState.Flying;
        public bool ReturnEnabled => IsSpawned && game != null && game.foundationMode && game.CanPlay && game.FoundationPhaseLabel == "Return";
        public float Charge => State.Value == LaunchState.Charging ? chargeLevel.Value : 0;
        internal bool ChargeAudioActive => IsOwner && ReturnEnabled && State.Value == LaunchState.Charging &&
            !chargeEndedLocally && !character.BlocksOwnerInteraction;
        void Awake()
        {
            controller = GetComponent<CharacterController>();
            movement = GetComponent<PlayerMovement>();
            character = GetComponent<GameplayCharacter>();
            input = GetComponent<GameplayInput>();
        }
        public override void OnNetworkDespawn() { flight = false; preparePending = false; HidePrediction(); movement.ResetVerticalVelocity(); }
        public override void OnLostOwnership() => HidePrediction();
        void OnDisable() => HidePrediction();
        public override void OnNetworkSpawn() { game = FindFirstObjectByType<GameStateManager>(); applied = Revision.Value; }
        static readonly Vector3[] SupportOffsets =
        {
            Vector3.zero, Vector3.right * .4f, Vector3.left * .4f, Vector3.forward * .4f, Vector3.back * .4f
        };
        readonly RaycastHit[] supportHits = new RaycastHit[16];

        bool Supported(out string summit) => Supported(transform.position, out summit);

        bool Supported(Vector3 feet, out string summit)
        {
            summit = PoleGeometry.Summit(feet, CooperativePole.Current?.mountain, controller.radius + .25f);
            if (summit == null) return false;
            foreach (var offset in SupportOffsets)
            {
                var origin = feet + offset + Vector3.up * .2f;
                int count = Physics.RaycastNonAlloc(origin, Vector3.down, supportHits, .6f, ~0, QueryTriggerInteraction.Ignore);
                var hits = supportHits;
                if (count == hits.Length)
                {
                    hits = Physics.RaycastAll(origin, Vector3.down, .6f, ~0, QueryTriggerInteraction.Ignore);
                    count = hits.Length;
                }
                bool supported = false;
                for (int i = 0; i < count && !supported; i++)
                    supported = hits[i].normal.y >= .85f && !hits[i].transform.IsChildOf(transform);
                if (!supported) return false;
            }
            return true;
        }
        bool Valid(RpcParams rpc, uint revision) => IsServer && rpc.Receive.SenderClientId == OwnerClientId && revision == Revision.Value && ReturnEnabled;
        [Rpc(SendTo.Server)]
        public void PrepareRpc(uint revision, RpcParams rpc = default)
        {
            if (!Valid(rpc, revision) || State.Value != LaunchState.Ready || character.BlocksServerInteraction || NetworkManager.ServerTime.Time < availableAt.Value ||
                CooperativePole.Current == null || CooperativePole.Current.IsAttached(OwnerClientId) || !Supported(out _)) return;
            began = NetworkManager.ServerTime.Time; chargeLevel.Value = 0; State.Value = LaunchState.Charging;
        }
        [Rpc(SendTo.Server)]
        public void FireRpc(uint revision, float direction, float elevation, float charge, RpcParams rpc = default)
        {
            if (!Valid(rpc, revision)) { Reject("Phase / owner / stale ticket"); return; }
            if (State.Value != LaunchState.Charging) { Reject("Not charging: " + State.Value); return; }
            if (!float.IsFinite(direction) || !float.IsFinite(elevation) || !float.IsFinite(charge)) { Reject("Nonfinite input"); return; }
            if (charge < 0 || charge > 1 || charge > Charge + .08f) { Reject($"Charge requested {charge:G3}, server {Charge:F3}"); return; }
            if (elevation < config.minPitch || elevation > config.maxPitch) { Reject("Pitch out of range"); return; }
            if (!Supported(out _)) { Reject("Launch support lost"); return; }
            InitialVelocity.Value = config.Velocity(direction % 360, elevation, charge);
            began = NetworkManager.ServerTime.Time;
            Revision.Value++; State.Value = LaunchState.Flying;
        }
        void Reject(string reason)
        {
            Failure.Value = reason;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"RETURN_REJECT owner={OwnerClientId} revision={Revision.Value} {reason}");
#endif
        }
        [Rpc(SendTo.Server)]
        public void CancelRpc(uint revision, RpcParams rpc = default)
        {
            if (!Valid(rpc, revision) || State.Value != LaunchState.Charging) return;
            ResetServer("Cancelled", config.cancelSeconds);
        }
        [Rpc(SendTo.Server)]
        void LandRpc(uint revision, RpcParams rpc = default)
        {
            if (!Valid(rpc, revision) || State.Value != LaunchState.Flying || NetworkManager.ServerTime.Time - began < .15 || !Supported(out var summit)) return;
            LastLanding.Value = summit; State.Value = LaunchState.Landing;
            availableAt.Value = NetworkManager.ServerTime.Time + config.landingSeconds;
        }
        public void BeforeRespawnServer() { if (IsServer) ResetServer("Respawn", config.cooldown); }
        public void ResetOwner() { if (IsSpawned && IsOwner) FlightVelocity.Value = Vector3.zero; flight = false; preparePending = false; chargeEndedLocally = true; HidePrediction(); velocity = Vector3.zero; nextLandingRequest = 0; movement.ResetVerticalVelocity(); }
        void ResetServer(string reason, float delay)
        {
            Revision.Value++; Failure.Value = reason; State.Value = LaunchState.Cooldown;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"RETURN_RECOVERY owner={OwnerClientId} {reason}");
#endif
            availableAt.Value = NetworkManager.ServerTime.Time + delay;
        }
        void Update()
        {
            if (!IsSpawned || config == null) { HidePrediction(); return; }
            if (IsServer)
            {
                if (State.Value == LaunchState.Charging) chargeLevel.Value = Mathf.Clamp01((float)(NetworkManager.ServerTime.Time - began) / config.chargeSeconds);
                if (!ReturnEnabled) returnCheckpoint = false;
                if (ReturnEnabled && !returnCheckpoint && Supported(out var at) && at == CooperativePole.Current.mountain.destination.id)
                {
                    FindFirstObjectByType<RespawnManager>().SetCheckpoint(movement, transform.position, transform.rotation, "Return / Final summit");
                    returnCheckpoint = true;
                }
                if (!ReturnEnabled && State.Value != LaunchState.Ready && State.Value != LaunchState.Cooldown) ResetServer("Phase changed", config.cooldown);
                if (State.Value == LaunchState.Flying && NetworkManager.ServerTime.Time - began > config.maxFlightSeconds)
                { ResetServer("Flight timeout / fall", config.cooldown); FindFirstObjectByType<RespawnManager>().RespawnPlayer(movement); }
                if (State.Value == LaunchState.Charging && (NetworkManager.ServerTime.Time - began > 15 || !Supported(out _))) ResetServer("Preparation lost support / timeout", config.cancelSeconds);
                if (State.Value == LaunchState.Landing && NetworkManager.ServerTime.Time >= availableAt.Value)
                { State.Value = LaunchState.Cooldown; availableAt.Value = NetworkManager.ServerTime.Time + config.cooldown; }
                if (State.Value == LaunchState.Cooldown && NetworkManager.ServerTime.Time >= availableAt.Value) State.Value = LaunchState.Ready;
            }
            if (!IsOwner) return;
            if (State.Value != LaunchState.Charging) chargeEndedLocally = false;
            if (State.Value != LaunchState.Ready || !ReturnEnabled || Time.unscaledTime >= prepareDeadline) preparePending = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var keys = Keyboard.current;
            if (keys != null && keys.f5Key.wasPressedThisFrame) debug = !debug;
#endif
            if (State.Value != LaunchState.Flying && flight) ResetOwner();
            if (!ReturnEnabled) { if (flight) ResetOwner(); HidePrediction(); return; }
            if (State.Value == LaunchState.Flying && applied != Revision.Value)
            { applied = Revision.Value; velocity = InitialVelocity.Value; previous = transform.position; flight = true; nextLandingRequest = 0; movement.ResetVerticalVelocity(); }
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevelopmentInputDriver) { HidePrediction(); return; }
#endif
            if (State.Value == LaunchState.Charging)
            {
                if (chargeEndedLocally) return;
                if (!input.AcceptsInput || character.BlocksOwnerInteraction || input.LaunchCancel.WasPressedThisFrame())
                {
                    chargeEndedLocally = true;
                    HidePrediction();
                    CancelRpc(Revision.Value);
                    return;
                }
                var camera = Camera.main;
                yaw = camera != null ? camera.transform.eulerAngles.y : transform.eulerAngles.y;
                pitch = Mathf.Clamp(pitch + input.Move.ReadValue<Vector2>().y * config.aimDegreesPerSecond * Time.deltaTime, config.minPitch, config.maxPitch);
                if (!input.Launch.IsPressed())
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    ReleasedPrediction = PredictLanding(config.Velocity(yaw, pitch, Charge));
#endif
                    chargeEndedLocally = true;
                    HidePrediction();
                    FireRpc(Revision.Value, yaw, pitch, Charge);
                    return;
                }
                if (controller.enabled) UpdatePrediction();
                else HidePrediction();
                return;
            }
            HidePrediction();
            if (State.Value == LaunchState.Ready && !character.BlocksOwnerInteraction && input.AcceptsInput && input.Launch.WasPressedThisFrame() && Supported(out _))
            {
                // Freeze local walking immediately; the server's grant may take a Relay round trip.
                preparePending = true; prepareDeadline = Time.unscaledTime + 2;
                PrepareRpc(Revision.Value);
            }
            if (preparePending && !Presentation.PresentationRoot.BlocksGameplay && input.LaunchCancel.WasPressedThisFrame()) { preparePending = false; CancelRpc(Revision.Value); }
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner || !flight || !controller.enabled) return;
            float remaining = Time.fixedDeltaTime;
            while (remaining > MountainLaunchConfig.SubstepEpsilon)
            {
                float dt = Mathf.Min(remaining, MountainLaunchConfig.MaxSubstep); remaining -= dt;
                var flags = controller.Move(config.Step(ref velocity, dt));
                if ((flags & CollisionFlags.Above) != 0) velocity.y = Mathf.Min(0, velocity.y);
                if ((flags & CollisionFlags.Sides) != 0) { velocity.x = 0; velocity.z = 0; }
                if (velocity.y < 0 && (flags & CollisionFlags.Below) != 0)
                {
                    velocity = Vector3.zero;
                    if (Supported(out _) && Time.time >= nextLandingRequest) { nextLandingRequest = Time.time + .2f; LandRpc(Revision.Value); }
                    break;
                }
            }
            FlightVelocity.Value = velocity;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debug) Debug.DrawLine(previous + Vector3.up, transform.position + Vector3.up, Color.cyan, 15);
#endif
            previous = transform.position;
        }
        // Diagnostic candidate only: exact ballistic height-plane crossing, without future collision/aim assistance.
        public static string EstimateCandidate(MountainLaunchConfig tuning, TwinEdge.Levels.MountainConfigV3 mountain, Vector3 feet, Vector3 initial, float margin)
        {
            if (tuning == null || mountain == null || tuning.gravity <= 0) return "none";
            float earliest = float.PositiveInfinity; string candidate = "none";
            foreach (var peak in new[] { mountain.start, mountain.destination }.Concat(mountain.smallPeaks))
            {
                float discriminant = initial.y * initial.y + 2 * tuning.gravity * (feet.y - peak.height);
                if (discriminant < 0) continue;
                float time = (initial.y + Mathf.Sqrt(discriminant)) / tuning.gravity;
                if (time <= 0 || time > tuning.maxFlightSeconds || time >= earliest) continue;
                var crossing = feet + initial * time + Vector3.down * (.5f * tuning.gravity * time * time);
                if (PoleGeometry.Summit(crossing, mountain, margin) != peak.id) continue;
                earliest = time; candidate = peak.id;
            }
            return candidate;
        }
        void OnControllerColliderHit(ControllerColliderHit hit) { if (flight) collision = hit.collider.name; }
        internal bool HasLaunchInput => preparePending || State.Value == LaunchState.Charging;

        internal string PresentationHint()
        {
            if (!ReturnEnabled || !IsOwner) return null;
            if (Time.unscaledTime >= nextLaunchHint)
            {
                nextLaunchHint = Time.unscaledTime + .15f;
                launchAvailable = Supported(out _);
            }
            if (!ControlsMovement && !launchAvailable) return null;
            if (InFlight) return "발사 중 · 착지 지점을 확인하세요.";
            if (State.Value != LaunchState.Charging || chargeEndedLocally)
                return "R을 길게 눌러 충전하고 떼어 발사하세요.";
            string landing = !Prediction.HasContact ? "착지점 미확인" :
                Prediction.Summit != null ? "봉우리 착지 예상" : "불안정한 착지 예상";
            return $"충전 {Charge:P0} · 고도 {pitch:F0}° · {landing}\n마우스 방향 · W/S 각도 · Esc 취소";
        }
    }
}
