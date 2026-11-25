#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwinEdge.Gameplay
{
    internal sealed class LocalMatch : MonoBehaviour
    {
        internal static bool Solo { get; private set; }
        internal static ushort Port { get; private set; }
        bool host;
        string directory;
        IntPtr window;
        string status = "Connecting local players...";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-localMatchPort");
            if (index < 0 || index + 1 >= args.Length || !ushort.TryParse(args[index + 1], out var port))
                return;
            Port = port;
            index = Array.IndexOf(args, "-soloMatch");
            if (index < 0 || index + 2 >= args.Length)
                return;
            Solo = true;
            var match = new GameObject(nameof(LocalMatch)).AddComponent<LocalMatch>();
            match.host = args[index + 1] == "host";
            match.directory = args[index + 2];
        }

        internal static bool Connect(NetworkManager manager, bool host)
        {
            var transport = (UnityTransport)manager.NetworkConfig.NetworkTransport;
            transport.SetConnectionData("127.0.0.1", Port, "127.0.0.1");
            return host ? manager.StartHost() : manager.StartClient();
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                while (window == IntPtr.Zero)
                {
                    window = FindOwnWindow();
                    if (window == IntPtr.Zero) yield return null;
                }
                SetWindowText(window, host ? "Twin Edge Solo - Player 1" : "Twin Edge Solo - Player 2");
                if (!host) ShowWindow(window, 0);
                File.WriteAllText(WindowFile(host), window.ToInt64().ToString());
            }

            var game = V3GameplayBootstrap.Current;
            var deadline = Time.realtimeSinceStartup + 90;
            string ready = Path.Combine(directory, "local-host-ready.txt");
            while (!game.LevelIsReady || (!host && !File.Exists(ready)))
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    status = "Local session timed out.";
                    yield break;
                }
                yield return null;
            }
            if (!Connect(game.manager, host))
            {
                status = "Could not start the local session.";
                yield break;
            }
            if (host) File.WriteAllText(ready, "ready");
            status = "";
        }

        string WindowFile(bool first) => Path.Combine(directory, first ? "host-window.txt" : "client-window.txt");

        void Update()
        {
            if (Keyboard.current?.f6Key.wasPressedThisFrame == true) SwitchPlayer();
        }

        void SwitchPlayer()
        {
            if (window == IntPtr.Zero || !IsWindowVisible(window)) return;
            string path = WindowFile(!host);
            if (!File.Exists(path) || !long.TryParse(File.ReadAllText(path), out var handle))
                return;
            var other = new IntPtr(handle);
            if (other == IntPtr.Zero || !IsWindow(other))
                return;
            ShowWindow(other, 9);
            SetForegroundWindow(other);
            ShowWindow(window, 0);
            UnityEngine.Debug.Log($"Solo view switched to Player {(host ? 2 : 1)}.");
        }

        void OnGUI()
        {
            var panel = Controls;
            GUI.Box(panel,
                $"SOLO  /  PLAYER {(host ? 1 : 2)}\n" + (status == "" ? "Shared carry and rowing" : status));
            if (GUI.Button(new Rect(panel.x + 12, Screen.height - 38, 249, 23), "Switch player (F6)"))
                SwitchPlayer();
        }

        static Rect Controls => new Rect(Mathf.Max(8, Screen.width - 285), Screen.height - 81, 273, 73);

        internal static bool PointerOverControls(Vector2 point) =>
            Solo && Controls.Contains(new Vector2(point.x, Screen.height - point.y));

        static IntPtr FindOwnWindow()
        {
            using var process = Process.GetCurrentProcess();
            uint processId = (uint)process.Id;
            IntPtr result = IntPtr.Zero;
            EnumWindows((handle, data) =>
            {
                GetWindowThreadProcessId(handle, out uint owner);
                if (owner != processId) return true;
                var className = new StringBuilder(64);
                GetClassName(handle, className, className.Capacity);
                if (className.ToString() != "UnityWndClass") return true;
                result = handle;
                return false;
            }, IntPtr.Zero);
            return result;
        }

        delegate bool WindowCallback(IntPtr handle, IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, IntPtr data);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr handle, StringBuilder name, int size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SetWindowText(IntPtr handle, string text);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle, int command);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr handle);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr handle);
    }
}
#endif
