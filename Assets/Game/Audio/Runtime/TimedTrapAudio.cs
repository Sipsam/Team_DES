using UnityEngine;

namespace TwinEdge.Audio
{
    // Observes the same synchronized phase as the hazard visuals. The first snapshot
    // establishes a baseline, including when a client joins during an attack.
    internal struct TimedTrapAudio
    {
        bool initialized;
        double lastWarning, lastAttack;

        public void Seed(double now, double start, float warningSeconds)
        {
            initialized = true;
            lastWarning = start;
            double attackAt = start + warningSeconds;
            lastAttack = now >= attackAt ? attackAt : double.NegativeInfinity;
        }

        public void Observe(GameStateManager game, double now, double start, float warningSeconds,
            Vector3 warningPosition, AudioCueId attack, Vector3 attackPosition)
        {
            if (!game.HasLiveAudio || game.Phase == FoundationPhase.Finished)
            {
                initialized = false;
                return;
            }

            double attackAt = start + warningSeconds;
            if (!initialized)
            {
                Seed(now, start, warningSeconds);
                return;
            }
            if (double.IsNegativeInfinity(start)) return;

            if (now >= start && start > lastWarning)
            {
                lastWarning = start;
                if (now - start < .45) game.PlayLiveCue(AudioCueId.TrapWarning, warningPosition);
            }
            if (now >= attackAt && attackAt > lastAttack)
            {
                lastAttack = attackAt;
                if (now - attackAt < .45) game.PlayLiveCue(attack, attackPosition);
            }
        }
    }
}
