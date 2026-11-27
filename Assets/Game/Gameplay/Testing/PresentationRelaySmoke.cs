#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TwinEdge.Audio;
using TwinEdge.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TwinEdge.Gameplay
{
    internal sealed class PresentationRelaySmoke : MonoBehaviour
    {
        bool host, capture, audioTrace;
        readonly Dictionary<string, int> audioCounts = new();
        string folder, code;
        V3GameplayBootstrap game;
        GameStateManager State => game.state;
        string Role => host ? "host" : "client";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int role = Array.IndexOf(args, "-presentationSmoke");
            if (role < 0) return;
            var smoke = new GameObject("V3 Presentation Relay Check").AddComponent<PresentationRelaySmoke>();
            smoke.host = args[role + 1] == "host";
            smoke.capture = args.Contains("-presentationScreenshots");
            smoke.audioTrace = args.Contains("-twinAudioTrace");
            smoke.folder = args[Array.IndexOf(args, "-presentationEvidence") + 1];
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 45;
            InputSystem.settings = Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.logMessageReceived += OnLog;
            game = V3GameplayBootstrap.Current;
            if (!game) game = FindFirstObjectByType<V3GameplayBootstrap>();
            yield return Wait(() => game.WorldReady, "saved world loaded");
            CheckListener("Menu World Camera");
            if (host)
            {
                var create = game.sessions.CreateRoomAsync("산마루");
                yield return Wait(() => create.IsCompleted && State.IsSpawned && State.ConnectedPlayerCount == 1, "host-only lobby");
                yield return Delay();
                Require(!State.HostCanStart && !LobbyAction().interactable, "host-only button disabled");
                yield return Screenshot("lobby-host-only", true);
                code = game.sessions.CurrentJoinCode;
                File.WriteAllText(Path.Combine(folder, "room.tmp"), code);
                File.Move(Path.Combine(folder, "room.tmp"), Path.Combine(folder, "room.txt"));
            }
            else
            {
                yield return Wait(() => Exists("room"), "host room available");
                code = File.ReadAllText(Path.Combine(folder, "room.txt"));
                yield return Join();
            }

            yield return Wait(() => State.IsGameReady, "two Runtime Ready reports");
            yield return Delay();
            Require(State.Phase == FoundationPhase.WaitingForPlayers && !State.ClientReady, "runtime ready leaves phase in lobby");
            Require(LobbyAction().interactable == !host, "initial action availability");
            State.RequestMatchStartRpc();
            yield return Screenshot("lobby-unready", true);
            yield return Barrier("unready");
            if (host) File.Delete(Path.Combine(folder, "room.txt"));
            Require(State.Phase == FoundationPhase.WaitingForPlayers, "unready and unauthorized start rejected");

            if (!host) LobbyAction().onClick.Invoke();
            yield return Wait(() => State.ClientReady, "client Ready replicated");
            yield return Delay();
            Require(LobbyAction().interactable, "ready action available");
            yield return Screenshot("lobby-ready", true);
            yield return Barrier("ready");
            if (!host) State.RequestMatchStartRpc();
            yield return Barrier("denied");
            Require(State.Phase == FoundationPhase.WaitingForPlayers, "ready Client cannot start");
            if (!host) LobbyAction().onClick.Invoke();
            yield return Wait(() => !State.ClientReady, "ready cancel replicated");
            if (host) State.RequestMatchStartRpc();
            yield return Delay();
            Require(State.Phase == FoundationPhase.WaitingForPlayers && LobbyAction().interactable == !host,
                "cancel wins before start and disables Host button");
            yield return Barrier("cancelled");

            if (!host) LobbyAction().onClick.Invoke();
            yield return Wait(() => State.ClientReady, "ready before disconnect");
            yield return Barrier("disconnect");
            if (!host)
            {
                var leave = game.sessions.LeaveRoomAsync();
                yield return Wait(() => leave.IsCompleted, "client left before start");
            }
            else
            {
                yield return Wait(() => State.ConnectedPlayerCount == 1 && !State.ClientReady, "pre-start disconnect cleared ready");
                Require(State.Phase == FoundationPhase.WaitingForPlayers, "host stayed in lobby");
                Signal("host-can-rejoin");
            }
            if (!host)
            {
                yield return Wait(() => Exists("host-can-rejoin"), "host saw disconnect");
                yield return Join();
            }
            yield return Wait(() => State.IsGameReady, "new client Runtime Ready");
            Require(!State.ClientReady && State.Phase == FoundationPhase.WaitingForPlayers, "new Client must opt in");
            yield return Delay();
            if (!host) LobbyAction().onClick.Invoke();
            yield return Wait(() => State.ClientReady, "second ready approved");
            yield return Barrier("start");
            if (host)
            {
                LobbyAction().onClick.Invoke();
                Require(State.Phase == FoundationPhase.Preparing, "host request enters Preparing synchronously");
                State.RequestMatchStartRpc();
                Signal("host-started");
            }
            else
            {
                yield return Wait(() => Exists("host-started"), "host started first");
                State.SetLobbyReadyRpc(false);
            }
            yield return Wait(() => State.CanPlay, "manual start reaches Outbound");
            yield return Wait(() => View("Gameplay HUD").activeInHierarchy && !PresentationRoot.BlocksGameplay,
                "start loading hands input back to gameplay");
            Require(State.Phase == FoundationPhase.Outbound && !State.ClientReady, "start consumed readiness");
            CheckListener(game.standbyCamera.name);
            if (audioTrace)
            {
                Require(AudioCount("Ready") == (host ? 0 : 3) && AudioCount("Unready") == (host ? 0 : 1) &&
                    AudioCount("Start") == (host ? 1 : 0), "Ready, cancel and Start each sound once for the approving player");
            }
            Require(SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11, "D3D11 Player");
            if (host)
            {
                Require(Camera.main && Camera.main.isActiveAndEnabled, "game camera active");
                yield return Screenshot("minimum-hud");
            }

            var input = game.manager.LocalClient.PlayerObject.GetComponent<GameplayInput>();
            input.SimulateCapturedCursor = true;
            Cursor.lockState = CursorLockMode.Locked;
            var keys = InputSystem.AddDevice<Keyboard>();
            var mini = View("Minimap");
            const string mapPreference = "TwinEdge.MinimapVisible";
            bool mapWasVisible = mini.activeInHierarchy;
            bool preferenceExisted = PlayerPrefs.HasKey(mapPreference);
            yield return Press(keys, Key.N);
            Require(mini.activeInHierarchy != mapWasVisible, "N toggles minimap");
            if (!mini.activeInHierarchy) yield return Press(keys, Key.N);
            Require(mini.activeInHierarchy, "N restores minimap");
            CheckMinimapSize(mini, (float)Screen.width / Screen.height);
            if (host) yield return CheckGameplayLayouts(mini);
            yield return Press(keys, Key.M);
            Require(PresentationRoot.BlocksGameplay && View("Detailed map").activeInHierarchy, "M opens modal and owns input");
            CheckListener(game.standbyCamera.name);
            yield return Screenshot("full-map");
            yield return Press(keys, Key.Escape);
            Require(!PresentationRoot.BlocksGameplay && mini.activeInHierarchy, "Esc closes map and releases input");
            Cursor.lockState = CursorLockMode.Locked;
            yield return RouteViews();
            InputSystem.RemoveDevice(keys);
            if (!mapWasVisible) PlayerPrefs.SetInt(mapPreference, 0);
            else if (!preferenceExisted) PlayerPrefs.DeleteKey(mapPreference);
            PlayerPrefs.Save();
            yield return Barrier("played");
            int oldTransactions = AudioCount("GemPickup") + AudioCount("GemSteal") + AudioCount("Seal") + AudioCount("Win") + AudioCount("Lose");

            if (!host)
            {
                var leave = game.sessions.LeaveRoomAsync();
                yield return Wait(() => leave.IsCompleted, "client left live match");
                yield return Wait(() => Exists("host-live-rejoin"), "host saw live disconnect");
                yield return Join();
            }
            else
            {
                yield return Wait(() => State.ConnectedPlayerCount == 1, "host saw live disconnect");
                Require(State.Phase == FoundationPhase.Return, "live phase preserved");
                Signal("host-live-rejoin");
            }
            yield return Wait(() => State.CanPlay, "live match recovered with no new user Ready");
            Require(State.Phase == FoundationPhase.Return && !State.ClientReady, "rejoin retains phase without user readiness");
            yield return Wait(() => View("Gameplay HUD").activeInHierarchy, "live match HUD restored");
            CheckListener(game.standbyCamera.name);
            if (audioTrace)
                Require(oldTransactions == AudioCount("GemPickup") + AudioCount("GemSteal") + AudioCount("Seal") +
                    AudioCount("Win") + AudioCount("Lose"), "rejoin does not replay gem acquisition, seal or result");
            yield return Barrier("rejoined");
            if (host)
            {
                var leave = game.sessions.LeaveRoomAsync();
                yield return Wait(() => leave.IsCompleted, "host ended room");
                var create = game.sessions.CreateRoomAsync("산마루");
                yield return Wait(() => create.IsCompleted && State.IsSpawned && State.ConnectedPlayerCount == 1, "host created fresh room");
                Require(State.Phase == FoundationPhase.WaitingForPlayers && !State.ClientReady, "fresh host room has no readiness");
                leave = game.sessions.LeaveRoomAsync();
                yield return Wait(() => leave.IsCompleted, "fresh room cleanup");
            }
            else yield return Wait(() => !game.sessions.HasSession, "client observed host closure");
            yield return Delay();
            CheckListener("Menu World Camera");
            if (audioTrace) Require(game.GetComponent<AudioService>().GetComponentsInChildren<AudioSource>().All(s => !s.isPlaying),
                "audio pool silent after session shutdown");
            Signal(Role + "-pass", "PASS Windows Player Relay readiness, input ownership and rejoin.");
            Application.Quit(0);
        }

        IEnumerator CheckGameplayLayouts(GameObject minimap)
        {
            foreach (int width in new[] { 1280, 1600, 1920, 2560 })
            {
                int height = width * 9 / 16;
                using var target = new CaptureTarget(game.Presentation.Canvas, game.standbyCamera, width, height);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                CheckMinimapSize(minimap, (float)width / height);
                foreach (var image in View("Gameplay HUD").GetComponentsInChildren<Graphic>())
                {
                    if (image is not Text label || label.text.Length == 0 || !label.isActiveAndEnabled) continue;
                    Require(label.preferredHeight <= label.rectTransform.rect.height + 2, $"HUD label fits at {width}×{height}");
                }
                if (capture) target.Save(Path.Combine(folder, $"{Role}-hud-{width}x{height}.png"));
                Debug.Log($"Presentation check passed: fixed D3D11 target layout {width}×{height}");
            }
            yield return Delay(.15f);
        }

        void CheckMinimapSize(GameObject minimap, float aspect)
        {
            var rect = (RectTransform)minimap.transform;
            float screenFraction = rect.rect.width / game.Presentation.Overlays.rect.width;
            Require(Mathf.Abs(screenFraction - Mathf.Min(.13f, .32f / aspect)) < .004f, "minimap diameter");
            Require(Mathf.Approximately(rect.rect.width, rect.rect.height), "circular minimap stays square");
        }

        IEnumerator RouteViews()
        {
            var map = Resources.Load<CourseMapData>("V3CourseMap");
            Require(FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1,
                "one gameplay EventSystem");
            Require(FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(camera => camera.enabled) == 1,
                "one active gameplay camera");
            Require(map && map.Sheets.Length == 9 &&
                map.Sheets.All(sheet => sheet.Texture && sheet.Bounds.width > 0 && sheet.Bounds.height > 0),
                "all public map sheets loaded");
            long mapBytes = map.Sheets.Select(sheet => sheet.Texture).Distinct().Sum(texture => 4L * texture.width * texture.height);
            Debug.Log($"Presentation map texture payload (RGBA32, base level): {mapBytes} bytes");
            var local = game.manager.LocalClient.PlayerObject.GetComponent<GameplayCharacter>();
            var pole = game.cooperativePole.Snapshot;
            yield return Place("mountain", pole.a - Vector3.forward * 1.2f,
                pole.a + Vector3.right * 3 - Vector3.forward * 1.2f);
            yield return Screenshot("mountain-names-and-pole");
            yield return Place("bridge", BridgeGameplay.Current.CratePoint, BridgeGameplay.Current.CratePoint + Vector3.right * 2);
            yield return Screenshot("bridge-hud");
            yield return Place("river", map.LowerDock, map.LowerDock + Vector3.right * 3);
            yield return Screenshot("river-map-and-hud");
            if (host)
                game.respawns.SetCheckpoint(local.GetComponent<PlayerMovement>(), local.transform.position,
                    local.transform.rotation, "Presentation checkpoint fixture");
            yield return Delay(.3f);
            if (host) yield return Screenshot("checkpoint-notice");

            Vector3 castle = map.CastleGate + new Vector3(-15, 0, 65);
            yield return Place("castle", castle, castle + Vector3.right * 3);
            yield return Screenshot("castle-floor-and-hud");
            var cave = UndergroundGameplay.Current;
            yield return Place("cavern", cave.Landing(0, 0), cave.Landing(1, 0), false);
            yield return Screenshot("underground-floor-and-hud");

            // Fixture advances only to SealPending and positions two players. Pickup still goes through the production RPC.
            Vector3 summit = map.FirstSummit;
            yield return Place("gem", summit + Vector3.forward * 2, summit + Vector3.right * 7);
            var gem = GemOwnership.Current;
            if (host)
            {
                Require(State.AdvanceFoundation(FoundationPhase.SealPending), "fixture entered SealPending");
                var feet = local.transform.position;
                gem.UnlockServer(feet + Vector3.up * .65f + Vector3.forward);
            }
            yield return Wait(() => gem.State == GemState.Available, "gem available on both peers");
            yield return Barrier("gem-available");
            if (host) gem.RequestAcquire();
            yield return Wait(() => gem.State == GemState.Carried && State.Phase == FoundationPhase.Return, "production pickup and Return transition");
            yield return Delay(.3f);
            yield return Screenshot("return-gem-notice");
            yield return Barrier("gem-carried");

            if (host)
                yield return Wait(() => gem.State == GemState.Dropped || gem.TryDropServer(local.OwnerClientId, local.transform.position),
                    "server accepted a drop after protection");
            yield return Wait(() => gem.State == GemState.Dropped, "drop replicated");
            yield return Screenshot("gem-dropped");
            Vector3 dropped = gem.Position;
            yield return Place("gem-recovery", dropped - Vector3.right * 5, dropped + Vector3.right * .9f);
            if (!host) gem.RequestAcquire();
            yield return Wait(() => gem.State == GemState.Carried && gem.Owner != Unity.Netcode.NetworkManager.ServerClientId,
                "client picked up the previously carried gem");
            yield return Screenshot("gem-stolen");
            yield return Barrier("gem-stolen");
            if (host)
                game.respawns.RespawnPlayer(game.manager.ConnectedClients[gem.Owner].PlayerObject.GetComponent<PlayerMovement>(), true);
            yield return Wait(() => gem.State == GemState.Relocated, "approved respawn relocates gem");
            yield return Delay(.3f);
            yield return Screenshot("gem-relocated");
            yield return Place("off-map", map.LastSummit, map.LowerDock);
            yield return Screenshot("return-edge-pins");
            yield return Barrier("route-views");
        }

        IEnumerator Place(string stage, Vector3 hostPoint, Vector3 clientPoint, bool safeGround = true)
        {
            if (host)
            {
                foreach (var peer in game.manager.ConnectedClientsList)
                {
                    Vector3 point = peer.ClientId == game.manager.LocalClientId ? hostPoint : clientPoint;
                    var pose = safeGround ? V3GameplayBootstrap.SafeGround(point) : point + Vector3.up * .08f;
                    peer.PlayerObject.GetComponent<PlayerMovement>().ApplyApprovedSpawnRpc(pose, Quaternion.identity);
                }
                Signal("placed-" + stage);
            }
            yield return Wait(() => Exists("placed-" + stage), "fixture " + stage);
            yield return Delay(1);
            yield return Barrier("observed-" + stage);
        }

        IEnumerator Join()
        {
            var task = game.sessions.JoinRoomAsync(code, "물결");
            yield return Wait(() => task.IsCompleted && game.sessions.HasSession, "client joined Relay");
        }

        GameObject View(string name) => game.Presentation.GetComponentsInChildren<RectTransform>(true)
            .Single(rect => rect.name == name).gameObject;

        Button LobbyAction() => View("준비").GetComponent<Button>();

        IEnumerator Screenshot(string name, bool redact = false)
        {
            if (!capture) yield break;
            var sensitive = redact ? game.Presentation.GetComponentsInChildren<Text>()
                .FirstOrDefault(t => t.text == game.sessions.CurrentJoinCode && !string.IsNullOrEmpty(t.text)) : null;
            var camera = redact ? GameObject.Find("Menu World Camera").GetComponent<Camera>() : game.standbyCamera;
            using var target = new CaptureTarget(game.Presentation.Canvas, camera, 1600, 900);
            if (sensitive) sensitive.enabled = false;
            try
            {
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                if (redact)
                {
                    var portraits = FindObjectsByType<Camera>(FindObjectsSortMode.None)
                        .Where(item => item.name == "Portrait Camera" && item.isActiveAndEnabled).ToArray();
                    Require(portraits.Length == State.ConnectedPlayerCount, "one active portrait camera per occupied slot");
                    foreach (var portrait in portraits) portrait.Render();
                }
                target.Save(Path.Combine(folder, Role + "-" + name + ".png"));
            }
            finally
            {
                if (sensitive) sensitive.enabled = true;
            }
        }

        sealed class CaptureTarget : IDisposable
        {
            readonly Canvas canvas;
            readonly Camera camera;
            readonly RenderTexture render, previous;
            readonly RenderMode previousMode;
            readonly Camera previousCamera;

            internal CaptureTarget(Canvas canvas, Camera camera, int width, int height)
            {
                this.canvas = canvas;
                this.camera = camera;
                previousMode = canvas.renderMode;
                previousCamera = canvas.worldCamera;
                previous = camera.targetTexture;
                render = new RenderTexture(width, height, 24);
                camera.targetTexture = render;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + .01f;
            }

            internal void Save(string path)
            {
                RenderTexture active = RenderTexture.active;
                var pixels = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
                try
                {
                    camera.Render();
                    RenderTexture.active = render;
                    pixels.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
                    pixels.Apply();
                    var sample = pixels.GetPixel(render.width / 2, render.height / 2);
                    Require(sample.maxColorComponent > .01f, "capture contains a rendered frame");
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = active;
                    Object.Destroy(pixels);
                }
            }

            public void Dispose()
            {
                camera.targetTexture = previous;
                canvas.worldCamera = previousCamera;
                canvas.renderMode = previousMode;
                Object.Destroy(render);
            }
        }

        IEnumerator Press(Keyboard keyboard, Key key)
        {
            // Use the same Keyboard.current gate as live controls.
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return Delay(.12f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Delay(.12f);
        }

        IEnumerator Barrier(string stage)
        {
            Signal(Role + "-" + stage);
            yield return Wait(() => Exists((host ? "client-" : "host-") + stage), stage);
        }

        IEnumerator Wait(Func<bool> condition, string stage)
        {
            float deadline = Time.realtimeSinceStartup + 75;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Require(condition(), stage);
            Debug.Log("Presentation check passed: " + stage);
        }

        static IEnumerator Delay(float seconds = .65f) => new WaitForSecondsRealtime(seconds);

        bool Exists(string name) => File.Exists(Path.Combine(folder, name + ".txt"));
        void Signal(string name, string contents = "ready") => File.WriteAllText(Path.Combine(folder, name + ".txt"), contents);

        static void Require(bool success, string stage)
        {
            if (!success) throw new InvalidOperationException("Presentation check failed: " + stage);
        }

        void CheckListener(string expected)
        {
            if (!audioTrace) return;
            var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(l => l.isActiveAndEnabled).ToArray();
            Require(listeners.Length == 1 && listeners[0].name == expected, "one audio listener on " + expected);
            Debug.Log("Presentation check passed: one audio listener on " + expected);
        }

        int AudioCount(string cue) => audioCounts.TryGetValue(cue, out int count) ? count : 0;

        void OnLog(string message, string trace, LogType type)
        {
            if (audioTrace && message.StartsWith("AUDIO_PLAY ", StringComparison.Ordinal))
            {
                int end = message.IndexOf(' ', 11);
                string cue = message.Substring(11, end - 11);
                audioCounts[cue] = AudioCount(cue) + 1;
            }
            if (type != LogType.Exception && !message.StartsWith("Presentation check failed:", StringComparison.Ordinal)) return;
            Signal(Role + "-failure", message);
            Application.Quit(1);
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;
    }
}
#endif
