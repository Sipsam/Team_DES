using TwinEdge.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwinEdge.Gameplay
{
    internal sealed class GameplayEffects : MonoBehaviour
    {
        internal enum Kind : byte { Dust, Stone, Land, Charge, Launch, Swing, Hit, Timber, Wake, Splash, Gem, GemDrop, GemRelocate }

        const int Capacity = 160;
        static GameplayEffects current;
        GameStateManager game;
        ParticleSystem particles;
        ParticleSystem.Particle[] liveParticles;
        Mesh mesh;
        float intensity;
        Unity.Mathematics.Random random = new(0xD7129u);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static readonly bool Trace = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-v3PolishTrace") >= 0;
        internal static ParticleSystem Inspect(GameStateManager game) => current && current.game == game ? current.particles : null;
#endif

        internal static bool Enabled(GameStateManager game) => game && game.HasLiveAudio && game.CanPlay &&
            MatchTransitionPresentation.EffectIntensity > 0 && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        internal static void Emit(GameStateManager game, Kind kind, Vector3 point, Vector3 direction, float strength = 1f)
        {
            if (!Enabled(game) ||
                (game.NetworkManager.LocalClient.PlayerObject.transform.position - point).sqrMagnitude > 48f * 48f) return;
            if (!current || current.game != game)
            {
                var material = Resources.Load<Material>("GameplayParticles");
                if (!material) return;
                var root = new GameObject("Gameplay effects");
                root.SetActive(false);
                root.transform.SetParent(game.transform, false);
                current = root.AddComponent<GameplayEffects>();
                current.Initialize(game, material);
                root.SetActive(true);
            }
            current.Emit(kind, point, direction, strength);
        }

        internal static void Clear(GameStateManager game)
        {
            if (current && current.game == game) current.particles.Clear();
        }

        void Initialize(GameStateManager owner, Material material)
        {
            game = owner;
            particles = gameObject.AddComponent<ParticleSystem>();
            liveParticles = new ParticleSystem.Particle[Capacity];
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = Capacity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.gravityModifier = .75f;
            main.startRotation3D = true;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.sharedMaterial = material;
            renderer.mesh = mesh = MatchTransitionPresentation.CreateCrystalMesh("Gameplay shard");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void Emit(Kind kind, Vector3 point, Vector3 direction, float strength)
        {
            ApplyIntensity();
            var (count, color, size, life, speed) = kind switch
            {
                Kind.Dust => (4, new Color32(161, 136, 103, 255), .062f, .32f, .70f),
                Kind.Stone => (4, new Color32(172, 170, 162, 255), .046f, .31f, .85f),
                Kind.Land => (9, new Color32(183, 164, 134, 255), .074f, .41f, 1.35f),
                Kind.Charge => (4, new Color32(224, 189, 113, 255), .048f, .16f, .6f),
                Kind.Launch => (13, new Color32(213, 192, 157, 255), .096f, .5f, 2.5f),
                Kind.Swing => (1, new Color32(252, 214, 147, 255), .05f, .12f, .25f),
                Kind.Hit => (8, new Color32(255, 176, 100, 255), .065f, .3f, 2.35f),
                Kind.Timber => (7, new Color32(167, 111, 65, 255), .069f, .43f, 1.35f),
                Kind.Wake => (5, new Color32(151, 210, 222, 255), .066f, .35f, .56f),
                Kind.Splash => (8, new Color32(188, 231, 240, 255), .057f, .38f, 1.5f),
                Kind.GemDrop => (6, new Color32(234, 186, 122, 255), .058f, .36f, 1.6f),
                Kind.GemRelocate => (10, new Color32(163, 211, 255, 255), .067f, .48f, 1.25f),
                _ => (9, new Color32(255, 211, 101, 255), .061f, .45f, 1.65f)
            };
            int amount = Mathf.CeilToInt(count * intensity * strength);
            amount = Mathf.Min(amount, Capacity - particles.particleCount);
            float scale = size * Mathf.Lerp(.55f, 1, intensity) * Mathf.Sqrt(strength);
            for (int i = 0; i < amount; i++)
            {
                Vector3 spread = Vector3.ClampMagnitude((Vector3)random.NextFloat3(-1, 1), 1);
                spread.y = Mathf.Abs(spread.y) + .22f;
                var particle = new ParticleSystem.EmitParams
                {
                    position = point + Vector3.ClampMagnitude((Vector3)random.NextFloat3(-1, 1), 1) * scale,
                    velocity = (spread + direction) * speed,
                    startLifetime = life * random.NextFloat(.75f, 1f),
                    startSize = scale * random.NextFloat(.75f, 1.25f),
                    startColor = color,
                    rotation3D = Vector3.ClampMagnitude((Vector3)random.NextFloat3(-1, 1), 1) * 180f
                };
                particles.Emit(particle, 1);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Trace && amount > 0)
                Debug.Log($"POLISH_FX {kind} intensity={intensity:F2} emitted={amount} active={particles.particleCount} pos={point:F2}");
#endif
        }

        void LateUpdate()
        {
            if (!game || !game.HasLiveAudio || !game.CanPlay)
            {
                particles.Clear();
                return;
            }
            ApplyIntensity();
        }

        void ApplyIntensity()
        {
            float next = MatchTransitionPresentation.EffectIntensity;
            if (next == intensity) return;
            if (next == 0) particles.Clear();
            else if (intensity > 0 && particles.particleCount > 0)
            {
                float ratio = Mathf.Lerp(.55f, 1, next) / Mathf.Lerp(.55f, 1, intensity);
                int count = particles.GetParticles(liveParticles);
                for (int i = 0; i < count; i++) liveParticles[i].startSize *= ratio;
                particles.SetParticles(liveParticles, count);
            }
            intensity = next;
        }

        void OnDestroy()
        {
            if (current == this) current = null;
            if (mesh) Destroy(mesh);
        }
    }
}
