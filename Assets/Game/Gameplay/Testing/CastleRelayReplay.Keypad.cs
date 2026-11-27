#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using TwinEdge.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace TwinEdge.Gameplay
{
    public sealed partial class CastleRelayReplay
    {
        Mouse keypadMouse;
        int keypadErrors, keypadAccepts;
        static bool TraceKeypadAudio => Environment.GetCommandLineArgs().Contains("-twinAudioTrace");

        IEnumerator CheckKeypad()
        {
            var root = V3GameplayBootstrap.Current.Presentation;
            var modal = root.Modals.Find("Castle Keypad").gameObject;
            var input = player.GetComponent<GameplayInput>();
            var station = Station(CastleGameplay.Action.Keypad, slot, 4);
            var display = modal.transform.Find("Panel/Content/Keypad display").GetComponentInChildren<Text>(true);
            keypadMouse = InputSystem.AddDevice<Mouse>();
            yield return Wait(() => modal.activeInHierarchy, "server-approved E opens the uGUI keypad");
            Require(PresentationRoot.BlocksGameplay && !input.AcceptsInput && Cursor.visible &&
                Cursor.lockState == CursorLockMode.None, "keypad owns gameplay input and cursor");
            yield return PressKeypadKey(Key.M);
            Require(modal.activeInHierarchy && !root.Modals.Cast<Transform>().Any(t => t.name == "Detailed map" && t.gameObject.activeSelf),
                "map hotkey leaves the keypad modal in control");
            yield return PressKeypadKey(Key.Escape);
            Require(!modal.activeSelf && !root.Screens.Find("Escape Menu").gameObject.activeSelf &&
                !EventSystem.current.currentSelectedGameObject, "one Escape closes only the keypad and clears focus");
            yield return PressKeypadKey(Key.M);
            Require(root.HasModal, "full map opens after keypad cancellation");
            yield return PressKeypadKey(Key.Escape);
            Require(!root.HasModal, "map releases modal ownership");
            yield return UseAndConfirm(station, () => modal.activeSelf, "keypad reopened through E");
            double attackReady = character.AttackReadyAtForTest;
            yield return ClickKeypad(modal, "닫기");
            Require(!modal.activeSelf && Cursor.lockState == CursorLockMode.None &&
                !EventSystem.current.currentSelectedGameObject && character.AttackReadyAtForTest == attackReady,
                "close click releases focus without recapturing the world or attacking");
            yield return UseAndConfirm(station, () => modal.activeSelf, "keypad reopened after close button");
            Require(KeypadDigits(display) == "____", "reopening starts a fresh local entry");

            var baseline = (keypadErrors, keypadAccepts);
            var remoteStart = castle.Progress(1 - slot).Revision;
            yield return PresentationMetrics.Sample(output, role, "keypad");
            yield return Rendezvous("keypad-private-baseline");
            if (host)
            {
                foreach (int width in new[] { 1280, 1600, 1920, 2560 })
                    yield return CaptureKeypad("keypad-open", modal, width);
                yield return ClickKeypad(modal, "0");
                Require(KeypadDigits(display) == "0___", "onscreen zero writes the first digit");
                yield return PressKeypadKey(Key.Numpad1);
                Require(KeypadDigits(display) == "01__", "mouse and numpad share one entry including leading zero");
                yield return PressKeypadKey(Key.Backspace);
                yield return ClickKeypad(modal, "삭제");
                Require(KeypadDigits(display) == "____" && !KeypadButton(modal, "확인").IsInteractable(),
                    "keyboard and click delete share the same entry and partial submit is disabled");
                string wrong = castle.LocalNumber == 123 ? "0124" : "0123";
                foreach (char digit in wrong) yield return ClickKeypad(modal, digit.ToString());
                yield return PressKeypadKey(Key.Digit2);
                Require(KeypadDigits(display) == wrong, "entry is capped at four digits");
                yield return CaptureKeypad("keypad-entry", modal, 1920);
                yield return ClickKeypad(modal, "확인");
                yield return Wait(() => modal.GetComponentsInChildren<Text>().Any(t => t.text == "번호가 일치하지 않습니다."),
                    "server rejection appears for the input owner");
                Require(!castle.DoorOpen && castle.Progress(slot).Feedback == -1 &&
                    castle.Progress(1 - slot).Revision == remoteStart, "wrong code preserves the door and the other player's progress");
                yield return CaptureKeypad("keypad-rejected", modal, 1920);
                for (int i = 0; i < 4; i++) yield return PressKeypadKey(Key.Backspace);
            }
            yield return Rendezvous("keypad-wrong-received");
            if (TraceKeypadAudio)
                Require(keypadErrors == baseline.keypadErrors + (host ? 1 : 0), "only the input owner hears the wrong-code cue");
            Require(host || KeypadDigits(display) == "____", "other player keeps their own entry");
            foreach (char digit in castle.LocalNumber.ToString("D4"))
                yield return PressKeypadKey(KeypadKey(digit));
            yield return ClickKeypad(modal, "확인");
            yield return Wait(() => !modal.activeSelf && !root.HasModal && !PresentationRoot.BlocksGameplay,
                "server acceptance dismisses the keypad and releases gameplay");
            if (TraceKeypadAudio)
                yield return Wait(() => keypadAccepts == baseline.keypadAccepts + 1, "each peer hears only their own accepted code");
            Require(!EventSystem.current.currentSelectedGameObject && root.Overlays.Find("Gameplay HUD").gameObject.activeInHierarchy,
                "HUD remains active with keypad focus released");
            PresentationMetrics.Snapshot(output, role, "keypad-accepted");
            InputSystem.RemoveDevice(keypadMouse);
        }

        IEnumerator CheckKeypadReconnect()
        {
            yield return Rendezvous("keypad-before-reconnect");
            if (!host)
            {
                yield return LeaveAndWaitForHost("after the accepted keypad codes");
                yield return Join();
                yield return BindPlayer();
            }
            yield return Rendezvous("keypad-after-reconnect");
            yield return Wait(() => game.AcceptsGameplay && castle.Stage == 4 && castle.DoorOpen && castle.PartitionsOpen &&
                V3GameplayBootstrap.Current.Presentation.Overlays.Find("Gameplay HUD").gameObject.activeInHierarchy,
                "reconnect restores solved castle and gameplay HUD");
            Require(!castle.IsKeypadOpen && !V3GameplayBootstrap.Current.Presentation.HasModal &&
                FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1,
                "reconnect keeps a single EventSystem and clears keypad ownership");
            PresentationMetrics.Snapshot(output, role, "keypad-reconnected");
        }

        IEnumerator CaptureKeypad(string label, GameObject modal, int width)
        {
            if (!screenshots || !host) yield break;
            int height = width * 9 / 16;
            var root = V3GameplayBootstrap.Current.Presentation;
            var privateHint = root.Overlays.GetComponentsInChildren<Text>()
                .FirstOrDefault(text => text.enabled && text.text.StartsWith("내 번호 ", StringComparison.Ordinal));
            if (privateHint) privateHint.enabled = false;
            try
            {
                using var target = new PresentationRelaySmoke.CaptureTarget(root.Canvas, Camera.main, width, height);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                if (modal)
                {
                    foreach (var text in modal.GetComponentsInChildren<Text>())
                        Require(text.preferredHeight <= text.rectTransform.rect.height + 1 &&
                            text.preferredWidth <= text.rectTransform.rect.width + 1,
                            $"keypad text fits at {width}x{height}", log: false);
                }
                target.Save(Path.Combine(output, $"{role}-{label}-{width}x{height}.png"));
            }
            finally
            {
                if (privateHint) privateHint.enabled = true;
            }
            // Let the CanvasScaler return to window coordinates before the next pointer input.
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        IEnumerator ClickKeypad(GameObject modal, string caption)
        {
            var button = KeypadButton(modal, caption);
            Require(button.IsInteractable(), "keypad button enabled", log: false);
            var rect = (RectTransform)button.transform;
            var position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(keypadMouse, new MouseState { position = position });
            yield return null;
            InputSystem.QueueStateEvent(keypadMouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(keypadMouse, new MouseState { position = position });
            yield return null;
        }

        IEnumerator PressKeypadKey(Key key)
        {
            SetKeys(key);
            yield return null;
            SetKeys();
            yield return null;
        }

        static Key KeypadKey(char digit) => digit == '0' ? Key.Digit0 : Key.Digit1 + (digit - '1');
        static string KeypadDigits(Text text) => text.text.Replace(" ", "");
        static Button KeypadButton(GameObject modal, string caption) =>
            modal.GetComponentsInChildren<Button>().Single(button => button.name == caption);
    }
}
#endif
