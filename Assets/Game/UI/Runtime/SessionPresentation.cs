using System.Threading.Tasks;
using TwinEdge.Gameplay;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MultiplayerSessionManager;

namespace TwinEdge.Presentation
{
    public sealed class SessionPresentation : MonoBehaviour
    {
        const string NicknamePreference = "TwinEdge.Nickname";
        enum ScreenView { WorldLoading, Landing, Join, Connecting, Lobby, Preparing, Match }

        V3GameplayBootstrap game;
        PresentationRoot root;
        MultiplayerSessionManager session;
        UiKit ui;
        Camera menuCamera;
        bool originalCameraEnabled;
        ScreenView visible = ScreenView.WorldLoading;
        ScreenView disconnectedView = ScreenView.Landing;
        GameObject landing, join, lobby, loading, quitModal;
        UiTextField nickname, roomCode;
        UiButton create, joinPage, connect, joinBack, paste, copy, leave, cancelQuit;
        Text landingStatus, joinStatus, lobbyStatus, lobbyCode, lobbyCount, loadingStatus, loadingDetail;
        RectTransform activity;
        readonly LobbySlot[] slots = { new(), new() };
        readonly GameplayCharacter[] peers = new GameplayCharacter[2];
        string landingMessage = "", joinMessage = "", lobbyMessage = "", matchMessage = "";
        string operationText, pendingJoinCode, previousClientRoom;
        bool rejoining, attempting, departing;
        bool usedDisconnectNotice;
        bool hadPartner;
        float copiedUntil;
        float matchMessageUntil;
        GameObject notice;
        Text matchNotice;
        bool noticeVisible;

        // The Editor component showcase temporarily borrows the live screen layer.
        public void ResumeScreens()
        {
            enabled = true;
            root.ShowScreen(CurrentPanel());
        }

        public void Initialize(V3GameplayBootstrap bootstrap, Font font, Sprite emblem)
        {
            game = bootstrap;
            root = game.Presentation;
            session = game.sessions;
            ui = new UiKit(font, emblem);
            originalCameraEnabled = game.standbyCamera.enabled;
            BuildLanding();
            BuildJoin();
            BuildLobby();
            BuildLoading();
            BuildQuitModal();
            BuildMatchNotice();
            root.ShowScreen(loading);
        }

        internal void SetWorldCamera(Pose pose)
        {
            if (!menuCamera)
            {
                menuCamera = new GameObject("Menu World Camera", typeof(Camera)).GetComponent<Camera>();
                SceneManager.MoveGameObjectToScene(menuCamera.gameObject, game.gameObject.scene);
                menuCamera.CopyFrom(game.standbyCamera);
                menuCamera.tag = "Untagged";
            }
            menuCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            UpdateWorldCamera();
        }

        void Update()
        {
            ObserveDisconnect();
            ReadPlayers();
            if (!session.HasSession) hadPartner = false;
            else if (peers[0] && peers[1]) hadPartner = true;
            ScreenView desired = DesiredView();
            if (visible != desired && !root.IsTransitioning) Show(desired);
            UpdateWorldCamera();
            RefreshControls(desired);
        }

        void ObserveDisconnect()
        {
            if (!session.WasDisconnected)
            {
                usedDisconnectNotice = false;
                return;
            }
            if (usedDisconnectNotice) return;
            usedDisconnectNotice = true;
            if (session.Issue == RoomIssue.HostLeft || string.IsNullOrEmpty(session.InterruptedRoomCode))
            {
                disconnectedView = ScreenView.Landing;
                landingMessage = "방장이 연결을 종료했습니다. 새 방을 만들어 주세요.";
                roomCode.SetTextWithoutNotify("");
                previousClientRoom = null;
                rejoining = false;
                return;
            }
            roomCode.SetTextWithoutNotify(session.InterruptedRoomCode);
            disconnectedView = ScreenView.Join;
            rejoining = true;
            joinMessage = "연결이 끊겼습니다. 방이 열려 있으면 같은 코드로 다시 참여할 수 있습니다.";
        }

