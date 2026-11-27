using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class GameplayCharacter
    {
        enum Motion : byte { Idle, Walk, Run, Air, CarryWalk, Takeoff, Fall, Land, LaunchLand, CarryRun }
        static readonly int IdleState = Animator.StringToHash("Idle");
        static readonly int WalkState = Animator.StringToHash("Walk");
        static readonly int RunState = Animator.StringToHash("Run");
        static readonly int JumpState = Animator.StringToHash("Jump");
        static readonly int CarryWalkState = Animator.StringToHash("Walk_Carry");
        static readonly int CarryRunState = Animator.StringToHash("Run_Carry");
        static readonly int PickupState = Animator.StringToHash("PickUp");
        static readonly int AirPose = Animator.StringToHash("AirPose");

        Vector3 sampledPosition;
        float sampledAt, airSince, airPeakHeight, groundHeight, landingUntil;
        bool motionBaseline, displayingAir;
        int sampledLife, visualState;
        float airPose, pickupUntil;

        Motion DisplayMotion => holder.Value != NoPlayer ? Motion.Idle : (Motion)motion.Value;
        static bool IsAirMotion(Motion value) => value is Motion.Takeoff or Motion.Air or Motion.Fall;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal bool LandingPoseForTest => DisplayMotion == Motion.Land;
#endif
        public string AnimationState => DisplayMotion switch
        {
            Motion.Walk => "Walk",
            Motion.Run => "Run",
            Motion.Takeoff or Motion.Air or Motion.Fall => "Jump",
            Motion.CarryWalk => "Walk_Carry",
            Motion.CarryRun => "Run_Carry",
            _ => "Idle"
        };

        void ResetMotionPresentation()
        {
            motionBaseline = displayingAir = false;
            airSince = airPeakHeight = groundHeight = landingUntil = pickupUntil = 0;
            visualState = 0;
            shoeBaseline = false;
            strideForEffects = takeoffEffectUntil = landingEffectUntil = 0;
            previousEffectMotion = DisplayMotion;
        }

        void SampleOwnerMotion(bool seated)
        {
            if (!IsOwner) return;
            float now = Time.time;
            float elapsed = now - sampledAt;
            if (motionBaseline && elapsed < .065f) return;
            Vector3 position = transform.position;
            Vector3 delta = position - sampledPosition;
            bool reset = !motionBaseline || sampledLife != RespawnCount.Value || elapsed > .3f || delta.sqrMagnitude > 16;
            float speed = reset ? 0 : new Vector2(delta.x, delta.z).magnitude / elapsed;
            float vertical = reset ? 0 : delta.y / elapsed;
            motionBaseline = true;
            sampledLife = RespawnCount.Value;
            sampledAt = now;
            sampledPosition = position;

            Motion ground = speed < .15f ? Motion.Idle : speed > 4f ? Motion.Run : Motion.Walk;
            var launch = audioLaunch ? audioLaunch.State.Value : LaunchState.Ready;
            var pole = CooperativePole.Current;
            var snapshot = pole && pole.IsSpawned ? pole.Snapshot : default;
            bool grippingPole = pole && pole.IsSpawned && snapshot.Has(OwnerClientId) && Attached;
            bool orbiting = grippingPole && snapshot.Paired && snapshot.Orbit == OwnerClientId && snapshot.phase == PolePhase.Rotating;

            Motion next;
            if (seated || holder.Value != NoPlayer || launch == LaunchState.Charging) next = Motion.Idle;
            else if (launch == LaunchState.Landing) next = Motion.LaunchLand;
            else if (launch == LaunchState.Flying || audioLaunch && audioLaunch.InFlight)
                next = audioLaunch.FlightVelocity.Value.y < -.5f ? Motion.Fall : Motion.Air;
            else if (orbiting) next = Motion.Air;
            else if (grippingPole) next = ground == Motion.Idle ? Motion.Idle : Motion.CarryWalk;
            else if (BridgeGameplay.Current?.Holds(OwnerClientId) == true)
                next = speed < .15f ? Motion.Idle : speed > 2.75f ? Motion.CarryRun : Motion.CarryWalk;
            else
            {
                if (carrying.Value && ground != Motion.Idle) ground = Motion.CarryWalk;
                motion.Value = (byte)GroundOrAir(ground, position.y, vertical, now, reset);
                return;
            }

            airSince = landingUntil = 0;
            groundHeight = airPeakHeight = position.y;
            motion.Value = (byte)next;
        }

        Motion GroundOrAir(Motion ground, float height, float vertical, float now, bool reset)
        {
            if (reset)
            {
                airSince = landingUntil = 0;
                groundHeight = airPeakHeight = height;
            }
            // Fast descents can clear the controller's grounded bit for several ticks while
            // the next tread is still directly underneath. Preserve an already confirmed flight.
            bool treadSupport = !contestMovement.IsGrounded && vertical < 1.4f &&
                ownerMotion != ContestMotion.Thrown && !IsAirMotion((Motion)motion.Value) &&
                TryEffectGround(transform.position, .26f, out _);
            if (!contestMovement.IsGrounded && !treadSupport)
            {
                if (airSince == 0)
                {
                    airSince = now;
                    airPeakHeight = groundHeight;
                }
                airPeakHeight = Mathf.Max(airPeakHeight, height);
                landingUntil = 0;
                float age = now - airSince;
                if (vertical > 1.4f && age < .12f) return Motion.Takeoff;
                if (ownerMotion == ContestMotion.Thrown || age >= .12f || height - groundHeight > .2f)
                    return vertical < -.75f ? Motion.Fall : Motion.Air;
                return ground;
            }

            // Require a sustained unsupported interval and a meaningful drop before showing compression.
            if (airSince > 0 && now - airSince >= .20f && airPeakHeight - height > .40f)
                landingUntil = now + .16f;
            airSince = 0;
            airPeakHeight = groundHeight = height;
            return now < landingUntil ? Motion.Land : ground;
        }

        internal void PresentGemPickup()
        {
            if (IsSpawned && DisplayMotion == Motion.Idle)
                pickupUntil = Time.time + .68f;
        }

        void RenderMotion(bool specialPose)
        {
            Motion display = DisplayMotion;
            bool pickup = !specialPose && display == Motion.Idle && Time.time < pickupUntil &&
                (!IsOwner || !contestMovement.IsGrounded || contestMovement.Velocity.sqrMagnitude < .5f);
            if (!pickup) pickupUntil = 0;
            int state = pickup ? PickupState : display switch
            {
                Motion.Walk => WalkState,
                Motion.Run => RunState,
                Motion.Takeoff or Motion.Air or Motion.Fall or Motion.Land or Motion.LaunchLand => JumpState,
                Motion.CarryWalk => CarryWalkState,
                Motion.CarryRun => CarryRunState,
                _ => IdleState
            };
            if (state == JumpState)
            {
                float target = display switch
                {
                    Motion.Takeoff => .18f,
                    Motion.Air => .43f,
                    Motion.Fall => .62f,
                    Motion.Land => .82f,
                    _ => .89f
                };
                airPose = displayingAir ? Mathf.MoveTowards(airPose, target, 3.8f * Time.deltaTime) : target;
                animator.SetFloat(AirPose, airPose);
            }
            displayingAir = state == JumpState;
            if (visualState == state) return;
            visualState = state;
            animator.speed = state == RunState ? PlayerMovement.SprintSpeedScale :
                state == PickupState ? 1.85f : state == CarryRunState ? .8f : 1f;
            animator.CrossFadeInFixedTime(state, .12f, 0);
        }
    }
}
