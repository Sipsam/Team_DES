using UnityEngine;

namespace TwinEdge.Presentation
{
    internal static class LocalPresentationSettings
    {
        const string MouseKey = "TwinEdge.MouseSensitivityPercent";
        const string EffectKey = "TwinEdge.EffectIntensityPercent";
        const string DisplayKey = "TwinEdge.DisplayFullscreen";
        internal const int DefaultPercent = 100;
        internal const int MinMousePercent = 10;
        internal const int MaxMousePercent = 200;
        internal const bool DefaultFullscreen = false;
        static int? mousePercent, effectPercent;
        internal static Vector2Int LastWindowSize { get; set; }

        internal static int MousePercent
        {
            get => mousePercent ??= Mathf.Clamp(PlayerPrefs.GetInt(MouseKey, DefaultPercent), MinMousePercent, MaxMousePercent);
            set
            {
                int percent = Mathf.Clamp(value, MinMousePercent, MaxMousePercent);
                mousePercent = percent;
                Save(MouseKey, percent);
            }
        }

        internal static int EffectPercent
        {
            get => effectPercent ??= Mathf.Clamp(PlayerPrefs.GetInt(EffectKey, DefaultPercent), 0, 100);
            set
            {
                int percent = Mathf.Clamp(value, 0, 100);
                effectPercent = percent;
                Save(EffectKey, percent);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCachedValues()
        {
            mousePercent = effectPercent = null;
            LastWindowSize = default;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RestoreDisplay()
        {
            if (Application.isEditor) return;
            foreach (var argument in System.Environment.GetCommandLineArgs())
                if (argument == "-screen-fullscreen" || argument == "-window-mode") return;

            bool fullscreen = PlayerPrefs.GetInt(DisplayKey) == 1;
            if (Screen.fullScreen != fullscreen)
                ApplyDisplay(DisplayMode(fullscreen), new Vector2Int(Screen.width, Screen.height));
        }

        internal static FullScreenMode DisplayMode(bool fullscreen) =>
            fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        internal static void ApplyDisplay(FullScreenMode mode, Vector2Int size) =>
            Screen.SetResolution(size.x, size.y, mode);

        internal static void ConfirmDisplay(bool fullscreen) => Save(DisplayKey, fullscreen ? 1 : 0);

        static void Save(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }
}
