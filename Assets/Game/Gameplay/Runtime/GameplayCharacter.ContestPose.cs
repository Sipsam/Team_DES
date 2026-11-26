using System.Linq;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class GameplayCharacter
    {
        const float AttackDrawBack = .12f;
        const float AttackRecovery = .38f;
        const float HitReaction = .38f;
        const float ThrowPoseSeconds = .25f;

        Transform[] contestJoints;
        Quaternion[] animatedRotations;
        bool contestPoseApplied;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int contestPoseFrame = -2;
#endif

        // The imported controller supplies locomotion. These offsets affect only the rendered rig.
        void BindContestPose()
        {
            var joints = visualRoot.GetComponentsInChildren<Transform>();
            contestJoints = new[]
            {
                "UpperArm.L", "LowerArm.L", "Fist.L", "UpperArm.R", "LowerArm.R", "Fist.R",
                "UpperLeg.L", "LowerLeg.L", "Foot.L", "UpperLeg.R", "LowerLeg.R", "Foot.R"
            }.Select(name => joints.First(joint => joint.name == name)).ToArray();
            animatedRotations = new Quaternion[contestJoints.Length];
            leftArm = contestJoints[0];
            rightArm = contestJoints[3];
        }

        void RestoreContestPose()
        {
            if (!contestPoseApplied) return;
            for (int i = 0; i < contestJoints.Length; i++)
                contestJoints[i].localRotation = animatedRotations[i];
            contestPoseApplied = false;
        }

        void AimLimb(int first, Vector3 upperDirection, Vector3 lowerDirection, float weight)
        {
            AimJoint(contestJoints[first], contestJoints[first + 1], upperDirection, weight);
            AimJoint(contestJoints[first + 1], contestJoints[first + 2], lowerDirection, weight);
        }

        void AimJoint(Transform joint, Transform tip, Vector3 localDirection, float weight)
        {
            var delta = Quaternion.FromToRotation(tip.position - joint.position, transform.TransformDirection(localDirection));
            joint.rotation = Quaternion.Slerp(Quaternion.identity, delta, weight) * joint.rotation;
        }

        void RenderContest()
        {
            if (wasRowing) return;
            visualRoot.localRotation = visualRest.rotation;
            double now = Now;
            float attackAge = (float)(now - attackStartedAt.Value);
            float hitAge = (float)(now - hitReactAt.Value);
            bool attacking = attackStartedAt.Value > 0 && attackAge >= 0 && attackAge < AttackWindup + AttackRecovery;
            bool reacting = hitReactAt.Value > 0 && hitAge >= 0 && hitAge < HitReaction;
            bool throwing = throwPoseUntil.Value > now;
            if (holder.Value == NoPlayer && held.Value == NoPlayer && !attacking && !reacting && !throwing) return;

            for (int i = 0; i < contestJoints.Length; i++)
                animatedRotations[i] = contestJoints[i].localRotation;
            contestPoseApplied = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            contestPoseFrame = Time.frameCount;
#endif

            if (holder.Value != NoPlayer)
            {
                Tilt(17, 0, 9);
                AimLimb(0, new(-.65f, -.25f, .3f), new(-.5f, .75f, .35f), 1);
                AimLimb(3, new(.65f, -.25f, .3f), new(.5f, .75f, .35f), 1);
                AimLimb(6, new(-.22f, -.5f, .65f), new(0, -1, -.55f), 1);
                AimLimb(9, new(.22f, -.55f, .5f), new(0, -1, -.7f), 1);
            }
            else if (held.Value != NoPlayer)
            {
                Tilt(6);
                AimLimb(0, new(-.2f, -.35f, .9f), new(.18f, .6f, .8f), 1);
                AimLimb(3, new(.2f, -.35f, .9f), new(-.18f, .6f, .8f), 1);
            }
            else if (reacting)
            {
                float weight = 1 - Mathf.SmoothStep(0, 1, hitAge / HitReaction);
                Tilt(-26 * weight, -19 * weight, -12 * weight);
                AimLimb(0, new(-.8f, -.3f, -.25f), new(-.5f, .8f, -.3f), weight);
                AimLimb(3, new(.8f, -.3f, -.25f), new(.5f, .8f, -.3f), weight);
            }
            else if (throwing)
            {
                float weight = Mathf.Sin((float)(throwPoseUntil.Value - now) / ThrowPoseSeconds * Mathf.PI);
                AimLimb(3, new(.2f, .25f, .9f), new(0, .1f, 1), Mathf.Clamp01(weight));
            }
            else
            {
                float extension = Mathf.SmoothStep(0, 1, (attackAge - AttackDrawBack) / (AttackWindup - AttackDrawBack));
                float weight = Mathf.SmoothStep(0, 1, attackAge / AttackDrawBack) *
                    (1 - Mathf.SmoothStep(0, 1, (attackAge - AttackWindup) / AttackRecovery));
                Tilt(Mathf.Lerp(-8, 15, extension) * weight, Mathf.Lerp(32, -24, extension) * weight);
                AimLimb(3, Vector3.Lerp(new(.7f, -.2f, -.65f), new(-.15f, .08f, 1), extension),
                    Vector3.Lerp(new(.3f, .8f, .45f), Vector3.forward, extension), weight);
                AimLimb(0, new(-.25f, -.7f, .35f), new(.3f, .7f, .55f), weight);
            }
        }

        void Tilt(float pitch, float yaw = 0, float roll = 0) =>
            visualRoot.localRotation = Quaternion.Euler(pitch, yaw, roll) * visualRest.rotation;
    }
}
