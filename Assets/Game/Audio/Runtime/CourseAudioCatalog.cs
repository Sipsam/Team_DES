using UnityEngine;
using UnityEngine.Audio;

namespace TwinEdge.Audio
{
    enum MusicTheme { None, Menu, Summit, Bridge, River, Castle, Cavern, Pursuit, Race }
    enum AmbientBed { None, Mountain, Canyon, River, CastleExterior, CastleInterior, Cavern }

    sealed class CourseAudioCatalog : ScriptableObject
    {
        public AudioMixerGroup musicOutput, ambienceOutput, effectsOutput;
        public AudioClip[] music, beds;
        public AudioClip water, rain, lava, flame, charge;

        internal AudioClip Music(MusicTheme theme) => music[(int)theme];
        internal AudioClip Bed(AmbientBed bed) => beds[(int)bed];
    }
}
