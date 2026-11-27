#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using TwinEdge.Audio;
using Unity.Profiling;
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
            bool course = args.Contains("-twinCourseAudioProbe");
            if (!restore && !course && !args.Contains("-twinAudioProbe")) return;
            var probe = new GameObject("Audio Foundation Probe").AddComponent<AudioFoundationProbe>();
            probe.StartCoroutine(probe.Run(restore, course));
        }

        IEnumerator Run(bool restore, bool course)
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
            if (course)
            {
                yield return RunCourse(service, listeners[0]);
                yield break;
            }
            service.SuspendCourseAudio();
            var sources = service.PooledSources;
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
            foreach (var cue in catalog.cues.Where(c => c.id <= AudioCueId.Lose && c.priority > 60).OrderByDescending(c => c.priority))
                AudioService.Play(cue.id, head + forward * 2);
            if (!Check(sources.All(s => s.isPlaying) &&
                AudioService.Play(AudioCueId.TrapWarning, head + forward * 2) &&
                service.PooledSources.SequenceEqual(sources),
                "voice cap, priority replacement and reused emitters")) yield break;
            yield return new WaitForSecondsRealtime(.08f);
            AudioService.StopSession();
            if (!Check(sources.All(s => !s.isPlaying && !s.clip), "session cleanup")) yield break;
            AudioService.SetMasterPercent(50);
            Debug.Log("AUDIO_PROBE_PASS");
            Application.Quit();
        }

        IEnumerator RunCourse(AudioService service, AudioListener listener)
        {
            var catalog = Resources.Load<CourseAudioCatalog>("V3CourseAudio");
            if (!Check(catalog && catalog.music.Skip(1).Concat(catalog.beds.Skip(1))
                .Concat(new[] { catalog.water, catalog.rain, catalog.lava, catalog.flame, catalog.charge })
                .All(c => c && c.loadType == AudioClipLoadType.Streaming), "streaming course imports")) yield break;
            if (!Check(new[] { catalog.water, catalog.rain, catalog.lava, catalog.flame, catalog.charge }
                .All(c => c.channels == 1) && catalog.music.Skip(1).All(c => c.channels == 2),
                "mono spatial clips and stereo music")) yield break;
            var sources = service.GetComponentsInChildren<AudioSource>();
            var menu = catalog.Music(MusicTheme.Menu);
            if (!Check(sources.Length == 25 && sources.Count(s => s.isPlaying && s.clip == menu) == 1,
                "production menu plays with 16 pool slots and nine dedicated slots")) yield break;

            using var audioMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Audio Used Memory");
            using var clipMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "AudioClip Memory");
            using var gcFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 60);
            AudioService.SetMasterPercent(100);
            MarkSignal("menu");
            yield return new WaitForSecondsRealtime(2);
            ReportCounters("menu", sources.Count(s => s.isPlaying));
            service.SuspendCourseAudio();

            var emitter = new GameObject("Course signal fixture").transform;
            var music = new LoopPlayback(emitter, "Music", catalog.musicOutput, .72f);
            var ambience = new LoopPlayback(emitter, "Ambient Bed", catalog.ambienceOutput, .63f);
            var spatial = LoopPlayback.CreateSource(emitter, "Spatial", catalog.ambienceOutput, 28);
            var hint = listener.transform.position + listener.transform.forward * 2;
            var originalTheme = catalog.Music(MusicTheme.Cavern);
            foreach (int percent in new[] { 100, 50, 0 })
            {
                AudioService.SetMasterPercent(percent);
                music.Stop();
                ambience.Stop();
                music.Select(originalTheme);
                ambience.Select(catalog.Bed(AmbientBed.Cavern));
                yield return Tick(1.6f);
                MarkSignal("mix-" + percent);
                AudioService.Play(AudioCueId.TrapWarning, hint);
                AudioService.Play(AudioCueId.AttackVoice, hint);
                yield return Tick(2.4f);
                if (percent == 100)
                    ReportCounters("steady music and bed", emitter.GetComponentsInChildren<AudioSource>().Count(s => s.isPlaying));
            }

            AudioService.SetMasterPercent(100);
            music.Stop();
            ambience.Stop();
            music.Select(originalTheme);
            yield return Tick(1.6f);
            MarkSignal("crossfade");
            music.Select(catalog.Music(MusicTheme.Pursuit));
            yield return Tick(.56f);
            music.Select(catalog.Music(MusicTheme.Race));
            yield return Tick(2.7f);
            var musicSources = emitter.GetComponentsInChildren<AudioSource>();
            if (!Check(musicSources.Count(s => s.isPlaying && s.clip == catalog.Music(MusicTheme.Race)) == 1 &&
                musicSources.Count(s => s.isPlaying) == 1, "interrupted fade settles to the last request")) yield break;

            music.Select(originalTheme);
            yield return Tick(2);
            var looping = musicSources.Single(s => s.isPlaying);
            looping.timeSamples = originalTheme.samples - originalTheme.frequency * 2;
            MarkSignal("loop-wrap");
            yield return Tick(4.4f);
            if (!Check(looping.isPlaying && looping.timeSamples < originalTheme.frequency * 4,
                "streaming music wrapped at the loop seam")) yield break;
            music.Stop();

            foreach (int distance in new[] { 2, 18 })
            {
                LoopPlayback.Clear(spatial);
                spatial.transform.position = listener.transform.position + listener.transform.forward * distance;
                spatial.volume = .42f;
                spatial.clip = catalog.water;
                spatial.Play();
                yield return new WaitForSecondsRealtime(1);
                MarkSignal("water-" + distance);
                yield return new WaitForSecondsRealtime(2);
            }
            LoopPlayback.Clear(spatial);
            Destroy(emitter.gameObject);
            AudioService.SetMasterPercent(50);
            Debug.Log("AUDIO_COURSE_PROBE_PASS");
            Application.Quit();

            void ReportCounters(string stage, int playing)
            {
                string bytes(ProfilerRecorder recorder) => recorder.Valid && recorder.Count > 0
                    ? recorder.LastValue.ToString() : "unavailable";
                string gc = gcFrame.Valid && gcFrame.Count > 0
                    ? gcFrame.ToArray().Average(s => s.Value).ToString("F0") : "unavailable";
                Debug.Log($"AUDIO_METRIC {stage} | playing={playing} | Audio Used Memory={bytes(audioMemory)} B | " +
                    $"AudioClip Memory={bytes(clipMemory)} B | GC Allocated In Frame (Player mean)={gc} B");
            }

            IEnumerator Tick(float seconds)
            {
                float until = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < until)
                {
                    music.Tick(Time.unscaledDeltaTime / 1.15f);
                    ambience.Tick(Time.unscaledDeltaTime / .85f);
                    yield return null;
                }
            }
        }

        static void MarkSignal(string name) => Debug.Log($"AUDIO_COURSE_SIGNAL {DateTime.UtcNow:O} {name}");

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
