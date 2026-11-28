#if DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public sealed partial class BridgeGameplay
    {
        internal enum GuardFixtureStage { Off, ObserveAdvance, DelaySample, ObserveResume, InvalidCurrent }

        internal GuardFixtureStage GuardFixture { get; set; }
        internal string GuardEvidence { get; set; }
        internal bool GuardRecorded { get; set; }
        ulong delayedHolder = LoadState.Empty;
        Vector3 delayedPosition;
        internal Vector3 GuardReplicaPosition => Player(delayedHolder).transform.position;

        Vector3 GuardFixturePosition(ulong id, Vector3 actual) =>
            GuardFixture is GuardFixtureStage.DelaySample or GuardFixtureStage.InvalidCurrent && id == delayedHolder
                ? delayedPosition : actual;

        void PrepareGuardFixture(LoadState current, LoadState next)
        {
            if (GuardFixture != GuardFixtureStage.DelaySample || delayedHolder != LoadState.Empty) return;
            bool right = current.right != NetworkManager.LocalClientId;
            Vector3 currentGrip = current.Grip(right, gripHalfWidth);
            Vector3 travel = next.Grip(right, gripHalfWidth) - currentGrip;
            if (travel.sqrMagnitude < .000001f) return;
            delayedHolder = right ? current.right : current.left;
            // Replay a server-observed position at the trailing edge of the valid interval.
            delayedPosition = currentGrip - travel.normalized * (2.5f - travel.magnitude * .5f);
        }

        internal void InvalidateGuardFixtureSample()
        {
            var current = load.Value;
            bool right = current.right != NetworkManager.LocalClientId;
            delayedHolder = right ? current.right : current.left;
            delayedPosition = current.Grip(right, gripHalfWidth) - Forward * 2.55f;
            GuardFixture = GuardFixtureStage.InvalidCurrent;
            GuardRecorded = false;
        }

        void RecordGuardFixture(string result, LoadState current, LoadState next)
        {
            if (GuardFixture == GuardFixtureStage.Off || GuardRecorded) return;
            bool currentLeft = HolderValid(current, false);
            bool currentRight = HolderValid(current, true);
            bool nextLeft = HolderValid(next, false);
            bool nextRight = HolderValid(next, true);
            bool expected = GuardFixture switch
            {
                GuardFixtureStage.ObserveAdvance or GuardFixtureStage.ObserveResume =>
                    result == "advanced" && currentLeft && currentRight && nextLeft && nextRight &&
                    Vector3.Distance(current.center, next.center) > .001f,
                GuardFixtureStage.DelaySample =>
                    result == "held" && currentLeft && currentRight && !(nextLeft && nextRight) && load.Value.center == current.center,
                GuardFixtureStage.InvalidCurrent =>
                    result == "returned" && !(currentLeft && currentRight),
                _ => false
            };
            File.AppendAllText(GuardEvidence, FormattableString.Invariant(
                $"serverTime={NetworkManager.ServerTime.Time:F5} stage={GuardFixture} branch={result} expected={expected} load={current.center:F4} candidate={next.center:F4} leftObserved={Player(current.left)?.transform.position:F4} rightObserved={Player(current.right)?.transform.position:F4} sampleHolder={delayedHolder} fixtureSample={delayedPosition:F4} currentL={current.Grip(false, gripHalfWidth):F4} currentR={current.Grip(true, gripHalfWidth):F4} nextL={next.Grip(false, gripHalfWidth):F4} nextR={next.Grip(true, gripHalfWidth):F4} currentValid={currentLeft}/{currentRight} nextValid={nextLeft}/{nextRight}\n"));
            GuardRecorded = expected;
        }
    }
}
#endif
