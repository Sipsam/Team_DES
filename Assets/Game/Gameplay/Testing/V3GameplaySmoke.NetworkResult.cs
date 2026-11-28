#if DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using TwinEdge.Gameplay;
using TwinEdge.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed partial class V3GameplaySmoke
{
    static readonly Vector3 ResultApproach = Vector3.forward * 8.9f;

    void PrepareNetworkResultStation()
    {
        var summit = MarkerPoint("Mountain.V3.ReturnFinish");
        var spawns = V3GameplayBootstrap.Current.spawnPoints;
        for (int i = 0; i < spawns.Length; i++)
        {
            var position = V3GameplayBootstrap.SafeGround(summit + ResultApproach + Vector3.right * (i == 0 ? -1.1f : 1.1f));
            spawns[i].SetPositionAndRotation(position, Quaternion.Euler(0, 180, 0));
        }
    }

    IEnumerator RunNetworkResult(GameStateManager game)
    {
        var probe = RelayNetworkProbe.Current;
        var gem = GemOwnership.Current;
        var summit = MarkerPoint("Mountain.V3.ReturnFinish");
        probe.Segment = "result-entry";
        // Same precondition as the isolated PRES-07 result fixture. The programmed
        // station supplies eligibility for pickup; pickup, Return and finish use live authority.
        if (host)
        {
            Check(game.AdvanceFoundation(FoundationPhase.SealPending), "Seeded isolated pickup precondition");
            gem.UnlockServer(V3GameplayBootstrap.SafeGround(summit + ResultApproach) + Vector3.up * .65f);
        }
        yield return Until(() => gem.State == GemState.Available, "Both see staged gem at finish approach");
        yield return SyncContest("result-stale-action");
        if (!host) gem.RequestAcquireForBaseline(0, 2);
        yield return new WaitForSecondsRealtime(1);
        Check(gem.State == GemState.Available, "Previous action revision did not acquire gem");
        yield return SyncContest("result-stale-gem");
        if (!host) gem.RequestAcquireForBaseline(1, 0);
        yield return new WaitForSecondsRealtime(1);
        Check(gem.State == GemState.Available, "Previous gem revision did not acquire gem");
        yield return SyncContest("result-pickup");
        gem.RequestAcquire();
        yield return Until(() => gem.State == GemState.Carried && game.Phase == FoundationPhase.Return, "A single gem request won");
        ulong carrier = gem.Owner;
        uint sequence = game.Transition.Sequence;
        Check(sequence != 0 && game.Transition.Kind == MatchTransition.Return, "Server Return sequence replicated");
        File.WriteAllText(Path.Combine(evidence, ContestRole + "-pickup.txt"), $"{carrier},{sequence}");
        yield return SyncContest("result-pickup-observed");
        Check(File.ReadAllText(Path.Combine(evidence, "host-pickup.txt")) ==
            File.ReadAllText(Path.Combine(evidence, "client-pickup.txt")), "Peers agree on single owner and Return sequence");
        yield return Until(() => !game.TransitionBlocksInput && !PresentationRoot.BlocksGameplay, "Server Return window ended and input restored");
        Check(gem.Owner == carrier, "Later pickup did not change the approved carrier");
        if (manager.LocalClientId == carrier)
        {
            var finish = GameObject.Find("V3 Summit Finish").GetComponent<BoxCollider>();
            Camera.main.GetComponent<ThirdPersonCamera>().FaceInitialSpawn(Quaternion.Euler(0, 180, 0));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return Until(() => game.Phase == FoundationPhase.Finished ||
                finish.bounds.Contains(player.transform.position + Vector3.up * .3f), "Genuine owner movement entered saved finish", 15);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }

        yield return Until(() => game.Phase == FoundationPhase.Finished && game.HasWinner, "Server approved supported finish", 20);
        ulong identity = game.MatchIdentity;
        var matchObject = game.NetworkObject;
        ulong playerObjectId = player.NetworkObjectId;
        bool won = game.Won(host ? 0 : 1);
        double stopped = game.ElapsedSeconds;
        Check(won == (manager.LocalClientId == carrier), "Approved winner is the only gem holder");
        yield return Until(() => VisiblePage("Match Result").activeInHierarchy, "Result UI visible");
        probe.Segment = "finished";
        string receipt = stopped.ToString("R", CultureInfo.InvariantCulture);
        File.WriteAllText(Path.Combine(evidence, ContestRole + "-finish-time.txt"), receipt);
        yield return SyncContest("result-time");
        Check(File.ReadAllText(Path.Combine(evidence, "host-finish-time.txt")) ==
            File.ReadAllText(Path.Combine(evidence, "client-finish-time.txt")), "Peers agree on frozen match time");
        string code = sessions.CurrentJoinCode;
        var character = player.GetComponent<GameplayCharacter>();
        string nickname = character.Nickname;
        byte cape = character.CapeColorIndex;
        ulong oldClient = host ? RemoteResultClientId() : manager.LocalClientId;
        int objectCount = manager.SpawnManager.SpawnedObjects.Count;
        int master = TwinEdge.Audio.AudioService.MasterPercent;
        int effects = LocalPresentationSettings.EffectPercent;
        bool minimap = CourseMapPresentation.MinimapVisible;

        yield return SyncContest("result-leave");
        probe.Segment = "finished-rejoin";
        if (!host)
        {
            manager.Shutdown();
            yield return Until(() => !manager.IsListening && !sessions.HasSession, "Client disconnected in Finished", 60);
        }
        else yield return Until(() => !game.IsGameReady && game.Phase == FoundationPhase.Finished, "Host retained Finished", 45);
        yield return SyncContest("result-disconnected", 60);
        if (!host)
        {
            probe.Note("finished manual join requested");
            var join = sessions.JoinRoomAsync(code, nickname);
            yield return Until(() => join.IsCompleted && sessions.HasSession, "Client rejoined Finished Relay room", 80);
            probe.Note("finished join completed");
        }
        yield return Until(() => game.IsGameReady && manager.LocalClient?.PlayerObject != null, "Both Runtime Ready after Finished rejoin", 80);
        player = manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
        character = player.GetComponent<GameplayCharacter>();
        ulong newClient = host ? RemoteResultClientId() : manager.LocalClientId;
        Check(newClient != oldClient && game.Won(host ? 0 : 1) == won && game.ElapsedSeconds == stopped &&
            game.MatchIdentity == identity && game.Transition.Sequence == sequence && character.Nickname == nickname &&
            character.CapeColorIndex == cape && gem.State == GemState.Carried && gem.Owner == carrier &&
            manager.SpawnManager.SpawnedObjects.Count == objectCount,
            "Finished rejoin restored original result, slot, time, transition, nickname, cape and object count");
        yield return Until(() => VisiblePage("Match Result").activeInHierarchy, "Original result UI restored");
        probe.Note("finished state and both runtime peers restored");

        yield return SyncContest("result-rebuild");
        probe.Segment = "rematch";
        game.SetRematchConsentRpc(identity + 1, true);
        yield return new WaitForSecondsRealtime(1);
        Check(!game.HasRematchVote(0) && !game.HasRematchVote(1), "Wrong match identity rejected in Finished");
        yield return SyncContest("result-rejected");
        game.SetRematchConsentRpc(identity, true);
        yield return Until(() => V3GameplayBootstrap.Current && V3GameplayBootstrap.Current.state != game &&
            V3GameplayBootstrap.Current.state.IsGameReady, "NGO rebuilt the same room and both Runtime Ready", 145);
        game = V3GameplayBootstrap.Current.state;
        Check(game.MatchIdentity != identity && !matchObject && !manager.SpawnManager.SpawnedObjects.ContainsKey(playerObjectId) &&
            game.Phase == FoundationPhase.WaitingForPlayers && !game.HasWinner && !game.ClientReady &&
            game.Transition.Kind == MatchTransition.None && sessions.CurrentJoinCode == code &&
            GemOwnership.Current.State == GemState.Sealed, "Same Relay room, retired objects and clean new match state");
        Check(TwinEdge.Audio.AudioService.MasterPercent == master && LocalPresentationSettings.EffectPercent == effects &&
            CourseMapPresentation.MinimapVisible == minimap, "Local audio and map preferences survived reconstruction");
        game.SetRematchConsentRpc(identity, true);
        yield return new WaitForSecondsRealtime(1);
        Check(!game.HasRematchVote(0) && !game.HasRematchVote(1), "Previous match consent rejected in new lobby");
        yield return SyncContest("result-new-lobby");
        yield return RelayMatchStart.Run(game);
        Check(game.Phase == FoundationPhase.Outbound && game.Transition.Kind == MatchTransition.Countdown,
            "Fresh Client Ready and Host Start produced a new countdown");
        probe.Note("new runtime peers and fresh countdown observed");

        yield return NetworkReplaceRoom(game);
    }

    ulong RemoteResultClientId()
    {
        foreach (ulong id in manager.ConnectedClientsIds)
            if (id != manager.LocalClientId) return id;
        throw new InvalidOperationException("A result case requires a remote peer.");
    }
}
#endif
