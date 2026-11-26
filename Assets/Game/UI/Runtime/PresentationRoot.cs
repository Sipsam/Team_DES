using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TwinEdge.Presentation
{
    public sealed class PresentationRoot : MonoBehaviour
    {
        public const float FadeSeconds = .14f;
        public Canvas Canvas { get; private set; }
        public RectTransform Screens => (RectTransform)screens.transform;
        public RectTransform Overlays => (RectTransform)overlays.transform;
        public RectTransform Modals => (RectTransform)modals.transform;
        public bool IsTransitioning => transition != null;

        static PresentationRoot inputOwner;
        static int releasedFrame = -1;
        public static bool BlocksGameplay => inputOwner != null || releasedFrame == Time.frameCount;

        CanvasGroup screens, overlays, modals, curtain;
        GameObject screen, overlay, modal, previousFocus;
        GameObject ownedEventSystem;
        EventSystem events;
        bool screenTakesInput, overlayTakesInput;
        Coroutine transition;
        GraphicRaycaster raycaster;

        public static PresentationRoot Create(Scene scene)
        {
            var host = new GameObject("Twin Edge UI", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(host, scene);
            return host.AddComponent<PresentationRoot>();
        }

        void Awake()
        {
            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 20;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            raycaster = gameObject.AddComponent<GraphicRaycaster>();

            screens = Layer("Screen");
            overlays = Layer("Overlay");
            modals = Layer("Modal");
            var veil = UiKit.Surface(transform, "Fade", PresentationTheme.Background, true);
            UiKit.Stretch(veil.rectTransform);
            curtain = veil.gameObject.AddComponent<CanvasGroup>();
            curtain.gameObject.SetActive(false);
            Refresh();
        }

        CanvasGroup Layer(string title)
        {
            var layer = UiKit.Rect(transform, title);
            UiKit.Stretch(layer);
            return layer.gameObject.AddComponent<CanvasGroup>();
        }

        // Supply an inactive or active direct child of the corresponding layer.
        public void ShowScreen(GameObject next, bool takesInput = true, GameObject focus = null)
        {
            CheckLayer(next, Screens);
            if (transition != null) throw new System.InvalidOperationException("Finish the current screen fade first.");
            ApplyScreen(next, takesInput);
            if (!modal && screenTakesInput) Select(focus);
        }

        public void ShowOverlay(GameObject next, bool takesInput = false, GameObject focus = null)
        {
            CheckLayer(next, Overlays);
            if ((overlay != next || !takesInput) && HasFocus(overlay)) Select(null);
            Replace(ref overlay, next);
            overlayTakesInput = next && takesInput;
            Refresh();
            if (!modal && overlayTakesInput && focus) Select(focus);
        }

        public bool FadeTo(GameObject next, bool takesInput = true, GameObject focus = null)
        {
            CheckLayer(next, Screens);
            if (transition != null || modal || !isActiveAndEnabled) return false;
            transition = StartCoroutine(Fade(next, takesInput, focus));
            return true;
        }

        public bool ShowModal(GameObject next, GameObject focus)
        {
            CheckLayer(next, Modals);
            if (!next || modal || transition != null || !isActiveAndEnabled) return false;
            EnsureEventSystem();
            previousFocus = events.currentSelectedGameObject;
            Select(null);
            Replace(ref modal, next);
            Refresh();
            Select(focus);
            return true;
        }

        public void CloseModal()
        {
            if (!modal) return;
            Select(null);
            Replace(ref modal, null);
            Refresh();
            if (previousFocus && previousFocus.activeInHierarchy &&
                (!previousFocus.TryGetComponent<Selectable>(out var control) || control.IsInteractable()))
                Select(previousFocus);
            previousFocus = null;
        }

        IEnumerator Fade(GameObject next, bool takesInput, GameObject focus)
        {
            Select(null);
            curtain.alpha = 0;
            curtain.gameObject.SetActive(true);
            Refresh();
            yield return FadeAlpha(0, 1);
            ApplyScreen(next, takesInput);
            yield return FadeAlpha(1, 0);
            curtain.gameObject.SetActive(false);
            transition = null;
            Refresh();
            if (screenTakesInput) Select(focus);
        }

        IEnumerator FadeAlpha(float from, float to)
        {
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < FadeSeconds)
            {
                curtain.alpha = Mathf.Lerp(from, to, (Time.unscaledTime - start) / FadeSeconds);
                yield return null;
            }
            curtain.alpha = to;
        }

        void ApplyScreen(GameObject next, bool takesInput)
        {
            if ((screen != next || !takesInput) && HasFocus(screen)) Select(null);
            Replace(ref screen, next);
            screenTakesInput = next && takesInput;
            Refresh();
        }

        void Refresh()
        {
            bool fading = curtain.gameObject.activeSelf;
            bool interactive = screenTakesInput || overlayTakesInput || modal || fading;
            Canvas.enabled = screen || overlay || modal || fading;
            raycaster.enabled = interactive;
            SetInteractive(screens, screenTakesInput && !modal && !fading);
            SetInteractive(overlays, overlayTakesInput && !modal && !fading);
            SetInteractive(modals, modal && !fading);

            if (interactive)
            {
                EnsureEventSystem();
                inputOwner = this;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else ReleaseInput();
        }

        static void SetInteractive(CanvasGroup layer, bool enabled)
        {
            layer.interactable = enabled;
            layer.blocksRaycasts = enabled;
        }

        void EnsureEventSystem()
        {
            if (!events || !events.isActiveAndEnabled)
                events = EventSystem.current ? EventSystem.current : FindFirstObjectByType<EventSystem>();
            if (!events)
            {
                ownedEventSystem = new GameObject("Twin Edge EventSystem", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(ownedEventSystem, gameObject.scene);
                events = ownedEventSystem.GetComponent<EventSystem>();
            }

            if (!events.TryGetComponent<InputSystemUIInputModule>(out var input))
                input = events.gameObject.AddComponent<InputSystemUIInputModule>();
            if (events.TryGetComponent<StandaloneInputModule>(out var legacy))
                legacy.enabled = false;
            input.enabled = true;
        }

        bool HasFocus(GameObject layer) => events && layer && events.currentSelectedGameObject &&
            events.currentSelectedGameObject.transform.IsChildOf(layer.transform);

        void Select(GameObject focus)
        {
            if (events) events.SetSelectedGameObject(focus);
        }

        static void CheckLayer(GameObject next, Transform layer)
        {
            if (next && next.transform.parent != layer)
                throw new System.ArgumentException("Presentation content must be a direct child of its layer.", nameof(next));
        }

        static void Replace(ref GameObject current, GameObject next)
        {
            if (current == next) return;
            if (current) current.SetActive(false);
            current = next;
            if (current) current.SetActive(true);
        }

        void ReleaseInput()
        {
            if (inputOwner != this) return;
            inputOwner = null;
            releasedFrame = Time.frameCount;
            // The next world click follows the existing capture flow.
        }

        void OnDisable()
        {
            if (transition != null) StopCoroutine(transition);
            transition = null;
            curtain.gameObject.SetActive(false);
            if (HasFocus(screen) || HasFocus(overlay) || HasFocus(modal)) Select(null);
            ReleaseInput();
        }

        void OnEnable() => Refresh();

        void OnDestroy()
        {
            if (ownedEventSystem) Destroy(ownedEventSystem);
        }
    }
}
