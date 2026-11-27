#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TwinEdge.Gameplay;
using TwinEdge.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class V3GameplaySmoke
{
    static readonly bool RematchTest = Environment.GetCommandLineArgs().Contains("-v3Rematch");
    static readonly bool RematchCapture = Environment.GetCommandLineArgs().Contains("-v3RematchScreenshots");
    int Seat => host ? 0 : 1;
    static V3GameplayBootstrap Bootstrap => V3GameplayBootstrap.Current;
    int winSounds, loseSounds;
    string courseTrack;
    string initialLobbyResources;

    void ObserveRematchAudio(string message)
    {
        if (message.StartsWith("AUDIO_PLAY Win ", StringComparison.Ordinal)) winSounds++;
        if (message.StartsWith("AUDIO_PLAY Lose ", StringComparison.Ordinal)) loseSounds++;
        if (message.StartsWith("AUDIO_COURSE ", StringComparison.Ordinal)) courseTrack = message;
    }

    GameObject VisiblePage(string name) => Bootstrap.Presentation.Screens.Find(name).gameObject;
    static UiButton PageButton(GameObject page, string caption) => page.GetComponentsInChildren<UiButton>()
        .Single(button => button.GetComponentInChildren<Text>().text == caption);

    IEnumerator RecordInitialLobby(GameStateManager state)
    {
        yield return Until(() => state.IsGameReady, "initial lobby has both initialized players", 80);
        var lobby = VisiblePage("Lobby");
        yield return Until(() => lobby.activeInHierarchy && !Bootstrap.Presentation.IsTransitioning, "initial lobby visible");
        initialLobbyResources = RematchResourceCounts();
        File.WriteAllText(Path.Combine(evidence, ContestRole + "-resources-initial.txt"), initialLobbyResources);
        yield return SyncContest("initial-lobby");
    }

    IEnumerator RunRematches(GameStateManager state)
    {
        string code = sessions.CurrentJoinCode;
        const string guest = "파하가나다라마바사아자차";
        int master = TwinEdge.Audio.AudioService.MasterPercent;
        bool minimap = CourseMapPresentation.MinimapVisible;
        int mouse = LocalPresentationSettings.MousePercent;
        int effects = LocalPresentationSettings.EffectPercent;
        bool fullscreen = Screen.fullScreen;
        Check(SessionPresentation.FormatResultTime(3599.99) == "59:59" &&
              SessionPresentation.FormatResultTime(3600) == "01:00:00", "result timer hour boundary");
        for (int cycle = 1; cycle <= 2; cycle++)
        {
            var view = VisiblePage("Match Result");
            if (cycle == 1) yield return ObserveResultMotion(view);
            yield return Until(() => view.activeInHierarchy && !Bootstrap.Presentation.IsTransitioning, "result fade completed");
            Check(PresentationRoot.BlocksGameplay && !player.GetComponent<GameplayInput>().AcceptsInput, "result owns local input");
            bool localWin = state.Won(Seat);
            string title = localWin ? "승리" : "패배";
            Check(localWin == !host && view.GetComponentsInChildren<Text>().Any(label => label.text == title),
                "approved Client victory and Host defeat");
            CheckRematchAudio(cycle, localWin);
            double elapsed = state.ElapsedSeconds;
            string timer = $"경기 시간  {SessionPresentation.FormatResultTime(elapsed)}";
            var character = player.GetComponent<GameplayCharacter>();
            Check(view.GetComponentsInChildren<Text>().Any(label => label.text == timer) &&
                view.GetComponentsInChildren<Text>().Any(label => label.text == character.Nickname),
                "result uses exact server time and approved nickname");
            File.WriteAllText(Path.Combine(evidence, $"{ContestRole}-time-{cycle}.txt"), elapsed.ToString("R", CultureInfo.InvariantCulture));
            yield return SyncContest($"timer-{cycle}");
            string other = host ? "client" : "host";
            Check(File.ReadAllText(Path.Combine(evidence, $"{other}-time-{cycle}.txt")) ==
                elapsed.ToString("R", CultureInfo.InvariantCulture) && elapsed == state.ElapsedSeconds, "peers agree and result time stays stopped");

            if (cycle == 1)
            {
                var portrait = view.GetComponentInChildren<RawImage>();
                Check(portrait && portrait.texture && portrait.texture.name == "Result Portrait", "result portrait has a render target");
                var preview = GameObject.Find("Result character preview");
                if (RematchCapture)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(evidence, $"{ContestRole}-window-{Screen.width}x{Screen.height}.png"));
                    yield return new WaitForEndOfFrame();
                }
                foreach (int width in new[] { 1280, 1600, 1920, 2560 })
                {
                    int height = width * 9 / 16;
                    using var target = new PresentationRelaySmoke.CaptureTarget(Bootstrap.Presentation.Canvas, Bootstrap.standbyCamera, width, height);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    foreach (var label in view.GetComponentsInChildren<Text>())
                        Check(label.preferredHeight <= label.rectTransform.rect.height + 2, $"result label fits at {width}x{height}");
                    if (RematchCapture)
                    {
                        preview.GetComponentInChildren<Camera>().Render();
                        target.Save(Path.Combine(evidence, $"{ContestRole}-result-{width}x{height}.png"));
                    }
                }
                yield return CheckResultWindows();

                if (host)
                {
                    PageButton(view, "재경기").onClick.Invoke();
                    state.SetRematchConsentRpc(state.MatchIdentity, true);
                }
                yield return Until(() => state.HasRematchVote(0) && !state.HasRematchVote(1), "single vote and duplicate are idempotent");
                if (RematchCapture) CaptureRematchView($"waiting-{cycle}");
                yield return SyncContest("waiting-pictured");
                if (host) PageButton(view, "재경기 취소").onClick.Invoke();
                yield return Until(() => !state.HasRematchVote(0), "Host cancelled before second vote");
                yield return SyncContest("cancellation-observed");
                if (host) PageButton(view, "재경기").onClick.Invoke();
                yield return Until(() => state.HasRematchVote(0), "Host votes again before Client disconnect");
                byte cape = character.CapeColorIndex;
                if (!host)
                {
                    PageButton(view, "메인 메뉴").onClick.Invoke();
                    yield return Until(() => !sessions.IsBusy && !manager.IsListening, "Client left results");
                }
                else yield return Until(() => !state.IsGameReady && !state.HasRematchVote(0), "disconnect clears consent");
                yield return SyncContest("result-disconnected");
                if (!host)
                {
                    var join = sessions.JoinRoomAsync(code, guest);
                    yield return Until(() => join.IsCompleted && sessions.HasSession, "Client manually rejoined Finished", 80);
                    player = manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
                }
                yield return Until(() => state.IsGameReady && view.activeInHierarchy && !Bootstrap.Presentation.IsTransitioning,
                    "original result restored", 80);
                Check(state.Won(Seat) == localWin && state.ElapsedSeconds == elapsed &&
                    player.GetComponent<GameplayCharacter>().CapeColorIndex == cape, "result restores winner slot, time and cape");
                CheckRematchAudio(cycle, localWin);
            }

            ulong oldMatch = state.MatchIdentity;
            ulong oldPlayer = player.NetworkObjectId;
            ulong oldPole = CooperativePole.Current.NetworkObjectId;
            var oldBootstrap = Bootstrap;
            var oldBridge = BridgeGameplay.Current;
            var hud = Bootstrap.Presentation.GetComponent<GameplayPresentation>();
            SetResultField(hud, "seenLessons", uint.MaxValue);
            if (host)
            {
                state.SetRematchConsentRpc(oldMatch + 1, true);
                SetResultFixture(oldBridge, "deliveries", oldBridge.TotalDeliveries);
                ResultField<NetworkList<int>>(oldBridge, "collapsed").Add(0);
                SetResultFixture(CastleGameplay.Current, "stage", 2);
                SetResultFixture(CastleGameplay.Current, "accepted", 3);
                SetResultFixture(CastleGameplay.Current, "sewerOpen", true);
                SetResultFixture(UndergroundGameplay.Current, "seal", UndergroundSeal.Unsealed);
                ResultField<NetworkList<double>>(UndergroundGameplay.Current, "deadlines")[0] = state.MatchServerTime + 40;
                var raft = RiverGameplay.Current.Read(0);
                raft.Status = RiverRaftStatus.Arrived;
                raft.Left = manager.LocalClientId;
                raft.Right = manager.ConnectedClientsIds.Single(id => id != manager.LocalClientId);
                raft.Hits = 2;
                raft.DamagedAt = state.MatchServerTime;
                SetResultFixture(RiverGameplay.Current, "shared", raft);
                player.GetComponent<GameplayCharacter>().CheckpointName.Value = "PRES07 fixture";
                if (cycle == 2)
                {
                    var gem = GemOwnership.Current;
                    var carrier = manager.ConnectedClients[gem.Owner].PlayerObject;
                    Check(gem.TryDropServer(gem.Owner, carrier.transform.position), "server staged the pre-reset gem drop");
                }
            }
            yield return Until(() => oldBridge.CompletedDeliveries == oldBridge.TotalDeliveries && oldBridge.IsCollapsed(0) &&
                CastleGameplay.Current.DoorOpen && CastleGameplay.Current.SewerOpen && CastleGameplay.Current.Stage == 2 &&
                UndergroundGameplay.Current.Seal == UndergroundSeal.Unsealed && UndergroundGameplay.Current.Deadline(0) > 0 &&
                RiverGameplay.Current.Read(0).Hits == 2 && RiverGameplay.Current.IsSeated(manager.LocalClientId) &&
                GemOwnership.Current.State == (cycle == 1 ? GemState.Carried : GemState.Dropped),
                "staged bridge, castle, sewer, underground, damaged raft with riders and old gem progress replicated");
            Check(!state.HasRematchVote(0) && !state.HasRematchVote(1), "wrong match identity rejected");
            yield return SyncContest($"fixture-{cycle}");
            PageButton(view, "재경기").onClick.Invoke();
            if (cycle == 2)
            {
                if (!host)
                {
                    yield return Until(() => Bootstrap && Bootstrap != oldBootstrap && !Bootstrap.WorldReady,
                        "Client entered the new scene before level initialization", 80);
                    manager.Shutdown();
                    yield return Until(() => !manager.IsListening && !sessions.HasSession && Bootstrap.WorldReady,
                        "Client disconnect recovered its local world", 80);
                }
                else yield return Until(() => Bootstrap && Bootstrap != oldBootstrap && Bootstrap.WorldReady &&
                    Bootstrap.state.IsSpawned && Bootstrap.state.ConnectedPlayerCount == 1 && manager.LocalClient?.PlayerObject != null,
                    "Host rebuilt with one remaining participant", 145);
                yield return SyncContest("rebuild-interrupted");
                if (!host)
                {
                    var join = sessions.JoinRoomAsync(code, guest);
                    yield return Until(() => join.IsCompleted && sessions.HasSession, "Client rejoined fresh lobby", 80);
                }
            }
            yield return Until(() => Bootstrap && Bootstrap != oldBootstrap && Bootstrap.WorldReady &&
                Bootstrap.state.IsGameReady && manager.LocalClient?.PlayerObject != null, "both peers rebuilt and ready in same Relay room", 145);
            state = Bootstrap.state;
            player = manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
            character = player.GetComponent<GameplayCharacter>();
            var spawn = Bootstrap.spawnPoints[Seat];
            Check(state.MatchIdentity != oldMatch && player.NetworkObjectId != oldPlayer &&
                CooperativePole.Current.NetworkObjectId != oldPole && BridgeGameplay.Current != oldBridge,
                "new match, players, pole and gameplay components");
            Check(state.Phase == FoundationPhase.WaitingForPlayers && !state.ClientReady && !state.HasWinner &&
                state.ElapsedSeconds == 0 && state.Transition.Kind == MatchTransition.None &&
                !state.HasRematchVote(0) && !state.HasRematchVote(1) && sessions.CurrentJoinCode == code,
                "same room and clean lobby with no automatic start");
            Check(ResultField<uint>(Bootstrap.Presentation.GetComponent<GameplayPresentation>(), "seenLessons") == 0 &&
                TwinEdge.Audio.AudioService.MasterPercent == master && CourseMapPresentation.MinimapVisible == minimap &&
                LocalPresentationSettings.MousePercent == mouse && LocalPresentationSettings.EffectPercent == effects &&
                Screen.fullScreen == fullscreen, "tutorial history resets and all local settings survive");
            if (Environment.GetCommandLineArgs().Contains("-twinAudioTrace"))
                Check(courseTrack?.Contains("WaitingForPlayers") == true && courseTrack.Contains(" Menu "), "rematch returned to menu music");
            Check(Vector3.Distance(player.transform.position, spawn.position) < .5f &&
                character.StaminaFraction >= .99f && character.CheckpointName.Value.ToString() == "Mountain start" &&
                character.CapeColorIndex == byte.MaxValue && character.RespawnCount.Value == 0,
                "initial spawn, stamina, checkpoint and unassigned cape");
            Check(BridgeGameplay.Current.CompletedDeliveries == 0 && !BridgeGameplay.Current.IsCollapsed(0) &&
                CastleGameplay.Current.Stage == 0 && !CastleGameplay.Current.DoorOpen && !CastleGameplay.Current.SewerOpen &&
                UndergroundGameplay.Current.Seal == UndergroundSeal.Sealed && UndergroundGameplay.Current.Deadline(0) == 0 &&
                GemOwnership.Current.State == GemState.Sealed && GemOwnership.Current.Owner == ulong.MaxValue &&
                ResultField<NetworkVariable<uint>>(GemOwnership.Current, "revision").Value == 0 &&
                RiverGameplay.Current.Read(0).Status == RiverRaftStatus.Hidden && RiverGameplay.Current.Read(0).Hits == 0 &&
                RiverGameplay.Current.Read(0).Left == ulong.MaxValue && RiverGameplay.Current.Read(0).Right == ulong.MaxValue,
                "bridge, castle, sewer, underground, gem and raft at defaults");
            state.SetRematchConsentRpc(state.MatchIdentity, true);
            state.SetRematchConsentRpc(oldMatch, true);
            if (host) state.RequestMatchStartRpc();
            yield return new WaitForSeconds(.4f);
            Check(state.Phase == FoundationPhase.WaitingForPlayers && !state.HasRematchVote(Seat), "lobby refuses consent and unready start");
            var lobby = VisiblePage("Lobby");
            yield return Until(() => lobby.activeInHierarchy && !Bootstrap.Presentation.IsTransitioning, "rematch lobby visible");
            string counts = RematchResourceCounts();
            Check(counts == initialLobbyResources, "lobby resource counts match the initial session");
            File.WriteAllText(Path.Combine(evidence, $"{ContestRole}-resources-{cycle}.txt"), counts);
            if (RematchCapture)
            {
                var roomLabel = lobby.GetComponentsInChildren<Text>().Single(label => label.text == code);
                roomLabel.enabled = false;
                CaptureRematchView($"lobby-{cycle}", Camera.allCameras.Single(camera => camera.name == "Menu World Camera"));
                roomLabel.enabled = true;
            }
            yield return SyncContest($"ready-{cycle}");
            yield return RelayMatchStart.Run(state);
            Check(state.Phase == FoundationPhase.Outbound && state.Transition.Kind == MatchTransition.Countdown &&
                state.Transition.End - state.Transition.Start == 3, "manual Ready and Start ran the server countdown");
            if (RematchCapture) CaptureRematchView($"outbound-{cycle}");
            if (cycle == 1) yield return FinishNewMatch(state);
        }

        var freshIdentity = state.MatchIdentity;
        byte freshCape = player.GetComponent<GameplayCharacter>().CapeColorIndex;
        yield return SyncContest("fresh-rejoin-entry");
        if (!host)
        {
            manager.Shutdown();
            yield return Until(() => !manager.IsListening && !sessions.HasSession, "Client disconnected in fresh Outbound", 60);
        }
        else yield return Until(() => !state.IsGameReady, "Host retained fresh Outbound during disconnect");
        yield return SyncContest("fresh-rejoin-disconnect");
        if (!host)
        {
            var join = sessions.JoinRoomAsync(code, guest);
            yield return Until(() => join.IsCompleted && sessions.HasSession, "Client rejoined fresh Outbound", 80);
            player = manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
        }
        yield return Until(() => state.IsGameReady && state.Phase == FoundationPhase.Outbound, "new match restored", 80);
        Check(state.MatchIdentity == freshIdentity && !state.HasWinner && GemOwnership.Current.State == GemState.Sealed &&
            player.GetComponent<GameplayCharacter>().CapeColorIndex == freshCape, "rejoin restores only current match and cape");
        yield return SyncContest("fresh-rejoin-verified");

        yield return FinishNewMatch(state);
        var lastResult = VisiblePage("Match Result");
        yield return Until(() => lastResult.activeInHierarchy && !Bootstrap.Presentation.IsTransitioning, "final result visible");
        yield return SyncContest("final-result");
        if (host)
        {
            PageButton(lastResult, "메인 메뉴").onClick.Invoke();
            yield return Until(() => !sessions.IsBusy && !manager.IsListening, "Host closed original room from the result screen", 80);
            var create = sessions.CreateRoomAsync();
            yield return Until(() => create.IsCompleted && sessions.HasSession, "Host created a new room", 80);
            var leave = Bootstrap.SessionUI.Leave();
            yield return Until(() => leave.IsCompleted && !manager.IsListening, "new room closed", 80);
        }
        else yield return Until(() => !manager.IsListening && sessions.Issue == MultiplayerSessionManager.RoomIssue.HostLeft,
            "Client received Host closed notice", 80);
        File.WriteAllText(Path.Combine(evidence, ContestRole + "-rematch-pass.txt"), "PASS real finish, result, consent and repeated rematch.");
    }

    IEnumerator ObserveResultMotion(GameObject view)
    {
        GameObject preview = null;
        yield return Until(() => view.activeInHierarchy && (preview = GameObject.Find("Result character preview")),
            "result rig activates in the fade");
        var arms = preview.GetComponentsInChildren<Transform>().Where(item => item.name is "UpperArm.L" or "UpperArm.R").ToArray();
        Check(arms.Length == 2, "result rig has both animated upper arms");
        var pose = arms.Select(arm => arm.localRotation).ToArray();
        yield return Until(() => arms.Where((arm, index) => Quaternion.Angle(pose[index], arm.localRotation) > .1f).Any(),
            "result FBX animates the copied skeleton", 2);
        Check(!preview.GetComponentInChildren<NetworkObject>() && !preview.GetComponentInChildren<Collider>() &&
            !preview.GetComponentInChildren<AudioListener>(), "result preview has visual components only");
    }

    IEnumerator CheckResultWindows()
    {
        int previousWidth = Screen.width, previousHeight = Screen.height;
        foreach (int width in new[] { 1280, 1600, 1920, 2560 })
        {
            int height = width * 9 / 16;
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            yield return new WaitForSeconds(.35f);
            Debug.Log($"RESULT_WINDOW requested={width}x{height} observed={Screen.width}x{Screen.height}");
        }
        Screen.SetResolution(previousWidth, previousHeight, FullScreenMode.Windowed);
        yield return new WaitForSeconds(.35f);
    }

    void CheckRematchAudio(int cycle, bool won)
    {
        if (!Environment.GetCommandLineArgs().Contains("-twinAudioTrace")) return;
        Check(winSounds == (won ? cycle : 0) && loseSounds == (won ? 0 : cycle) &&
              courseTrack?.Contains(won ? " Summit " : " Cavern ") == true, "correct result music and single local result cue");
    }

    IEnumerator FinishNewMatch(GameStateManager state)
    {
        var gem = GemOwnership.Current;
        Vector3 summit = MarkerPoint("Mountain.V3.ReturnFinish");
        var station = summit + Vector3.forward * 10;
        ContestPlace(station + Vector3.back * (host ? 5 : 1.1f), Quaternion.identity);
        yield return SyncContest($"gem-placed-{state.MatchIdentity}");
        if (host)
        {
            Check(state.AdvanceFoundation(FoundationPhase.SealPending), "test staged SealPending");
            gem.UnlockServer(V3GameplayBootstrap.SafeGround(station) + Vector3.up);
        }
        yield return Until(() => gem.State == GemState.Available, "staged gem available");
        yield return new WaitForSeconds(.25f);
        if (!host) gem.RequestAcquire();
        yield return Until(() => gem.State == GemState.Carried && state.Phase == FoundationPhase.Return,
            "approved pickup and first Return transition work again");
        yield return Until(() => !state.TransitionBlocksInput, "Return transition cleared");
        if (!host) ContestPlace(summit, Quaternion.identity);
        yield return Until(() => state.Phase == FoundationPhase.Finished, "approved finish works again");
    }

    void CaptureRematchView(string label, Camera camera = null)
    {
        foreach (var portrait in Camera.allCameras.Where(item => item.name == "Portrait Camera"))
            portrait.Render();
        using var target = new PresentationRelaySmoke.CaptureTarget(Bootstrap.Presentation.Canvas,
            camera ? camera : Bootstrap.standbyCamera, 1600, 900);
        Canvas.ForceUpdateCanvases();
        target.Save(Path.Combine(evidence, $"{ContestRole}-{label}-1600x900.png"));
    }

    static string RematchResourceCounts() =>
        $"Net={FindObjectsByType<NetworkObject>(FindObjectsSortMode.None).Length}, " +
        $"Camera={Camera.allCamerasCount}, Listener={FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.enabled)}, " +
        $"EventSystem={FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length}, Canvas={FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length}, " +
        $"Player={FindObjectsByType<GameplayCharacter>(FindObjectsSortMode.None).Length}, AudioSource={FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length}, " +
        $"Preview={FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(camera => camera.name == "Portrait Camera")}, " +
        $"LevelRoots={UnityEngine.SceneManagement.SceneManager.GetSceneByName(Bootstrap.levelScene).rootCount}";

    static FieldInfo ResultFieldInfo(Component owner, string name) =>
        owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

    static T ResultField<T>(Component owner, string name) => (T)ResultFieldInfo(owner, name).GetValue(owner);

    static void SetResultField(Component owner, string name, object value) => ResultFieldInfo(owner, name).SetValue(owner, value);

    static void SetResultFixture<T>(Component owner, string name, T value) where T : unmanaged =>
        ResultField<NetworkVariable<T>>(owner, name).Value = value;
}
#endif
