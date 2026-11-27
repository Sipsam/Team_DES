using TwinEdge.Audio;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class GameplayCharacter
    {
        readonly RaycastHit[] visualGroundHits = new RaycastHit[8];
        Vector3 lastShoePosition, previousFist;
        float lastShoeSample, strideForEffects, takeoffEffectUntil, landingEffectUntil, nextChargeEffect, nextTrail;
        double initialAttack;
        int shoeLife;
        bool shoeBaseline;
        Motion previousEffectMotion;

        void BindEffects()
        {
            initialAttack = attackStartedAt.Value;
            hitReactAt.OnValueChanged += OnImpact;
            if (audioLaunch) audioLaunch.State.OnValueChanged += OnLaunchEffect;
        }

        void UnbindEffects()
        {
            hitReactAt.OnValueChanged -= OnImpact;
            if (audioLaunch) audioLaunch.State.OnValueChanged -= OnLaunchEffect;
        }

        void OnImpact(double before, double after)
        {
            if (after > 0 && Now - after < .32)
                GameplayEffects.Emit(contestGame, GameplayEffects.Kind.Hit, transform.position + Vector3.up * .98f,
                    -transform.forward * .38f);
        }

        void OnLaunchEffect(LaunchState before, LaunchState after)
        {
            if (after == LaunchState.Flying && before == LaunchState.Charging)
                GameplayEffects.Emit(contestGame, GameplayEffects.Kind.Launch, transform.position + Vector3.up * .09f, -transform.forward);
            if (after == LaunchState.Landing && before == LaunchState.Flying)
                landingEffectUntil = Time.time + .22f;
        }

        void RenderAttackTrail()
        {
            Vector3 fist = contestJoints[5].position;
            double start = attackStartedAt.Value;
            double age = Now - start;
            if (start > 0 && start != initialAttack && contestPoseApplied && age >= AttackWindup - .045f &&
                age < AttackWindup + .09f && Time.time >= nextTrail && (fist - previousFist).sqrMagnitude is > .0025f and < 1f)
            {
                nextTrail = Time.time + .022f;
                GameplayEffects.Emit(contestGame, GameplayEffects.Kind.Swing, Vector3.Lerp(previousFist, fist, .55f), Vector3.zero);
            }
            previousFist = fist;
        }

        void RenderMovementEffects(bool seated)
        {
            Motion current = DisplayMotion;
            float now = Time.time;
            if (previousEffectMotion != current)
            {
                if (current == Motion.Takeoff)
                    takeoffEffectUntil = now + .12f;
                if (current == Motion.Land && IsAirMotion(previousEffectMotion))
                    landingEffectUntil = now + .2f;
                previousEffectMotion = current;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (GameplayEffects.Trace)
                    Debug.Log($"POLISH_MOTION {current} slot={LobbySlot} owner={IsOwner} time={Now:F3}");
#endif
            }

            if (seated || holder.Value != NoPlayer || !GameplayEffects.Enabled(contestGame))
            {
                shoeBaseline = false;
                takeoffEffectUntil = landingEffectUntil = 0;
                return;
            }

            Vector3 feet = transform.position;
            LaunchState launch = audioLaunch ? audioLaunch.State.Value : LaunchState.Ready;
            if (launch == LaunchState.Charging && now >= nextChargeEffect)
            {
                nextChargeEffect = now + .14f;
                GameplayEffects.Emit(contestGame, GameplayEffects.Kind.Charge, feet + Vector3.up * .16f,
                    transform.forward * .3f, .6f + audioLaunch.Charge * .4f);
            }
            if (shoeBaseline && now - lastShoeSample < .065f) return;
            float elapsed = now - lastShoeSample;
            Vector3 delta = feet - lastShoePosition;
            bool reset = !shoeBaseline || shoeLife != RespawnCount.Value || elapsed > .3f || delta.sqrMagnitude > 16;
            shoeBaseline = true;
            shoeLife = RespawnCount.Value;
            lastShoeSample = now;
            lastShoePosition = feet;
            if (reset)
            {
                strideForEffects = takeoffEffectUntil = landingEffectUntil = 0;
                return;
            }

            if (landingEffectUntil > now && !IsAirMotion(current) && TryEffectGround(feet, .5f, out var land))
            {
                landingEffectUntil = 0;
                bool strong = current == Motion.LaunchLand || launch == LaunchState.Landing;
                GameplayEffects.Emit(contestGame, strong ? GameplayEffects.Kind.Launch : GameplayEffects.Kind.Land,
                    land.point + land.normal * .045f, land.normal * .35f);
            }
            if (takeoffEffectUntil > now && TryEffectGround(feet - delta, .38f, out var takeoff))
            {
                takeoffEffectUntil = 0;
                EmitFootDust(takeoff, -transform.forward * .25f, 1.1f);
            }

            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            bool stride = current is Motion.Walk or Motion.Run or Motion.CarryWalk or Motion.CarryRun;
            if (!stride || !Attached && IsOwner && !contestMovement.IsGrounded || horizontal / elapsed < .28f)
            {
                strideForEffects = 0;
                return;
            }
            bool fast = current is Motion.Run or Motion.CarryRun;
            strideForEffects += horizontal;
            float spacing = fast ? 1.85f : 1.2f;
            if (strideForEffects < spacing || !TryEffectGround(feet, .24f, out var ground)) return;
            strideForEffects %= spacing;
            EmitFootDust(ground, -Vector3.ProjectOnPlane(delta, Vector3.up).normalized * .42f, fast ? 1.4f : .85f);
        }

        void EmitFootDust(RaycastHit ground, Vector3 direction, float strength)
        {
            GameplayEffects.Emit(contestGame, AudioService.HasHardFooting(ground.point) ? GameplayEffects.Kind.Stone : GameplayEffects.Kind.Dust,
                ground.point + ground.normal * .04f, direction, strength);
        }

        bool TryEffectGround(Vector3 feet, float depth, out RaycastHit surface)
        {
            surface = default;
            if (RiverGameplay.Current?.InWater(feet) == true) return false;
            int count = Physics.RaycastNonAlloc(feet + Vector3.up * .22f, Vector3.down, visualGroundHits,
                .22f + depth, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = visualGroundHits[i];
                if (hit.distance >= distance || hit.normal.y < .5f || !IsGround(hit)) continue;
                distance = hit.distance;
                surface = hit;
            }
            return distance < float.PositiveInfinity;
        }
    }
}
