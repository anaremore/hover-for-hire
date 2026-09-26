using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HoverForHire
{
    public enum GraphicsPreset { Low, Medium, High, Ultra }

    /// <summary>
    /// Graphics choices saved with the pilot's settings. Resolution and window mode are not here: Unity keeps the
    /// player's last display mode itself, and command-line display arguments keep working.
    /// </summary>
    [Serializable]
    public sealed class GraphicsChoices
    {
        public GraphicsPreset Preset = GraphicsPreset.High;
        [Tooltip("Match the display's refresh rate. It overrides the frame-rate cap.")]
        public bool VSync = true;
        [Tooltip("Frame-rate cap when VSync is off; 0 is uncapped.")]
        public int FrameCap;

        public void Sanitize()
        {
            if (!Enum.IsDefined(typeof(GraphicsPreset), Preset)) Preset = GraphicsPreset.High;
            FrameCap = FrameCap <= 0 ? 0 : Mathf.Clamp(FrameCap, 15, 1000);
        }
    }

    /// <summary>
    /// Applies a graphics preset: shadows, antialiasing, render scale and how far away trees are drawn, plus VSync
    /// and the frame-rate cap. Presets change a runtime copy of the render-pipeline asset, never the project's asset
    /// (which play mode in the editor would save). <c>-hover-graphics Low|Medium|High|Ultra</c> overrides the saved
    /// preset for one run.
    /// </summary>
    public static class GraphicsQuality
    {
        public static readonly string[] PresetNames = { "Low", "Medium", "High", "Ultra" };
        public static readonly int[] FrameCaps = { 0, 30, 60, 90, 120, 144, 165, 240 };
        public static readonly string[] FrameCapNames = { "Uncapped", "30", "60", "90", "120", "144", "165", "240" };

        private struct Level
        {
            public float ShadowDistance, RenderScale, TreeDistance;
            public int Cascades, ShadowResolution, Msaa;
            public string Description;
        }

        // Tree distance 0 draws every tree to the far clip plane. The island is 2.4 km across.
        private static readonly Level[] Levels =
        {
            new Level { ShadowDistance = 150, Cascades = 2, ShadowResolution = 1024, Msaa = 1, RenderScale = .8f, TreeDistance = 900,
                Description = "Shadows to 150 m, no antialiasing, 80% render resolution, trees to 900 m" },
            new Level { ShadowDistance = 250, Cascades = 2, ShadowResolution = 2048, Msaa = 2, RenderScale = 1f, TreeDistance = 1400,
                Description = "Shadows to 250 m, 2x antialiasing, full resolution, trees to 1400 m" },
            new Level { ShadowDistance = 400, Cascades = 4, ShadowResolution = 4096, Msaa = 4, RenderScale = 1f, TreeDistance = 0,
                Description = "Shadows to 400 m, 4x antialiasing, full resolution, every tree" },
            new Level { ShadowDistance = 550, Cascades = 4, ShadowResolution = 4096, Msaa = 8, RenderScale = 1f, TreeDistance = 0,
                Description = "Shadows to 550 m, 8x antialiasing, full resolution, every tree" },
        };

        private static RenderPipelineAsset projectPipeline;
        private static UniversalRenderPipelineAsset runtimePipeline;

        public static string Describe(GraphicsPreset preset) => Levels[(int)preset].Description;

        /// <summary>The preset in effect: a command-line override, else the saved one.</summary>
        public static GraphicsPreset Effective(GraphicsChoices choices)
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-hover-graphics");
            if (option >= 0 && option < args.Length - 1 && Enum.TryParse(args[option + 1], true, out GraphicsPreset forced)
                && Enum.IsDefined(typeof(GraphicsPreset), forced))
                return forced;
            return choices.Preset;
        }

        public static void Apply(GraphicsChoices choices, Camera camera)
        {
            Level level = Levels[(int)Effective(choices)];
            UniversalRenderPipelineAsset pipeline = RuntimePipeline();
            if (pipeline != null)
            {
                pipeline.shadowDistance = level.ShadowDistance;
                pipeline.shadowCascadeCount = level.Cascades;
                pipeline.cascade2Split = .25f;
                pipeline.cascade4Split = new Vector3(.06f, .18f, .45f);
                pipeline.mainLightShadowmapResolution = level.ShadowResolution;
                pipeline.msaaSampleCount = level.Msaa;
                pipeline.renderScale = level.RenderScale;
            }
            if (camera != null)
            {
                // Vegetation renderers share the vegetation layer with their collision; cull whole cells past the distance.
                var distances = new float[32];
                distances[WorldConstants.VegetationLayer] = level.TreeDistance;
                camera.layerCullDistances = distances;
                camera.layerCullSpherical = true;
            }
            QualitySettings.vSyncCount = choices.VSync ? 1 : 0;
            Application.targetFrameRate = choices.VSync || choices.FrameCap <= 0 ? -1 : choices.FrameCap;
        }

        /// <summary>Put the project's pipeline asset back and drop the runtime copy (on leaving play mode, and in tests).</summary>
        public static void Release()
        {
            if (runtimePipeline == null) return;
            QualitySettings.renderPipeline = projectPipeline;
            Object.Destroy(runtimePipeline);
            runtimePipeline = null;
            projectPipeline = null;
        }

        private static UniversalRenderPipelineAsset RuntimePipeline()
        {
            if (runtimePipeline != null) return runtimePipeline;
            projectPipeline = QualitySettings.renderPipeline;
            var source = (projectPipeline != null ? projectPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (source == null) return null;
            runtimePipeline = Object.Instantiate(source);
            runtimePipeline.name = source.name + " (runtime)";
            QualitySettings.renderPipeline = runtimePipeline;
            return runtimePipeline;
        }
    }
}
