#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using TwinEdge.Gameplay;
using TwinEdge.Levels;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed partial class V3GameplaySmoke
{
    Mouse contestMouse;
    string ContestRole => host ? "host" : "client";
    const float StagePause = .65f;

    IEnumerator SyncContest(string label)
    {
        File.WriteAllText(Path.Combine(evidence, $"{ContestRole}-{label}.ready"), "");
        string peer = host ? "client" : "host";
        yield return Until(() => File.Exists(Path.Combine(evidence, $"{peer}-{label}.ready")), label, 20);
    }

    Vector3 MarkerPoint(string id) => FindObjectsByType<LevelMarker>(FindObjectsSortMode.None).Single(m => m.id == id).transform.position;

    void ContestPlace(Vector3 point, Quaternion facing) => player.PlaceAtOwner(V3GameplayBootstrap.SafeGround(point), facing);

    IEnumerator PlacePair(Vector3 center, float gap, string label)
    {
        ContestPlace(center + Vector3.right * (host ? -.5f : .5f) * gap, Quaternion.Euler(0, host ? 90 : -90, 0));
        yield return SyncContest(label);
        yield return new WaitForSeconds(StagePause);
    }

    IEnumerator Tap(Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return new WaitForSeconds(.055f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(.105f);
    }

    IEnumerator Click()
    {
        InputSystem.QueueStateEvent(contestMouse, new MouseState().WithButton(MouseButton.Left));
        yield return new WaitForSeconds(.055f);
        InputSystem.QueueStateEvent(contestMouse, new MouseState());
    }

    IEnumerator RunContest(GameStateManager game, GameplayCharacter remote)
    {
        var own = player.GetComponent<GameplayCharacter>();
        var hitter = host ? own : remote;
        var victim = host ? remote : own;
        var gem = GemOwnership.Current;
        contestMouse = InputSystem.AddDevice<Mouse>();
        Vector3 summit = MarkerPoint("Mountain.V3.ReturnFinish");
        Vector3 arena = summit + Vector3.forward * 11;
        if (Environment.GetCommandLineArgs().Contains("-v3ContestLifecycle"))
        {
            yield return TestContestLifecycle(game, hitter, victim, arena);
            yield break;
        }
        yield return PlacePair(arena, 5, "out-of-range");
        Vector3 before = victim.transform.position;
        if (host) yield return Click();
        yield return Until(() => hitter.AttackReadyAtForTest > 0, "real mouse attack registered");
        yield return new WaitForSeconds(.25f);
        Check(victim.ControlledUntilForTest == 0 && Vector3.Distance(before, victim.transform.position) < .25f,
            "out-of-range attack has no effect");
        yield return new WaitForSeconds(.8f);

        yield return PlacePair(arena, 2.2f, "strike-ready");
        before = victim.transform.position;
        if (host) yield return Click();
        yield return Until(() => victim.ControlledUntilForTest > 0, "server hit and stun replicated");
        double firstAttack = hitter.AttackReadyAtForTest;
        yield return new WaitForSeconds(.25f);
        if (host) yield return Click();
        yield return new WaitForSeconds(.27f);
        Check(hitter.AttackReadyAtForTest == firstAttack, "attack cooldown refused the second click");
        Check(victim.transform.position.x > before.x + .4f, "capsule moved away from hit");
        yield return SyncContest("hit-done");

        yield return PlacePair(arena, 1.55f, "grab-ready");
        if (host) yield return Tap(Key.F);
        yield return Until(() => hitter.HeldForTest == victim.OwnerClientId && victim.HolderForTest == hitter.OwnerClientId,
            "grab acknowledged by both network variables");
        yield return Until(() => victim.transform.position.y > hitter.transform.position.y + .2f &&
            hitter.ChargeForTest > .35f, "synchronized carry pose and charge");
        if (!host)
            for (int i = 0; i < 8; i++) yield return Tap(Key.F);
        yield return Until(() => hitter.HeldForTest == ulong.MaxValue && victim.HolderForTest == ulong.MaxValue,
            "distinct F presses released the victim");
        double cooldown = hitter.GrabReadyAtForTest;
        if (host) yield return Tap(Key.F);
        yield return new WaitForSeconds(.25f);
        Check(hitter.HeldForTest == ulong.MaxValue && hitter.GrabReadyAtForTest == cooldown, "grab cooldown blocked reuse");
        yield return Until(() => manager.ServerTime.Time > cooldown + .1, "grab cooldown expires");

        var wall = new GameObject("Contest throw wall");
        wall.transform.position = V3GameplayBootstrap.SafeGround(arena + Vector3.right * 4) + Vector3.up * 2;
        wall.AddComponent<BoxCollider>().size = new Vector3(.25f, 4, 4);
        yield return PlacePair(arena, 1.55f, "throw-ready");
        if (host) yield return Tap(Key.F);
        yield return Until(() => hitter.HeldForTest == victim.OwnerClientId && hitter.ChargeForTest >= .6f, "second grab charged");
        yield return SyncContest("throw-charged");
        before = victim.transform.position;
        if (host) yield return Tap(Key.F);
        yield return Until(() => hitter.HeldForTest == ulong.MaxValue, "F released into throw");
        yield return Until(() => victim.transform.position.x > before.x + 2.5f, "throw travelled over time");
        yield return Until(() => manager.ServerTime.Time > hitter.GrabReadyAtForTest + .1 &&
            hitter.StaminaFraction > .7f, "carrier recovered enough for exhaustion probe");
        Check(victim.transform.position.x < wall.transform.position.x - .12f, "thrown capsule did not cross wall");
        Destroy(wall);

        yield return PlacePair(arena, 1.55f, "exhaustion-ready");
        if (host) yield return Tap(Key.F);
        yield return Until(() => hitter.HeldForTest == victim.OwnerClientId, "third grab accepted");
        yield return Until(() => hitter.HeldForTest == ulong.MaxValue, "stamina exhaustion put victim down", 8);
        Check(hitter.StaminaFraction <= .005f, "carrier stamina reached zero");
        yield return new WaitForSeconds(2);
        Check(hitter.StaminaFraction <= .005f, "zero-stamina recovery delayed");
        yield return Until(() => hitter.StaminaFraction > .02f, "stamina regeneration resumed", 7);

        // Real seal/altar traversal is covered by CastleRelayReplay. Place an unlocked gem here
        // so this fixture can exercise the contest at the actual summit without a route teleport mid-assertion.
        yield return PlacePair(arena, 1.65f, "pickup-ready");
        if (host)
        {
            Check(game.AdvanceFoundation(FoundationPhase.SealPending), "fixture phase setup");
            gem.UnlockServer(V3GameplayBootstrap.SafeGround(arena) + Vector3.up * .65f);
            yield return new WaitForSeconds(.25f);
            yield return Tap(Key.E);
        }
        yield return Until(() => game.Phase == FoundationPhase.Return && gem.State == GemState.Carried &&
            gem.Owner == hitter.OwnerClientId, "real E pickup started Return");
        if (!host) yield return Click();
        yield return Until(() => hitter.ControlledUntilForTest > 0, "pickup protection still allows hit reaction");
        Check(gem.State == GemState.Carried && gem.Owner == hitter.OwnerClientId, "pickup protection retained the gem");
        yield return new WaitForSeconds(1.65f);
        yield return PlacePair(arena, 1.65f, "gem-holder-ready");
        if (host) yield return Tap(Key.F);
        yield return new WaitForSeconds(.25f);
        Check(hitter.HeldForTest == ulong.MaxValue, "gem carrier cannot grab");
        if (!host) yield return Click();
        yield return Until(() => gem.State == GemState.Dropped, "unprotected gem dropped from a real hit");
        Vector3 drop = gem.Position;
        Check(Vector3.Distance(drop, arena) < 2 && drop.y > arena.y, "dropped gem near hit and above the surface");
        yield return new WaitForSeconds(.35f);
        if (!host) yield return Tap(Key.E);
        yield return Until(() => gem.State == GemState.Carried && gem.Owner == victim.OwnerClientId, "opponent took the dropped gem");

        yield return Until(() => manager.ServerTime.Time > victim.AttackReadyAtForTest + .1, "gem carrier attack cooldown expired");
        yield return PlacePair(arena, 1.6f, "gem-carrier-can-hit");
        double lastHit = hitter.ControlledUntilForTest;
        if (!host) yield return Click();
        yield return Until(() => hitter.ControlledUntilForTest > lastHit && gem.Owner == victim.OwnerClientId,
            "gem carrier strike succeeded without losing ownership");
        yield return SyncContest("carrier-hit-complete");

        if (Environment.GetCommandLineArgs().Contains("-v3ContestRegions"))
            yield return TestDeathRegions(game, victim);
        ContestPlace(host ? summit : arena, Quaternion.identity);
        yield return SyncContest("non-carrier-finish");
        yield return new WaitForSeconds(.8f);
        Check(game.Phase == FoundationPhase.Return && !game.HasWinner, "non-carrier summit arrival refused");
        yield return Until(() => manager.ServerTime.Time > hitter.AttackReadyAtForTest + .1, "finishing strike available");
        if (host) ContestPlace(summit + Vector3.right * .8f, Quaternion.Euler(0, 90, 0));
        else ContestPlace(summit + Vector3.right * 3.1f, Quaternion.Euler(0, -90, 0));
        yield return SyncContest("finish-contest-ready");
        yield return new WaitForSeconds(.85f);
        if (!host) ContestPlace(summit + Vector3.right * 1.95f, Quaternion.Euler(0, -90, 0));
        if (host)
        {
            yield return Until(() => victim.transform.position.x - summit.x < 2.1f, "carrier enters finish on server");
            yield return Click();
        }
        yield return Until(() => gem.State == GemState.Dropped && !game.HasWinner, "hit drop cancels an unfinished dwell");
        if (!host)
        {
            yield return new WaitForSeconds(.8f);
            ContestPlace(gem.Position + Vector3.back * 1.25f, Quaternion.identity);
            yield return new WaitForSeconds(.6f);
            yield return Tap(Key.E);
        }
        yield return Until(() => game.Phase == FoundationPhase.Finished && game.HasWinner, "carrier summit arrival finished");
        Check(game.WinnerClientId == victim.OwnerClientId, "both peers report the client as winner");
        double time = game.ElapsedSeconds;
        yield return new WaitForSeconds(.5f);
        Check(Math.Abs(game.ElapsedSeconds - time) < .001, "match timer stopped");
        if (!host) yield return Tap(Key.E);
        else yield return Click();
        Check(game.WinnerClientId == victim.OwnerClientId && gem.Owner == victim.OwnerClientId, "late inputs leave result stable");
        CaptureFinish(summit);
        yield return new WaitForSeconds(.25f);
        yield return SyncContest("completed");
        File.WriteAllText(Path.Combine(evidence, ContestRole + "-pass.txt"), "PASS V3 Relay contest and finish from staged stations.");
    }

    IEnumerator TestContestLifecycle(GameStateManager game, GameplayCharacter hitter, GameplayCharacter victim, Vector3 arena)
    {
        var pole = CooperativePole.Current;
        Vector3 grip = pole.Snapshot.a;
        ContestPlace(grip + (host ? Vector3.zero : Vector3.right * 2.1f), Quaternion.Euler(0, host ? 90 : -90, 0));
        yield return SyncContest("pole-hit-placement");
        yield return new WaitForSeconds(.65f);
        if (host) pole.UseRpc(pole.Snapshot.revision);
        yield return Until(() => pole.IsAttached(hitter.OwnerClientId), "one pole grip attached");
        yield return SyncContest("pole-grip-ready");
        if (!host) yield return Click();
        yield return Until(() => !pole.IsAttached(hitter.OwnerClientId) && hitter.ControlledUntilForTest > 0,
            "approved hit released pole and applied stun");
        yield return Until(() => hitter.GetComponent<CharacterController>().enabled, "pole collision capsule restored");
        yield return Until(() => manager.ServerTime.Time > victim.AttackReadyAtForTest + .1, "pole strike cooldown expired");

        int oldLife = victim.RespawnCount.Value;
        yield return PlacePair(arena, 1.55f, "lifecycle-initial-grab");
        if (host) yield return Tap(Key.F);
        yield return Until(() => hitter.HeldForTest == victim.OwnerClientId, "held before respawn");
        yield return SyncContest("held-before-respawn");
        if (host) FindFirstObjectByType<RespawnManager>().RespawnPlayer(victim.GetComponent<PlayerMovement>(), true);
        yield return Until(() => victim.RespawnCount.Value > oldLife && hitter.HeldForTest == ulong.MaxValue &&
            victim.HolderForTest == ulong.MaxValue, "respawn cleared both ends of the grab");
        yield return new WaitForSeconds(.85f);
        yield return PlacePair(arena, 1.55f, "stale-hit");
        double attackReady = victim.AttackReadyAtForTest, controlledUntil = hitter.ControlledUntilForTest;
        if (!host) victim.RequestHitForLifeForTest(oldLife);
        yield return new WaitForSeconds(.6f);
        Check(victim.AttackReadyAtForTest == attackReady && hitter.ControlledUntilForTest == controlledUntil, "old-life hit RPC refused");
        yield return Until(() => manager.ServerTime.Time > hitter.GrabReadyAtForTest + .1, "respawn grab cooldown expired");

        yield return PlacePair(arena, 1.55f, "disconnect-grab");
        if (host) yield return Tap(Key.F);
        yield return Until(() => hitter.HeldForTest == victim.OwnerClientId, "held before disconnect");
        yield return SyncContest("held-before-disconnect");
        if (host)
        {
            yield return Until(() => !game.IsGameReady && hitter.HeldForTest == ulong.MaxValue,
                "Relay departure released the carrier");
        }
        else
        {
            string code = File.ReadAllText(Path.Combine(evidence, "join-code.txt"));
            var leave = sessions.LeaveRoomAsync();
            yield return Until(() => leave.IsCompleted && !manager.IsListening, "client disconnected while held");
            var rejoin = sessions.JoinRoomAsync(code);
            yield return Until(() => rejoin.IsCompleted && sessions.HasSession, "client rejoined same Relay room", 80);
        }
        yield return Until(() => game.CanPlay && manager.LocalClient?.PlayerObject != null, "live phase resumed with two peers", 80);
        player = manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
        var own = player.GetComponent<GameplayCharacter>();
        player.GetComponent<GameplayInput>().SimulateCapturedCursor = true;
        Check(own.HeldForTest == ulong.MaxValue && own.HolderForTest == ulong.MaxValue &&
            player.GetComponent<CharacterController>().enabled, "owner controls restored after rejoin");

        var gem = GemOwnership.Current;
        yield return PlacePair(arena, 1.8f, "simultaneous-pickup-ready");
        if (host)
        {
            Check(game.AdvanceFoundation(FoundationPhase.SealPending), "pickup race setup");
            gem.UnlockServer(V3GameplayBootstrap.SafeGround(arena) + Vector3.up * .65f);
        }
        yield return Until(() => gem.State == GemState.Available, "same gem revision available");
        yield return SyncContest("race-now");
        yield return Tap(Key.E);
        yield return Until(() => gem.State == GemState.Carried && game.Phase == FoundationPhase.Return, "one acquisition won the race");
        var winner = gem.Owner;
        File.WriteAllText(Path.Combine(evidence, ContestRole + "-carrier.txt"), winner.ToString());
        yield return SyncContest("race-observed");
        Check(File.ReadAllText(Path.Combine(evidence, "host-carrier.txt")) == File.ReadAllText(Path.Combine(evidence, "client-carrier.txt")),
            "both peers observed the same acquisition winner");
        yield return new WaitForSeconds(.5f);
        Check(gem.Owner == winner, "later acquisition did not overwrite the owner");
        yield return SyncContest("lifecycle-completed");
        File.WriteAllText(Path.Combine(evidence, ContestRole + "-pass.txt"), "PASS Relay grab/respawn, stale request, disconnect/rejoin and pickup race.");
    }

    void CaptureFinish(Vector3 summit)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        var camera = Camera.main;
        var pose = new Pose(camera.transform.position, camera.transform.rotation);
        var target = RenderTexture.GetTemporary(960, 540, 24);
        var previous = RenderTexture.active;
        try
        {
            camera.transform.position = summit + new Vector3(5, 4, -7);
            camera.transform.LookAt(summit + Vector3.up);
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(evidence, ContestRole + "-finish.png"), image.EncodeToPNG());
            Destroy(image);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
        }
    }

    IEnumerator TestDeathRegions(GameStateManager game, GameplayCharacter carrier)
    {
        var upper = MarkerPoint("Valley.V3.UpperCheckpoint");
        var lower = MarkerPoint("Valley.V3.LowerCheckpoint");
        var big = MarkerPoint("Mountain.V3.Final");
        var cave = UndergroundGameplay.Current;
        var castle = CastleGameplay.Current;
        var bridge = BridgeGameplay.Current;
        var mountain = CooperativePole.Current.mountain;
        Vector3 Peak(string id)
        {
            var peak = mountain.smallPeaks.Single(p => p.id == id);
            Vector2 center = mountain.clusterCenter + Vector2.Scale(peak.position, mountain.clusterSize);
            return new Vector3(center.x, peak.height, center.y);
        }
        Vector3 castleFeet = V3GameplayBootstrap.SafeGround(castle.World(0, 0, 201));
        Check(castle.Contains(castleFeet), "fixture trap corridor lies inside castle");
        yield return TestDeathRegion(game, carrier, "cavern", V3GameplayBootstrap.SafeGround(cave.Altar), cave.Entry);
        yield return TestDeathRegion(game, carrier, "castle", castleFeet, upper);
        yield return TestDeathRegion(game, carrier, "river", Vector3.Lerp(upper, lower, .5f), lower);
        yield return TestDeathRegion(game, carrier, "bridge", bridge.StartPoint + bridge.Forward * (bridge.Length * .5f), big);
        yield return TestDeathRegion(game, carrier, "small-peak", V3GameplayBootstrap.SafeGround(Peak("Peak_10")), big);
        Vector3 approach = V3GameplayBootstrap.SafeGround(Peak("Peak_04"));
        yield return TestDeathRegion(game, carrier, "final-approach", approach, approach);
    }

    IEnumerator TestDeathRegion(GameStateManager game, GameplayCharacter carrier, string label,
        Vector3 deathFeet, Vector3 destination)
    {
        var gem = GemOwnership.Current;
        int life = carrier.RespawnCount.Value;
        if (host) ContestPlace(MarkerPoint("Mountain.V3.Start") + Vector3.back * 7, Quaternion.identity);
        else player.PlaceAtOwner(deathFeet, Quaternion.identity);
        yield return SyncContest(label + "-death-placement");
        if (host)
        {
            yield return Until(() => Vector3.ProjectOnPlane(carrier.transform.position - deathFeet, Vector3.up).sqrMagnitude < 4 ||
                carrier.RespawnCount.Value > life, label + " server received station placement");
            if (carrier.RespawnCount.Value == life)
            {
                yield return new WaitForSeconds(.18f);
                if (carrier.RespawnCount.Value == life)
                    FindFirstObjectByType<RespawnManager>().RespawnPlayer(carrier.GetComponent<PlayerMovement>(), useAssignedCheckpoint: true);
            }
        }
        yield return Until(() => carrier.RespawnCount.Value > life && gem.State == GemState.Relocated,
            label + " carrier respawn and gem relocation");
        Check(Vector3.Distance(gem.Position, destination) < 4, label + " gem relocation destination");
        Check(game.Phase == FoundationPhase.Return, label + " Return remains active");
        if (!host)
        {
            yield return new WaitForSeconds(.85f);
            ContestPlace(gem.Position + Vector3.right * .75f, Quaternion.identity);
            yield return new WaitForSeconds(.7f);
            yield return Tap(Key.E);
        }
        yield return Until(() => gem.State == GemState.Carried && gem.Owner == carrier.OwnerClientId,
            label + " real pickup after death");
        yield return SyncContest(label + "-death-complete");
    }
}
#endif
