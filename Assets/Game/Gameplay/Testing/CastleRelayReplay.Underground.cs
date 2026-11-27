#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
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
            Require(cave.ProbeTrapLayoutMatches(out string layoutError), layoutError);
            for (int attack = 0; attack < 3; attack++)
            {
                int index = attack;
                yield return Wait(() => cave.ProbeWarningAt(index), $"replicated warning for {cave.ProbeAttackName(index)}", 13);
                yield return Wait(() => cave.ProbeAttackAt(index), $"replicated attack for {cave.ProbeAttackName(index)}", 4);
                if (screenshots)
                {
                    // Let the nozzle particles appear before framing the whole shelf.
                    yield return new WaitForSeconds(.4f);
                    var point = cave.ProbeAttackCenter(index);
                    CaptureView("cavern-trap-" + cave.ProbeAttackName(index),
                        point + new Vector3(-1, 9.5f, -16f), point);
                }
            }
            yield return Rendezvous("hazard-views");
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

                yield return Wait(() => cave.ProbeVanishVisible && !cave.ProbeVanishWarning,
                    "independent vanish collider available", 10);
                player.PlaceAtOwner(cave.ProbeVanishTop + Vector3.up * .1f, Quaternion.identity);
                yield return Wait(() => player.IsGrounded && cave.ProbeVanishWarning, "warning while standing on the trap stone", 10);
                int old = character.RespawnCount.Value;
                yield return Wait(() => !cave.ProbeVanishVisible, "independent trap collider retracts");
                yield return Wait(() => character.RespawnCount.Value > old, "normal gravity from the trap collider reaches lava");
            }
            yield return Rendezvous("hazards-passed");
            File.WriteAllText(Path.Combine(output, role + "-hazards-pass.txt"), "PASS full-width attacks, warning, slow/knockback and retracting trap support");
            yield return new WaitForSeconds(1);
            Application.Quit();
        }

        IEnumerator TraverseUnderground(UndergroundGameplay cave, bool isolated = false)
        {
            Require(cave != null && cave.IsBound, "saved underground gameplay bound");
            if (!isolated)
            {
                yield return Walk(0, 247.5f);
                if (fullMatch == null && screenshots)
                {
                    yield return Walk(host ? 5 : 9, host ? 246.6f : 249);
                    yield return CaptureHeading("stair-entry", 90);
                }
                yield return Walk(20, 248);
                for (int i = 0; i < cave.Stairs.Length; i++)
                {
                    var point = cave.Stairs[i];
                    yield return Walk(point.x, point.z, sprint: true);
                    if (fullMatch == null && i is 1 or 5 or 11)
                    {
                        yield return CaptureHeading($"stair-descent-{i}-south", 180);
                        yield return CaptureHeading($"stair-descent-{i}-east", 90);
                    }
                }
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
            var arguments = Environment.GetCommandLineArgs();
            bool transitionsOnly = isolated && Array.IndexOf(arguments, "-castleTransitionOnly") >= 0;
            bool transitionReconnect = transitionsOnly && Array.IndexOf(arguments, "-castleTransitionReconnect") >= 0;
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
            if (transitionsOnly)
            {
                // Stay within the pad's participation radius and the gem's acquire radius.
                yield return WalkPoint(cave.Altar + Vector3.right * (host ? -2.05f : 2.05f));
                FaceAltarForCapture(cave);
            }
            yield return Wait(() => cave.Seal == UndergroundSeal.Unsealing || cave.Seal == UndergroundSeal.Unsealed,
                "both peers joined the seal");
            yield return Wait(() => cave.Seal == UndergroundSeal.Unsealed && gem.State == GemState.Available,
                "joint seal opens and exposes the gem");
            Require(game.Phase == FoundationPhase.SealPending, "unsealing leaves Return pending");
            if (fullMatch == null)
            {
                var window = game.Transition;
                Require(window.Kind == MatchTransition.Seal && window.End - window.Start == 1.5 && game.TransitionBlocksInput,
                    "real unseal starts the shared seal window");
                double attack = character.AttackReadyAtForTest;
                character.ProbeTransitionActions(game.ActionRevision);
                if (transitionsOnly)
                {
                    Require(Vector3.Distance(player.transform.position + Vector3.up, gem.Position) < 2.7f,
                        "seal acquire probe uses the actual pickup radius");
                    gem.RequestAcquire();
                }
                if (screenshots)
                {
                    yield return Wait(() => game.MatchServerTime >= window.Start + .25, "visible seal effect");
                    yield return new WaitForEndOfFrame();
                    PresentationRelaySmoke.CaptureTransition(V3GameplayBootstrap.Current, output, role + "-seal", true);
                    if (host && transitionsOnly)
                        CaptureView("seal-crystals", cave.Altar + new Vector3(4, 2.2f, 4), window.Point);
                }
                Require(gem.State == GemState.Available, "acquire request during seal did not reserve the gem");
                if (transitionReconnect) yield return ReconnectDuringTransition(window);
                yield return Wait(() => game.AcceptsGameplay, "seal window releases");
                Require(character.AttackReadyAtForTest == attack, "server rejected attacks during real seal");
            }
            else yield return Wait(() => game.AcceptsGameplay, "seal window releases");
            Capture("altar-unsealed");
            CaptureView("altar-gem", cave.Altar + new Vector3(5, 3, 5), cave.Altar + Vector3.up);

            yield return WalkPoint(cave.Altar + Vector3.right * (host ? -1.1f : 1.1f));
            yield return Rendezvous("gem-ready");
            if (host || (!transitionReconnect && !testReconnect && fullMatch == null))
            {
                SetKeys(Key.E);
                yield return new WaitForSeconds(.16f);
                SetKeys();
            }
            yield return Wait(() => game.Phase == FoundationPhase.Return && gem.State == GemState.Carried, "one server-approved E pickup starts Return");
            if (fullMatch == null)
            {
                var window = game.Transition;
                uint lockedRevision = game.ActionRevision;
                Require(window.Kind == MatchTransition.Return && window.End - window.Start == 2 && game.TransitionBlocksInput,
                    "real first pickup starts the shared Return window");
                var rotation = Camera.main.transform.rotation;
                double attack = character.AttackReadyAtForTest;
                double grab = character.GrabReadyAtForTest;
                character.ProbeTransitionActions(lockedRevision);
                if (screenshots)
                {
                    yield return Wait(() => game.MatchServerTime >= window.Start + .25, "visible Return effect");
                    yield return new WaitForEndOfFrame();
                    PresentationRelaySmoke.CaptureTransition(V3GameplayBootstrap.Current, output, role + "-first-pickup", true);
                }
                Require(Quaternion.Angle(rotation, Camera.main.transform.rotation) < .1f, "transition retains camera rotation");
                if (transitionReconnect) yield return ReconnectDuringTransition(window);
                yield return Wait(() => game.AcceptsGameplay, "Return window releases");
                character.ProbeTransitionActions(lockedRevision);
                yield return new WaitForSeconds(.25f);
                Require(character.AttackReadyAtForTest == attack && character.GrabReadyAtForTest == grab,
                    "server rejected combat within the window and stale requests after it");
            }
            else yield return Wait(() => game.AcceptsGameplay, "Return window releases");
            if (fullMatch == null)
                yield return Wait(() => System.Linq.Enumerable.Count(FindObjectsByType<GameplayCharacter>(FindObjectsSortMode.None),
                    c => c.CheckpointName.Value.ToString() == "Underground seal") == 2, "Gem pickup assigns both Return altar checkpoints");
            bool carrier = gem.Owner == player.OwnerClientId;
            File.WriteAllText(Path.Combine(output, role + "-carrier.txt"), gem.Owner.ToString());
            yield return Rendezvous("carrier-seen");
            Require(File.ReadAllText(Path.Combine(output, "host-carrier.txt")) == File.ReadAllText(Path.Combine(output, "client-carrier.txt")),
                "both Relay peers see the same carrier");
            Require(cave.AllGroupsPermanent, "all eight cooperative groups physically present in Return");
            if (transitionsOnly)
            {
                if (screenshots && host) PresentationRelaySmoke.CaptureGameplayHud(V3GameplayBootstrap.Current, output);
                yield break;
            }
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
            {
                yield return Walk(stairs[i].x, stairs[i].z, sprint: true);
                if (fullMatch == null && !isolated && i is 3 or 9)
                {
                    yield return CaptureHeading($"stair-ascent-{i}-west", 270);
                    yield return CaptureHeading($"stair-ascent-{i}-north", 0);
                }
            }
            yield return Walk(fullMatch == null && !isolated ? (host ? 1.3f : -1.3f) : 0, 247.5f);
            Require(Mathf.Abs(player.transform.position.y - castle.World(0, 0, 0).y) < 1, "physical return ascends to the castle door");
            if (fullMatch == null && !isolated)
                yield return CheckBasementRecovery(FoundationPhase.Return, 246.8f);
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

        void FaceAltarForCapture(UndergroundGameplay cave)
        {
            if (!screenshots) return;
            Vector3 heading = Vector3.ProjectOnPlane(cave.Altar - player.transform.position, Vector3.up);
            if (heading.sqrMagnitude > .01f)
                Camera.main.GetComponent<ThirdPersonCamera>().FaceSpawn(
                    Quaternion.LookRotation(Quaternion.Euler(0, host ? 16 : -16, 0) * heading));
        }

        IEnumerator ReconnectDuringTransition(MatchTransitionWindow window)
        {
            if (!host)
            {
                Require(game.TransitionBlocksInput, "Client initiates disconnect while the presentation is active");
                Debug.Log($"MATCH_TRANSITION client-leave kind={window.Kind} time={game.MatchServerTime:F3} end={window.End:F3}");
                yield return RejoinCavern();
            }
            else
            {
                yield return Wait(() => !game.IsGameReady, "server observes Client disconnect");
                Debug.Log($"MATCH_TRANSITION server-disconnect kind={window.Kind} time={game.MatchServerTime:F3} end={window.End:F3}");
            }
            yield return Wait(() => game.IsGameReady, "Runtime Ready restored after transition disconnect", 90);
            Require(game.Transition.Sequence == window.Sequence && game.Transition.End == window.End,
                "same-room reconnect preserves the original transition and deadline");
            yield return Wait(() => !game.TransitionBlocksInput, "completed presentation remains expired after rejoin");
            var banner = V3GameplayBootstrap.Current.Presentation.GetComponentsInChildren<UnityEngine.CanvasGroup>(true);
            Require(!Array.Exists(banner, item => item.name == "Match transition" && item.gameObject.activeInHierarchy),
                "expired transition overlay is clear after rejoin");
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
            bool sawVisible = false, sawRetracted = false, sawAttack = false, sawWarning = false;
            double until = cave.Now + 18;
            while (cave.Now < until && !(sawVisible && sawRetracted && sawAttack && sawWarning))
            {
                sawVisible |= cave.ProbeVanishVisible;
                sawRetracted |= !cave.ProbeVanishVisible;
                sawAttack |= cave.ProbeAttackVisible;
                sawWarning |= cave.ProbeWarningVisible;
                yield return null;
            }
            Require(sawVisible && sawRetracted && sawAttack && sawWarning && cave.AllGroupsPermanent,
                "warning, attack and vanish cycles continue alongside permanent supports");
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
            SetKeys(Key.LeftShift);
            do
            {
                Vector3 delta = Vector3.ProjectOnPlane(top - player.transform.position, Vector3.up);
                SteerWorld(delta.sqrMagnitude > .12f * .12f ? delta.normalized : Vector3.zero);
                yield return null;
                Require(Time.realtimeSinceStartup - started < 2 && player.transform.position.y > top.y - 1,
                    $"jump from {start:F2} missed {top:F2}, feet at {player.transform.position:F2}", log: false);
            } while (!player.IsGrounded);
            SteerWorld(Vector3.zero);
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

        internal Vector3 ProbeAttackFeet(int index) =>
            traps[index].Frame.TransformPoint(new Vector3(-traps[index].Size.x * .35f, .1f, -.18f));
        internal Vector3 ProbeAttackCenter(int index) => traps[index].Attack.position;
        internal bool ProbeWarningAt(int index) => traps[index].Warning.activeSelf;
        internal bool ProbeAttackAt(int index) => traps[index].Attack.gameObject.activeSelf;

        internal bool ProbeTrapLayoutMatches(out string check)
        {
            int deckCount = 0;
            foreach (var group in groups) deckCount += group.Count;
            check = "only the 111 timed decks and two retracting stones are built";
            int deckColliders = 0;
            foreach (var support in layout.GetComponentsInChildren<BoxCollider>(true))
                if (support.enabled) deckColliders++;
            if (deckCount != 111 || deckColliders != deckCount + vanishing.Length) return false;
            for (int i = 0; i < traps.Length; i++)
            {
                var trap = traps[i];
                int lane = i / 3, stage = i % 3 + 1;
                var surface = landingSurfaces[lane, stage];
                check = $"trap {i + 1} fits its shelf in the local basis and leaves the switch clear";
                if (Vector3.Dot(surface.transform.right, trap.Frame.right) < .999f ||
                    !float.IsFinite(trap.Size.x) || trap.Size.x < landings[lane, stage].size.x ||
                    !surface.Raycast(new Ray(trap.Frame.TransformPoint(new Vector3(.5f - trap.Size.x / 2, 2, 0)), -trap.Frame.up), out _, 3) ||
                    !surface.Raycast(new Ray(trap.Frame.TransformPoint(new Vector3(trap.Size.x / 2 - .5f, 2, 0)), -trap.Frame.up), out _, 3) ||
                    Mathf.Abs(trap.Frame.InverseTransformPoint(stations[lane * Stages + stage].transform.position).z)
                        <= .45f + trap.Size.z / 2) return false;

                var meshBounds = new Bounds(Vector3.zero, Vector3.zero);
                foreach (var view in trap.Attack.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var bounds = view.localBounds;
                    for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        meshBounds.Encapsulate(trap.Attack.InverseTransformPoint(view.transform.TransformPoint(corner)));
                    }
                }
                check = $"trap {i + 1} mesh {meshBounds.size:F2} at {meshBounds.center:F2} matches damage volume {trap.Size:F2}";
                if (Vector3.Distance(meshBounds.size, trap.Size) > .05f ||
                    meshBounds.center.sqrMagnitude > .0025f) return false;
            }
            check = "six traps fit their shelves with matching visual and damage bounds and clear switches";
            return true;
        }
        internal string ProbeAttackName(int index) => traps[index].Kind.ToString();
        internal double ProbeSlowDeadline => slowedUntil[0];
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
