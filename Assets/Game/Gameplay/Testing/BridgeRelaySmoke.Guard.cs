#if DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwinEdge.Gameplay
{
    public sealed partial class BridgeRelaySmoke
    {
        static readonly bool CarryGuard = Array.IndexOf(Environment.GetCommandLineArgs(), "-bridgeCarryGuard") >= 0;

        void PrepareGuardSpawns()
        {
            // Initial station spawn only. The server creates both players there through NGO.
            var spawns = V3GameplayBootstrap.Current.spawnPoints;
            for (int i = 0; i < spawns.Length; i++)
            {
                Vector3 side = Vector3.right * (i == 0 ? -bridge.GripHalfWidth : bridge.GripHalfWidth);
                spawns[i].SetPositionAndRotation(V3GameplayBootstrap.SafeGround(bridge.SupplyPoint + side), Quaternion.identity);
            }
        }

        IEnumerator RunCarryGuard(MultiplayerSessionManager sessions, NetworkManager manager)
        {
            var localPath = Path.Combine(output, peer + "-guard-capsule.txt");
            var capsule = player.GetComponent<CharacterController>();
            IEnumerator SampleCapsule()
            {
                using var writer = new StreamWriter(localPath) { AutoFlush = true };
                while (bridge.Load.Paired)
                {
                    writer.WriteLine(FormattableString.Invariant(
                        $"serverTime={manager.ServerTime.Time:F5} capsuleCenter={capsule.transform.TransformPoint(capsule.center):F4} feet={capsule.transform.position:F4} load={bridge.Load.center:F4}"));
                    yield return null;
                }
            }

            if (host) yield return Tap(Key.E);
            yield return Until(() => bridge.Load.supplied, "Supply approved through E");
            if (!host) yield return Until(() => bridge.Load.left != BridgeGameplay.LoadState.Empty, "Original left grip occupied");
            yield return Tap(Key.E);
            yield return Until(() => bridge.Load.Paired, "Both original grip requests accepted");
            if (RelayNetworkProbe.Current != null) RelayNetworkProbe.Current.Segment = "bridge-guard";
            StartCoroutine(SampleCapsule());
            yield return Barrier("guard-approach");
            if (host)
            {
                bridge.GuardEvidence = Path.Combine(output, "host-guard-branches.txt");
                bridge.GuardFixture = BridgeGameplay.GuardFixtureStage.ObserveAdvance;
            }

            Steer(bridge.Forward);
            if (host)
            {
                yield return Until(() => bridge.GuardRecorded, "Both valid next grips advanced timber");
                bridge.GuardRecorded = false;
                bridge.GuardFixture = BridgeGameplay.GuardFixtureStage.DelaySample;
                yield return Until(() => bridge.GuardRecorded, "Current valid and next invalid held timber");
                Vector3 oldReplica = bridge.GuardReplicaPosition;
                yield return Until(() => Vector3.Distance(oldReplica, bridge.GuardReplicaPosition) > .015f,
                    "New owner position was replicated while the timber held");
                bridge.GuardRecorded = false;
                bridge.GuardFixture = BridgeGameplay.GuardFixtureStage.ObserveResume;
                yield return Until(() => bridge.GuardRecorded, "Latest replicated position resumed timber movement");
                bridge.InvalidateGuardFixtureSample();
                yield return Until(() => bridge.GuardRecorded && !bridge.Load.supplied, "Invalid current grip returned timber");
                bridge.GuardFixture = BridgeGameplay.GuardFixtureStage.Off;
            }
            yield return Until(() => !bridge.Load.supplied && !bridge.Holds(player.OwnerClientId), "Return replicated");
            if (RelayNetworkProbe.Current != null) RelayNetworkProbe.Current.Segment = "transition";
            Steer(Vector3.zero);
            Check(bridge.CompletedDeliveries == 0, "No installation");
            yield return Barrier("guard-verified");
            if (RelayNetworkProbe.Current is { } probe)
            {
                probe.Automatic = false;
                probe.Restore();
            }
            yield return Barrier("guard-restored");
            if (host) yield return Until(() => manager.ConnectedClientsIds.Count == 1, "Client closed guard room", 45);
            var leave = sessions.LeaveRoomAsync();
            yield return Until(() => leave.IsCompleted && !manager.IsListening, "Guard session closed", 60);
            File.WriteAllText(Path.Combine(output, peer + "-guard-pass.txt"), "PASS deterministic server range fixture, normal input and real Relay.");
            Application.Quit(failed ? 2 : 0);
        }
    }
}
#endif
