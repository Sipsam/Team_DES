#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using TwinEdge.Audio;
using UnityEngine;

namespace TwinEdge.Gameplay.Testing
{
    // Optional on-demand check using the ordinary V3 scene and catalog.
    sealed class AudioFoundationProbe : MonoBehaviour
    {
        const string Backup = "TwinEdge.Tests.Audio.PreviousMaster";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool restore = args.Contains("-twinAudioRestore");
            if (!restore && !args.Contains("-twinAudioProbe")) return;
            var probe = new GameObject("Audio Foundation Probe").AddComponent<AudioFoundationProbe>();
            probe.StartCoroutine(probe.Run(restore));
        }

        IEnumerator Run(bool restore)
        {
            Application.runInBackground = true;
            var game = V3GameplayBootstrap.Current;
            yield return new WaitUntil(() => game.WorldReady && !game.Presentation.IsTransitioning);
            var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(l => l.isActiveAndEnabled).ToArray();
            if (!Check(listeners.Length == 1 && listeners[0].name == "Menu World Camera", "active menu listener") ||
                !Check(SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11, "D3D11")) yield break;

            var catalog = Resources.Load<AudioCueCatalog>("CoreAudioCues");
            if (!Check(catalog && catalog.cues.Length == Enum.GetValues(typeof(AudioCueId)).Length &&
                catalog.cues.Select(c => c.id).Distinct().Count() == catalog.cues.Length &&
                catalog.cues.All(c => c.clip && c.output && c.output.audioMixer == catalog.mixer &&
                    c.clip.loadState == AudioDataLoadState.Loaded && (c.spatialBlend == 0 || c.clip.channels == 1)),
                "catalog, mixer routing and preloaded imports")) yield break;
            string[] groups = { "Master", "Music", "Gameplay SFX", "Ambience", "UI", "Character Voice" };
            if (!Check(groups.All(g => catalog.mixer.FindMatchingGroups(g).Any(m => m.name == g)), "six mixer groups")) yield break;

            if (restore)
            {
                bool saved = AudioService.MasterPercent == 50 && MixerGain(catalog, -6.0206f);
                int previous = PlayerPrefs.GetInt(Backup, -1);
                AudioService.SetMasterPercent(previous < 0 ? 100 : previous);
                if (previous < 0) PlayerPrefs.DeleteKey(AudioService.MasterPreference);
                PlayerPrefs.DeleteKey(Backup);
                PlayerPrefs.Save();
                if (Check(saved, "Master restored at 50 percent in fresh process")) Application.Quit();
                yield break;
            }

            PlayerPrefs.SetInt(Backup, PlayerPrefs.GetInt(AudioService.MasterPreference, -1));
            var service = game.GetComponent<AudioService>();
            var sources = service.GetComponentsInChildren<AudioSource>();
            if (!Check(sources.Length == 16, "16 pooled sources")) yield break;
            Vector3 head = listeners[0].transform.position;
            Vector3 forward = listeners[0].transform.forward;
            foreach (var sample in new[] { (100, 0f), (50, 0f), (0, 0f), (100, 2f), (100, 16f) })
            {
                AudioService.SetMasterPercent(sample.Item1);
                yield return new WaitForSecondsRealtime(.2f);
                if (!Check(MixerGain(catalog, sample.Item1 == 0 ? -80 : sample.Item1 == 50 ? -6.0206f : 0) &&
                    AudioListener.volume == (sample.Item1 == 0 ? 0 : 1), $"Master {sample.Item1} percent")) yield break;
                var cue = sample.Item2 == 0 ? AudioCueId.Confirm : AudioCueId.DoorOpen;
                Debug.Log($"AUDIO_SIGNAL {DateTime.UtcNow:O} {sample.Item1} {sample.Item2}");
                if (!Check(AudioService.Play(cue, head + forward * sample.Item2) &&
                    !AudioService.Play(cue, head + forward * sample.Item2), "played once and rejected immediate duplicate")) yield break;
                yield return new WaitForSecondsRealtime(1.55f);
            }

            if (!Check(!AudioService.Play(AudioCueId.DoorOpen, head + forward * 80f), "culled beyond maximum distance")) yield break;
            Debug.Log($"AUDIO_SIGNAL {DateTime.UtcNow:O} stress");
            foreach (var cue in catalog.cues.Where(c => c.priority > 60).OrderByDescending(c => c.priority))
                AudioService.Play(cue.id, head + forward * 2);
            if (!Check(sources.All(s => s.isPlaying) &&
                AudioService.Play(AudioCueId.TrapWarning, head + forward * 2) &&
                service.GetComponentsInChildren<AudioSource>().SequenceEqual(sources),
                "voice cap, priority replacement and reused emitters")) yield break;
            yield return new WaitForSecondsRealtime(.08f);
            AudioService.StopSession();
            if (!Check(sources.All(s => !s.isPlaying && !s.clip), "session cleanup")) yield break;
            AudioService.SetMasterPercent(50);
            Debug.Log("AUDIO_PROBE_PASS");
            Application.Quit();
        }

        static bool MixerGain(AudioCueCatalog catalog, float expected) =>
            catalog.mixer.GetFloat(AudioService.MasterParameter, out float actual) && Mathf.Abs(actual - expected) < .01f;

        static bool Check(bool condition, string message)
        {
            if (condition) Debug.Log("AUDIO_CHECK " + message);
            else
            {
                Debug.LogError("AUDIO_FAIL " + message);
                Application.Quit(1);
            }
            return condition;
        }
    }
}
#endif
