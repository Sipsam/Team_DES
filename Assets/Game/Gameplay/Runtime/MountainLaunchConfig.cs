using UnityEngine;

namespace TwinEdge.Gameplay
{
    [CreateAssetMenu(menuName = "Twin Edge/Mountain Return Launch")]
    public sealed class MountainLaunchConfig : ScriptableObject
    {
        internal const float MaxSubstep = 1f / 120;
        internal const float SubstepEpsilon = .00001f;

        [Header("Unconfirmed tuning hypotheses: metres, seconds, degrees")]
        public float minSpeed = 10, maxSpeed = 29, chargeSeconds = 1.8f;
        public float minPitch = 20, maxPitch = 70, gravity = 20;
        public float landingSeconds = .25f, cooldown = .6f, cancelSeconds = .2f;
        public float maxFlightSeconds = 8, aimDegreesPerSecond = 45;
        // Zero intentionally: deterministic ballistic flight, no midair steering.
        [Range(0, 0)] public float airControl;
        void OnValidate()
        {
            minSpeed = Mathf.Max(1, minSpeed); maxSpeed = Mathf.Max(minSpeed, maxSpeed);
            chargeSeconds = Mathf.Max(.1f, chargeSeconds); gravity = Mathf.Max(1, gravity);
            minPitch = Mathf.Clamp(minPitch, 1, 85); maxPitch = Mathf.Clamp(maxPitch, minPitch, 85);
            landingSeconds = Mathf.Max(.05f, landingSeconds); cooldown = Mathf.Max(0, cooldown);
            cancelSeconds = Mathf.Max(0, cancelSeconds); maxFlightSeconds = Mathf.Max(1, maxFlightSeconds);
        }
        public Vector3 Velocity(float yaw, float pitch, float charge) =>
            Quaternion.Euler(-pitch, yaw, 0) * Vector3.forward * Mathf.Lerp(minSpeed, maxSpeed, charge);
        public Vector3 Step(ref Vector3 velocity, float dt)
        {
            var displacement = velocity * dt + Vector3.down * (.5f * gravity * dt * dt);
            velocity += Vector3.down * (gravity * dt);
            return displacement;
        }
    }
}
