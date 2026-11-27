using TwinEdge.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TwinEdge.Presentation
{
    public sealed partial class SessionPresentation
    {
        static readonly Key[] digitKeys =
        {
            Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
        };
        static readonly Key[] numpadKeys =
        {
            Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4,
            Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9
        };

        GameObject keypad;
        Text keypadEntry, keypadStatus;
        UiButton keypadSubmit, keypadDelete;
        GameplayCharacter keypadPlayer;
        string keypadDigits = "";
        int keypadLife, submittedRevision;
        float keypadDeadline;

        void BuildKeypad()
        {
            var content = ui.Modal(root.Modals);
            var frame = (RectTransform)content.parent;
            frame.anchorMin = frame.anchorMax = new Vector2(.5f, .5f);
            frame.sizeDelta = new Vector2(500, 584);
            keypad = frame.parent.gameObject;
            keypad.name = "Castle Keypad";
            UiKit.Stack(content, 10);
            Label(content, "성채 키패드", 34, 46, PresentationTheme.PrimaryText, TextAnchor.MiddleCenter);
            Label(content, "내 방의 네 자리 숫자를 입력하세요.", 23, 34, PresentationTheme.SecondaryText, TextAnchor.MiddleCenter);
            var display = UiKit.Surface(content, "Keypad display", PresentationTheme.Stone);
            UiKit.Measure(display.rectTransform, 60);
            keypadEntry = ui.Text(display.transform, "", 36, PresentationTheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(keypadEntry.rectTransform);
            keypadStatus = Label(content, "", 21, 32, PresentationTheme.SecondaryText, TextAnchor.MiddleCenter);

            for (int first = 1; first < 10; first += 3)
            {
                var row = Row(content, 56);
                for (int digit = first; digit < first + 3; digit++) AddDigitButton(row, digit);
            }
            var actions = Row(content, 56);
            keypadDelete = ui.Button(actions, "삭제", DeleteKeypadDigit);
            AddDigitButton(actions, 0);
            keypadSubmit = ui.Button(actions, "확인", SubmitKeypad, clickSound: false);
            ui.Button(content, "닫기", CloseKeypad);
            keypad.SetActive(false);
        }

        void AddDigitButton(Transform row, int digit) => ui.Button(row, digit.ToString(), () => AddKeypadDigit(digit));

        void UpdateKeypad(bool matchVisible)
        {
            var castle = CastleGameplay.Current;
            bool showing = keypad.activeSelf;
            if (!showing && castle?.IsKeypadOpen != true) return;
            var local = game.manager.LocalClient?.PlayerObject;
            if (!matchVisible || !castle || !castle.TryReadLocalKeypad(local, out var state) ||
                showing && (!keypadPlayer || keypadPlayer.NetworkObject != local || keypadPlayer.RespawnCount.Value != keypadLife))
            {
                CloseKeypad();
                return;
            }
            if (!showing)
            {
                if (PresentationRoot.BlocksGameplay || !root.ShowModal(keypad, null))
                {
                    castle.CloseLocalKeypad();
                    return;
                }
                keypadPlayer = local.GetComponent<GameplayCharacter>();
                keypadLife = keypadPlayer.RespawnCount.Value;
                keypadDigits = "";
                keypadDeadline = 0;
                keypadStatus.text = state.Feedback < 0 ? "번호가 일치하지 않습니다." : "";
                RefreshKeypadEntry();
            }

            if (keypadDeadline > 0 && (state.Revision != submittedRevision || Time.unscaledTime >= keypadDeadline))
            {
                keypadDeadline = 0;
                keypadStatus.text = state.Revision == submittedRevision ? "응답이 지연되고 있습니다. 다시 확인하세요." :
                    state.Feedback < 0 ? "번호가 일치하지 않습니다." : "";
                RefreshKeypadEntry();
            }

            if (Keyboard.current is not { } keys || keys.escapeKey.wasPressedThisFrame) return;
            if (keys.backspaceKey.wasPressedThisFrame || keys.deleteKey.wasPressedThisFrame) DeleteKeypadDigit();
            for (int digit = 0; digit < digitKeys.Length; digit++)
                if (keys[digitKeys[digit]].wasPressedThisFrame || keys[numpadKeys[digit]].wasPressedThisFrame)
                    AddKeypadDigit(digit);
        }

        void AddKeypadDigit(int digit)
        {
            if (keypadDeadline > 0 || keypadDigits.Length >= 4) return;
            keypadDigits += (char)('0' + digit);
            keypadStatus.text = "";
            RefreshKeypadEntry();
        }

        void DeleteKeypadDigit()
        {
            if (keypadDeadline > 0 || keypadDigits.Length == 0) return;
            keypadDigits = keypadDigits.Substring(0, keypadDigits.Length - 1);
            keypadStatus.text = "";
            RefreshKeypadEntry();
        }

        void SubmitKeypad()
        {
            var castle = CastleGameplay.Current;
            if (keypadDeadline > 0 || keypadDigits.Length != 4 || !castle || !keypadPlayer ||
                !castle.TryReadLocalKeypad(keypadPlayer.NetworkObject, out var state)) return;
            submittedRevision = state.Revision;
            keypadDeadline = Time.unscaledTime + 3;
            keypadStatus.text = "서버 확인 중";
            RefreshKeypadEntry();
            castle.SubmitNumberRpc(int.Parse(keypadDigits), submittedRevision);
        }

        void RefreshKeypadEntry()
        {
            keypadEntry.text = string.Join("   ", keypadDigits.PadRight(4, '_').ToCharArray());
            keypadDelete.interactable = keypadDeadline == 0 && keypadDigits.Length > 0;
            keypadSubmit.interactable = keypadDeadline == 0 && keypadDigits.Length == 4;
        }

        void CloseKeypad()
        {
            CastleGameplay.Current?.CloseLocalKeypad();
            if (keypad.activeSelf) root.CloseModal();
            keypadPlayer = null;
            keypadDigits = "";
            keypadDeadline = 0;
        }
    }
}