        void ReadPlayers()
        {
            peers[0] = peers[1] = null;
            var spawnManager = game.manager.SpawnManager;
            if (!game.manager.IsListening || spawnManager == null) return;
            foreach (var spawned in spawnManager.SpawnedObjectsList)
            {
                if (!spawned.IsPlayerObject || !spawned.TryGetComponent(out GameplayCharacter peer) ||
                    peer.LobbySlot >= peers.Length) continue;
                peers[peer.LobbySlot] = peer;
            }
        }

        ScreenView DesiredView()
        {
            if (!game.WorldReady && !game.WorldFailed) return ScreenView.WorldLoading;
            if (departing) return ScreenView.Connecting;
            if (attempting)
                return !string.IsNullOrEmpty(pendingJoinCode) && game.manager.IsListening
                    ? ScreenView.Lobby : ScreenView.Connecting;
            if (!session.HasSession) return disconnectedView;
            if (!game.manager.IsConnectedClient || !game.state.IsSpawned) return ScreenView.Connecting;

            var phase = game.state.Phase;
            if (phase == FoundationPhase.Finished) return ScreenView.Match;
            if (phase >= FoundationPhase.Outbound && game.state.IsGameReady &&
                game.manager.LocalClient?.PlayerObject is { } local &&
                local.TryGetComponent(out GameplayCharacter character) && character.FoundationReady)
                return ScreenView.Match;
            if (visible != ScreenView.Lobby && (peers[game.manager.IsHost ? 0 : 1] == null ||
                (!game.manager.IsHost && phase >= FoundationPhase.Outbound)))
                return ScreenView.Preparing;
            return ScreenView.Lobby;
        }

        void Show(ScreenView view)
        {
            var panel = view switch
            {
                ScreenView.Landing => landing,
                ScreenView.Join => join,
                ScreenView.Lobby => lobby,
                ScreenView.Match => null,
                _ => loading
            };
            if (view == ScreenView.Match && rejoining)
            {
                matchMessage = "방에 다시 참여했습니다.";
                matchMessageUntil = Time.unscaledTime + 4;
                rejoining = false;
            }
            if (panel != CurrentPanel())
            {
                var focus = view == ScreenView.Join ? roomCode.gameObject : null;
                if (!root.FadeTo(panel, panel != null, focus)) return;
            }
            visible = view;
        }

        GameObject CurrentPanel() => visible switch
        {
            ScreenView.Landing => landing,
            ScreenView.Join => join,
            ScreenView.Lobby => lobby,
            ScreenView.Match => null,
            _ => loading
        };

        void UpdateWorldCamera()
        {
            if (!menuCamera) return;
            bool showMenu = !game.WorldFailed && CurrentScreenIsActive();
            menuCamera.enabled = showMenu;
            game.standbyCamera.enabled = !showMenu && originalCameraEnabled;
        }

        bool CurrentScreenIsActive() => landing.activeSelf || join.activeSelf || lobby.activeSelf || loading.activeSelf;

