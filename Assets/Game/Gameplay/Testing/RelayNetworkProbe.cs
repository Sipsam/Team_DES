#if DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    // Opt-in process-wide probe. Transport totals restart with each new driver.
    internal sealed class RelayNetworkProbe : MonoBehaviour
    {
        internal static RelayNetworkProbe Current { get; private set; }
        internal string Segment { private get; set; } = "transition";
        internal bool Automatic { private get; set; } = true;

        static readonly string[] MessageTypes =
        {
            "RpcMessage", "NetworkVariableDeltaMessage", "NetworkTransformMessage", "TimeSyncMessage",
            "ConnectionRequestMessage", "ConnectionApprovedMessage", "SceneEventMessage",
            "CreateObjectMessage", "DestroyObjectMessage", "NamedMessage", "DisconnectReasonMessage"
        };

        readonly List<ProfilerRecorder> recorders = new();
        readonly List<int> recordedSamples = new();
        readonly List<string> markerNames = new();
        readonly List<ProfilerRecorderHandle> availableHandles = new();
        readonly FrameTiming[] timings = new FrameTiming[1];
        readonly System.Diagnostics.Stopwatch cost = new();
        Func<ulong, NetworkConnection> parseConnection;
        NetworkManager manager;
        StreamWriter samples, frames, events;
        double nextSample;
        long previousCost;
        int generation;
        bool wasCreated, configured;
        char profile;
        NetworkSimulatorParameter original;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-relayNetworkProbe");
            if (at < 0) return;
            var probe = new GameObject(nameof(RelayNetworkProbe)).AddComponent<RelayNetworkProbe>();
            DontDestroyOnLoad(probe.gameObject);
            Current = probe;
            probe.Initialize(args[at + 1], args[at + 2], args[at + 3][0]);
        }

        void Initialize(string folder, string role, char selectedProfile)
        {
            profile = selectedProfile;
            samples = new StreamWriter(Path.Combine(folder, role + "-network.csv"));
            frames = new StreamWriter(Path.Combine(folder, role + "-network-frames.csv"));
            events = new StreamWriter(Path.Combine(folder, role + "-network-events.txt")) { AutoFlush = true };
            samples.WriteLine("time,segment,generation,objects,connected,rttMs,rawRttMs,utpSmoothedMs,txBytes,rxBytes,txPackets,rxPackets,reliableSent,reliableReceived,reliableDropped,reliableResent,delayMs,jitterMs,sendLossPercent,receiveLossPercent,probeMs");
            var parse = typeof(UnityTransport).GetMethod("ParseClientId", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(ulong) }, null);
            parseConnection = parse?.CreateDelegate(typeof(Func<ulong, NetworkConnection>)) as Func<ulong, NetworkConnection>;

            foreach (string type in MessageTypes)
            {
                AddMarker("NetworkMessageManager.SerializeAndEnqueue." + type);
                AddMarker("NetworkMessageManager.DeserializeAndHandle." + type);
            }
            AddMarker("NetworkMessageManager.SendBatch");
            AddMarker("NetworkMessageManager.ReceiveBatchBatch");
            AddMarker("GC Allocated In Frame");
            frames.WriteLine("time,segment,frameMs,gpuMs," +
                string.Join(",", markerNames.Select(name => name + ".count," + name + ".value")));
        }

        void AddMarker(string name)
        {
            markerNames.Add(name);
            recorders.Add(default);
            recordedSamples.Add(0);
        }

        void BindMarkers()
        {
            ProfilerRecorderHandle.GetAvailable(availableHandles);
            foreach (var handle in availableHandles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                int index = markerNames.IndexOf(description.Name);
                if (index < 0 || recorders[index].Valid) continue;
                // A runner stops each Player before 540 seconds at a 60 FPS cap.
                // Retain frame samples and consume each index once, including gaps between events.
                var recorder = new ProfilerRecorder(handle, 32768, ProfilerRecorderOptions.SumAllSamplesInFrame);
                recorder.Start();
                recorders[index] = recorder;
                Note($"recorder {description.Name} category={description.Category.Name} unit={description.UnitType}");
            }
        }

        internal void Apply()
        {
            if (configured) return;
            var transport = (UnityTransport)manager.NetworkConfig.NetworkTransport;
            var driver = transport.GetNetworkDriver();
            if (!driver.CurrentSettings.TryGet(out original))
                throw new InvalidOperationException("Development UTP simulator layer is missing.");

            var parameters = original;
            (parameters.SendDelayMS, parameters.SendJitterMS, parameters.SendPacketLossPercent) = profile switch
            {
                'B' => (50u, 10u, 0f),
                'C' => (100u, 30u, 1f),
                'D' => (150u, 50u, 3f),
                _ => (0u, 0u, 0f)
            };
            if (profile != 'A') driver.ModifyNetworkSimulatorParameters(parameters);
            VerifySettings(driver, parameters, "applied " + profile);
            configured = true;
        }

        internal void Restore()
        {
            var driver = ((UnityTransport)manager.NetworkConfig.NetworkTransport).GetNetworkDriver();
            if (profile != 'A') driver.ModifyNetworkSimulatorParameters(original);
            VerifySettings(driver, original, "restored");
            configured = false;
        }

        internal void Note(string message) =>
            events.WriteLine(FormattableString.Invariant($"{Time.realtimeSinceStartupAsDouble:F3} {message}"));

        internal void NoteClose(string message) =>
            Note(FormattableString.Invariant($"close utc={DateTime.UtcNow:O} generation={generation} {message}"));

        void VerifySettings(NetworkDriver driver, NetworkSimulatorParameter expected, string label)
        {
            if (!driver.CurrentSettings.TryGet<NetworkSimulatorParameter>(out var actual) ||
                actual.SendDelayMS != expected.SendDelayMS || actual.SendJitterMS != expected.SendJitterMS ||
                actual.SendPacketLossPercent != expected.SendPacketLossPercent ||
                actual.ReceivePacketLossPercent != expected.ReceivePacketLossPercent)
                throw new InvalidOperationException("UTP simulator settings did not round trip.");
            Note(FormattableString.Invariant($"{label} generation={generation} txDelayMs={actual.SendDelayMS} txJitterMs={actual.SendJitterMS} txLossPct={actual.SendPacketLossPercent} rxLossPct={actual.ReceivePacketLossPercent}"));
        }

        void Update()
        {
            cost.Start();
            Observe();
            cost.Stop();
        }

        void Observe()
        {
            manager = manager ? manager : NetworkManager.Singleton;
            if (!manager || manager.NetworkConfig.NetworkTransport is not UnityTransport transport) return;
            var driver = transport.GetNetworkDriver();
            if (!driver.IsCreated)
            {
                if (wasCreated) Note("driver closed");
                wasCreated = configured = false;
                return;
            }

            if (!wasCreated)
            {
                wasCreated = true;
                generation++;
                bool relay = driver.CurrentSettings.TryGet<RelayNetworkParameter>(out var relaySettings);
                Note(FormattableString.Invariant($"driver opened generation={generation} relay={relay} secure={relay && relaySettings.ServerData.IsSecure == 1} tickHz={manager.NetworkConfig.TickRate} fixedStep={Time.fixedDeltaTime} Unity={Application.unityVersion} graphics={SystemInfo.graphicsDeviceType} window={Screen.width}x{Screen.height} fpsLimit={Application.targetFrameRate} vsync={QualitySettings.vSyncCount}"));
            }
            if (Automatic && !configured && manager.IsConnectedClient) Apply();

            if (Segment != "transition")
                SampleFrame();

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextSample) return;
            nextSample = now + .25;
            var peer = NetworkManager.ServerClientId;
            if (manager.IsHost)
            {
                peer = ulong.MaxValue;
                foreach (ulong id in manager.ConnectedClientsIds)
                    if (id != manager.LocalClientId) { peer = id; break; }
            }
            var linkId = manager.GetTransportIdFromClientId(peer);
            var link = linkId != ulong.MaxValue && parseConnection != null ? parseConnection(linkId) : default;
            bool connected = link.IsCreated && driver.GetConnectionState(link) == NetworkConnection.State.Connected;
            var linkStats = connected ? driver.GetConnectionStatistics(link) : default;
            var stats = driver.GetStatistics();
            driver.CurrentSettings.TryGet<NetworkSimulatorParameter>(out var settings);
            double probeMs = (cost.ElapsedTicks - previousCost) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            previousCost = cost.ElapsedTicks;
            samples.WriteLine(FormattableString.Invariant($"{now:F3},{Segment},{generation},{manager.SpawnManager?.SpawnedObjects.Count ?? 0},{(connected ? 1 : 0)},{(connected ? transport.GetCurrentRtt(peer) : 0)},{linkStats.Latency.Current},{linkStats.Latency.SmoothedCurrent:F3},{stats.TxTotalBytes},{stats.RxTotalBytes},{stats.TxTotalPackets},{stats.RxTotalPackets},{linkStats.Reliable.PacketsSent},{linkStats.Reliable.PacketsReceived},{linkStats.Reliable.PacketsDropped},{linkStats.Reliable.PacketsResent},{settings.SendDelayMS},{settings.SendJitterMS},{settings.SendPacketLossPercent},{settings.ReceivePacketLossPercent},{probeMs:F4}"));
            samples.Flush();
            frames.Flush();
        }

        void SampleFrame()
        {
            // ProfilingHooks registers each message marker the first time the message occurs.
            if (Time.frameCount % 60 == 0) BindMarkers();
            FrameTimingManager.CaptureFrameTimings();
            double gpu = FrameTimingManager.GetLatestTimings(1, timings) != 0 ? timings[0].gpuFrameTime : 0;
            frames.Write(FormattableString.Invariant($"{Time.realtimeSinceStartupAsDouble:F3},{Segment},{Time.unscaledDeltaTime * 1000:F3},{gpu:F3}"));
            for (int index = 0; index < recorders.Count; index++)
            {
                var recorder = recorders[index];
                if (!recorder.Valid)
                {
                    frames.Write(",,");
                    continue;
                }
                int count = recorder.Count;
                long calls = 0, value = 0;
                for (int sampleIndex = recordedSamples[index]; sampleIndex < count; sampleIndex++)
                {
                    var sample = recorder.GetSample(sampleIndex);
                    calls += sample.Count;
                    value += sample.Value;
                }
                recordedSamples[index] = count;
                frames.Write(',');
                frames.Write(calls);
                frames.Write(',');
                frames.Write(value);
            }
            frames.WriteLine();
        }

        void OnDestroy()
        {
            foreach (var recorder in recorders)
                if (recorder.Valid) recorder.Dispose();
            frames?.Dispose();
            samples?.Dispose();
            events?.Dispose();
        }
    }
}
#endif
