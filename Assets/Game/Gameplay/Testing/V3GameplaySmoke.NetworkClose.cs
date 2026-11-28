#if DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using TwinEdge.Audio;
using TwinEdge.Gameplay;
using TwinEdge.Presentation;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public sealed partial class V3GameplaySmoke
{
    static readonly string NetworkCloseCase = ReadNetworkCloseCase();

    static string ReadNetworkCloseCase()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-v3NetworkClose");
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    IEnumerator RunNetworkClose(GameStateManager game)
    {
        if (NetworkCloseCase == "networkClose")
        {
            yield return SyncContest("close-client-graceful");
            RelayNetworkProbe.Current.Segment = "close-client-graceful";
            yield return NetworkReconnect(game, graceful: true);
        }
        else if (NetworkCloseCase == "networkCloseClientLoss")
        {
            yield return SyncContest("close-client-loss");
            RelayNetworkProbe.Current.Segment = "close-client-loss";
            yield return NetworkReconnect(game, blackhole: true);
        }

        yield return ReplaceNetworkCloseRoom(game, unreachable: NetworkCloseCase == "networkCloseUnreachable");
    }

    IEnumerator ConfirmNetworkDeparture()
    {
        var phase = Bootstrap.state.Phase;
        if (phase == FoundationPhase.Finished)
        {
            var result = VisiblePage("Match Result");
            yield return Until(() => result.activeInHierarchy && !Bootstrap.Presentation.IsTransitioning, "Result leave can be selected");
            PageButton(result, "메인 메뉴").onClick.Invoke();
        }
        else if (phase == FoundationPhase.WaitingForPlayers)
        {
            var lobby = VisiblePage("Lobby");
            yield return Until(() => lobby.activeInHierarchy && !Bootstrap.Presentation.IsTransitioning, "Lobby leave can be selected");
            PageButton(lobby, "방 나가기").onClick.Invoke();
        }
        else
        {
            var escape = VisiblePage("Escape Menu");
            yield return Until(() => !Bootstrap.Presentation.IsTransitioning && !PresentationRoot.BlocksGameplay, "Match accepts Esc");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Until(() => escape.activeInHierarchy, "Esc opened the match menu");
            PageButton(escape, "방 나가기").onClick.Invoke();
            yield return Until(() => Bootstrap.Presentation.Modals.GetComponentsInChildren<Text>().Any(label =>
                label.text == "방에서 나갈까요?"), "Role-specific leave confirmation visible");
            PageButton(Bootstrap.Presentation.Modals.gameObject, "확인").onClick.Invoke();
        }
        RelayNetworkProbe.Current.NoteClose("leave UI clicked");
        yield return Until(() => !sessions.HasSession && !sessions.IsBusy && !manager.IsListening, "Local confirmed leave settled", 60);
    }

    void BlockCloseTransport()
    {
        var transport = (UnityTransport)manager.NetworkConfig.NetworkTransport;
        var driver = transport.GetNetworkDriver();
        Check(driver.CurrentSettings.TryGet<NetworkSimulatorParameter>(out var parameters), "Client UTP simulator available");
        parameters.SendPacketLossPercent = 100;
        parameters.ReceivePacketLossPercent = 100;
        driver.ModifyNetworkSimulatorParameters(parameters);
        Check(driver.CurrentSettings.TryGet<NetworkSimulatorParameter>(out var actual) &&
            actual.SendPacketLossPercent == 100 && actual.ReceivePacketLossPercent == 100,
            "Client transport isolated in both directions");
        RelayNetworkProbe.Current.NoteClose($"blackhole enabled txLoss={actual.SendPacketLossPercent} rxLoss={actual.ReceivePacketLossPercent}");
    }

    IEnumerator ReplaceNetworkCloseRoom(GameStateManager previousGame, bool unreachable)
    {
        var probe = RelayNetworkProbe.Current;
        var oldPlayer = manager.LocalClient.PlayerObject;
        string oldRoom = sessions.MatchRoomId;
        string nickname = oldPlayer.GetComponent<GameplayCharacter>().Nickname;
        int volume = AudioService.MasterPercent;
        int mouse = LocalPresentationSettings.MousePercent;
        int effects = LocalPresentationSettings.EffectPercent;
        bool minimap = CourseMapPresentation.MinimapVisible;
        probe.Segment = "close-host";
        var transport = (UnityTransport)manager.NetworkConfig.NetworkTransport;
        ulong peer = host ? RemoteResultClientId() : Unity.Netcode.NetworkManager.ServerClientId;
        probe.NoteClose($"before host close phase={previousGame.Phase} peer={peer} rttMs={transport.GetCurrentRtt(peer)}");
        if (unreachable && !host) BlockCloseTransport();
        yield return SyncContest("close-host-trigger");
        if (host)
        {
            yield return ConfirmNetworkDeparture();
        }
        else
        {
            var expected = unreachable ? MultiplayerSessionManager.RoomIssue.Connection : MultiplayerSessionManager.RoomIssue.HostLeft;
            yield return Until(() => sessions.WasDisconnected, "Client classified the old room closure", 55);
            Check(sessions.Issue == expected && sessions.ReceivedHostCloseForTest == !unreachable, $"Client received expected close and classification {expected}");
            var page = VisiblePage(unreachable ? "Join Room" : "Main Menu");
            string notice = unreachable ? "연결이 끊겼습니다." : "방장이 연결을 종료했습니다.";
            yield return Until(() => page.activeInHierarchy && page.GetComponentsInChildren<Text>().Any(label =>
                label.text.StartsWith(notice, StringComparison.Ordinal)), $"Client saw Korean {expected} notice");
            probe.NoteClose($"UI notice visible issue={sessions.Issue}");
            yield return Until(() => !manager.IsListening && !sessions.IsBusy, "Client stopped original NGO instance", 55);
        }
        Check(!oldPlayer && !previousGame.IsSpawned && PresentationRoot.BlocksGameplay &&
            FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None).Length == 0,
            "Old player objects and gameplay input released on departure");
        probe.NoteClose("offline NGO and gameplay objects settled");

        if (host)
        {
            var create = sessions.CreateRoomAsync(nickname);
            yield return Until(() => create.IsCompleted && sessions.HasSession, "Host created replacement Relay room", 80);
            probe.NoteClose("replacement room created");
            File.WriteAllText(Path.Combine(evidence, "replacement-code.tmp"), sessions.CurrentJoinCode);
            File.Move(Path.Combine(evidence, "replacement-code.tmp"), Path.Combine(evidence, "replacement-code.txt"));
        }
        else
        {
            string replacement = Path.Combine(evidence, "replacement-code.txt");
            yield return Until(() => File.Exists(replacement), "Replacement room code available", 80);
            var join = sessions.JoinRoomAsync(File.ReadAllText(replacement), nickname);
            yield return Until(() => join.IsCompleted && sessions.HasSession, "Client joined replacement Relay room", 80);
            probe.NoteClose("replacement room joined");
        }
        var game = Bootstrap.state;
        yield return Until(() => game.IsGameReady && manager.LocalClient?.PlayerObject != null, "Replacement peers Runtime Ready", 80);
        yield return Until(() => VisiblePage("Lobby").activeInHierarchy && !Bootstrap.Presentation.IsTransitioning, "Clean replacement lobby visible");
        Check(sessions.MatchRoomId != oldRoom && !sessions.WasDisconnected && !sessions.ReceivedHostCloseForTest &&
            manager.LocalClient.PlayerObject != oldPlayer && game.Phase == FoundationPhase.WaitingForPlayers &&
            !game.ClientReady && !game.HasWinner && GemOwnership.Current.State == GemState.Sealed &&
            !manager.LocalClient.PlayerObject.GetComponent<GameplayInput>().AcceptsInput &&
            FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None).Length == 2 &&
            FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1 &&
            FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.enabled) == 1,
            "Replacement has new players, clean match, lobby input ownership and one UI and audio listener");
        Check(volume == AudioService.MasterPercent && mouse == LocalPresentationSettings.MousePercent &&
            effects == LocalPresentationSettings.EffectPercent && minimap == CourseMapPresentation.MinimapVisible,
            "Local audio, input and presentation preferences survived replacement");
        probe.NoteClose("replacement room and both player slots ready");
        yield return SyncContest("close-replacement-verified");
        if (!host) yield return ConfirmNetworkDeparture();
        else
        {
            yield return Until(() => !game.IsGameReady, "Replacement Client slot released", 55);
            yield return ConfirmNetworkDeparture();
        }
        File.WriteAllText(Path.Combine(evidence, ContestRole + "-pass.txt"), "PASS: session close, classification, cleanup and joined replacement.");
        Application.Quit(failed ? 2 : 0);
    }
}
#endif
