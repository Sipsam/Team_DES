using System;
using UnityEngine;

namespace TwinEdge.Audio
{
    public sealed class AudioService : MonoBehaviour
    {
        public const string MasterPreference = "TwinEdge.Audio.MasterPercent";
        public const string MasterParameter = "MasterVolumeDb";
        const int VoiceCount = 16;

        struct Voice
        {
            internal AudioSource Source;
            internal AudioCueCatalog.Entry Cue;
            internal double Started;
        }

        static AudioService current;
        AudioCueCatalog catalog;
        readonly System.Random pitchRandom = new();
        readonly Voice[] voices = new Voice[VoiceCount];
        AudioCueCatalog.Entry[] cues;
        double[] lastPlayed;
        AudioListener listener;
        bool started;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool trace;
#endif

        public static int MasterPercent => Mathf.Clamp(PlayerPrefs.GetInt(MasterPreference, 100), 0, 100);

        public static void SetMasterPercent(int percent)
        {
            PlayerPrefs.SetInt(MasterPreference, Mathf.Clamp(percent, 0, 100));
            PlayerPrefs.Save();
            if (current && current.started) current.ApplyMaster();
        }

        internal static float MasterDecibels(int percent) => percent <= 0 ? -80 : 20 * Mathf.Log10(percent / 100f);

        internal static void UseListener(AudioListener active)
        {
            if (current) current.listener = active;
        }

        void Awake()
        {
            if (current)
            {
                Debug.LogError("Twin Edge audio service already exists.");
                enabled = false;
                return;
            }
            current = this;
            catalog = Resources.Load<AudioCueCatalog>("CoreAudioCues");
            if (!catalog || !catalog.mixer)
            {
                Debug.LogError("Twin Edge audio catalog or mixer is missing.");
                return;
            }
            cues = new AudioCueCatalog.Entry[Enum.GetValues(typeof(AudioCueId)).Length];
            lastPlayed = new double[cues.Length];
            foreach (var cue in catalog.cues) cues[(int)cue.id] = cue;
            ClearRepeatLimits();

            for (int i = 0; i < voices.Length; i++)
            {
                var emitter = new GameObject($"Audio Voice {i + 1}");
                emitter.transform.SetParent(transform, false);
                var source = emitter.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.dopplerLevel = 0;
                voices[i].Source = source;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            trace = Array.IndexOf(Environment.GetCommandLineArgs(), "-twinAudioTrace") >= 0;
#endif
        }

        void Start()
        {
            if (!catalog || !catalog.mixer) return;
            started = true;
            ApplyMaster();
        }

        void ApplyMaster()
        {
            int percent = MasterPercent;
            if (!catalog.mixer.SetFloat(MasterParameter, MasterDecibels(percent)))
                Debug.LogError($"Twin Edge audio mixer is missing the exposed {MasterParameter} parameter.");
            // The mixer gain floor is finite. Listener volume provides hard silence at zero.
            AudioListener.volume = percent == 0 ? 0 : 1;
        }

        public static bool Play(AudioCueId id, Vector3 position = default) =>
            current && current.isActiveAndEnabled && current.started && current.PlayCue(id, position);

        bool PlayCue(AudioCueId id, Vector3 position)
        {
            if ((int)id >= cues.Length) return false;
            var cue = cues[(int)id];
            if (cue == null || !cue.clip || !cue.output || !listener || !listener.isActiveAndEnabled) return false;
            if (cue.spatialBlend > 0 && (listener.transform.position - position).sqrMagnitude >= cue.maxDistance * cue.maxDistance)
                return false;
            double now = AudioSettings.dspTime;
            if (now - lastPlayed[(int)id] < cue.repeatInterval) return false;

            int free = -1, replace = -1, count = 0;
            for (int i = 0; i < voices.Length; i++)
            {
                ref Voice voice = ref voices[i];
                if (!voice.Source.isPlaying)
                {
                    if (free < 0) free = i;
                    continue;
                }
                if (voice.Cue == cue) count++;
                if (voice.Cue.priority <= cue.priority) continue;
                if (replace < 0 || voice.Cue.priority > voices[replace].Cue.priority ||
                    (voice.Cue.priority == voices[replace].Cue.priority && voice.Started < voices[replace].Started))
                    replace = i;
            }

            int slot = free >= 0 ? free : replace;
            if (slot < 0 || count >= cue.simultaneous) return false;
            ref Voice selected = ref voices[slot];
            AudioSource source = selected.Source;
            source.Stop();
            source.transform.position = position;
            source.clip = cue.clip;
            source.outputAudioMixerGroup = cue.output;
            source.volume = cue.volume;
            source.pitch = Mathf.Lerp(cue.pitch.x, cue.pitch.y, (float)pitchRandom.NextDouble());
            source.spatialBlend = cue.spatialBlend;
            source.minDistance = cue.minDistance;
            source.maxDistance = cue.maxDistance;
            source.rolloffMode = cue.rolloff;
            source.priority = cue.priority;
            selected.Cue = cue;
            selected.Started = lastPlayed[(int)id] = now;
            source.Play();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (trace) Debug.Log($"AUDIO_PLAY {id} {now:F3} {position:F2}");
#endif
            return true;
        }

        internal static void StopSession()
        {
            if (!current) return;
            foreach (var voice in current.voices)
            {
                if (!voice.Source) continue;
                voice.Source.Stop();
                voice.Source.clip = null;
            }
            current.ClearRepeatLimits();
        }

        void ClearRepeatLimits()
        {
            if (lastPlayed != null) Array.Fill(lastPlayed, double.NegativeInfinity);
        }

        void OnDestroy()
        {
            if (current != this) return;
            current = null;
            AudioListener.volume = 1;
        }
    }
}