        void RefreshControls(ScreenView desired)
        {
            var busy = session.IsBusy || attempting || departing;
            create.interactable = joinPage.interactable = game.WorldReady && !busy;
            connect.interactable = joinBack.interactable = paste.interactable = !busy;
            copy.interactable = !string.IsNullOrEmpty(session.CurrentJoinCode);
            leave.interactable = !busy && session.HasSession;
            SetText(landingStatus, game.WorldFailed ? "월드를 불러오지 못했습니다. 게임을 다시 실행해 주세요." : landingMessage);
            SetText(joinStatus, joinMessage);
            if (lobby.activeSelf)
            {
                int connected = game.state.IsSpawned ? game.state.ConnectedPlayerCount : 0;
                SetText(lobbyCode, string.IsNullOrEmpty(session.CurrentJoinCode) ? pendingJoinCode : session.CurrentJoinCode);
                SetText(lobbyCount, $"연결된 플레이어  {connected} / 2");
                SetText(lobbyStatus, Time.unscaledTime < copiedUntil ? "방 코드를 복사했습니다." :
                    attempting ? "방에 연결 중입니다." :
                    !string.IsNullOrEmpty(lobbyMessage) ? lobbyMessage :
                    hadPartner && connected < 2 ?
                        "상대 플레이어의 재입장을 기다리는 중입니다." :
                        connected < 2 ? "상대 플레이어를 기다리는 중입니다." : "두 플레이어의 경기 준비를 확인하는 중입니다.");
                var visual = game.manager.NetworkConfig.PlayerPrefab.transform.Find("VisualRoot");
                for (int i = 0; i < slots.Length; i++)
                    slots[i].Update(peers[i], game.manager.LocalClientId, visual, i);
            }
            else foreach (var slot in slots) slot.Clear();

            if (loading.activeSelf)
            {
                SetText(loadingStatus, departing ? "방에서 나가는 중" : operationText ?? (desired switch
                {
                    ScreenView.WorldLoading => "월드 준비 중",
                    ScreenView.Preparing => "경기 시작 준비 중",
                    _ => "연결 중"
                }));
                SetText(loadingDetail, desired == ScreenView.Preparing ?
                    "두 플레이어의 월드와 캐릭터 준비 상태를 확인하는 중입니다." : "");
                float offset = Mathf.PingPong(Time.unscaledTime * .75f, .75f);
                activity.anchorMin = new Vector2(offset, 0);
                activity.anchorMax = new Vector2(offset + .25f, 1);
            }

            bool showNotice = visible == ScreenView.Match && !root.IsTransitioning &&
                Time.unscaledTime < matchMessageUntil;
            if (showNotice != noticeVisible)
            {
                noticeVisible = showNotice;
                SetText(matchNotice, matchMessage);
                root.ShowOverlay(showNotice ? notice : null);
            }
        }

        static void SetText(Text label, string value)
        {
            if (label.text != value) label.text = value;
        }

        async Task Submit(bool host)
        {
            if (!game.WorldReady || session.HasSession || session.IsBusy || attempting || departing) return;
            if (!PlayerNickname.TryNormalize(nickname.text, out string name))
            {
                const string invalid = "닉네임은 줄바꿈이나 제어문자 없이 2~12자로 입력해 주세요.";
                if (host) landingMessage = invalid;
                else joinMessage = invalid;
                return;
            }
            string code = "";
            if (!host && !TryNormalizeJoinCode(roomCode.text, out code))
            {
                joinMessage = ErrorMessage(RoomIssue.InvalidCode, false, rejoining);
                return;
            }
            if (!host) rejoining = code == previousClientRoom || code == session.InterruptedRoomCode;

            nickname.SetTextWithoutNotify(name);
            PlayerPrefs.SetString(NicknamePreference, name);
            PlayerPrefs.Save();
            departing = false;
            attempting = true;
            pendingJoinCode = code;
            operationText = host ? "방 생성 중" : "방에 연결 중";
            lobbyMessage = "";
            copiedUntil = 0;
            if (host) disconnectedView = ScreenView.Landing;

            if (host) await session.CreateRoomAsync(name);
            else await session.JoinRoomAsync(code, name);
            attempting = false;
            pendingJoinCode = null;
            operationText = null;
            if (!this) return;
            if (session.HasSession)
            {
                if (!host) previousClientRoom = code;
                landingMessage = "";
                joinMessage = "";
                if (rejoining) lobbyMessage = "방에 다시 참여했습니다.";
            }
            else
            {
                var message = ErrorMessage(session.Issue, host, rejoining);
                if (host) landingMessage = message;
                else if (rejoining && session.Issue == RoomIssue.NotFound)
                {
                    BackToLanding();
                    landingMessage = message;
                    roomCode.SetTextWithoutNotify("");
                    previousClientRoom = null;
                }
                else joinMessage = message;
            }
        }

