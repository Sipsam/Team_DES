using UnityEngine;

namespace TwinEdge.Gameplay
{
    // Receives the shared server clock from the cavern. The warning and support use one epoch.
    public sealed class UndergroundVanishPlatform : MonoBehaviour
    {
        GameObject deck, warning;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal bool SupportVisible => deck.activeSelf;
        internal bool WarningVisible => warning.activeSelf;
        internal Vector3 SupportTop => deck.transform.position;
#endif

        internal void Configure(GameObject surface, GameObject signal)
        {
            deck = surface;
            warning = signal;
        }

        internal void Present(bool enabled, double elapsed, float period, float warningSeconds, float absentSeconds)
        {
            double phase = elapsed % period;
            bool retracting = phase >= period - warningSeconds - absentSeconds && phase < period - absentSeconds;
            deck.SetActive(enabled && phase < period - absentSeconds);
            warning.SetActive(enabled && retracting);
        }
    }
}
