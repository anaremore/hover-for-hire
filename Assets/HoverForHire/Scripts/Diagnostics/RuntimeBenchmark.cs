using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering.Universal;

namespace HoverForHire
{
    /// <summary>
    /// Opt-in performance benchmark (<c>-hover-benchmark &lt;folder&gt;</c>). With the frame rate uncapped it measures
    /// fixed views and a fixed low pass across the island, then writes benchmark.json and benchmark.txt and quits.
    /// Reports average and 1%-low frame rates, CPU and GPU frame times where the platform provides them, and the cost
    /// of building the world at startup. Included in release builds so players can report performance; it never runs
    /// during ordinary play. <c>-hover-bench-without hud,shadows,vegetation,postfx,msaa</c> removes those parts first,
    /// to measure what each one costs.
    /// </summary>
    public sealed class RuntimeBenchmark : MonoBehaviour
    {
        const float SettleSeconds = 1.5f, ViewSeconds = 6f, PassSpeed = 60f, PassHeight = 50f;

        // Low pass: home pad, the town, the ferry harbor, the northern highlands and the west coast (x, z in metres).
        static readonly Vector2[] Route =
        {
            new Vector2(-440, -500), new Vector2(-180, -170), new Vector2(-880, -205), new Vector2(-350, 450), new Vector2(-700, 420),
        };

        static int collectionsBeforeLoad;
        static float startupSeconds, worldBuildSeconds;
        static int worldBuildCollections;

