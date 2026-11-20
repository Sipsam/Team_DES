#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwinEdge.Gameplay
{
    public sealed partial class CastleRelayReplay
    {
        IEnumerator RunCavernProbe()
        {
            var cave = UndergroundGameplay.Current;
            Require(cave != null && cave.IsBound, "saved underground gameplay bound");
            if (host)
            {
                string[] args = Environment.GetCommandLineArgs();
                int duration = Array.IndexOf(args, "-castleCavernSeconds");
                if (duration >= 0) cave.SetProbeSeconds(int.Parse(args[duration + 1]));
                castle.PrepareCavernProbe();
            }
            yield return Wait(() => castle.PartitionsOpen, "isolated cavern probe setup");
            player.PlaceAtOwner(cave.Entry + new Vector3(host ? -2 : 2, .15f, 0), Quaternion.identity);
            Debug.Log("CASTLE SETUP placed at cave start for isolated platform tuning");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-castleCavernHazards") >= 0)
            {
                yield return RunUndergroundHazardProbe(cave);
                yield break;
            }
            yield return TraverseUnderground(cave, isolated: true);
            File.WriteAllText(Path.Combine(output, role + "-cavern-pass.txt"), "PASS isolated cavern route");
            yield return Rendezvous("cavern-finished");
            yield return new WaitForSeconds(1);
            Application.Quit();
        }

        IEnumerator RunUndergroundHazardProbe(UndergroundGameplay cave)
        {
            yield return Rendezvous("hazards-start");
            if (host)
            {
                for (int attack = 0; attack < 3; attack++)
                {
                    Vector3 feet = cave.ProbeAttackFeet(attack);
                    player.PlaceAtOwner(feet, Quaternion.identity);
                    yield return Wait(() => cave.ProbeSlowDeadline > cave.Now, $"actual hit by {cave.ProbeAttackName(attack)}", 13);
                    double hitUntil = cave.ProbeSlowDeadline;
                    Require(cave.SpeedScale(player) < 1, "hit slows the character");
                    yield return new WaitForSeconds(.35f);
                    Require(cave.ProbeSlowDeadline == hitUntil &&
                        Vector3.ProjectOnPlane(player.transform.position - feet, Vector3.up).sqrMagnitude > .001f,
                        "actual knockback and one application in the attack window");
                    yield return Wait(() => cave.SpeedScale(player) == 1, "movement speed recovers");
                }

                float prior = cave.ProbeMovingFraction;
                bool readyToBoard = false;
                yield return Wait(() =>
                {
                    float fraction = cave.ProbeMovingFraction;
                    readyToBoard |= fraction > prior && fraction is > .60f and < .70f;
                    prior = fraction;
                    return readyToBoard;
                }, "moving stone emerges from its wall", 10);
                Vector3 platform = cave.ProbeMovingPosition;
                player.PlaceAtOwner(platform + Vector3.up * .31f, Quaternion.identity);
                yield return new WaitForSeconds(.65f);
                Require(player.IsGrounded && player.transform.position.x - platform.x > 1f &&
                    Mathf.Abs(player.transform.position.x - cave.ProbeMovingPosition.x) < .85f,
                    "CharacterController rides the moving collider");

                yield return Wait(() => cave.ProbeVanishVisible && !cave.ProbeVanishWarning,
                    "independent vanish collider available", 10);
                player.PlaceAtOwner(cave.ProbeVanishTop + Vector3.up * .1f, Quaternion.identity);
                yield return Wait(() => player.IsGrounded && cave.ProbeVanishWarning, "warning while standing on the trap stone", 10);
                int old = character.RespawnCount.Value;
                yield return Wait(() => !cave.ProbeVanishVisible, "independent trap collider retracts");
                yield return Wait(() => character.RespawnCount.Value > old, "normal gravity from the trap collider reaches lava");
            }
            yield return Rendezvous("hazards-passed");
            File.WriteAllText(Path.Combine(output, role + "-hazards-pass.txt"), "PASS attacks, carry, warning, and trap support");
            yield return new WaitForSeconds(1);
            Application.Quit();
        }

        IEnumerator TraverseUnderground(UndergroundGameplay cave, bool isolated = false)
        {
            Require(cave != null && cave.IsBound, "saved underground gameplay bound");
            if (!isolated)
            {
                yield return Walk(0, 247.5f);
                yield return Walk(20, 248);
                foreach (var point in cave.Stairs)
                    yield return Walk(point.x, point.z, sprint: true);
                yield return Walk(40, 240);
            }
            Require(Mathf.Abs(player.transform.position.y - cave.Entry.y) < 1, "physical descent reaches the lava start");
            fullMatch?.Capture("cavern-entry");
            Capture("cavern-entry");
            CaptureView("cavern-overview", cave.Entry + new Vector3(-8, 6, 1), cave.Entry + new Vector3(-12, -4, -55));
            yield return Split(cave);
            yield return Rendezvous("at-start");

            if (!isolated && fullMatch == null) yield return CheckLavaExpiry(cave);

            for (int round = 0; round < 8; round++)
            {
                int index = round;
                int receiver = index % 2 == 0 ? 1 : 0;
                double deadline;
                if (slot != receiver)
                {
                    yield return UseUnderground(cave.Station(cave.SwitchFor(index)));
                    yield return Wait(() => cave.GroupActive(index), $"switch activates opposite group {index + 1}");
                    deadline = cave.Deadline(index);
                    if (index == 0)
                    {
                        SetKeys(Key.E);
                        yield return new WaitForSeconds(.13f);
                        SetKeys();
                        Require(cave.Deadline(index) == deadline, "repeated E preserves the original expiry");
                    }
                }
                else
                {
                    yield return Wait(() => cave.GroupActive(index), $"partner activated group {index + 1}");
                    deadline = cave.Deadline(index);
                    Vector3 start = cave.Landing(slot, index / 2);
                    yield return WalkPoint(start);
                    foreach (var deck in cave.GroupDecks(index)) yield return JumpTo(deck.Top);
                    int destination = index / 2 + 1;
                    Vector3 landing = destination == 4 ? cave.EndOfRoute(slot) : cave.Landing(slot, destination);
                    yield return JumpTo(landing, cave.ReplayLandingSupport(slot, destination));
                    Require(cave.Now <= cave.Deadline(index), $"physical crossing {index + 1} completed before deadline");
                }
                yield return Wait(() => cave.Step > index, $"server registers arrival for crossing {index + 1}", 36);
                Debug.Log($"CAVERN_DURATION group={index + 1} remaining={deadline - cave.Now:F2}s");
            }

            yield return Wait(() => game.Phase == FoundationPhase.SealPending && cave.Seal == UndergroundSeal.ReadyForCooperation,
                "both players at altar while still in Cooperation");
            var gem = GemOwnership.Current;
            bool testReconnect = isolated && Array.IndexOf(Environment.GetCommandLineArgs(), "-castleCavernReconnect") >= 0;
            if (fullMatch == null)
            {
                if (host) gem.RequestAcquire();
                yield return Rendezvous("sealed-attempt");
                Require(gem.State == GemState.Sealed && game.CurrentState == GameState.Cooperation, "sealed gem refused");
                if (host)
                {
                    yield return UseUnderground(cave.SealPad(slot));
                    yield return Wait(() => cave.Participants == 1, "one seal participant");
                    yield return new WaitForSeconds(3);
                    Require(cave.Seal == UndergroundSeal.ReadyForCooperation, "one player cannot unseal");
                    SetKeys(Key.Escape);
                    yield return new WaitForSeconds(.15f);
                    SetKeys();
                    yield return Wait(() => cave.Participants == 0, "Escape cancels participation");
                }
                yield return Rendezvous("seal-cancel");
            }
            if (testReconnect)
            {
                yield return UseUnderground(cave.SealPad(slot));
                yield return Wait(() => cave.Seal == UndergroundSeal.Unsealing, "seal started before disconnect probe");
                if (!host) yield return RejoinCavern();
                else
                {
                    yield return Wait(() => !game.IsGameReady && cave.Participants == 0, "disconnect cancels the shared channel");
                    Require(gem.State == GemState.Sealed, "interrupted seal keeps the gem sealed");
                    yield return Wait(() => game.IsGameReady, "partner reconnected after seal interruption", 90);
                }
                yield return Wait(() => cave.Seal == UndergroundSeal.ReadyForCooperation && cave.Step == 8,
                    "rejoined player restored altar and completed crossings");
                yield return Rendezvous("seal-rejoined");
            }
            yield return UseUnderground(cave.SealPad(slot));
            yield return Wait(() => cave.Seal == UndergroundSeal.Unsealing || cave.Seal == UndergroundSeal.Unsealed,
                "both peers joined the seal");
            yield return Wait(() => cave.Seal == UndergroundSeal.Unsealed && gem.State == GemState.Available,
                "joint seal opens and exposes the gem");
            Require(game.Phase == FoundationPhase.SealPending, "unsealing leaves Return pending");
            Capture("altar-unsealed");
            CaptureView("altar-gem", cave.Altar + new Vector3(5, 3, 5), cave.Altar + Vector3.up);

            yield return WalkPoint(cave.Altar + Vector3.right * (host ? -1.1f : 1.1f));
            yield return Rendezvous("gem-ready");
            if (host || (!testReconnect && fullMatch == null))
            {
                SetKeys(Key.E);
                yield return new WaitForSeconds(.16f);
                SetKeys();
            }
            yield return Wait(() => game.Phase == FoundationPhase.Return && gem.State == GemState.Carried, "one server-approved E pickup starts Return");
            bool carrier = gem.Owner == player.OwnerClientId;
            File.WriteAllText(Path.Combine(output, role + "-carrier.txt"), gem.Owner.ToString());
            yield return Rendezvous("carrier-seen");
            Require(File.ReadAllText(Path.Combine(output, "host-carrier.txt")) == File.ReadAllText(Path.Combine(output, "client-carrier.txt")),
                "both Relay peers see the same carrier");
            Require(cave.AllGroupsPermanent, "all eight cooperative groups physically present in Return");
            if (fullMatch != null)
            {
                fullMatch.Capture("altar-gem-carried");
                yield return fullMatch.Barrier("seal-and-gem");
                yield return CrossReturn(cave);
            }
            else
            {
                yield return CheckReturnHazards(cave);

                int returnDeaths = character.RespawnCount.Value;
                if (carrier)
                {
                    yield return WalkPoint(cave.Altar + Vector3.forward * 12);
                    yield return Wait(() => character.RespawnCount.Value > returnDeaths, "carrier falls and respawns at the seal");
                    Require(Vector3.Distance(player.transform.position, cave.Altar) < 6, "player respawn stays at altar");
                }
                yield return Wait(() => gem.State == GemState.Relocated && Vector3.Distance(gem.Position, cave.Entry) < 1.5f,
                    "gem relocates independently to the lava start");
                Require(game.Phase == FoundationPhase.Return, "carrier death preserves Return");

                yield return CrossReturn(cave);
                if (!carrier)
                {
                    yield return UseUnderground(cave.Station(11));
                    yield return Wait(() => gem.Owner == player.OwnerClientId, "the partner recovers the relocated gem");
                }
                yield return Wait(() => gem.State == GemState.Carried, "relocated gem owned again");
                Require(character.RespawnCount.Value == returnDeaths + (carrier ? 1 : 0), "reverse cavern was traversed without additional deaths");
                if (testReconnect)
                {
                    yield return Rendezvous("gem-disconnect-start");
                    if (!host) yield return RejoinCavern();
                    else
                    {
                        yield return Wait(() => !game.IsGameReady && gem.State == GemState.Relocated, "carrier disconnect releases and relocates gem");
                        yield return Wait(() => game.IsGameReady, "partner reconnected after gem ownership", 90);
                    }
                    yield return Wait(() => cave.Seal == UndergroundSeal.Unsealed && cave.AllGroupsPermanent &&
                        game.Phase == FoundationPhase.Return && gem.State == GemState.Relocated, "reconnect preserves return and relocated gem");
                    yield return Rendezvous("gem-rejoined");
                    if (host)
                    {
                        yield return UseUnderground(cave.Station(11));
                        yield return Wait(() => gem.Owner == player.OwnerClientId, "gem can be reclaimed after carrier disconnect");
                    }
                    yield return Wait(() => gem.State == GemState.Carried, "new carrier replicates after reconnect");
                    if (!host)
                    {
                        // The returning reconnect checkpoint is the seal terrace. Cross the route again.
                        yield return CrossReturn(cave);
                    }
                }
            }
            Capture("cavern-return");
            var stairs = cave.Stairs;
            for (int i = stairs.Length - 1; i >= 0; i--)
                yield return Walk(stairs[i].x, stairs[i].z, sprint: true);
            yield return Walk(0, 247.5f);
            Require(Mathf.Abs(player.transform.position.y - castle.World(0, 0, 0).y) < 1, "physical return ascends to the castle door");
            yield return Walk(host ? 2.3f : -2.3f, 241.5f);
            yield return Rendezvous("underground-exit");
        }

        IEnumerator CrossReturn(UndergroundGameplay cave)
        {
            for (int stage = 3; stage >= 0; stage--)
            {
                int group = stage * 2 + (slot == 0 ? 1 : 0);
                yield return WalkPoint(stage == 3 ? cave.EndOfRoute(slot) : cave.Landing(slot, stage + 1));
                // Permanent Return decks allow a normal stamina break on each fixed landing.
                yield return Wait(() => character.StaminaFraction >= .95f, "stamina recovered on fixed Return landing", 14);
                var decks = cave.GroupDecks(group);
                for (int i = decks.Count - 1; i >= 0; i--)
                    yield return JumpTo(decks[i].Top);
                yield return JumpTo(cave.Landing(slot, stage), cave.ReplayLandingSupport(slot, stage));
            }
            yield return Walk(slot == 0 ? -17 : 47, 240);
            yield return Walk(slot == 0 ? 37.7f : 42.3f, 240);
        }

        IEnumerator RejoinCavern()
        {
            ulong original = manager.LocalClientId;
            manager.Shutdown();
            var leave = sessions.LeaveRoomAsync();
            yield return Wait(() => leave.IsCompleted && !manager.IsListening, "cavern client disconnected", 30);
            yield return new WaitForSeconds(1);
            yield return Join();
            yield return BindPlayer();
            Require(manager.LocalClientId != original, "new client ID assigned on rejoin");
        }

        IEnumerator CheckReturnHazards(UndergroundGameplay cave)
        {
            Vector3 movingStart = cave.ProbeMovingPosition;
            bool sawVisible = false, sawRetracted = false, sawAttack = false, sawWarning = false, sawMovement = false;
            double until = cave.Now + 18;
            while (cave.Now < until && !(sawVisible && sawRetracted && sawAttack && sawWarning && sawMovement))
            {
                sawVisible |= cave.ProbeVanishVisible;
                sawRetracted |= !cave.ProbeVanishVisible;
                sawAttack |= cave.ProbeAttackVisible;
                sawWarning |= cave.ProbeWarningVisible;
                sawMovement |= Vector3.Distance(cave.ProbeMovingPosition, movingStart) > .5f;
                yield return null;
            }
            Require(sawVisible && sawRetracted && sawAttack && sawWarning && sawMovement && cave.AllGroupsPermanent,
                "moving, warning, attack and vanish cycles continue alongside permanent supports");
        }

        IEnumerator CheckLavaExpiry(UndergroundGameplay cave)
        {
            // Stand on a timed support through its original deadline and fall through to lava.
            int oldDeaths = character.RespawnCount.Value;
            if (host) yield return UseUnderground(cave.Station(0));
            yield return Wait(() => cave.GroupActive(0), "first opposing group activated");
            double expiry = cave.Deadline(0);
            if (!host)
            {
                yield return WalkPoint(cave.Landing(1, 0));
                for (int i = 0; i < 4; i++)
                {
                    var deck = cave.GroupDecks(0)[i];
                    yield return JumpTo(deck.Top);
                }
                yield return Wait(() => !cave.GroupActive(0), "server deadline retracts the occupied support", 34);
                Require(!cave.GroupDecks(0)[3].Object.activeInHierarchy && cave.Deadline(0) == expiry, "expiry removes collider and view");
            }
            yield return Wait(() => character.RespawnCount.Value > oldDeaths, "lava contact resets reciprocal traversal", 40);
            yield return Split(cave);
            yield return Rendezvous("retry");
        }

        IEnumerator Split(UndergroundGameplay cave)
        {
            yield return Walk(slot == 0 ? -17 : 47, 240);
            yield return WalkPoint(cave.Landing(slot, 0));
        }

        IEnumerator WalkPoint(Vector3 point)
        {
            Vector3 local = point - castle.World(0, 0, 0);
            yield return Walk(local.x, local.z);
        }

        IEnumerator UseUnderground(UndergroundStation station)
        {
            yield return WalkPoint(station.transform.position);
            yield return Wait(() => player.GetComponent<PlayerInteraction>().CurrentTarget is UndergroundStation target && target == station,
                $"actual target {station.Prompt} at {station.transform.position:F2}", 4);
            SetKeys(Key.E);
            yield return new WaitForSeconds(.15f);
            SetKeys();
            yield return new WaitForSeconds(.10f);
        }

        IEnumerator JumpTo(Vector3 top, Collider fixedShelf = null)
        {
            yield return Wait(() => player.IsGrounded && UndergroundGameplay.Current.SpeedScale(player) >= 1,
                $"grounded and ready to jump towards {top:F2}", 2.5f);
            Vector3 start = player.transform.position;
            // Press Space from confirmed support before advancing toward the next waypoint.
            // Losing ground over the edge alone is not evidence of an accepted jump.
            for (int attempt = 0; attempt < 2 && player.transform.position.y < start.y + .055f; attempt++)
            {
                SetKeys();
                yield return new WaitForSeconds(.04f);
                SetKeys(Key.Space, Key.LeftShift);
                float takeoffDeadline = Time.realtimeSinceStartup + .27f;
                while (player.transform.position.y < start.y + .055f && Time.realtimeSinceStartup < takeoffDeadline)
                    yield return null;
            }
            Require(player.transform.position.y >= start.y + .055f, $"Space did not take off from {start:F2} toward {top:F2}; feet={player.transform.position:F2}, grounded={player.IsGrounded}, stamina={character.StaminaFraction:P0}, {character.StaminaTuningForTest}", log: false);
            float started = Time.realtimeSinceStartup;
            do
            {
                Vector3 delta = Vector3.ProjectOnPlane(top - player.transform.position, Vector3.up);
                var keys = new List<Key>();
                if (delta.magnitude > .12f)
                {
                    keys.Add(Key.LeftShift);
                    Vector3 direction = delta.normalized;
                    var camera = Camera.main.transform;
                    float forward = Vector3.Dot(direction, Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized);
                    float right = Vector3.Dot(direction, Vector3.ProjectOnPlane(camera.right, Vector3.up).normalized);
                    if (Mathf.Abs(forward) > .36f) keys.Add(forward > 0 ? Key.W : Key.S);
                    if (Mathf.Abs(right) > .36f) keys.Add(right > 0 ? Key.D : Key.A);
                }
                SetKeys(keys.ToArray());
                yield return null;
                Require(Time.realtimeSinceStartup - started < 2 && player.transform.position.y > top.y - 1,
                    $"jump from {start:F2} missed {top:F2}, feet at {player.transform.position:F2}", log: false);
            } while (!player.IsGrounded);
            SetKeys();
            Vector3 offset = player.transform.position - top;
            // A timed waypoint may overlap the preceding shelf. The arrival at a fixed shelf
            // is registered against that shelf's collider, including the walkable edge of its footprint.
            bool reachedTarget = fixedShelf == null
                ? offset.y > -.65f && offset.y < 1.1f && Vector3.ProjectOnPlane(offset, Vector3.up).magnitude < 1.65f
                : fixedShelf.Raycast(new Ray(player.transform.position + Vector3.up * .22f, Vector3.down),
                    out var contact, .57f) && contact.normal.y > .65f;
            Require(reachedTarget, $"jump from {start:F2} towards {top:F2} landed at {player.transform.position:F2}", log: false);
            yield return null;
            if (Mathf.Abs(offset.y) < .45f && Vector3.ProjectOnPlane(offset, Vector3.up).sqrMagnitude > .33f * .33f)
                yield return WalkPoint(top);
        }

        IEnumerator Rendezvous(string mark)
        {
            File.WriteAllText(Path.Combine(output, role + "-" + mark + ".ready"), "");
            yield return Wait(() => File.Exists(Path.Combine(output, (host ? "client" : "host") + "-" + mark + ".ready")),
                "both peers reached " + mark, 100);
        }
    }

    public sealed partial class CastleGameplay
    {
        internal void PrepareCavernProbe()
        {
            if (!IsServer) return;
            stage.Value = Stages;
            accepted.Value = crossed.Value = Both;
        }
    }

    public sealed partial class UndergroundGameplay
    {
        internal void SetProbeSeconds(int seconds)
        {
            if (IsServer) platformSeconds = Mathf.Clamp(seconds, 20, 30);
        }

        internal Vector3 ProbeAttackFeet(int index) => World(traps[index].Origin) + new Vector3(.23f, .1f, -.18f);
        internal string ProbeAttackName(int index) => traps[index].Kind.ToString();
        internal double ProbeSlowDeadline => slowedUntil[0];
        internal Vector3 ProbeMovingPosition => moving[0].bounds.center;
        internal float ProbeMovingFraction => (ProbeMovingPosition.x - movingStarts[0].x) / movingTravel[0].x;
        internal Vector3 ProbeVanishTop => vanishing[0].SupportTop;
        internal bool ProbeVanishWarning => vanishing[0].WarningVisible;
        internal bool ProbeVanishVisible => vanishing[0].SupportVisible;
        internal bool ProbeAttackVisible => System.Array.Exists(traps, t => t.Attack.gameObject.activeSelf);
        internal bool ProbeWarningVisible => System.Array.Exists(traps, t => t.Warning.activeSelf);
        internal Vector3[] Stairs => config.stairs;
        internal int SwitchFor(int index) => SwitchOrder[index];
        internal UndergroundStation SealPad(int slot) => stations[SealStation + slot];
        internal Collider ReplayLandingSupport(int lane, int stage) => stage == Stages ? altarSurface : landingSurfaces[lane, stage];
        internal Vector3 EndOfRoute(int slot) => Altar + new Vector3(slot == 0 ? -8 : 8, 0, 5);
        internal bool AllGroupsPermanent => Returning && System.Linq.Enumerable.All(groups, g =>
            System.Linq.Enumerable.All(g, d => d.Object.activeInHierarchy));
    }
}
#endif
