using System;
using TwinEdge.Audio;
using TwinEdge.Gameplay;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TwinEdge.Presentation
{
    public sealed partial class SessionPresentation
    {
        const float DisplayConfirmationSeconds = 15;
        GameObject settingsPage, escapePage, localDialog;
        UiButton resumeButton, settingsBack, displayButton, dialogCancel, dialogConfirm;
        Text displayValue, minimapValue, dialogTitle, dialogMessage;
        Slider masterSlider, mouseSlider, effectSlider;
        Action cancelDialog, confirmDialog;
        bool restoreCapture;
        float displayDeadline;

        void BuildLocalMenus()
        {
            escapePage = Page("Escape Menu", 176);
            var pause = Panel(escapePage, new Vector2(.5f, .5f), new Vector2(520, 480));
            Label(pause, "경기 메뉴", 36, 58, PresentationTheme.PrimaryText);
            Label(pause, "메뉴를 열어도 온라인 경기는 계속 진행됩니다.", 21, 52, PresentationTheme.SecondaryText);
            resumeButton = ui.Button(pause, "계속하기", ResumeMatch);
            ui.Button(pause, "설정", OpenSettings);
            ui.Button(pause, "조작법 다시 보기", () => gameplay.ShowControls());
            ui.Button(pause, "방 나가기", ConfirmLeave);

            settingsPage = Page("Settings", 184);
            var panel = Panel(settingsPage, new Vector2(.5f, .5f), new Vector2(790, 630));
            Label(panel, "설정", 36, 60, PresentationTheme.PrimaryText);
            masterSlider = AddPercent(panel, "사운드  /  전체 음량", 0, 100, AudioService.SetMasterPercent);
            displayButton = AddChoice(panel, "화면  /  화면 모드", ToggleDisplay, out displayValue);
            displayButton.interactable = !Application.isEditor;
            AddChoice(panel, "화면  /  미니맵 표시", () =>
            {
                CourseMapPresentation.MinimapVisible = !CourseMapPresentation.MinimapVisible;
                RefreshSettings();
            }, out minimapValue);
            mouseSlider = AddPercent(panel, "조작  /  마우스 감도", LocalPresentationSettings.MinMousePercent,
                LocalPresentationSettings.MaxMousePercent, value => LocalPresentationSettings.MousePercent = value);
            effectSlider = AddPercent(panel, "연출  /  효과 강도", 0, 100, value => LocalPresentationSettings.EffectPercent = value);
            var buttons = Row(panel, 56);
            ui.Button(buttons, "기본값 복원", ConfirmDefaults);
            settingsBack = ui.Button(buttons, "뒤로 가기", CloseSettings);

            var modal = ui.Modal(root.Modals);
            localDialog = modal.parent.parent.gameObject;
            UiKit.Stack(modal, 16, 8);
            dialogTitle = Label(modal, "", 30, 52, PresentationTheme.PrimaryText);
            dialogMessage = Label(modal, "", 23, 94, PresentationTheme.SecondaryText);
            var actions = Row(modal, 56);
            dialogCancel = ui.Button(actions, "취소", CloseLocalDialog);
            dialogConfirm = ui.Button(actions, "확인", AcceptLocalDialog);
            localDialog.SetActive(false);
        }

        UiButton AddChoice(Transform parent, string title, Action change, out Text value)
        {
            var row = SettingsRow(parent, title);
            var button = ui.Button(row, "", change);
            button.name = title;
            button.GetComponent<LayoutElement>().flexibleWidth = 1;
            value = button.GetComponentInChildren<Text>();
            return button;
        }

        RectTransform SettingsRow(Transform parent, string title)
        {
            var row = Row(parent, 56);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var label = Label(row, title, 24, 56, PresentationTheme.PrimaryText);
            label.GetComponent<LayoutElement>().minWidth = 348;
            return row;
        }

        Slider AddPercent(Transform parent, string title, int min, int max, Action<int> apply)
        {
            var row = SettingsRow(parent, title);
            var control = UiKit.Rect(row, title);
            control.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var hit = UiKit.Surface(control, "Slider hit area", Color.clear, true);
            UiKit.Stretch(hit.rectTransform, 0, 0, 77);
            var track = UiKit.Surface(control, "Track", PresentationTheme.Stone);
            UiKit.Stretch(track.rectTransform, 15, 24, 80, 24);
            var fill = UiKit.Surface(track.transform, "Fill", PresentationTheme.Accent);
            UiKit.Stretch(fill.rectTransform);
            var travel = UiKit.Rect(control, "Thumb area");
            UiKit.Stretch(travel, 15, 8, 80, 8);
            var thumb = UiKit.Surface(travel, "Thumb", Color.white, true);
            thumb.rectTransform.sizeDelta = new Vector2(25, 0);
            var value = ui.Text(control, "", 22, PresentationTheme.PrimaryText, TextAnchor.MiddleRight);
            UiKit.Stretch(value.rectTransform);
            value.rectTransform.anchorMin = new Vector2(1, 0);
            value.rectTransform.offsetMin = new Vector2(-77, 0);

            var slider = control.gameObject.AddComponent<Slider>();
            slider.handleRect = thumb.rectTransform;
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = thumb;
            slider.colors = PresentationTheme.Controls(false);
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            // Update the label for both selection changes and silent synchronization.
            slider.onValueChanged.AddListener(amount =>
            {
                value.text = $"{amount:0}%";
                if (!syncingSettings) apply((int)amount);
            });
            value.text = $"{slider.value:0}%";
            return slider;
        }

        bool syncingSettings;
        void RefreshSettings()
        {
            syncingSettings = true;
            masterSlider.value = AudioService.MasterPercent;
            mouseSlider.value = LocalPresentationSettings.MousePercent;
            effectSlider.value = LocalPresentationSettings.EffectPercent;
            syncingSettings = false;
            displayValue.text = Screen.fullScreen ? "전체화면" : "창모드";
            minimapValue.text = CourseMapPresentation.MinimapVisible ? "켜짐" : "꺼짐";
        }

        void OpenSettings()
        {
            RefreshSettings();
            root.ShowScreen(settingsPage, true, settingsBack.gameObject, aboveOverlay: true);
        }

        void CloseSettings()
        {
            if (visible == ScreenView.Match) ShowEscapeMenu();
            else root.FadeTo(BasePanel());
        }

        void ShowEscapeMenu() => root.ShowScreen(escapePage, true, resumeButton.gameObject, aboveOverlay: true);

        void ResumeMatch()
        {
            root.ShowScreen(null, false);
            // Root keeps this frame blocked even if capture is restored immediately.
            if (restoreCapture && Application.isFocused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void UpdateLocalMenus(ScreenView view)
        {
            if (settingsPage.activeSelf) RefreshSettings();
            if (displayDeadline > 0)
            {
                float remaining = displayDeadline - Time.unscaledTime;
                if (remaining <= 0)
                {
                    CloseLocalDialog();
                    return;
                }
                dialogMessage.text = $"이 화면 모드를 유지할까요?\n{Mathf.CeilToInt(remaining)}초 후 이전 화면 모드로 돌아갑니다.";
            }

            if (Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            if (keypad.activeSelf) CloseKeypad();
            else if (localDialog.activeSelf) CloseLocalDialog();
            else if (gameplay.HasOpenModal) gameplay.CloseModal();
            else if (root.HasModal) root.CloseModal();
            else if (settingsPage.activeSelf) CloseSettings();
            else if (escapePage.activeSelf) ResumeMatch();
            else if (view == ScreenView.Match && !PresentationRoot.BlocksGameplay && game.state.AcceptsGameplay &&
                game.manager.LocalClient?.PlayerObject is { } local && !GameplayUsesEscape(local))
            {
                restoreCapture = Cursor.lockState == CursorLockMode.Locked;
                ShowEscapeMenu();
            }
        }

        static bool GameplayUsesEscape(NetworkObject local)
        {
            ulong owner = local.OwnerClientId;
            if (DoorPuzzleUI.IsPanelVisible ||
                CooperativePole.Current?.IsAttached(owner) == true || BridgeGameplay.Current?.Holds(owner) == true ||
                RiverGameplay.Current?.IsSeated(owner) == true || UndergroundGameplay.Current?.HasParticipant(owner) == true ||
                local.GetComponent<MountainReturnLaunch>()?.HasLaunchInput == true) return true;

            var interaction = local.GetComponent<PlayerInteraction>();
            foreach (var target in FindObjectsByType<NetworkInteractionTarget>(FindObjectsSortMode.None))
                if (target.IsInteracting(interaction)) return true;
            return false;
        }

        void ConfirmLeave()
        {
            if (!session.HasSession || session.IsBusy) return;
            ShowLocalDialog("방에서 나갈까요?", game.manager.IsHost
                ? "방을 종료하면 상대 플레이어의 연결도 종료됩니다."
                : "방을 나가면 진행 중인 경기에서 연결이 종료됩니다.", () => _ = Leave());
        }

        void ConfirmDefaults()
        {
            ShowLocalDialog("기본값을 복원할까요?", "현재 화면의 설정을 기본값으로 되돌립니다.", () =>
            {
                AudioService.SetMasterPercent(100);
                CourseMapPresentation.MinimapVisible = true;
                LocalPresentationSettings.MousePercent = LocalPresentationSettings.DefaultPercent;
                LocalPresentationSettings.EffectPercent = LocalPresentationSettings.DefaultPercent;
                if (!Application.isEditor)
                {
                    if (Screen.fullScreen != LocalPresentationSettings.DefaultFullscreen) ToggleDisplay();
                    else LocalPresentationSettings.ConfirmDisplay(LocalPresentationSettings.DefaultFullscreen);
                }
                RefreshSettings();
            });
        }

        void ToggleDisplay()
        {
            bool previous = Screen.fullScreen;
            FullScreenMode previousMode = Screen.fullScreenMode;
            var previousSize = new Vector2Int(Screen.width, Screen.height);
            if (!previous) LocalPresentationSettings.LastWindowSize = previousSize;
            Vector2Int windowSize = LocalPresentationSettings.LastWindowSize;
            var targetSize = previous && windowSize.x > 0 ? windowSize : previousSize;
            if (!ShowLocalDialog("화면 모드 확인", "이 화면 모드를 유지할까요?", () =>
                {
                    if (Screen.fullScreen == !previous) LocalPresentationSettings.ConfirmDisplay(!previous);
                    else LocalPresentationSettings.ApplyDisplay(previousMode, previousSize);
                },
                () => LocalPresentationSettings.ApplyDisplay(previousMode, previousSize))) return;

            displayDeadline = Time.unscaledTime + DisplayConfirmationSeconds;
            LocalPresentationSettings.ApplyDisplay(LocalPresentationSettings.DisplayMode(!previous), targetSize);
        }

        bool ShowLocalDialog(string title, string message, Action accept, Action cancel = null)
        {
            if (!root.ShowModal(localDialog, dialogCancel.gameObject)) return false;
            dialogTitle.text = title;
            dialogMessage.text = message;
            confirmDialog = accept;
            cancelDialog = cancel;
            return true;
        }

        void AcceptLocalDialog()
        {
            var accept = confirmDialog;
            cancelDialog = null;
            CloseLocalDialog();
            accept?.Invoke();
        }

        void CloseLocalDialog()
        {
            if (!localDialog || !localDialog.activeSelf) return;
            root.CloseModal();
            var cancel = cancelDialog;
            cancelDialog = confirmDialog = null;
            displayDeadline = 0;
            cancel?.Invoke();
        }
    }
}
