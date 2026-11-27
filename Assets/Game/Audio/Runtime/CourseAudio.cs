using TwinEdge.Gameplay;
using TwinEdge.Presentation;
using Unity.Netcode;
using UnityEngine;
using Zone = TwinEdge.Presentation.CourseMapData.Zone;

namespace TwinEdge.Audio
{
    sealed class CourseAudio
    {
        const float ZoneDwell = .65f;
        readonly CourseAudioCatalog catalog;
        readonly CourseMapData map;
        readonly LoopPlayback music, ambience;
        readonly AudioSource water, rain, lava, flame, charge;
        NetworkObject player;
        MountainReturnLaunch launch;
        Zone zone, candidate;
        float candidateSince;
        MusicTheme theme;
        AmbientBed bed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool chargeAudible;
        int tracedSpatialLoops;
#endif

        internal CourseAudio(Transform parent, CourseAudioCatalog catalog, CourseMapData map)
        {
            this.catalog = catalog;
            this.map = map;
            music = new LoopPlayback(parent, "Music", catalog.musicOutput, .72f);
            ambience = new LoopPlayback(parent, "Ambient Bed", catalog.ambienceOutput, .63f);
            water = LoopPlayback.CreateSource(parent, "River Water", catalog.ambienceOutput, 28);
            rain = LoopPlayback.CreateSource(parent, "River Rain", catalog.ambienceOutput, 45);
            lava = LoopPlayback.CreateSource(parent, "Cavern Lava", catalog.ambienceOutput, 48);
            flame = LoopPlayback.CreateSource(parent, "Cavern Flame", catalog.ambienceOutput, 15);
            charge = LoopPlayback.CreateSource(parent, "Launch Charge", catalog.effectsOutput);
        }

        internal void Tick(V3GameplayBootstrap game, AudioListener listener, bool trace)
        {
            var state = game.state;
            var phase = state.HasLiveAudio ? state.Phase : FoundationPhase.WaitingForPlayers;
            var local = phase >= FoundationPhase.Outbound ? game.manager.LocalClient.PlayerObject : null;
            Vector3 feet = local ? local.transform.position : default;
            if (local != player)
            {
                player = local;
                launch = local ? local.GetComponent<MountainReturnLaunch>() : null;
                if (local) zone = candidate = map.At(feet);
            }
            else if (local) UpdateZone(feet, Time.unscaledTime);

            bool won = local && state.Won(local.GetComponent<GameplayCharacter>().LobbySlot);
            var nextTheme = ThemeFor(phase, zone, won);
            bool inCourse = local && phase < FoundationPhase.Finished;
            var nextBed = inCourse ? BedFor(zone) : AmbientBed.None;
            if (nextTheme != theme || nextBed != bed)
            {
                theme = nextTheme;
                bed = nextBed;
                music.Select(catalog.Music(theme));
                ambience.Select(catalog.Bed(bed));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (trace) Debug.Log($"AUDIO_COURSE {phase} {zone} {theme} {bed}");
#endif
            }

            float dt = Time.unscaledDeltaTime;
            music.Tick(dt / 1.15f);
            ambience.Tick(dt / .85f);
            UpdateSpatial(inCourse, feet, listener.transform.position, dt);
            bool charging = inCourse && launch && launch.ChargeAudioActive;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (trace) TraceSpatialLoops();
            if (trace && charging != chargeAudible) Debug.Log($"AUDIO_CHARGE_LOOP {(charging ? "on" : "off")}");
            chargeAudible = charging;
#endif
            LoopPlayback.Spatial(charge, catalog.charge, default, charging ? .12f : 0, dt);
        }

        void UpdateZone(Vector3 feet, float now)
        {
            var observed = map.At(feet);
            if (observed != candidate)
            {
                candidate = observed;
                candidateSince = now;
            }
            if (now - candidateSince >= ZoneDwell) zone = candidate;
        }

        internal static MusicTheme ThemeFor(FoundationPhase phase, Zone zone, bool won)
        {
            if (phase == FoundationPhase.Finished) return won ? MusicTheme.Summit : MusicTheme.Cavern;
            if (phase < FoundationPhase.Outbound) return MusicTheme.Menu;
            if (phase == FoundationPhase.Return)
                return zone is Zone.Mountain or Zone.Finish or Zone.River ? MusicTheme.Race : MusicTheme.Pursuit;

            return zone switch
            {
                Zone.Bridge or Zone.Canyon => MusicTheme.Bridge,
                Zone.River => MusicTheme.River,
                Zone.CastleExterior or Zone.CastleInterior => MusicTheme.Castle,
                Zone.Stair or Zone.Cavern => MusicTheme.Cavern,
                _ => MusicTheme.Summit
            };
        }

        internal static AmbientBed BedFor(Zone zone) => zone switch
        {
            Zone.Bridge or Zone.Canyon => AmbientBed.Canyon,
            Zone.River => AmbientBed.River,
            Zone.CastleExterior => AmbientBed.CastleExterior,
            Zone.CastleInterior or Zone.Stair => AmbientBed.CastleInterior,
            Zone.Cavern => AmbientBed.Cavern,
            _ => AmbientBed.Mountain
        };

        void UpdateSpatial(bool inCourse, Vector3 feet, Vector3 listener, float dt)
        {
            Vector3 waterAt = default, rainAt = default, lavaAt = default, flameAt = default;
            bool hasWater = false, hasRain = false, hasLava = false, hasFlame = false;
            if (inCourse && (zone is Zone.River or Zone.Canyon or Zone.CastleExterior) && RiverGameplay.Current is { } river)
            {
                hasWater = river.TryAudioWater(listener, water.maxDistance, out waterAt);
                hasRain = river.TryAudioRain(feet, out rainAt);
            }
            if (inCourse && zone == Zone.Cavern && UndergroundGameplay.Current is { } underground)
            {
                hasLava = underground.TryAudioLava(listener, lava.maxDistance, out lavaAt);
                hasFlame = underground.TryAudioFlame(listener, flame.maxDistance, out flameAt);
            }

            LoopPlayback.Spatial(water, catalog.water, waterAt, hasWater ? .42f : 0, dt);
            LoopPlayback.Spatial(rain, catalog.rain, rainAt, hasRain ? .48f : 0, dt);
            LoopPlayback.Spatial(lava, catalog.lava, lavaAt, hasLava ? .38f : 0, dt);
            LoopPlayback.Spatial(flame, catalog.flame, flameAt, hasFlame ? .3f : 0, dt);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void TraceSpatialLoops()
        {
            int playing = (water.isPlaying ? 1 : 0) | (rain.isPlaying ? 2 : 0) |
                (lava.isPlaying ? 4 : 0) | (flame.isPlaying ? 8 : 0);
            if (playing == tracedSpatialLoops) return;
            tracedSpatialLoops = playing;
            Debug.Log($"AUDIO_SPATIAL water={water.isPlaying} rain={rain.isPlaying} lava={lava.isPlaying} flame={flame.isPlaying}");
        }
#endif

        internal void StopSession(bool keepMenuMusic)
        {
            player = null;
            launch = null;
            if (keepMenuMusic && theme == MusicTheme.Menu) music.Stop(catalog.Music(MusicTheme.Menu));
            else
            {
                theme = MusicTheme.None;
                music.Stop();
            }
            bed = AmbientBed.None;
            ambience.Stop();
            LoopPlayback.Clear(water);
            LoopPlayback.Clear(rain);
            LoopPlayback.Clear(lava);
            LoopPlayback.Clear(flame);
            LoopPlayback.Clear(charge);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            chargeAudible = false;
#endif
        }
    }
}
