using UnityEngine;
using UnityEngine.Audio;

namespace TwinEdge.Audio
{
    // Each layer owns a fixed pair of sources. A third request keeps the louder source until a slot frees up.
    sealed class LoopPlayback
    {
        readonly AudioSource[] sources;
        readonly float gain;
        AudioClip requested;

        internal LoopPlayback(Transform parent, string name, AudioMixerGroup output, float gain)
        {
            this.gain = gain;
            sources = new[]
            {
                CreateSource(parent, name + " A", output),
                CreateSource(parent, name + " B", output)
            };
        }

        internal void Select(AudioClip clip) => requested = clip;

        internal void Tick(float fadeStep)
        {
            AudioSource available = null;
            bool hasRequested = false;
            AudioSource hold = null;
            if (requested && sources[0].clip && sources[1].clip &&
                sources[0].clip != requested && sources[1].clip != requested)
                hold = sources[0].volume >= sources[1].volume ? sources[0] : sources[1];

            foreach (var source in sources)
            {
                bool selected = source.clip && source.clip == requested;
                hasRequested |= selected;
                if (source == hold) continue;
                source.volume = Mathf.MoveTowards(source.volume, selected ? gain : 0, fadeStep * gain);
                if (!selected && source.volume == 0) Clear(source);
                if (!source.clip) available = source;
            }
            if (requested && !hasRequested && available)
            {
                available.clip = requested;
                available.Play();
            }
        }

        internal void Stop(AudioClip keep = null)
        {
            requested = keep;
            foreach (var source in sources)
                if (source.clip != keep) Clear(source);
        }

        internal static AudioSource CreateSource(Transform parent, string name, AudioMixerGroup output, float range = 0)
        {
            var emitter = new GameObject(name);
            emitter.transform.SetParent(parent, false);
            var source = emitter.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.dopplerLevel = 0;
            source.volume = 0;
            source.priority = 160;
            source.outputAudioMixerGroup = output;
            if (range > 0)
            {
                source.spatialBlend = 1;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 2;
                source.maxDistance = range;
            }
            return source;
        }

        internal static void Spatial(AudioSource source, AudioClip clip, Vector3 position, float volume, float delta)
        {
            if (volume > 0)
            {
                source.transform.position = position;
                if (!source.clip)
                {
                    source.clip = clip;
                    source.Play();
                }
            }
            source.volume = Mathf.MoveTowards(source.volume, volume, delta * 1.8f);
            if (source.volume == 0 && volume == 0) Clear(source);
        }

        internal static void Clear(AudioSource source)
        {
            if (source.clip)
            {
                source.Stop();
                source.clip = null;
            }
            source.volume = 0;
        }
    }
}