        static string ErrorMessage(RoomIssue issue, bool host, bool rejoin) => issue switch
        {
            RoomIssue.InvalidCode => "방 코드는 영문과 숫자 6자리입니다. 코드를 확인해 주세요.",
            RoomIssue.NotFound when rejoin => "기존 방이 종료되었습니다. 새 방을 만들어 주세요.",
            RoomIssue.NotFound => "방을 찾을 수 없습니다. 코드를 다시 확인해 주세요.",
            RoomIssue.Full => "방에 이미 두 명이 참여하고 있습니다.",
            RoomIssue.InvalidNickname => "닉네임을 확인해 주세요. 메인 메뉴에서 수정할 수 있습니다.",
            _ when host => "방을 만들지 못했습니다. 연결 상태를 확인하고 다시 시도해 주세요.",
            _ => "방에 연결하지 못했습니다. 네트워크 상태를 확인하고 다시 시도해 주세요."
        };

        internal async Task Leave()
        {
            if (session.IsBusy || departing || !session.HasSession) return;
            departing = true;
            await session.LeaveRoomAsync();
            if (!this) return;
            departing = false;
            disconnectedView = ScreenView.Landing;
            landingMessage = "방에서 나왔습니다.";
            joinMessage = "";
            roomCode.SetTextWithoutNotify("");
            rejoining = false;
        }

        void OpenJoin()
        {
            if (!PlayerNickname.TryNormalize(nickname.text, out _))
            {
                landingMessage = "닉네임은 2~12자로 입력해 주세요.";
                return;
            }
            joinMessage = "";
            disconnectedView = ScreenView.Join;
        }

        void BackToLanding()
        {
            disconnectedView = ScreenView.Landing;
            joinMessage = "";
            rejoining = false;
        }

        void CopyCode()
        {
            if (string.IsNullOrEmpty(session.CurrentJoinCode)) return;
            GUIUtility.systemCopyBuffer = session.CurrentJoinCode;
            copiedUntil = Time.unscaledTime + 2.5f;
        }

        GameObject Page(string title, byte shade)
        {
            var backdrop = UiKit.Surface(root.Screens, title, new Color32(19, 17, 15, shade), true);
            UiKit.Stretch(backdrop.rectTransform);
            backdrop.gameObject.SetActive(false);
            return backdrop.gameObject;
        }

        RectTransform Panel(GameObject page, Vector2 anchor, Vector2 size)
        {
            var content = ui.Panel(page.transform, PresentationTheme.Accent);
            var frame = (RectTransform)content.parent;
            frame.anchorMin = frame.anchorMax = anchor;
            frame.sizeDelta = size;
            UiKit.Stack(content, 13, 12);
            return content;
        }

        Text Label(Transform parent, string text, int size, float height, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = ui.Text(parent, text, size, color, alignment);
            UiKit.Measure(label.rectTransform, height);
            return label;
        }

        void BuildLanding()
        {
            landing = Page("Main Menu", 25);
            var panel = Panel(landing, new Vector2(.28f, .5f), new Vector2(540, 595));
            ui.Brand(panel);
            Label(panel, "닉네임", PresentationTheme.CaptionSize, 36, PresentationTheme.SecondaryText);
            nickname = ui.Field(panel, "닉네임 입력 (2~12자)", 128);
            nickname.onValidateInput = (_, _, c) => PlayerNickname.IsAllowed(c) ? c : '\0';
            if (PlayerNickname.TryNormalize(PlayerPrefs.GetString(NicknamePreference), out string saved))
                nickname.SetTextWithoutNotify(saved);
            nickname.onValueChanged.AddListener(_ => landingMessage = "");
            create = ui.Button(panel, "방 생성", () => _ = Submit(true));
            joinPage = ui.Button(panel, "방 참여", OpenJoin);
            ui.Button(panel, "게임 종료", () => root.ShowModal(quitModal, cancelQuit.gameObject));
            landingStatus = Label(panel, "", PresentationTheme.CaptionSize, 75, PresentationTheme.Parchment);
        }

