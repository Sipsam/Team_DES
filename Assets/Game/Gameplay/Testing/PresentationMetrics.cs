#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using TwinEdge.Presentation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace TwinEdge.Gameplay
{
    internal static class PresentationMetrics
    {
        static readonly bool enabled = Environment.GetCommandLineArgs().Contains("-v3PresentationMetrics");

        internal static IEnumerator Sample(string folder, string role, string segment)
        {
            if (!enabled) yield break;
            const int frames = 120;
            using var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            using var layout = ProfilerRecorder.StartNew(ProfilerCategory.Gui, "UI.Layout");
            using var canvas = ProfilerRecorder.StartNew(ProfilerCategory.Gui, "Canvas.BuildBatch");
            using var audio = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Audio Used Memory");
            double start = Time.realtimeSinceStartupAsDouble, gpu = 0;
            int gpuSamples = 0;
            var timings = new FrameTiming[1];
            var recorders = new[] { main, gc, draws, batches, layout, canvas, audio };
            var totals = new long[recorders.Length];
            for (int i = 0; i < frames; i++)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                {
                    gpu += timings[0].gpuFrameTime;
                    gpuSamples++;
                }
                for (int counter = 0; counter < totals.Length; counter++)
                    if (recorders[counter].Valid) totals[counter] += recorders[counter].LastValue;
            }
            double elapsed = Time.realtimeSinceStartupAsDouble - start;
            string Mean(int index, double scale = 1) =>
                recorders[index].Valid && recorders[index].Count > 0
                    ? FormattableString.Invariant($"{totals[index] / (frames * scale):F2}")
                    : "NOT_RUN";
            string gpuMean = gpuSamples > 0 ? FormattableString.Invariant($"{gpu / gpuSamples:F2}") : "NOT_RUN";
            File.AppendAllText(Path.Combine(folder, role + "-presentation-metrics.txt"), FormattableString.Invariant(
                $"{segment} Unity={Application.unityVersion} os={SystemInfo.operatingSystem} gpu={SystemInfo.graphicsDeviceType} window={Screen.width}x{Screen.height} fpsLimit={Application.targetFrameRate} vsync={QualitySettings.vSyncCount} effect={LocalPresentationSettings.EffectPercent} minimapSetting={CourseMapPresentation.MinimapVisible} samples={frames} seconds={elapsed:F2} frameIntervalMs={elapsed * 1000 / frames:F2} mainThreadMs={Mean(0, 1e6)} gpuMs={gpuMean} gpuSamples={gpuSamples} gcBytesPerFrame={Mean(1)} draws={Mean(2)} batches={Mean(3)} layoutMs={Mean(4, 1e6)} canvasBatchMs={Mean(5, 1e6)} audioBytes={Mean(6)}") + "\n");
            Snapshot(folder, role, segment);
        }

        internal static void Snapshot(string folder, string role, string segment)
        {
            if (!enabled) return;
            var game = V3GameplayBootstrap.Current;
            var roots = UnityEngine.Object.FindObjectsByType<PresentationRoot>(FindObjectsSortMode.None);
            var particles = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            var textures = Resources.FindObjectsOfTypeAll<RenderTexture>()
                .Where(rt => rt.IsCreated() && (rt.name.StartsWith("Lobby Portrait") || rt.name == "Result Portrait")).ToArray();
            var atlas = Resources.Load<CourseMapData>("V3CourseMap");
            var map = atlas ? atlas.Sheets.Select(sheet => sheet.Texture).Append(atlas.Overview.Texture)
                .Where(texture => texture).Distinct().ToArray() : Array.Empty<Texture2D>();
            int Count<T>() where T : Component => UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;
            int Enabled<T>() where T : Behaviour => UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None)
                .Count(item => item.isActiveAndEnabled);
            File.AppendAllText(Path.Combine(folder, role + "-presentation-resources.txt"), FormattableString.Invariant(
                $"{segment} roots={roots.Length} activeCanvases={Enabled<Canvas>()} eventSystems={Count<EventSystem>()} cameras={Enabled<Camera>()} listeners={Enabled<AudioListener>()} audioSources={Count<AudioSource>()} playingSources={UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(source => source.isPlaying)} portraitTextures={textures.Length} portraitBytes={textures.Sum(rt => Profiler.GetRuntimeMemorySizeLong(rt))} mapTextures={map.Length} mapBytes={map.Sum(tex => Profiler.GetRuntimeMemorySizeLong(tex))} particleSystems={particles.Length} activeParticles={particles.Sum(p => p.particleCount)} loadedMaterials={Resources.FindObjectsOfTypeAll<Material>().Length} networkObjects={game.manager.SpawnManager?.SpawnedObjectsList.Count ?? 0}") + "\n");
        }
    }
}
#endif
