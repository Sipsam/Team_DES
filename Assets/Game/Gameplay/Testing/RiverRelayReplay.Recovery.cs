#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwinEdge.Gameplay
{
    public sealed partial class RiverRelayReplay
    {
        IEnumerator ValidateRecovery(GameStateManager game, NetworkManager manager)
        {
            var character = player.GetComponent<GameplayCharacter>();
            int initial = character.RespawnCount.Value;
            yield return HitRockRepeatedly(0, 5, manager);
            yield return Wait(() => river.Read(0).Status == RiverRaftStatus.Docked &&
                river.Read(0).Hits == 0 && character.RespawnCount.Value == initial + 1, "shared raft and rider recover together");
            Require(Vector3.Distance(player.transform.position, river.Landing(false, host ? 0 : 1).position) < 20,
                "rider returned to lower checkpoint");
            yield return new WaitForSeconds(.35f);
            Capture("recovered-lower");
            yield return FileBarrier("shared-recovered");
            if (host) game.RequestDevelopmentReturn();
            yield return Wait(() => game.Phase == FoundationPhase.Return &&
                river.Read(1).Status == RiverRaftStatus.Docked, "dev Return after recovery");

            // New test station. This scenario exercises damage thresholds, not crossing distance.
            var start = river.Landing(true, host ? 0 : 1);
            player.PlaceAtOwner(start.position, start.rotation);
            yield return new WaitForSeconds(.8f);
            initial = character.RespawnCount.Value;
            yield return Press(Key.E);
            yield return Wait(() => river.Read(1).Status == RiverRaftStatus.Sailing && river.Read(2).Status == RiverRaftStatus.Sailing,
                "both solo rafts launched");
            var unaffected = river.Read(2);
            if (host)
            {
                yield return HitRockRepeatedly(1, 3, manager);
                yield return Wait(() => river.Read(1).Status == RiverRaftStatus.Docked &&
                    river.Read(1).Hits == 0 && character.RespawnCount.Value == initial + 1, "only destroyed solo rider recovers");
                Require(Vector3.Distance(player.transform.position, river.Landing(true, 0).position) < 5, "rider returned to upper dock");
                yield return new WaitForSeconds(.35f);
                Capture("recovered-upper");
            }
            else
            {
                // Keep the second hull at the headwater while the adjacent racer takes damage.
                SetKeys(Key.S);
                for (int hit = 1; hit <= 3; hit++)
                {
                    int wanted = hit;
                    yield return Wait(() => river.Read(1).Hits == wanted, $"remote solo replicated hit {wanted}", 40);
                    yield return ValidateHitFlash(1, wanted, false);
                }
                Require(river.Read(1).Status == RiverRaftStatus.Destroyed, "remote solo destroyed");
                yield return Wait(() => river.Read(1).Status == RiverRaftStatus.Docked, "remote solo rebuilt");
                SetKeys(Key.W);
                float z = river.Read(2).Position.z;
                yield return Wait(() => river.Read(2).Position.z < z - 5, "unaffected racer can keep sailing");
                SetKeys();
                Require(character.RespawnCount.Value == initial, "unaffected rider did not respawn");
            }
            var other = river.Read(2);
            Require(other.Voyage == unaffected.Voyage && other.Hits == unaffected.Hits &&
                other.Status == RiverRaftStatus.Sailing, "other race raft damage and voyage preserved");
            yield return FileBarrier("solo-recovered");
            yield return ValidateReconnect(game, manager);
            File.WriteAllText(Path.Combine(output, peer + "-pass.txt"),
                "PASS. Real Relay, controlled rock placement, production keyboard strokes and physical hits. Five shared and three solo. Rider rejoined the live race. Station teleports are setup.");
            Application.Quit();
        }

        IEnumerator ValidateReconnect(GameStateManager game, NetworkManager manager)
        {
            var sessions = FindFirstObjectByType<MultiplayerSessionManager>();
            uint otherVoyage = river.Read(1).Voyage;
            byte otherHits = river.Read(1).Hits;
            if (host)
            {
                yield return Wait(() => game.ConnectedPlayerCount == 1 &&
                    river.Read(2).Status == RiverRaftStatus.Destroyed, "disconnect returns the abandoned solo raft", 30);
                yield return Wait(() => game.IsGameReady && river.Read(2).Status == RiverRaftStatus.Sailing,
                    "replacement connection boards the recovered solo raft", 100);
                Require(river.Read(1).Voyage == otherVoyage && river.Read(1).Hits == otherHits,
                    "peer rejoin preserved the surviving hull");
            }
            else
            {
                ulong firstId = player.OwnerClientId;
                string code = File.ReadAllText(Path.Combine(output, "room.txt"));
                var leave = sessions.LeaveRoomAsync();
                yield return Wait(() => leave.IsCompleted && !manager.IsListening, "client left Relay", 30);
                yield return new WaitForSeconds(2.5f);
                var join = sessions.JoinRoomAsync(code);
                yield return Wait(() => join.IsCompleted && sessions.HasSession, "client rejoined existing Relay room", 80);
                yield return Wait(() => game.IsGameReady && manager.LocalClient?.PlayerObject != null, "new owner synchronized", 80);
                player = manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
                Require(player.OwnerClientId != firstId, "rejoin has a new NGO connection ID");
                yield return Wait(() => Vector3.Distance(player.transform.position, river.Landing(true, 1).position) < 5,
                    "rejoin placed the new owner at the upper dock");
                var input = player.GetComponent<GameplayInput>();
                input.SimulateCapturedCursor = true;
                yield return Wait(() => input.AcceptsInput && river.Read(2).Status == RiverRaftStatus.Docked,
                    "rejoin screen returned gameplay input and the vacant raft is docked");
                yield return Press(Key.E);
                yield return Wait(() => river.TrySeat(player.OwnerClientId, out int slot, out _, out _) && slot == 2,
                    "rejoined owner can board their vacant slot");
            }
            yield return FileBarrier("reconnected");
        }

        IEnumerator HitRockRepeatedly(int index, int hits, NetworkManager manager)
        {
            bool shared = index == 0;
            for (int expected = 1; expected <= hits; expected++)
            {
                int wanted = expected;
                float deadline = Time.time + 36;
                while (river.Read(index).Hits < wanted && Time.time < deadline)
                {
                    var s = river.Read(index);
                    Vector3 target = river.RecoveryFixtureRock(shared) - s.Position;
                    target.y = 0;
                    float yaw = Quaternion.LookRotation(target).eulerAngles.y;
                    float error = Mathf.DeltaAngle(s.Heading, yaw);
                    if (shared)
                    {
                        bool left = s.Left == player.OwnerClientId;
                        bool preferred = error > 0 ? left : !left;
                        if (Mathf.Abs(error) > 9 && preferred) SetKeys(Key.W, Key.LeftShift);
                        else if (Mathf.Abs(error) > 20 && !preferred && Math.Floor(manager.ServerTime.Time * 5) % 3 == 0) SetKeys();
                        else SetKeys(Key.W);
                    }
                    else if (error > 6) SetKeys(Key.W, Key.D);
                    else if (error < -6) SetKeys(Key.W, Key.A);
                    else SetKeys(Key.W);

                    yield return new WaitForSeconds(.075f);
                }
                var hit = river.Read(index);
                Require(hit.Hits == wanted && hit.Contact == RiverContact.Rock,
                    $"physical rock contact {wanted}/{hits}, {hit.Position} {hit.Contact}");
                yield return ValidateHitFlash(index, wanted, true);
                if (wanted == 1 && shared)
                {
                    int blocked = river.RecoveryBlockedContacts(index);
                    SetKeys(Key.W);
                    yield return Wait(() => manager.ServerTime.Time - hit.DamagedAt >= .58, "first flash and cooldown window", 2);
                    Require(river.Read(index).Hits == wanted && river.Read(index).DamagedAt == hit.DamagedAt &&
                        !river.HullFlashVisible(index) && (!host || river.RecoveryBlockedContacts(index) > blocked),
                        "blocked rock contact during cooldown does not replay the flash");
                }
                if (wanted < hits && shared)
                {
                    SetKeys(Key.S);
                    yield return new WaitForSeconds(1.1f);
                }
            }
            SetKeys();
            Require(river.Read(index).Status == RiverRaftStatus.Destroyed, "threshold replicated destroyed");
            yield return new WaitForSeconds(.4f);
            Capture("destroyed");
        }

        IEnumerator ValidateHitFlash(int index, int hit, bool ownHud)
        {
            yield return Wait(() => river.HullFlashVisible(index), $"hull {index} rendered flash for hit {hit}", .25f);
            string hud = river.Hud(player, out bool damaged);
            Require(hud != null && damaged == ownHud, $"hull {index} hit {hit} owner HUD emphasis = {ownHud}");
            if (!screenshots || hit != 1) yield break;

            string label = index == 0 ? "linked-hit" : "solo-hit";
            Capture(label, river.Read(index).Position, new Vector3(4, 7, -8));
        }

        IEnumerator FileBarrier(string label)
        {
            File.WriteAllText(Path.Combine(output, $"{peer}-{label}.txt"), label);
            yield return Wait(() => File.Exists(Path.Combine(output, $"{(host ? "client" : "host")}-{label}.txt")), label);
        }
    }
}
#endif