        void BuildJoin()
        {
            join = Page("Join Room", 75);
            var panel = Panel(join, new Vector2(.5f, .5f), new Vector2(570, 450));
            Label(panel, "방 참여", PresentationTheme.TitleSize, 68, PresentationTheme.PrimaryText);
            Label(panel, "방 코드", PresentationTheme.CaptionSize, 36, PresentationTheme.SecondaryText);
            var row = Row(panel, 58);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            roomCode = ui.Field(row, "영문과 숫자 6자리", 24);
            roomCode.GetComponent<LayoutElement>().flexibleWidth = 1;
            paste = ui.Button(row, "붙여넣기", () => roomCode.text = GUIUtility.systemCopyBuffer);
            paste.GetComponent<LayoutElement>().minWidth = 145;
            joinStatus = Label(panel, "", PresentationTheme.CaptionSize, 80, PresentationTheme.Parchment);
            var actions = Row(panel, 56);
            joinBack = ui.Button(actions, "뒤로 가기", BackToLanding);
            connect = ui.Button(actions, "참여", () => _ = Submit(false));
        }

        void BuildLobby()
        {
            lobby = Page("Lobby", 85);
            var panel = Panel(lobby, new Vector2(.5f, .5f), new Vector2(850, 835));
            Label(panel, "온라인 대기실", PresentationTheme.TitleSize, 66, PresentationTheme.PrimaryText);
            var codeRow = Row(panel, 56);
            codeRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var codeLabel = Label(codeRow, "방 코드", PresentationTheme.BodySize, 56, PresentationTheme.SecondaryText);
            codeLabel.GetComponent<LayoutElement>().minWidth = 128;
            lobbyCode = Label(codeRow, "", PresentationTheme.HeadingSize, 56, PresentationTheme.PrimaryText);
            lobbyCode.GetComponent<LayoutElement>().flexibleWidth = 1;
            copy = ui.Button(codeRow, "코드 복사", CopyCode);
            copy.GetComponent<LayoutElement>().minWidth = 160;
            lobbyCount = Label(panel, "", PresentationTheme.CaptionSize, 38, PresentationTheme.SecondaryText);
            var row = Row(panel, 430);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            for (int i = 0; i < slots.Length; i++)
            {
                var card = UiKit.Surface(row, "Player " + (i + 1), new Color32(47, 43, 37, 255));
                var width = card.gameObject.AddComponent<LayoutElement>();
                width.minWidth = width.preferredWidth = 0;
                width.flexibleWidth = 1;
                UiKit.Stack(card.transform, 5, 12);
                var slot = slots[i];
                slot.Name = Label(card.transform, "", PresentationTheme.BodySize, 44, PresentationTheme.PrimaryText, TextAnchor.MiddleCenter);
                slot.Name.resizeTextForBestFit = true;
                slot.Name.resizeTextMinSize = 18;
                slot.Name.resizeTextMaxSize = PresentationTheme.BodySize;
                var portrait = UiKit.Rect(card.transform, "Character portrait");
                UiKit.Measure(portrait, 280);
                slot.Empty = ui.Text(portrait, "빈 자리", PresentationTheme.CaptionSize,
                    PresentationTheme.SecondaryText, TextAnchor.MiddleCenter);
                UiKit.Stretch(slot.Empty.rectTransform);
                slot.Image = UiKit.Rect(portrait, "Character render target").gameObject.AddComponent<RawImage>();
                var rect = slot.Image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(280, 280);
                slot.Image.raycastTarget = false;
                slot.Image.enabled = false;
                slot.Status = Label(card.transform, "", PresentationTheme.CaptionSize, 68, PresentationTheme.SecondaryText, TextAnchor.MiddleCenter);
            }
            lobbyStatus = Label(panel, "", PresentationTheme.CaptionSize, 51, PresentationTheme.Parchment);
            leave = ui.Button(panel, "방 나가기", () => _ = Leave());
        }

