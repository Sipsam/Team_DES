using TwinEdge.Audio;
using TwinEdge.Gameplay;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TwinEdge.Presentation
{
    [DefaultExecutionOrder(-30)]
    internal sealed class GameplayPresentation : MonoBehaviour
    {
        enum Lesson { Basics, Maps, Pole, Timber, SharedRaft, Castle, Crossing, Seal, Gem, Contest, SoloRaft, Bridge, Launch, Finish }
        static readonly string[] Lessons =
        {
            "WASD 이동 · Shift 달리기 · Space 점프 · E 상호작용",
            "미니맵은 북쪽 고정입니다. M 전체 지도 · N 미니맵 표시",
            "막대 양끝을 잡고 중심과 회전 역할을 나누세요. 착지한 뒤 E로 교대합니다.",
            "목재 양끝을 잡고 설치 위치까지 함께 옮기세요. 두 사람 모두 E를 눌러 설치합니다.",
            "공동 뗏목에서 각자 한쪽 노를 맡습니다. W/S 노젓기 · Shift 강한 스트로크",
            "내 문양을 상대에게 보내고 상대가 보낸 문양을 발판에 입력하세요.",
            "스위치가 상대의 발판을 엽니다. 발판이 닫히기 전에 건너세요.",
            "두 사람 모두 제단에 도착한 뒤 봉인을 함께 해제하세요.",
            "E로 보석을 얻으세요. 보석을 들고 결승에 도달하면 승리합니다.",
            "좌클릭 공격 · F 잡기/던지기 · 잡혔을 때는 F 연타",
            "개인 뗏목 · A/D 조향 · W/S 노젓기 · Shift 강한 스트로크",
            "붕괴 전조를 살피세요. Space 점프 · Shift + Space 장거리 점프",
            "R을 눌러 충전합니다. 마우스로 조준하고 W/S로 각도를 바꾸세요.",
            "보석을 들고 결승 지점에 착지하세요."
        };

        V3GameplayBootstrap game;
        PresentationRoot root;
        RectTransform overlay, staminaFill, pulse;
        GameObject staminaPanel, contextPanel, promptRow, tutorialPanel, notificationPanel, controls;
        Text context, promptKey, promptAction, tutorial, notification;
        Image track, bar, pulseImage;
        Canvas worldFeedback;
        readonly Nameplate[] names = { new(), new() };
        readonly RaycastHit[] sightHits = new RaycastHit[16];
        CourseMapPresentation map;
        MatchTransitionPresentation transitions;
        GameplayCharacter local;
        string roomId;
        ulong matchIdentity;
        uint seenLessons;
        Lesson showingLesson;
        FoundationPhase previousPhase;
        FixedString64Bytes checkpoint;
        int respawnCount;
        ulong gemOwner, formerCarrier;
        GemState gemState;
        float sampleAt, tutorialUntil, noticeUntil, pulseUntil;
        Vector3 pulsePoint;
        bool active, modalOpen, recovering, gemObserved, rejoined;
        float previousStamina;

        internal void Initialize(V3GameplayBootstrap bootstrap, UiKit ui)
        {
            game = bootstrap;
            root = game.Presentation;
            overlay = UiKit.Rect(root.Overlays, "Gameplay HUD");
            UiKit.Stretch(overlay);
            overlay.gameObject.SetActive(false);

            var stamina = Panel(ui, overlay, new Vector2(1, 0), new Vector2(-26, 24), new Vector2(218, 52));
            staminaPanel = stamina.parent.gameObject;
            track = UiKit.Surface(stamina, "Stamina track", PresentationTheme.Stone);
            UiKit.Stretch(track.rectTransform, 0, -5, 0, -5);
            bar = UiKit.Surface(track.transform, "Stamina", PresentationTheme.Accent);
            staminaFill = bar.rectTransform;
            UiKit.Stretch(staminaFill);
            var panel = Panel(ui, overlay, Vector2.zero, new Vector2(26, 24), new Vector2(546, 135));
            contextPanel = panel.parent.gameObject;
            UiKit.Stack(panel, 4);
            ui.KeyPrompt(panel, "", "");
            promptRow = panel.GetChild(0).gameObject;
            var texts = promptRow.GetComponentsInChildren<Text>();
            promptKey = texts[0];
            promptAction = texts[1];
            LayoutElement flexible = promptAction.gameObject.AddComponent<LayoutElement>();
            flexible.flexibleWidth = 1;
            context = ui.Text(panel, "", 22, PresentationTheme.PrimaryText);
            UiKit.Measure(context.rectTransform, 51);

            tutorial = LabelPanel(ui, overlay, Vector2.zero, new Vector2(26, 166), new Vector2(546, 92), out tutorialPanel);
            notification = LabelPanel(ui, overlay, new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(464, 78), out notificationPanel);
            worldFeedback = new GameObject("World notifications", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            worldFeedback.transform.SetParent(game.transform, false);
            worldFeedback.renderMode = RenderMode.WorldSpace;
            var worldRect = (RectTransform)worldFeedback.transform;
            worldRect.sizeDelta = new Vector2(20, 20);
            worldRect.localScale = Vector3.one * .021f;
            pulseImage = UiKit.Surface(worldRect, "World event", Color.white);
            pulse = pulseImage.rectTransform;
            pulse.sizeDelta = new Vector2(16, 16);
            pulse.localRotation = Quaternion.Euler(0, 0, 45);
            pulse.gameObject.SetActive(false);
            foreach (var name in names) name.Create(ui, overlay);

            BuildControls(ui);
            map = new CourseMapPresentation(root, ui, overlay, CloseModal);
            transitions = new MatchTransitionPresentation(ui, overlay, game.transform);
        }

        internal void Refresh(bool matchView, GameplayCharacter[] players)
        {
            var peer = players[game.manager.IsHost ? 0 : 1];
            bool show = matchView && peer && peer.IsSpawned && game.state.Phase != FoundationPhase.Finished;
            if (!show)
            {
                CloseModal();
                transitions.Hide();
                if (active)
                {
                    root.ShowOverlay(null);
                    active = false;
                    local = null;
                    pulse.gameObject.SetActive(false);
                }
                return;
            }
            if (!active)
            {
                root.ShowOverlay(overlay.gameObject);
                active = true;
            }

            if (local != peer)
            {
                local = peer;
                if (roomId != game.sessions.MatchRoomId || matchIdentity != game.state.MatchIdentity)
                {
                    roomId = game.sessions.MatchRoomId;
                    matchIdentity = game.state.MatchIdentity;
                    seenLessons = 0;
                }
                checkpoint = peer.CheckpointName.Value;
                respawnCount = peer.RespawnCount.Value;
                previousPhase = game.state.Phase;
                gemObserved = false;
                formerCarrier = ulong.MaxValue;
                noticeUntil = tutorialUntil = pulseUntil = 0;
                sampleAt = 0;
                previousStamina = peer.StaminaFraction;
                recovering = false;
            }

            if (rejoined)
            {
                Announce("방에 다시 참여했습니다.", null);
                rejoined = false;
            }
            float now = Time.unscaledTime;
            if (game.state.Phase != previousPhase)
            {
                previousPhase = game.state.Phase;
                tutorialUntil = 0;
                sampleAt = 0;
            }
            if (respawnCount != local.RespawnCount.Value)
            {
                game.state.PlayLiveCue(AudioCueId.Recovery);
                respawnCount = local.RespawnCount.Value;
                checkpoint = local.CheckpointName.Value;
                tutorialUntil = 0;
                sampleAt = 0;
                Announce("체크포인트에서 복구했습니다.", local.transform.position);
            }
            else if (!checkpoint.Equals(local.CheckpointName.Value))
            {
                game.state.PlayLiveCue(AudioCueId.Checkpoint);
                checkpoint = local.CheckpointName.Value;
                Announce("체크포인트를 활성화했습니다.", local.transform.position);
            }

            var gem = GemOwnership.Current;
            if (gem && gem.IsSpawned) ObserveGem(gem, players);
            bool presenting = transitions.Refresh(game.state, game.manager.IsHost ? 0 : 1);
            bool hideContext = presenting && game.state.TransitionBlocksInput;
            if (hideContext)
            {
                contextPanel.SetActive(false);
                tutorialUntil = 0;
                sampleAt = 0;
            }
            else if (now >= sampleAt)
            {
                sampleAt = now + .1f;
                RefreshContext(players[game.manager.IsHost ? 1 : 0]);
            }

            float ratio = Mathf.Clamp01(peer.StaminaFraction);
            if (ratio != previousStamina)
            {
                recovering = ratio < 1 && ratio > previousStamina;
                previousStamina = ratio;
            }
            var fill = new Vector2(ratio, 1);
            if (staminaFill.anchorMax != fill) staminaFill.anchorMax = fill;
            bar.color = recovering ? new Color32(177, 198, 154, 255) : PresentationTheme.Accent;
            track.color = ratio <= 0 ? new Color32(159, 79, 59, 255) : PresentationTheme.Stone;
            staminaPanel.SetActive(true);
            tutorialPanel.SetActive(now < tutorialUntil);
            notificationPanel.SetActive(!presenting && now < noticeUntil);
            map.Refresh(game, peer, players, gem);
            for (int i = 0; i < names.Length; i++) names[i].Bind(players[i]);
        }

        internal void QueueRejoinNotice() => rejoined = true;

        void ObserveGem(GemOwnership gem, GameplayCharacter[] peers)
        {
            bool representedByTransition = gem.IsFirstReturnPickup ||
                gem.State == GemState.Available && game.state.Transition.Kind == MatchTransition.Seal;
            if (!representedByTransition && gemObserved && (gem.State != gemState || gem.Owner != gemOwner))
            {
                if (gemState == GemState.Carried) formerCarrier = gemOwner;
                bool stolen = formerCarrier != ulong.MaxValue && gem.Owner != formerCarrier &&
                    gemState is GemState.Dropped or GemState.Carried;
                string message = gem.State switch
                {
                    GemState.Available => "보석 봉인이 풀렸습니다.",
                    GemState.Carried when gem.Owner == local.OwnerClientId => stolen ? "상대의 보석을 탈취했습니다." : "보석을 획득했습니다.",
                    GemState.Carried => stolen ? "상대가 보석을 탈취했습니다." : "상대가 보석을 획득했습니다.",
                    GemState.Dropped => formerCarrier == local.OwnerClientId ? "보석을 떨어뜨렸습니다." : "상대가 보석을 떨어뜨렸습니다.",
                    GemState.Relocated => "보석이 복로 체크포인트로 이동했습니다.",
                    _ => null
                };
                Vector3 point = gem.Position;
                if (gem.State == GemState.Carried)
                    foreach (var peer in peers)
                        if (peer && peer.OwnerClientId == gem.Owner) point = peer.NameplateAnchor;
                switch (gem.State)
                {
                    case GemState.Carried:
                        game.state.PlayLiveCue(stolen ? AudioCueId.GemSteal : AudioCueId.GemPickup, point);
                        break;
                    case GemState.Dropped:
                        game.state.PlayLiveCue(AudioCueId.GemDrop, point);
                        break;
                    case GemState.Relocated:
                        game.state.PlayLiveCue(AudioCueId.GemRelocate, point);
                        break;
                }
                if (message != null) Announce(message, point);
            }
            gemObserved = true;
            gemState = gem.State;
            gemOwner = gem.Owner;
        }

        void Announce(string message, Vector3? point)
        {
            float now = Time.unscaledTime;
            if (now < noticeUntil - 3.3f && !notification.text.Contains('\n') && notification.text != message)
                message = notification.text + "\n" + message;
            SetText(notification, message);
            noticeUntil = now + 3.5f;
            if (!point.HasValue) return;
            pulsePoint = point.Value + Vector3.up * .65f;
            pulseUntil = Time.unscaledTime + .85f;
        }

        void RefreshContext(GameplayCharacter rival)
        {
            var player = local.NetworkObject;
            var movement = local.GetComponent<PlayerMovement>();
            var target = local.GetComponent<PlayerInteraction>().CurrentTarget;
            var phase = game.state.Phase;
            bool returning = phase == FoundationPhase.Return;
            string key = null, action = null, detail = null;
            Lesson lesson = Lesson.Basics;
            var bridge = BridgeGameplay.Current;
            var pole = game.cooperativePole?.PresentationHint(player);
            bool damaged = false;
            var river = RiverGameplay.Current?.Hud(movement, out damaged);
            context.color = damaged ? new Color32(255, 177, 139, 255) : PresentationTheme.PrimaryText;
            var castle = CastleGameplay.Current?.PresentationHint(player);
            var cave = UndergroundGameplay.Current?.PresentationHint(player);
            var launch = local.GetComponent<MountainReturnLaunch>()?.PresentationHint();
            var contest = local.ContestPresentation(rival);
            if (target is CastleStation station && station.CanInteract)
            {
                key = "E";
                action = station.Kind switch
                {
                    CastleGameplay.Action.Pad => "문양 발판 입력",
                    CastleGameplay.Action.ReadHint => "내 문양 확인",
                    CastleGameplay.Action.Signal => "상대에게 문양 보내기",
                    CastleGameplay.Action.ClearSignal => "송신 문양 지우기",
                    CastleGameplay.Action.Collect => "내 방의 번호 확인",
                    CastleGameplay.Action.Keypad => "키패드 열기",
                    CastleGameplay.Action.ClearSewer => "하수도 입력 지우기",
                    _ => "하수도 문양 입력"
                };
            }
            else if (target is UndergroundStation underground && underground.CanInteract)
            {
                key = "E";
                action = underground.IsGem ? "보석 줍기" :
                    underground.IsSeal ? "공동 봉인 해제" : "상대 발판 열기";
            }
            else if (target is RiverDockInteraction dock && dock.CanInteract)
            {
                key = "E";
                action = returning ? "개인 뗏목 타기" : "공동 뗏목 타기";
            }
            if (local.HasContestPose && contest != null)
            {
                lesson = Lesson.Contest;
                detail = contest;
                key = action = null;
            }
            else if (map.NearFinish(local.transform.position) && returning)
            {
                lesson = Lesson.Finish;
                detail = gemState == GemState.Carried && gemOwner == local.OwnerClientId ?
                    "보석 소유 · 결승 지점에 착지하세요." : "결승에는 보석을 가진 채 도착해야 합니다.";
            }
            else if (cave != null)
            {
                lesson = action == "보석 줍기" ? Lesson.Gem :
                    phase == FoundationPhase.SealPending || action == "공동 봉인 해제" ? Lesson.Seal : Lesson.Crossing;
                detail = cave;
            }
            else if (castle != null) { lesson = Lesson.Castle; detail = castle; }
            else if (river != null) { lesson = returning ? Lesson.SoloRaft : Lesson.SharedRaft; detail = river; }
            else if (launch != null) { lesson = Lesson.Launch; detail = launch; }
            else if (pole != null)
            {
                lesson = Lesson.Pole;
                detail = pole;
                if (ReferenceEquals(target, game.cooperativePole)) { key = "E"; action = "막대 잡기 / 교대"; }
            }
            else if (bridge && bridge.PlankCount > 0 && bridge.InBridgeAirspace(local.transform.position, 9) &&
                Mathf.Abs(local.transform.position.y - bridge.StartPoint.y) < 9)
            {
                lesson = returning ? Lesson.Bridge : Lesson.Timber;
                if (returning) detail = bridge.NearbyWarning(local.transform.position)
                    ? "발판이 무너집니다! Space 점프 · Shift + Space 장거리 점프"
                    : "Space 점프 · Shift + Space 장거리 점프";
                else
                {
                    detail = $"목재 설치 {bridge.CompletedDeliveries}/{bridge.TotalDeliveries}";
                    if (bridge.Holds(local.OwnerClientId))
                        detail += bridge.Load.Paired ? "\n함께 이동하고 반대 방향 입력으로 회전 · Esc 반납" :
                            "\n상대가 반대편을 잡을 때까지 기다리세요. Esc 반납";
                    if (ReferenceEquals(target, bridge) && (!bridge.Holds(local.OwnerClientId) || bridge.CanConfirmTimber(local.OwnerClientId)))
                    {
                        key = "E";
                        action = !bridge.Load.supplied ? "목재 꺼내기" :
                            bridge.CanConfirmTimber(local.OwnerClientId) ? "목재 설치 확인" : "목재 끝 잡기";
                    }
                }
            }
            else if (contest != null) { lesson = Lesson.Contest; detail = contest; }

            if (showingLesson > Lesson.Maps && showingLesson != lesson) tutorialUntil = 0;
            if (!game.state.CanPlay)
            {
                detail = "상대 플레이어의 재입장을 기다리는 중입니다.";
                key = action = null;
            }
            else if (Time.unscaledTime >= tutorialUntil)
            {
                if ((seenLessons & 1) == 0) Teach(Lesson.Basics);
                else if (lesson != Lesson.Basics) Teach(lesson);
                if (Time.unscaledTime >= tutorialUntil && (seenLessons & 2) == 0) Teach(Lesson.Maps);
            }
            promptRow.SetActive(action != null);
            if (action != null)
            {
                SetText(promptKey, key);
                SetText(promptAction, action);
            }
            SetText(context, detail ?? "");
            contextPanel.SetActive(detail != null || action != null);
        }

        void Teach(Lesson lesson)
        {
            uint flag = 1u << (int)lesson;
            if ((seenLessons & flag) != 0) return;
            seenLessons |= flag;
            showingLesson = lesson;
            SetText(tutorial, Lessons[(int)lesson]);
            tutorialUntil = Time.unscaledTime + 6;
        }

        void Update()
        {
            if (!active || !local || !local.IsSpawned || Keyboard.current is not { } keys) return;
            if (modalOpen)
            {
                if (keys.escapeKey.wasPressedThisFrame || (map.IsOpen && keys.mKey.wasPressedThisFrame))
                    CloseModal();
                return;
            }
            if (PresentationRoot.BlocksGameplay || CastleGameplay.Current?.IsKeypadOpen == true ||
                DoorPuzzleUI.IsPanelVisible ||
                EventSystem.current?.currentSelectedGameObject?.GetComponent<InputField>() is { isFocused: true }) return;
            if (keys.nKey.wasPressedThisFrame) map.ToggleMinimap();
            if (keys.mKey.wasPressedThisFrame && local.GetComponent<MountainReturnLaunch>()?.HasLaunchInput != true)
                modalOpen = map.Open();
        }

        internal void ShowControls()
        {
            if (CastleGameplay.Current?.IsKeypadOpen != true && root.ShowModal(controls, null)) modalOpen = true;
        }

        void CloseModal()
        {
            if (!modalOpen) return;
            root.CloseModal();
            modalOpen = false;
        }

        void BuildControls(UiKit ui)
        {
            var content = ui.Modal(root.Modals);
            controls = content.parent.parent.gameObject;
            var splits = UiKit.Rect(content, "Controls");
            UiKit.Stretch(splits, 16, 56, 16, 42);
            var layouts = splits.gameObject.AddComponent<HorizontalLayoutGroup>();
            layouts.spacing = 16;
            layouts.childControlWidth = layouts.childControlHeight = true;
            layouts.childForceExpandWidth = true;
            for (int i = 0; i < 2; i++)
            {
                var column = UiKit.Rect(splits, "Instructions");
                UiKit.Stack(column, 4);
                for (int j = i * 7; j < (i + 1) * 7; j++)
                {
                    var text = ui.Text(column, Lessons[j], 17, PresentationTheme.PrimaryText);
                    UiKit.Measure(text.rectTransform, 46);
                }
            }
            var title = ui.Text(content, "조작법", 25, PresentationTheme.PrimaryText, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(.5f, 1), new Vector2(0, -17), new Vector2(240, 36));
            var close = ui.Button(content, "닫기", CloseModal);
            Place((RectTransform)close.transform, new Vector2(.5f, 0), new Vector2(0, 4), new Vector2(130, 42));
            var card = (RectTransform)content.parent;
            card.anchorMin = new Vector2(.12f, .16f);
            card.anchorMax = new Vector2(.88f, .84f);
            card.offsetMin = card.offsetMax = Vector2.zero;
            controls.SetActive(false);
        }

        void OnDestroy()
        {
            transitions?.Dispose();
            map?.Dispose();
            if (worldFeedback) Destroy(worldFeedback.gameObject);
        }

        void LateUpdate()
        {
            if (!active) return;
            var camera = game.standbyCamera;
            if (!camera || !camera.isActiveAndEnabled) return;
            foreach (var name in names) name.Draw(camera, overlay, sightHits);
            bool show = Time.unscaledTime < pulseUntil;
            pulse.gameObject.SetActive(show);
            if (!show) return;
            worldFeedback.transform.SetPositionAndRotation(pulsePoint, camera.transform.rotation);
            float t = Mathf.Clamp01((pulseUntil - Time.unscaledTime) / .85f);
            pulse.localScale = Vector3.one * Mathf.Lerp(2.8f, .6f, t);
            pulseImage.color = new Color(1, .81f, .49f, t * .7f);
        }

        static bool Project(Camera camera, RectTransform parent, Vector3 position, out Vector2 localPoint)
        {
            Vector3 viewport = camera.WorldToViewportPoint(position);
            localPoint = default;
            if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) return false;
            localPoint = parent.rect.min + Vector2.Scale(viewport, parent.rect.size);
            return true;
        }

        static RectTransform Panel(UiKit ui, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var content = ui.Panel(parent, PresentationTheme.Accent);
            var rect = (RectTransform)content.parent;
            rect.GetComponent<Image>().raycastTarget = false;
            Place(rect, anchor, position, size);
            return content;
        }

        static Text LabelPanel(UiKit ui, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, out GameObject panel)
        {
            var content = Panel(ui, parent, anchor, position, size);
            panel = content.parent.gameObject;
            var label = ui.Text(content, "", 22, PresentationTheme.PrimaryText, TextAnchor.MiddleCenter);
            UiKit.Stretch(label.rectTransform, 0, -8, 0, -8);
            return label;
        }

        internal static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x == 0 ? 0 : anchor.x == 1 ? 1 : .5f,
                anchor.y == 0 ? 0 : anchor.y == 1 ? 1 : .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void SetText(Text value, string text)
        {
            if (value.text != text) value.text = text;
        }

        sealed class Nameplate
        {
            GameplayCharacter peer;
            FixedString64Bytes nickname;
            float nextCheck;
            bool occluded;
            Text label;

            internal void Create(UiKit ui, Transform parent)
            {
                label = ui.Text(parent, "", 25, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                label.rectTransform.sizeDelta = new Vector2(280, 46);
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0, 0, 0, .9f);
                outline.effectDistance = new Vector2(2, -2);
                label.enabled = false;
            }

            internal void Bind(GameplayCharacter value)
            {
                if (peer != value)
                {
                    peer = value;
                    nextCheck = 0;
                }
                if (!peer || nickname.Equals(peer.ApprovedNickname)) return;
                nickname = peer.ApprovedNickname;
                label.text = nickname.ToString();
            }

            internal void Draw(Camera camera, RectTransform parent, RaycastHit[] hits)
            {
                if (!peer || !peer.IsSpawned || nickname.Length == 0)
                {
                    label.enabled = false;
                    return;
                }
                Vector3 point = peer.NameplateAnchor;
                Vector3 direction = point - camera.transform.position;
                float distance = direction.magnitude;
                if (distance > 60 || !Project(camera, parent, point, out var projected))
                {
                    label.enabled = false;
                    return;
                }
                if (Time.unscaledTime >= nextCheck)
                {
                    nextCheck = Time.unscaledTime + .1f;
                    int count = Physics.RaycastNonAlloc(camera.transform.position, direction.normalized, hits,
                        Mathf.Max(0, distance - .12f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    occluded = count == hits.Length;
                    for (int i = 0; i < count && !occluded; i++)
                        occluded = !hits[i].transform.GetComponentInParent<GameplayCharacter>();
                }
                label.enabled = !occluded;
                if (occluded) return;
                label.rectTransform.anchoredPosition = projected;
                label.rectTransform.localScale = Vector3.one * Mathf.Lerp(1, .58f, distance / 60);
            }
        }
    }
}
