using System;
using System.Collections;
using System.IO;
using TwinEdge.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TwinEdge.Presentation.Editor
{
    [InitializeOnLoad]
    public static class PresentationCheck
    {
        const string RunKey = "TwinEdge.Presentation.Check";
        const string GameplayScene = "Assets/Game/Gameplay/Scenes/V3Gameplay.unity";

        static IEnumerator run;
        static double deadline;

        static PresentationCheck()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        // Run with -batchmode -force-d3d11 and without -quit.
        public static void Validate()
        {
            Require(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11, "D3D11 graphics device");
            EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
            SessionState.SetBool(RunKey, true);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunKey, false)) return;
            SessionState.EraseBool(RunKey);
            deadline = EditorApplication.timeSinceStartup + 90;
            run = Run();
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Presentation check timed out.");
                EditorApplication.QueuePlayerLoopUpdate();
                if (run.MoveNext()) return;
                Debug.Log("PRESENTATION_CHECK PASS");
                Finish(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        static void Finish(int exitCode)
        {
            EditorApplication.update -= Tick;
            (run as IDisposable)?.Dispose();
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            else EditorApplication.isPlaying = false;
        }

        static IEnumerator Run()
        {
            Application.runInBackground = true;
            var bootstrap = V3GameplayBootstrap.Current;
            Require(bootstrap && bootstrap.Presentation, "normal bootstrap installed the root");
            while (!SceneManager.GetSceneByName(bootstrap.levelScene).isLoaded || Mathf.Abs(bootstrap.standbyCamera.transform.position.y) < 1)
                yield return null;
            Canvas.ForceUpdateCanvases();
            var root = bootstrap.Presentation;
            Require(root.Canvas.renderMode == RenderMode.ScreenSpaceOverlay && !root.Canvas.enabled &&
                !root.GetComponent<GraphicRaycaster>().enabled, "idle overlay canvas and raycaster");
            var scaler = root.GetComponent<CanvasScaler>();
            Require(scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                scaler.referenceResolution == new Vector2(1920, 1080), "reference canvas scaler");
            Require(!PresentationRoot.BlocksGameplay && V3GameplayBootstrap.PointerOverMenu(new Vector2(24, Screen.height - 24)),
                "legacy room menu still visible and hit-tested");
            var phase = bootstrap.state.Phase;
            var status = bootstrap.sessions.CurrentStatus;
            int eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length;
            int listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;

            var fixture = new GameObject("Presentation input test").AddComponent<GameplayInput>();
            fixture.SimulateCapturedCursor = true;
            Require(fixture.AcceptsInput, "idle presentation leaves gameplay input enabled");

            var showcase = new PresentationShowcase(root);
            var font = AssetDatabase.LoadAssetAtPath<Font>(PresentationShowcase.FontPath);
            Require(font && font.dynamic, "Korean dynamic font");
            const string sample = "한글 TWIN EDGE 0123456789 가힣 ᄀᆨ ㄱㅎ !?·→★※";
            font.RequestCharactersInTexture(sample, 26);
            foreach (char character in sample)
                Require(font.GetCharacterInfo(character, out _, 26), $"atlas glyph U+{(int)character:X4}");
            for (int character = 0xAC00; character <= 0xD7A3; character++)
                Require(font.HasCharacter((char)character), $"Hangul font coverage U+{character:X4}");

            // An existing EventSystem must be reused. Its Input System module is added only once.
            EventSystem system = EventSystem.current;
            if (!system) system = new GameObject("Existing EventSystem fixture").AddComponent<EventSystem>();
            showcase.Open();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Require(EventSystem.current == system &&
                UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == Mathf.Max(1, eventSystems),
                "reused one EventSystem");
            var module = system.GetComponent<InputSystemUIInputModule>();
            Require(module && module.isActiveAndEnabled && module.point.action.enabled && module.leftClick.action.enabled,
                "enabled Input System point and click actions");
            Require(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == listeners,
                "audio listeners preserved");
            Require(!fixture.AcceptsInput && Cursor.lockState == CursorLockMode.None && Cursor.visible &&
                !V3GameplayBootstrap.PointerOverMenu(new Vector2(24, Screen.height - 24)), "interactive screen owns input");

            var settings = InputSystem.settings;
            var editorInput = settings.editorInputBehaviorInPlayMode;
            var backgroundInput = settings.backgroundBehavior;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var button = showcase.DialogButton;
                var marker = button.transform.Find("State marker").GetComponent<RectTransform>();
                Require(marker.sizeDelta.x == 2, "normal button mark");
                QueueMouse(mouse, button.transform, false);
                // Wait for a game frame, including EventSystem.Update.
                int frame = Time.frameCount;
                while (Time.frameCount <= frame + 1) yield return null;
                Require(marker.sizeDelta.x == 4,
                    $"Input System hover; mouse {mouse.position.ReadValue()}, target {button.transform.position}, " +
                    $"screen {Screen.width}x{Screen.height}, hit {module.GetLastRaycastResult(mouse.deviceId).gameObject}, " +
                    $"module {system.currentInputModule}, marker {marker.sizeDelta.x}");
                QueueMouse(mouse, button.transform, true);
                frame = Time.frameCount;
                while (Time.frameCount <= frame + 1) yield return null;
                Require(marker.sizeDelta.x == 8, "Input System pressed");
                QueueMouse(mouse, button.transform, false);
                frame = Time.frameCount;
                while (Time.frameCount <= frame + 1) yield return null;
                Require(showcase.Dialog.activeSelf && system.currentSelectedGameObject == showcase.CloseButton.gameObject &&
                    !button.IsInteractable() && !fixture.AcceptsInput, "click opens modal, traps focus and blocks gameplay");
                Require(showcase.CloseButton.transform.Find("State marker").GetComponent<RectTransform>().sizeDelta.x == 6,
                    "selected button mark");
                root.CloseModal();
                Require(system.currentSelectedGameObject == button.gameObject, "modal restores previous focus");

                var field = showcase.Nickname;
                QueueMouse(mouse, field.transform, true);
                frame = Time.frameCount;
                while (Time.frameCount <= frame + 1) yield return null;
                QueueMouse(mouse, field.transform, false);
                frame = Time.frameCount;
                while (Time.frameCount <= frame + 1) yield return null;
                Require(field.isFocused && !fixture.AcceptsInput, "focused nickname blocks gameplay");
                field.SetTextWithoutNotify("");
                field.caretPosition = 0;
                field.ProcessEvent(new Event { type = EventType.KeyDown, character = '한' });
                field.ProcessEvent(new Event { type = EventType.KeyDown, character = '글' });
                Require(field.text == "한글", "Korean character commit to the nickname");
                field.SetTextWithoutNotify("산길 탐험가");
                system.SetSelectedGameObject(null);
                showcase.RoomCode.SetTextWithoutNotify("AB12CD");
                Require(showcase.RoomCode.text == "AB12CD" && !showcase.DisabledButton.IsInteractable(),
                    "room code and disabled control");
                showcase.RoomCode.SetTextWithoutNotify("");
                var disabled = showcase.DisabledButton;
                var disabledLabel = disabled.GetComponentInChildren<Text>();
                Require(disabledLabel.color == PresentationTheme.SecondaryText, "disabled label tint");
                disabled.interactable = true;
                Require(disabledLabel.color == PresentationTheme.PrimaryText, "enabled label tint restored");
                disabled.interactable = false;

                root.ShowScreen(showcase.Sample, focus: showcase.DialogButton.gameObject);
                root.ShowScreen(showcase.Sample, takesInput: false);
                Require(!system.currentSelectedGameObject, "passive screen releases focus");
                showcase.Open();

                float start = Time.unscaledTime;
                Require(root.FadeTo(showcase.Alternate) && !root.FadeTo(showcase.Sample), "fade rejects overlap");
                while (!showcase.Alternate.activeSelf || root.transform.Find("Fade").gameObject.activeSelf)
                {
                    Require(!fixture.AcceptsInput, "fade blocks gameplay");
                    yield return null;
                }
                Require(Time.unscaledTime - start >= 2 * PresentationRoot.FadeSeconds &&
                    !showcase.Sample.activeSelf, "unscaled fade swaps one screen");
                showcase.Open();

                root.ShowScreen(null);
                Require(!fixture.AcceptsInput, "closing frame blocks click-through");
                frame = Time.frameCount;
                while (Time.frameCount == frame) yield return null;
                Require(fixture.AcceptsInput && Cursor.lockState == CursorLockMode.None &&
                    V3GameplayBootstrap.PointerOverMenu(new Vector2(24, Screen.height - 24)), "legacy room menu and input restored");
                Require(!bootstrap.manager.IsListening && !bootstrap.sessions.HasSession &&
                    bootstrap.state.Phase == phase && bootstrap.sessions.CurrentStatus == status,
                    "showcase left multiplayer and game phase unchanged");
                showcase.Open();
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                settings.editorInputBehaviorInPlayMode = editorInput;
                settings.backgroundBehavior = backgroundInput;
            }

            var camera = bootstrap.standbyCamera;
            // Frame the same live landscape from above the entry slope for material and contrast review.
            float yaw = camera.transform.eulerAngles.y;
            Vector3 courseDirection = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            camera.transform.SetPositionAndRotation(camera.transform.position + Vector3.up * 36 - courseDirection * 14,
                Quaternion.Euler(9, yaw, 0));
            root.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            root.Canvas.worldCamera = camera;
            root.Canvas.planeDistance = camera.nearClipPlane + 1;
            string directory = Argument("-twinPresentationOutput", "Docs/Development/Presentation/V1/Review");
            string suffix = Argument("-twinPresentationSuffix", "working");
            Directory.CreateDirectory(directory);
            foreach (int width in new[] { 1280, 1600, 1920, 2560 })
            {
                int height = width * 9 / 16;
                using var target = new RenderTarget(camera, width, height);
                int frame = Time.frameCount;
                while (Time.frameCount <= frame + 1) yield return null;
                Canvas.ForceUpdateCanvases();
                Require(Mathf.Abs(root.Canvas.scaleFactor - width / 1920f) < .005f,
                    $"canvas scale for {width}x{height}, got {root.Canvas.scaleFactor:F3}");
                CheckLayout(showcase);
                target.Capture(Path.Combine(directory, $"UI_Showcase_{width}x{height}_v3_{suffix}.png"));
                if (width == 1920)
                {
                    showcase.OpenDialog();
                    Canvas.ForceUpdateCanvases();
                    target.Capture(Path.Combine(directory, $"UI_Modal_{width}x{height}_v3_{suffix}.png"));
                    root.CloseModal();
                }
                Debug.Log($"PRESENTATION_LAYOUT PASS {width}x{height}");
            }

            int dirty = 0;
            showcase.Stamina.RegisterDirtyVerticesCallback(() => dirty++);
            Canvas.ForceUpdateCanvases();
            int initialDirty = dirty;
            for (int i = 0; i < 120; i++) UiKit.SetProgress(showcase.Stamina, .68f);
            Canvas.ForceUpdateCanvases();
            Require(dirty == initialDirty, "steady progress does not dirty its graphic");
        }

        static void QueueMouse(Mouse mouse, Transform target, bool down)
        {
            var state = new MouseState { position = RectTransformUtility.WorldToScreenPoint(null, target.position) };
            InputSystem.QueueStateEvent(mouse, state.WithButton(MouseButton.Left, down));
        }

        static void CheckLayout(PresentationShowcase showcase)
        {
            var error = showcase.LongError;
            Require(error.preferredHeight <= error.rectTransform.rect.height + 1,
                $"long Korean error unclipped, text height {error.preferredHeight:F1}, rect {error.rectTransform.rect.height:F1}");
            foreach (Transform panel in showcase.Sample.transform)
            {
                var content = (RectTransform)panel.Find("Content");
                float preferredHeight = LayoutUtility.GetPreferredHeight(content);
                Require(preferredHeight <= content.rect.height + 1,
                    $"content fits its panel, preferred {preferredHeight:F1}, actual {content.rect.height:F1}");
            }
        }

        static void Require(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException("Presentation check failed: " + reason);
        }

        static string Argument(string flag, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        sealed class RenderTarget : IDisposable
        {
            readonly Camera camera;
            readonly RenderTexture previous, target;
            readonly Texture2D pixels;

            internal RenderTarget(Camera camera, int width, int height)
            {
                this.camera = camera;
                previous = camera.targetTexture;
                target = new RenderTexture(width, height, 24);
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                camera.targetTexture = target;
            }

            internal void Capture(string path)
            {
                var active = RenderTexture.active;
                try
                {
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                }
                finally { RenderTexture.active = active; }
            }

            public void Dispose()
            {
                camera.targetTexture = previous;
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(pixels);
            }
        }
    }
}
