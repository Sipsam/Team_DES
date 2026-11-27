#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using System.Linq;
using TwinEdge.Audio;
using TwinEdge.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace TwinEdge.Gameplay
{
    internal sealed partial class PresentationRelaySmoke
    {
        bool settingsOnly, localSettingsProbe, restoreSettingsProbe;
        PresentationRoot Root => game.Presentation;
        Transform Landing => Root.Screens.Find("Main Menu");
        Transform Settings => Root.Screens.Find("Settings");
        Transform EscapeMenu => Root.Screens.Find("Escape Menu");
        Transform Dialog => Root.Modals.Cast<Transform>().Single(item => item.gameObject.activeSelf);
        Slider SettingSlider(string name) => Settings.GetComponentsInChildren<Slider>().Single(item => item.name == name);

        static void Click(Transform page, string name) =>
            page.GetComponentsInChildren<Button>().Single(item => item.name == name).onClick.Invoke();

        IEnumerator CheckLocalSettings()
        {
            yield return Wait(() => Landing.gameObject.activeSelf && !Root.IsTransitioning, "main menu interactive");
            var width = Screen.width;
            var height = Screen.height;
            if (!restoreSettingsProbe)
            {
                PlayerPrefs.SetInt("TwinEdge.MouseSensitivityPercent", -300);
                PlayerPrefs.SetInt("TwinEdge.EffectIntensityPercent", 600);
            }
            Click(Landing, "설정");
            var master = SettingSlider("사운드  /  전체 음량");
            var mouse = SettingSlider("조작  /  마우스 감도");
            var effect = SettingSlider("연출  /  효과 강도");
            const string display = "화면  /  화면 모드";
            const string minimap = "화면  /  미니맵 표시";
            const string nickname = "TwinEdge.Nickname";
            var savedName = PlayerPrefs.GetString(nickname);
            if (restoreSettingsProbe)
            {
                Require(Screen.fullScreen && master.value == 50 && mouse.value == 175 && effect.value == 25 &&
                    !CourseMapPresentation.MinimapVisible, "confirmed display and all values restored after Player restart");
                Click(Settings, "기본값 복원");
                Click(Dialog, "확인");
                yield return Wait(() => !Screen.fullScreen && Root.HasModal, "defaults preview windowed mode");
                Click(Dialog, "확인");
                Require(master.value == 100 && mouse.value == 100 && effect.value == 100 &&
                    CourseMapPresentation.MinimapVisible && PlayerPrefs.GetString(nickname) == savedName,
                    "defaults restore current settings and preserve nickname");
                yield break;
            }

            Require(!Screen.fullScreen, "Player starts windowed for display trial");
            Require(mouse.value == 10 && effect.value == 100, "bad saved percentages clamped in controls");
            foreach (int size in new[] { 1280, 1600, 1920, 2560 })
            {
                Screen.SetResolution(size, size * 9 / 16, FullScreenMode.Windowed);
                yield return Delay(.45f);
                Debug.Log($"SETTINGS_WINDOW requested={size}x{size * 9 / 16} actual={Screen.width}x{Screen.height}");
            }
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            yield return Wait(() => Screen.width == width && Screen.height == height, "actual initial window size restored");
            Click(Settings, display);
            yield return Wait(() => Screen.fullScreen, "actual fullscreen applied");
            yield return CaptureSettingsView("DisplayConfirmation", game.standbyCamera);
            Click(Dialog, "취소");
            yield return Wait(() => !Screen.fullScreen && Screen.width == width && Screen.height == height,
                "display cancel restores prior mode and size");
            Click(Settings, display);
            yield return Wait(() => Screen.fullScreen, "fullscreen preview for timeout");
            yield return Wait(() => !Root.HasModal && !Screen.fullScreen && Screen.width == width && Screen.height == height,
                "timeout restores prior mode and size");
            Click(Settings, display);
            yield return Wait(() => Screen.fullScreen, "fullscreen preview for confirmation");
            Click(Dialog, "확인");
            Require(PlayerPrefs.GetInt("TwinEdge.DisplayFullscreen") == 1, "fullscreen confirmation persisted");

            foreach (int percent in new[] { 0, 100, 50 })
            {
                master.value = percent;
                yield return null;
                Require(AudioService.MasterPercent == percent && AudioListener.volume == (percent == 0 ? 0 : 1),
                    $"Master {percent} reaches the audio service and listener");
                Debug.Log($"SETTINGS_AUDIO percent={percent} utc={System.DateTime.UtcNow:O}");
                yield return Delay(1.3f);
            }
            yield return CheckMouseSensitivity(mouse);
            mouse.value = 175;
            effect.value = 25;
            if (CourseMapPresentation.MinimapVisible) Click(Settings, minimap);
            Require(PlayerPrefs.GetString(nickname) == savedName, "settings preserve nickname");
            yield return CaptureSettingsView("MainSettings", game.standbyCamera);
        }

        IEnumerator CheckMouseSensitivity(Slider slider)
        {
            Root.ShowScreen(null, false);
            var target = new GameObject("Camera sensitivity check target");
            var rig = new GameObject("Camera sensitivity check rig", typeof(Camera), typeof(ThirdPersonCamera));
            rig.GetComponent<Camera>().enabled = false;
            rig.GetComponent<ThirdPersonCamera>().SetTarget(target.transform);
            var mouse = InputSystem.AddDevice<Mouse>();
            yield return null;
            try
            {
                Require(Cursor.lockState == CursorLockMode.Locked, "Windows camera fixture captures pointer");
                foreach (int percent in new[] { 10, 100, 200 })
                {
                    slider.value = percent;
                    float before = rig.transform.eulerAngles.y;
                    mouse.MakeCurrent();
                    InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(40, 0) });
                    yield return null;
                    yield return null;
                    float turn = Mathf.DeltaAngle(before, rig.transform.eulerAngles.y);
                    Require(Mathf.Abs(turn - 6 * percent / 100f) < .05f, $"mouse {percent}% produced {turn:F2} degrees");
                    Debug.Log($"SETTINGS_CAMERA percent={percent} yaw={turn:F2}");
                }
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                rig.SetActive(false);
                Destroy(rig);
                Destroy(target);
                Root.ShowScreen(Settings.gameObject);
            }
        }

        IEnumerator RunSettingsRelay()
        {
            const string minimap = "화면  /  미니맵 표시";
            yield return Wait(() => Landing.gameObject.activeSelf && !Root.IsTransitioning, "main menu interactive");
            if (host)
            {
                var menuCamera = GameObject.Find("Menu World Camera").GetComponent<Camera>();
                yield return CaptureSettingsView("Landing", menuCamera);
                Click(Landing, "설정");
                yield return CaptureSettingsView("MainSettings", menuCamera);
                Click(Settings, "뒤로 가기");
                yield return Wait(() => Landing.gameObject.activeSelf && !Root.IsTransitioning, "settings returns to main menu");
                var create = game.sessions.CreateRoomAsync("솔마루");
                yield return Wait(() => create.IsCompleted && game.sessions.HasSession, "settings host room");
                code = game.sessions.CurrentJoinCode;
                File.WriteAllText(Path.Combine(folder, "room.tmp"), code);
                File.Move(Path.Combine(folder, "room.tmp"), Path.Combine(folder, "room.txt"));
            }
            else
            {
                yield return Wait(() => Exists("room"), "settings room available");
                code = File.ReadAllText(Path.Combine(folder, "room.txt"));
                yield return Join();
            }

            yield return Wait(() => State.IsGameReady && LobbyAction().isActiveAndEnabled, "settings lobby ready");
            if (!host) LobbyAction().onClick.Invoke();
            yield return Wait(() => State.ClientReady, "client ready");
            if (host) LobbyAction().onClick.Invoke();
            var keys = InputSystem.AddDevice<Keyboard>();
            yield return Wait(() => State.IsSpawned && State.TransitionBlocksInput &&
                View("Match transition").activeInHierarchy, "settings countdown");
            var local = game.manager.LocalClient.PlayerObject;
            var input = local.GetComponent<GameplayInput>();
            input.SimulateCapturedCursor = true;
            yield return Press(keys, Key.Escape);
            Require(!EscapeMenu.gameObject.activeSelf && !input.AcceptsInput, "countdown Escape preserves action guard");
            yield return Wait(() => State.AcceptsGameplay && View("Gameplay HUD").activeInHierarchy &&
                !PresentationRoot.BlocksGameplay, "ordinary gameplay accepts Escape");

            int originalEffect = LocalPresentationSettings.EffectPercent;
            bool originalMap = CourseMapPresentation.MinimapVisible;
            var mini = View("Minimap");
            if (host)
            {
                yield return Press(keys, Key.M);
                Require(View("Detailed map").activeInHierarchy, "M opens full map");
                yield return Press(keys, Key.Escape);
                Require(!Root.HasModal && !EscapeMenu.gameObject.activeSelf, "map consumes Escape");
                yield return Press(keys, Key.N);
                Require(mini.activeInHierarchy != originalMap, "N updates the HUD");
                yield return Press(keys, Key.Escape);
                Require(EscapeMenu.gameObject.activeSelf && !input.AcceptsInput && Cursor.visible, "Escape owns local input and cursor");
                yield return CaptureSettingsView("EscapeMenu", game.standbyCamera);
                Click(EscapeMenu, "설정");
                Click(Settings, minimap);
                yield return null;
                Require(mini.activeInHierarchy == originalMap, "settings toggles the same value as N");
                SettingSlider("연출  /  효과 강도").value = originalEffect == 37 ? 41 : 37;
                yield return CaptureSettingsView("MatchSettings", game.standbyCamera);
            }
            yield return Barrier("host-menu-open");
            double before = State.ElapsedSeconds;
            if (!host)
            {
                Require(input.AcceptsInput, "host menu leaves client input available");
                Require(Mathf.Approximately(MatchTransitionPresentation.EffectIntensity, originalEffect / 100f),
                    $"host setting leaves client effects independent (initial {originalEffect}, current {LocalPresentationSettings.EffectPercent})");
                Vector3 oldPosition = local.transform.position;
                yield return Press(keys, Key.W);
                Require(Vector3.ProjectOnPlane(local.transform.position - oldPosition, Vector3.up).magnitude > .08f,
                    "client moves while host has settings open");
            }
            yield return Delay(.35f);
            Require(State.ElapsedSeconds > before && Time.timeScale == 1, "online timer advanced during local menu");
            yield return Barrier("host-menu-checked");
            if (host)
            {
                LocalPresentationSettings.EffectPercent = originalEffect;
                yield return Press(keys, Key.Escape);
                Click(EscapeMenu, "조작법 다시 보기");
                Require(Root.HasModal, "original controls help opens from menu");
                yield return CaptureSettingsView("Controls", game.standbyCamera);
                yield return Press(keys, Key.Escape);
                Require(EscapeMenu.gameObject.activeSelf && !Root.HasModal, "controls Escape returns to menu");
                Click(EscapeMenu, "방 나가기");
                yield return CaptureSettingsView("HostLeave", game.standbyCamera);
                Click(Dialog, "취소");
                Require(game.sessions.HasSession, "cancel leave preserves session");
                Click(EscapeMenu, "계속하기");
                Require(PresentationRoot.BlocksGameplay, "resume frame blocks click-through");
                yield return null;
                yield return null;
                Require(input.AcceptsInput, "resume restores local input");
            }
            yield return Barrier("before-client-leave");
            if (GameplayEffects.Trace) yield return CheckGameplayPool(local.transform.position, originalEffect);
            yield return CheckEffectIntensity(local.transform.position, originalEffect);
            if (!host)
            {
                yield return Press(keys, Key.Escape);
                Click(EscapeMenu, "방 나가기");
                yield return CaptureSettingsView("ClientLeave", game.standbyCamera);
                Click(Dialog, "확인");
                yield return Wait(() => !game.sessions.HasSession && !game.sessions.IsBusy &&
                    Landing.gameObject.activeSelf && !Root.IsTransitioning, "client leave and shutdown complete");
            }
            else
            {
                yield return Wait(() => State.ConnectedPlayerCount == 1, "host sees client leave");
                Signal("can-rejoin");
            }
            if (!host)
            {
                yield return Wait(() => Exists("can-rejoin"), "client can manually re-enter");
                yield return Join();
            }
            yield return Wait(() => State.CanPlay && State.ConnectedPlayerCount == 2 && !PresentationRoot.BlocksGameplay,
                "match ready after manual re-entry");
            Require(LocalPresentationSettings.EffectPercent == originalEffect && CourseMapPresentation.MinimapVisible == originalMap,
                "local settings survive re-entry");
            yield return Barrier("before-host-leave");
            if (host)
            {
                yield return Press(keys, Key.Escape);
                Click(EscapeMenu, "방 나가기");
                Click(Dialog, "확인");
            }
            yield return Wait(() => !game.sessions.HasSession && Landing.gameObject.activeSelf, "host leave returns both peers to landing");
            InputSystem.RemoveDevice(keys);
        }

        IEnumerator CheckGameplayPool(Vector3 feet, int original)
        {
            if (host)
            {
                Vector3 point = feet + Vector3.up * .12f;
                LocalPresentationSettings.EffectPercent = 0;
                yield return null;
                yield return null;
                var existing = GameplayEffects.Inspect(State);
                GameplayEffects.Emit(State, GameplayEffects.Kind.Land, point, Vector3.up);
                var disabled = GameplayEffects.Inspect(State);
                Require(disabled == existing && (!disabled || disabled.particleCount == 0), "0% skips optional emission and clears active particles");
                if (capture)
                {
                    using var target = new CaptureTarget(Root.Canvas, game.standbyCamera, 1600, 900);
                    yield return null;
                    target.Save(Path.Combine(folder, Role + "-gameplay-fx-0.png"));
                }

                LocalPresentationSettings.EffectPercent = 50;
                GameplayEffects.Emit(State, GameplayEffects.Kind.Land, point, Vector3.up);
                var pool = GameplayEffects.Inspect(State);
                Require(pool && pool.particleCount == 5, "50% emits five of nine test landing shards");
                LocalPresentationSettings.EffectPercent = 0;
                yield return null;
                yield return null;
                Require(pool.particleCount == 0, "0% clears particles already in flight");

                LocalPresentationSettings.EffectPercent = 100;
                GameplayEffects.Emit(State, GameplayEffects.Kind.Land, point, Vector3.up);
                Require(GameplayEffects.Inspect(State) == pool && pool.particleCount == 9, "100% emits nine shards in the same pool");
                if (capture)
                {
                    using var target = new CaptureTarget(Root.Canvas, game.standbyCamera, 1600, 900);
                    yield return null;
                    target.Save(Path.Combine(folder, Role + "-gameplay-fx-100.png"));
                }
                for (int i = 0; i < 25; i++)
                    GameplayEffects.Emit(State, GameplayEffects.Kind.Land, point, Vector3.up);
                Require(pool.particleCount == 160 && pool.main.maxParticles == 160, "concurrent effects stay within the particle budget");
                LocalPresentationSettings.EffectPercent = 0;
                yield return null;
                yield return null;
                Require(pool.particleCount == 0, "zero intensity clears the saturated pool");
                Signal("optional-fx-verified");
            }
            else
            {
                yield return Wait(() => Exists("optional-fx-verified"), "host optional VFX settings exercised");
                Require(LocalPresentationSettings.EffectPercent == original, "host effects leave client setting unchanged");
            }
            LocalPresentationSettings.EffectPercent = original;
            yield return Barrier("optional-fx-settings-restored");
        }

        IEnumerator CheckEffectIntensity(Vector3 point, int original)
        {
            LocalPresentationSettings.EffectPercent = 0;
            yield return Barrier("before-return-presentation");
            if (host)
            {
                Require(State.AdvanceFoundation(FoundationPhase.SealPending), "seal fixture phase accepted");
                Require(State.CommitFirstGemReturn(game.manager.LocalClientId, point, 1), "return fixture transition accepted");
            }
            var banner = View("Match transition");
            var wash = View("Return emphasis").GetComponent<Image>();
            yield return Wait(() => State.TransitionBlocksInput && banner.activeInHierarchy, "return instructions visible at 0%");
            Require(!GameObject.Find("Match transition world effects") && wash.color.a == 0,
                "0% hides world decoration and screen emphasis");
            float previousLight = 0, previousWash = 0;
            foreach (int percent in new[] { 50, 100 })
            {
                LocalPresentationSettings.EffectPercent = percent;
                yield return null;
                yield return null;
                var optional = GameObject.Find("Match transition world effects");
                Require(optional && banner.activeInHierarchy && State.TransitionBlocksInput,
                    $"return decoration and unchanged input guard at {percent}%");
                float light = optional.GetComponent<Light>().intensity;
                Require(light > previousLight && wash.color.a > previousWash, $"return light and screen emphasis at {percent}%");
                previousLight = light;
                previousWash = wash.color.a;
            }
            LocalPresentationSettings.EffectPercent = original;
            yield return Wait(() => !State.TransitionBlocksInput, "return presentation ends at server deadline");
        }

        IEnumerator CaptureSettingsView(string label, Camera camera)
        {
            if (!capture) yield break;
            foreach (int width in new[] { 1280, 1600, 1920, 2560 })
            {
                int height = width * 9 / 16;
                using var target = new CaptureTarget(Root.Canvas, camera, width, height);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (var text in Root.GetComponentsInChildren<Text>())
                    if (!string.IsNullOrWhiteSpace(text.text) && text.enabled && text.transform.IsChildOf(Root.Screens))
                        Require(text.preferredHeight <= text.rectTransform.rect.height + 1,
                            $"{label} {width} fits {text.text}");
                target.Save(Path.Combine(folder, $"{Role}-{label}-{width}x{height}.png"));
            }
        }
    }

}
#endif
