#if DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using TwinEdge.Gameplay;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed partial class V3GameplaySmoke
{
    IEnumerator MeasureNetworkLobby(GameStateManager game)
    {
        var probe = RelayNetworkProbe.Current;
        yield return Until(() => game.IsGameReady, "Both Runtime Ready in lobby", 80);
        yield return SyncContest("network-lobby");
        probe.Apply();
        yield return new WaitForSecondsRealtime(5);
        probe.Segment = "lobby";
        yield return new WaitForSecondsRealtime(32);
        probe.Segment = "start";
        yield return VerifyNetworkReady(game);
        if (Environment.GetCommandLineArgs().Contains("-v3NetworkDisconnect"))
            yield return NetworkReconnectLobby(game);
        yield return SyncContest("network-start");
    }

    IEnumerator VerifyNetworkReady(GameStateManager game)
    {
        if (!host) game.SetLobbyReadyRpc(true);
        yield return Until(() => game.ClientReady, "Server approved Client Ready");
        yield return SyncContest("network-ready-observed");
        if (!host) game.SetLobbyReadyRpc(false);
        yield return Until(() => !game.ClientReady, "Server approved Ready cancellation");
        yield return SyncContest("network-cancel-observed");
        game.RequestMatchStartRpc();
        yield return new WaitForSecondsRealtime(1);
        Check(game.Phase == FoundationPhase.WaitingForPlayers && !game.ClientReady, "Host Start without current Ready was rejected");
    }

    IEnumerator RunNetworkBaseline(GameStateManager game, GameplayCharacter remote)
    {
        var probe = RelayNetworkProbe.Current;
        Check(game.Phase == FoundationPhase.Outbound && game.Transition.Kind == MatchTransition.Countdown &&
            game.Transition.End - game.Transition.Start == 3, "Server countdown and Outbound phase agreed");
        probe.Note("both runtime ready and server countdown observed");
        var origin = player.transform.position;
        var remoteOrigin = remote.transform.position;
        probe.Segment = "movement";
        yield return NetworkMovement(32);
        Check(Vector3.Distance(origin, player.transform.position) < 5f &&
            Vector3.Distance(remoteOrigin, remote.transform.position) < 6f, "Measured movement remained around original spawns");

        probe.Segment = "link-check";
        yield return MeasureSequenceDelivery(remote.OwnerClientId);
        probe.Segment = "transition";
        yield return SyncContest("network-restore");
        probe.Restore();
        yield return NetworkMovement(5);
        probe.Segment = "restored";
        yield return NetworkMovement(8);
        probe.Segment = "transition";

        probe.Automatic = true;
        probe.Apply();
        yield return NetworkReconnect(game);
        probe.Automatic = false;
        probe.Restore();

        if (Environment.GetCommandLineArgs().Contains("-v3NetworkDisconnect"))
        {
            yield return NetworkReplaceRoom(game);
            yield break;
        }

        yield return SyncContest("network-complete");
        if (host) yield return Until(() => !game.IsGameReady, "Client left after baseline");
        var leave = sessions.LeaveRoomAsync();
        yield return Until(() => leave.IsCompleted && !manager.IsListening, "Closed baseline Relay room", 60);
        File.WriteAllText(Path.Combine(evidence, ContestRole + "-pass.txt"), "PASS: lobby, start, genuine movement and remote replication.");
        Application.Quit(failed ? 2 : 0);
    }

    IEnumerator MeasureSequenceDelivery(ulong peer)
    {
        const string message = "TwinEdge.NetworkBaseline.Sequence";
        const int total = 300;
        var observed = new bool[total];
        int unique = 0;
        var messaging = manager.CustomMessagingManager;
        messaging.RegisterNamedMessageHandler(message, (sender, reader) =>
        {
            reader.ReadValueSafe(out int sequence);
            if (sender != peer || sequence < 0 || sequence >= total || observed[sequence]) return;
            observed[sequence] = true;
            unique++;
        });
        try
        {
            yield return SyncContest("network-sequences-start");
            for (int sequence = 0; sequence < total; sequence++)
            {
                using (var payload = new FastBufferWriter(sizeof(int), Allocator.Temp))
                {
                    payload.WriteValueSafe(sequence);
                    messaging.SendNamedMessage(message, peer, payload, NetworkDelivery.Unreliable);
                }
                yield return new WaitForSecondsRealtime(.05f);
            }
            yield return SyncContest("network-sequences-sent");
            yield return new WaitForSecondsRealtime(3);
            RelayNetworkProbe.Current.Note($"unreliable sequence calibration expected={total} unique={unique} missing={total - unique}");
        }
        finally
        {
            messaging.UnregisterNamedMessageHandler(message);
        }
    }

    IEnumerator NetworkReconnectLobby(GameStateManager game)
    {
        string code = sessions.CurrentJoinCode;
        if (!host) game.SetLobbyReadyRpc(true);
        yield return Until(() => game.ClientReady, "Client Ready before controlled lobby disconnect");
        yield return SyncContest("network-lobby-leave");
        if (!host)
        {
            manager.Shutdown();
            yield return Until(() => !manager.IsListening && !sessions.HasSession, "Client shutdown before Host Start", 60);
        }
        else
        {
            yield return Until(() => !game.IsGameReady && !game.ClientReady, "Departure cleared the old Ready");
            game.RequestMatchStartRpc();
            yield return new WaitForSecondsRealtime(1);
            Check(game.Phase == FoundationPhase.WaitingForPlayers, "Start with previous Ready stayed in lobby");
        }
        yield return SyncContest("network-lobby-disconnected");
        if (!host)
        {
            var join = sessions.JoinRoomAsync(code);
            yield return Until(() => join.IsCompleted && sessions.HasSession, "Client manually rejoined lobby", 80);
        }
        yield return Until(() => game.IsGameReady && !game.ClientReady, "Both Runtime Ready with fresh lobby approval required", 80);
        RelayNetworkProbe.Current.Note("pre-start rejoin restored two runtime-ready peers");
    }

    IEnumerator NetworkMovement(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        var remote = FindObjectsByType<GameplayCharacter>(FindObjectsSortMode.None).Single(x => !x.IsOwner);
        Vector3 localStart = player.transform.position;
        Vector3 remoteStart = remote.transform.position;
        float localTravel = 0, remoteTravel = 0;
        IEnumerator Stroke(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            float end = Time.realtimeSinceStartup + .45f;
            while (Time.realtimeSinceStartup < end)
            {
                localTravel = Mathf.Max(localTravel, Vector3.Distance(player.transform.position, localStart));
                remoteTravel = Mathf.Max(remoteTravel, Vector3.Distance(remote.transform.position, remoteStart));
                yield return null;
            }
        }
        while (Time.realtimeSinceStartup < until)
        {
            yield return Stroke(Key.W);
            yield return Stroke(Key.S);
        }
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Check(localTravel > .3f, $"Network sample used owner movement input, travel={localTravel:F3}m");
        Check(remoteTravel > .25f, $"Network sample observed remote movement, travel={remoteTravel:F3}m");
    }

    IEnumerator NetworkReconnect(GameStateManager game)
    {
        var probe = RelayNetworkProbe.Current;
        string code = sessions.CurrentJoinCode;
        var character = player.GetComponent<GameplayCharacter>();
        string nickname = character.Nickname;
        string checkpoint = character.CheckpointName.Value.ToString();
        byte cape = character.CapeColorIndex;
        int slot = character.LobbySlot;
        int networkObjects = manager.SpawnManager.SpawnedObjects.Count;
        ulong oldOwner = character.OwnerClientId;
        ulong identity = game.MatchIdentity;
        if (!host)
        {
            manager.Shutdown();
            yield return Until(() => !manager.IsListening && !sessions.HasSession, "Controlled client disconnect", 60);
            probe.Note("client disconnected");
        }
        else
        {
            yield return Until(() => !game.IsGameReady, "Server observed client disconnect", 45);
        }
        yield return SyncContest("network-disconnected", 60);
        if (!host)
        {
            yield return new WaitForSecondsRealtime(2);
            probe.Note("manual join requested");
            var join = sessions.JoinRoomAsync(code, nickname);
            yield return Until(() => join.IsCompleted && sessions.HasSession, "Rejoined original Relay room", 80);
            probe.Note("join completed");
        }
        yield return Until(() => game.IsGameReady && manager.LocalClient?.PlayerObject != null, "Both Runtime Ready after manual rejoin", 80);
        probe.Note("both runtime ready after rejoin");
        player = manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
        character = player.GetComponent<GameplayCharacter>();
        Check(nickname == character.Nickname && cape == character.CapeColorIndex && checkpoint == character.CheckpointName.Value.ToString() &&
            slot == character.LobbySlot && (host || oldOwner != character.OwnerClientId) &&
            manager.SpawnManager.SpawnedObjects.Count == networkObjects &&
            game.MatchIdentity == identity && game.Phase == FoundationPhase.Outbound, "Original nickname, cape, checkpoint, match and phase restored");
        player.GetComponent<GameplayInput>().SimulateCapturedCursor = true;
        yield return SyncContest("network-rejoin-ready");
        yield return NetworkMovement(4);
        probe.Note("both owners resumed movement");
    }

    IEnumerator NetworkReplaceRoom(GameStateManager game)
    {
        var probe = RelayNetworkProbe.Current;
        yield return SyncContest("network-close-original");
        if (host)
        {
            string oldRoom = sessions.MatchRoomId;
            var leave = sessions.LeaveRoomAsync();
            yield return Until(() => leave.IsCompleted && !manager.IsListening, "Host closed original room", 60);
            var create = sessions.CreateRoomAsync();
            yield return Until(() => create.IsCompleted && sessions.HasSession, "Host created a fresh room", 80);
            Check(sessions.MatchRoomId != oldRoom && game.Phase == FoundationPhase.WaitingForPlayers &&
                !game.ClientReady && !game.HasWinner, "Fresh room has new session and clean lobby");
            probe.Note("fresh room clean lobby observed");
            leave = sessions.LeaveRoomAsync();
            yield return Until(() => leave.IsCompleted && !manager.IsListening, "Host closed fresh room", 60);
        }
        else
        {
            yield return Until(() => !manager.IsListening && !sessions.HasSession && sessions.WasDisconnected,
                "Client released the closed room", 80);
            Check((sessions.Issue is MultiplayerSessionManager.RoomIssue.HostLeft or MultiplayerSessionManager.RoomIssue.Connection) &&
                string.IsNullOrEmpty(sessions.CurrentJoinCode), "Closed room recovered to an offline session");
            probe.Note($"closed room issue={sessions.Issue}");
        }
        File.WriteAllText(Path.Combine(evidence, ContestRole + "-pass.txt"), "PASS: manual rejoin, old room teardown and fresh room lifecycle. See network events for close classification.");
        Application.Quit(failed ? 2 : 0);
    }
}
#endif