        string output, without = "";
        GameBootstrap game;
        Segment measuring;
        readonly List<float> frameTimes = new List<float>();
        readonly FrameTiming[] timing = new FrameTiming[1];
        readonly Report report = new Report();
        double cpu, mainThread, renderThread, gpu, garbage;
        int timed, gpuTimed, garbageFrames;
        // Managed allocations per frame, and HUD costs by instrument group; development builds only.
        ProfilerRecorder allocated;
        readonly List<ProfilerRecorder> hudRecorders = new List<ProfilerRecorder>();
        double[] hudTotals;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeLoad()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-hover-benchmark") < 0)
                return;
            collectionsBeforeLoad = GC.CollectionCount(0);
            worldBuildSeconds = Time.realtimeSinceStartup;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnableOnRequest()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-hover-benchmark")
                {
                    // The scene's Awake built the world between the two load hooks.
                    startupSeconds = Time.realtimeSinceStartup;
                    worldBuildSeconds = startupSeconds - worldBuildSeconds;
                    worldBuildCollections = GC.CollectionCount(0) - collectionsBeforeLoad;
                    var benchmark = new GameObject("Opt-in runtime benchmark").AddComponent<RuntimeBenchmark>();
                    benchmark.output = args[i + 1];
                    int option = Array.IndexOf(args, "-hover-bench-without");
                    if (option >= 0 && option < args.Length - 1)
                        benchmark.without = args[option + 1].ToLowerInvariant();
                    return;
                }
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            yield return null;
            game = GameBootstrap.Instance;
            if (game == null)
            {
                File.WriteAllText(Path.Combine(output, "benchmark-failed.txt"), "No bootstrap");
                Application.Quit(1);
                yield break;
            }
            // Unattended and uncapped: no focus pause, no welcome page, and no saved first-launch state.
            game.Input.PauseOnFocusLoss = false;
            var hud = game.GetComponent<FlightHUD>();
            hud.DismissFirstRun(false);
            hud.SetPause(false);
            game.Input.enabled = false;
            game.Aircraft.enabled = false;
            game.Aircraft.Body.isKinematic = true;
            game.Aircraft.Body.interpolation = RigidbodyInterpolation.None;
            Remove(hud);
            allocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            foreach (string marker in HudMarkers())
                hudRecorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Scripts, marker));
            hudTotals = new double[hudRecorders.Count];

            Vector3 home = game.Zones[0].transform.position;
            yield return View("Home pad, chase view", home + Vector3.up * 1.55f, 45f, false);
            Vector3 town = new Vector3(-330, 0, -330);
            town.y = Mathf.Max(0f, IslandWorld.MeshHeight(town.x, town.z)) + 60f;
            yield return View("Town from 60 m, chase view", town, 45f, false);
            yield return View("Town from 60 m, cockpit view", town, 45f, true);
            yield return LowPass();

            allocated.Dispose();
            foreach (var recorder in hudRecorders)
                recorder.Dispose();
            report.Without = without;
            report.Version = Application.version;
            report.DevelopmentBuild = Debug.isDebugBuild;
            report.Device = SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ")";
            report.Processor = SystemInfo.processorType;
            report.Resolution = Screen.width + "x" + Screen.height;
            var pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null)
                report.Rendering = $"shadows {pipeline.shadowDistance:0} m x{pipeline.shadowCascadeCount}, MSAA {pipeline.msaaSampleCount}x, scale {pipeline.renderScale:0.##}";
            report.FrameTimingAvailable = FrameTimingManager.IsFeatureEnabled();
            report.StartupSeconds = startupSeconds;
            report.WorldBuildSeconds = worldBuildSeconds;
            report.WorldBuildCollections = worldBuildCollections;
            report.ManagedHeapMB = Profiler.GetMonoHeapSizeLong() / 1048576f;
            report.TotalAllocatedMB = Profiler.GetTotalAllocatedMemoryLong() / 1048576f;
            File.WriteAllText(Path.Combine(output, "benchmark.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(output, "benchmark.txt"), Table());
            Application.Quit(0);
        }

        IEnumerator View(string label, Vector3 position, float heading, bool cockpit)
        {
            Place(position, heading);
            if (game.CameraRig.IsCockpit != cockpit)
                game.CameraRig.ToggleCamera();
            game.CameraRig.SnapToTarget();
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Begin(label);
            yield return new WaitForSecondsRealtime(ViewSeconds);
            End();
        }

        IEnumerator LowPass()
        {
            if (game.CameraRig.IsCockpit)
                game.CameraRig.ToggleCamera();
            float length = 0f;
            for (int i = 1; i < Route.Length; i++)
                length += Vector2.Distance(Route[i - 1], Route[i]);
            Place(PassPoint(0f, out float heading), heading);
            game.CameraRig.SnapToTarget();
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Begin("Island low pass at 60 m/s, chase view");
            float start = Time.realtimeSinceStartup;
            for (float travelled = 0f; travelled < length; travelled = (Time.realtimeSinceStartup - start) * PassSpeed)
            {
                Place(PassPoint(travelled, out heading), heading);
                yield return null;
            }
            End();
        }

        // Position along the route at a smoothed height above the ground ahead, with the heading of the leg.
        static Vector3 PassPoint(float distance, out float heading)
        {
            for (int i = 1; i < Route.Length; i++)
            {
                float leg = Vector2.Distance(Route[i - 1], Route[i]);
                if (distance <= leg || i == Route.Length - 1)
                {
                    Vector2 direction = (Route[i] - Route[i - 1]).normalized;
                    Vector2 flat = Route[i - 1] + direction * Mathf.Min(distance, leg);
                    heading = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
                    float ground = 0f;
                    for (float ahead = 0f; ahead <= 150f; ahead += 50f)
                    {
                        Vector2 sample = flat + direction * ahead;
                        ground = Mathf.Max(ground, IslandWorld.MeshHeight(sample.x, sample.y));
                    }
                    return new Vector3(flat.x, ground + PassHeight, flat.y);
                }
                distance -= leg;
            }
            heading = 0f;
            return Vector3.zero;
        }

        // Ablation: remove named parts to measure what each costs.
        void Remove(FlightHUD hud)
        {
            if (without.Contains("hud"))
                hud.enabled = false;
            var pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (without.Contains("shadows") && pipeline != null)
                pipeline.shadowDistance = 0f;
            if (without.Contains("msaa") && pipeline != null)
                pipeline.msaaSampleCount = 1;
            if (without.Contains("postfx"))
                game.CameraRig.GetComponent<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing = false;
            if (without.Contains("vegetation"))
                foreach (Transform child in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (child.name.StartsWith("Vegetation cell", StringComparison.Ordinal))
                        child.gameObject.SetActive(false);
        }

        static string[] HudMarkers()
        {
            var names = new List<string> { "HUD.Model", "HUD.OnGUI" };
            names.AddRange(FlightInstrumentsView.MarkerNames);
            return names.ToArray();
        }

        void Place(Vector3 position, float heading) =>
            game.Aircraft.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));

        void Begin(string label)
        {
            measuring = new Segment { Label = label, CollectionsAtStart = GC.CollectionCount(0) };
            frameTimes.Clear();
            cpu = mainThread = renderThread = gpu = garbage = 0;
            Array.Clear(hudTotals, 0, hudTotals.Length);
            timed = gpuTimed = garbageFrames = 0;
        }

        void End()
        {
            var segment = measuring;
            measuring = null;
            float total = 0f;
            foreach (float frame in frameTimes)
                total += frame;
            segment.Frames = frameTimes.Count;
            segment.AverageFps = frameTimes.Count / Mathf.Max(1e-4f, total);
            // 1% low: the frame rate over the slowest 1% of frames.
            frameTimes.Sort();
            int worst = Mathf.Max(1, frameTimes.Count / 100);
            float slowest = 0f;
            for (int i = frameTimes.Count - worst; i < frameTimes.Count; i++)
                slowest += frameTimes[i];
            segment.OnePercentLowFps = worst / Mathf.Max(1e-4f, slowest);
            if (timed > 0)
            {
                segment.CpuFrameMs = (float)(cpu / timed);
                segment.MainThreadMs = (float)(mainThread / timed);
                segment.RenderThreadMs = (float)(renderThread / timed);
            }
            segment.GpuFrameMs = gpuTimed > 0 ? (float)(gpu / gpuTimed) : -1f;
            segment.GarbageKBPerFrame = garbageFrames > 0 ? (float)(garbage / garbageFrames / 1024) : -1f;
            var markers = HudMarkers();
            for (int i = 0; i < hudRecorders.Count; i++)
                if (hudRecorders[i].Valid && frameTimes.Count > 0)
                    segment.HudMs.Add(markers[i] + " " + (hudTotals[i] / frameTimes.Count / 1e6).ToString("0.000"));
            segment.Collections = GC.CollectionCount(0) - segment.CollectionsAtStart;
            report.Segments.Add(segment);
        }

        void Update()
        {
            if (measuring == null)
                return;
            frameTimes.Add(Time.unscaledDeltaTime);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
            {
                cpu += timing[0].cpuFrameTime;
                mainThread += timing[0].cpuMainThreadFrameTime;
                renderThread += timing[0].cpuRenderThreadFrameTime;
                timed++;
                // Frames whose GPU timestamps are not ready report nonsense; keep plausible ones only.
                double gpuMs = timing[0].gpuFrameTime;
                if (gpuMs > 0 && gpuMs < 1000)
                {
                    gpu += gpuMs;
                    gpuTimed++;
                }
            }
            if (allocated.Valid && allocated.Count > 0)
            {
                garbage += allocated.LastValue;
                garbageFrames++;
            }
            for (int i = 0; i < hudRecorders.Count; i++)
                if (hudRecorders[i].Valid)
                    hudTotals[i] += hudRecorders[i].LastValue;
        }

        string Table()
        {
            var text = new StringBuilder();
            text.AppendLine($"Hover for Hire {report.Version} {(report.DevelopmentBuild ? "development" : "release")} build, {report.Resolution}"
                + (string.IsNullOrEmpty(report.Without) ? "" : ", without " + report.Without));
            text.AppendLine($"{report.Device}; {report.Processor}");
            text.AppendLine($"Rendering: {report.Rendering}");
            text.AppendLine($"Startup {report.StartupSeconds:0.0} s (world build {report.WorldBuildSeconds:0.0} s, {report.WorldBuildCollections} collections); managed heap {report.ManagedHeapMB:0} MB");
            text.AppendLine();
            text.AppendLine($"{"View",-40} {"avg fps",8} {"1% low",8} {"CPU ms",7} {"main",6} {"render",7} {"GPU ms",7} {"GCs",4} {"KB/frame",9}");
            foreach (var s in report.Segments)
                text.AppendLine($"{s.Label,-40} {s.AverageFps,8:0.0} {s.OnePercentLowFps,8:0.0} {s.CpuFrameMs,7:0.00} {s.MainThreadMs,6:0.00} {s.RenderThreadMs,7:0.00} {s.GpuFrameMs,7:0.00} {s.Collections,4} {s.GarbageKBPerFrame,9:0.0}");
            foreach (var s in report.Segments)
                if (s.HudMs.Count > 0)
                    text.AppendLine($"  HUD ms per frame, {s.Label}: " + string.Join(", ", s.HudMs));
            return text.ToString();
        }

        [Serializable]
        class Segment
        {
            public string Label;
            public int Frames, Collections;
            public float AverageFps, OnePercentLowFps, CpuFrameMs, MainThreadMs, RenderThreadMs, GpuFrameMs, GarbageKBPerFrame;
            public List<string> HudMs = new List<string>();
            [NonSerialized] public int CollectionsAtStart;
        }

        [Serializable]
        class Report
        {
            public string Version, Device, Processor, Resolution, Rendering, Without;
            public bool DevelopmentBuild, FrameTimingAvailable;
            public float StartupSeconds, WorldBuildSeconds, ManagedHeapMB, TotalAllocatedMB;
            public int WorldBuildCollections;
            public List<Segment> Segments = new List<Segment>();
        }
    }
}