        void BuildLoading()
        {
            loading = Page("Loading", 253);
            var panel = Panel(loading, new Vector2(.5f, .5f), new Vector2(610, 360));
            ui.Brand(panel);
            loadingStatus = Label(panel, "월드 준비 중", PresentationTheme.HeadingSize, 62, PresentationTheme.PrimaryText);
            var groove = UiKit.Surface(panel, "Activity track", PresentationTheme.Stone);
            UiKit.Measure(groove.rectTransform, 5);
            activity = UiKit.Surface(groove.transform, "Activity", PresentationTheme.Accent).rectTransform;
            UiKit.Stretch(activity);
            loadingDetail = Label(panel, "", PresentationTheme.CaptionSize, 74, PresentationTheme.SecondaryText);
        }

        void BuildQuitModal()
        {
            var modal = ui.Modal(root.Modals);
            UiKit.Stack(modal, 18, 8);
            Label(modal, "게임을 종료할까요?", PresentationTheme.HeadingSize, 75, PresentationTheme.PrimaryText);
            var actions = Row(modal, 58);
            cancelQuit = ui.Button(actions, "취소", root.CloseModal);
            ui.Button(actions, "게임 종료", Application.Quit);
            quitModal = modal.parent.parent.gameObject;
            quitModal.SetActive(false);
        }

        void BuildMatchNotice()
        {
            var message = ui.Panel(root.Overlays, PresentationTheme.Accent);
            var frame = (RectTransform)message.parent;
            frame.anchorMin = frame.anchorMax = new Vector2(.5f, .16f);
            frame.sizeDelta = new Vector2(460, 88);
            matchNotice = ui.Text(message, "", PresentationTheme.CaptionSize, PresentationTheme.PrimaryText, TextAnchor.MiddleCenter);
            UiKit.Stretch(matchNotice.rectTransform);
            notice = frame.gameObject;
            notice.SetActive(false);
        }

        static RectTransform Row(Transform parent, int height)
        {
            var row = UiKit.Rect(parent, "Row");
            UiKit.Measure(row, height);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            return row;
        }

        void OnDestroy()
        {
            foreach (var slot in slots) slot.Clear();
            if (menuCamera) Destroy(menuCamera.gameObject);
            if (game && game.standbyCamera) game.standbyCamera.enabled = originalCameraEnabled;
        }

        sealed class LobbySlot
        {
            internal Text Name, Status, Empty;
            internal RawImage Image;
            LobbyCharacterPreview preview;

            internal void Update(GameplayCharacter peer, ulong localClient, Transform visual, int slot)
            {
                if (peer == null)
                {
                    Clear();
                    SetText(Name, $"플레이어 {slot + 1}");
                    SetText(Status, "연결 대기");
                    return;
                }
                string name = peer.OwnerClientId == localClient ? peer.Nickname + " (나)" : peer.Nickname;
                SetText(Name, name);
                string state = peer.FoundationReady ? "준비 완료" : "연결됨 · 준비 중";
                bool assigned = peer.CapeColorIndex < GameplayCharacter.CapeColorCount;
                SetText(Status, assigned ? state : state + "\n망토 배정 대기");
                preview ??= new LobbyCharacterPreview(Image, visual, slot);
                Empty.enabled = false;
                preview.SetCape(peer.CapeColorIndex);
            }

            internal void Clear()
            {
                if (preview == null) return;
                preview.Dispose();
                preview = null;
                Empty.enabled = true;
            }
        }
    }
}
