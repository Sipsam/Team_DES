using TwinEdge.Audio;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class GameplayCharacter
    {
        void BindAudio()
        {
            attackStartedAt.OnValueChanged += PlaySwing;
            hitReactAt.OnValueChanged += PlayHit;
            held.OnValueChanged += PlayGrab;
            throwPoseUntil.OnValueChanged += PlayThrow;
        }

        void UnbindAudio()
        {
            attackStartedAt.OnValueChanged -= PlaySwing;
            hitReactAt.OnValueChanged -= PlayHit;
            held.OnValueChanged -= PlayGrab;
            throwPoseUntil.OnValueChanged -= PlayThrow;
        }

        void PlaySwing(double before, double after)
        {
            if (after > 0 && Now - after < .45) PlayContestCue(AudioCueId.Swing);
        }

        void PlayHit(double before, double after)
        {
            if (after > 0 && Now - after < .45) PlayContestCue(AudioCueId.Hit);
        }

        void PlayGrab(ulong before, ulong after)
        {
            if (after != NoPlayer) PlayContestCue(AudioCueId.Grab);
        }

        void PlayThrow(double before, double after)
        {
            if (after > Now) PlayContestCue(AudioCueId.Throw);
        }

        void PlayContestCue(AudioCueId cue) =>
            contestGame.PlayLiveCue(cue, transform.position + Vector3.up);
    }
}
