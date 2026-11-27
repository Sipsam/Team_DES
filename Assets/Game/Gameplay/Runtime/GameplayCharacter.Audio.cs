using TwinEdge.Audio;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class GameplayCharacter
    {
        Vector3 audioPosition;
        float audioSampleAt, strideDistance, airPeak, airStartHeight, airStartedAt, nextCharacterVoice, nextStroke;
        int audioLife;
        bool sampledGround, movementBaseline, jumpPending;
        MountainReturnLaunch audioLaunch;

        void BindAudio()
        {
            audioLaunch = GetComponent<MountainReturnLaunch>();
            if (audioLaunch) audioLaunch.State.OnValueChanged += PlayLaunch;
            movementBaseline = false;
            nextCharacterVoice = nextStroke = 0;
            attackStartedAt.OnValueChanged += PlaySwing;
            hitReactAt.OnValueChanged += PlayHit;
            held.OnValueChanged += PlayGrab;
            throwPoseUntil.OnValueChanged += PlayThrow;
        }

        void UnbindAudio()
        {
            if (audioLaunch) audioLaunch.State.OnValueChanged -= PlayLaunch;
            attackStartedAt.OnValueChanged -= PlaySwing;
            hitReactAt.OnValueChanged -= PlayHit;
            held.OnValueChanged -= PlayGrab;
            throwPoseUntil.OnValueChanged -= PlayThrow;
        }

        void PlaySwing(double before, double after)
        {
            if (after <= 0 || Now - after >= .45) return;
            PlayContestCue(AudioCueId.Swing);
            PlayCharacterVoice(AudioCueId.AttackVoice);
        }

        void PlayHit(double before, double after)
        {
            if (after <= 0 || Now - after >= .45) return;
            PlayContestCue(AudioCueId.Hit);
            PlayCharacterVoice(AudioCueId.HurtVoice);
        }

        void PlayGrab(ulong before, ulong after)
        {
            if (after != NoPlayer) PlayContestCue(AudioCueId.Grab);
        }

        void PlayThrow(double before, double after)
        {
            if (after <= Now) return;
            PlayContestCue(AudioCueId.Throw);
            PlayCharacterVoice(AudioCueId.EffortVoice);
        }

        void PlayContestCue(AudioCueId cue) =>
            contestGame.PlayLiveCue(cue, transform.position + Vector3.up);

        void PlayCharacterVoice(AudioCueId cue)
        {
            if (Time.unscaledTime < nextCharacterVoice || !contestGame.HasLiveAudio) return;
            if (AudioService.Play(cue, transform.position + Vector3.up * 1.5f))
                nextCharacterVoice = Time.unscaledTime + .48f;
        }

        void PlayLaunch(LaunchState before, LaunchState after)
        {
            switch (after)
            {
                case LaunchState.Charging: PlayContestCue(AudioCueId.LaunchCharge); break;
                case LaunchState.Flying:
                    PlayContestCue(AudioCueId.LaunchFire);
                    PlayCharacterVoice(AudioCueId.EffortVoice);
                    break;
                case LaunchState.Landing: PlayContestCue(AudioCueId.LaunchLand); break;
            }
        }

        void UpdateMovementAudio()
        {
            if (!contestGame.HasLiveAudio || !contestGame.CanPlay)
            {
                movementBaseline = false;
                return;
            }
            float now = Time.time;
            if (movementBaseline && now - audioSampleAt < .075f) return;
            Vector3 feet = transform.position;
            Vector3 delta = feet - audioPosition;
            float elapsed = now - audioSampleAt;
            bool reset = !movementBaseline || audioLife != RespawnCount.Value || elapsed > .3f || delta.sqrMagnitude > 16;
            movementBaseline = true;
            audioLife = RespawnCount.Value;
            audioPosition = feet;
            audioSampleAt = now;

            if (RiverGameplay.Current is { } river &&
                river.TrySeatPose(OwnerClientId, out var seat, out float stroke, out int side))
            {
                if (now >= nextStroke && Mathf.Abs(stroke) > .15f && river.TrySeat(OwnerClientId, out _, out _, out var hull) &&
                    hull.Velocity.sqrMagnitude > .16f)
                {
                    nextStroke = now + .68f;
                    contestGame.PlayLiveCue(AudioCueId.RaftStroke, seat.position + seat.rotation *
                        new Vector3(side == 0 ? -1 : 1, -.3f, 0));
                }
                ResetStride(feet.y);
                return;
            }
            if (reset || Attached || holder.Value != NoPlayer || disabledUntil.Value > Now ||
                audioLaunch && audioLaunch.ControlsMovement || IsOwner && ownerMotion != ContestMotion.Free)
            {
                ResetStride(feet.y);
                return;
            }

            float distance = new Vector2(delta.x, delta.z).magnitude;
            if (BridgeGameplay.Current?.Holds(OwnerClientId) == true)
            {
                if (distance > .045f && AccumulateStride(distance, 2.4f)) PlayContestCue(AudioCueId.TimberCarry);
                sampledGround = false;
                airStartedAt = float.PositiveInfinity;
                jumpPending = false;
                return;
            }

            // Remote support is represented by the existing replicated grounded animation.
            bool grounded = IsOwner ? contestMovement.IsGrounded : !IsAirMotion(DisplayMotion);
            if (!grounded)
            {
                if (sampledGround)
                {
                    airStartedAt = now;
                    airStartHeight = airPeak = feet.y - delta.y;
                    jumpPending = true;
                }
                if (jumpPending && feet.y - airStartHeight > .2f)
                {
                    contestGame.PlayLiveCue(AudioCueId.Jump, feet);
                    jumpPending = false;
                }
                if (delta.y < -.03f || now - airStartedAt > .2f) jumpPending = false;
                airPeak = Mathf.Max(airPeak, feet.y);
                strideDistance = 0;
            }
            else if (!sampledGround)
            {
                // A CharacterController can briefly lose contact between consecutive stairs.
                if (now - airStartedAt >= .22f && airPeak - feet.y > .42f)
                    contestGame.PlayLiveCue(AudioCueId.Land, feet);
                strideDistance = 0;
                jumpPending = false;
            }
            else if (distance / elapsed > .25f)
            {
                bool running = IsOwner ? distance / elapsed > 4f : DisplayMotion == Motion.Run;
                if (AccumulateStride(distance, running ? 2.5f : 1.5f))
                    contestGame.PlayLiveCue(AudioService.HasHardFooting(feet) ? AudioCueId.StepHard : AudioCueId.StepSoft, feet);
            }
            else strideDistance = 0;
            sampledGround = grounded;
        }

        bool AccumulateStride(float distance, float stride)
        {
            strideDistance += distance;
            if (strideDistance < stride) return false;
            strideDistance %= stride;
            return true;
        }

        void ResetStride(float height)
        {
            sampledGround = false;
            strideDistance = 0;
            airPeak = height;
            airStartedAt = float.PositiveInfinity;
            jumpPending = false;
        }
    }
}
