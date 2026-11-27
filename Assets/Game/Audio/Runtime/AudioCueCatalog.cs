using System;
using UnityEngine;
using UnityEngine.Audio;

namespace TwinEdge.Audio
{
    public enum AudioCueId : byte
    {
        Click, Confirm, Error, CopyCode, Ready, Unready, Start,
        Swing, Hit, Grab, Throw, Resist,
        TrapWarning, TrapFire, TrapArrow, TrapSpikes, TrapHit,
        BridgeWarning, BridgeCollapse, RaftHit, RaftBreak, RaftRestore,
        PuzzleAccept, PuzzleError, PuzzleComplete, DoorOpen, Switch, Platform,
        Seal, GemPickup, GemDrop, GemSteal, GemRelocate,
        Checkpoint, Recovery, Win, Lose
    }

    [CreateAssetMenu(menuName = "Twin Edge/Audio Cue Catalog")]
    public sealed class AudioCueCatalog : ScriptableObject
    {
        public AudioMixer mixer;
        public Entry[] cues;

        [Serializable]
        public sealed class Entry
        {
            public AudioCueId id;
            public AudioClip clip;
            public AudioMixerGroup output;
            [Range(0, 1)] public float volume = .2f;
            public Vector2 pitch = Vector2.one;
            [Range(0, 1)] public float spatialBlend;
            [Min(.1f)] public float minDistance = 2;
            [Min(.2f)] public float maxDistance = 20;
            public AudioRolloffMode rolloff = AudioRolloffMode.Linear;
            [Range(0, 256)] public int priority = 128;
            [Range(1, 16)] public int simultaneous = 2;
            [Min(0)] public float repeatInterval = .09f;
        }
    }
}
