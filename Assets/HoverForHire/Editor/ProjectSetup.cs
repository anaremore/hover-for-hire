using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace HoverForHire.Editor
{
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/HoverForHire/Scenes/PortMeridian.unity";
        /// <summary>The game's version, set only here. Builds, archive names and the Flight Desk read it.</summary>
        public const string Version = "0.3.0";
        [MenuItem("Hover for Hire/Prepare project")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/HoverForHire/Scenes");
            Directory.CreateDirectory("Assets/HoverForHire/Resources");
            Directory.CreateDirectory("Assets/HoverForHire/Settings");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/HoverForHire/Settings/IslandURP.asset");
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, "Assets/HoverForHire/Settings/IslandRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "Island URP";
                pipeline.msaaSampleCount = 4;
                pipeline.shadowDistance = 220;
                pipeline.renderScale = 1;
                AssetDatabase.CreateAsset(pipeline, "Assets/HoverForHire/Settings/IslandURP.asset");
            }
            ConfigureGraphics(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            PlayerSettings.companyName = "Hover for Hire";
            PlayerSettings.productName = "Hover for Hire";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;
            // CPU and GPU frame times for the opt-in benchmark (FrameTimingManager), also in release builds.
            PlayerSettings.enableFrameTimingStats = true;
            var ps = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            ps.FindProperty("activeInputHandler").intValue = 2;
            ps.ApplyModifiedPropertiesWithoutUndo();
            if (AssetDatabase.LoadAssetAtPath<FlightTuning>("Assets/HoverForHire/Resources/UtilityHelicopter.asset") == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<FlightTuning>(), "Assets/HoverForHire/Resources/UtilityHelicopter.asset");
            // A referenced material keeps the runtime palette shader in builds without every Lit variant.
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/HoverForHire/Resources/Palette.asset") == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Lit")), "Assets/HoverForHire/Resources/Palette.asset");
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Hover for Hire").AddComponent<GameBootstrap>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("HOVER_SETUP_OK");
        }
        static void ConfigureGraphics(UniversalRenderPipelineAsset pipeline)
        {
            // Fog is enabled by the runtime world, so scene-based stripping must retain its variant.
            var graphics = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("GraphicsSettings"));
            graphics.FindProperty("m_FogStripping").intValue = 1;
            graphics.FindProperty("m_FogKeepExp2").boolValue = true;
            graphics.ApplyModifiedPropertiesWithoutUndo();
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            GraphicsSettings.lightsUseLinearIntensity = true;
            pipeline.shadowDistance = 550;
            pipeline.shadowCascadeCount = 4;
            pipeline.cascade4Split = new Vector3(.06f, .18f, .45f);
            pipeline.mainLightShadowmapResolution = 4096;
            pipeline.shadowDepthBias = .35f;
            pipeline.shadowNormalBias = .35f;
            pipeline.supportsCameraDepthTexture = true;
            var pipelineSettings = new SerializedObject(pipeline);
            foreach (string property in new[] { "m_SoftShadowsSupported", "m_ReflectionProbeBlending", "m_ReflectionProbeBoxProjection" })
                pipelineSettings.FindProperty(property).boolValue = true;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            pipeline.colorGradingMode = ColorGradingMode.HighDynamicRange;
            EditorUtility.SetDirty(pipeline);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/HoverForHire/Settings/IslandRenderer.asset");
            var serialized = new SerializedObject(renderer);
            serialized.FindProperty("postProcessData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            Directory.CreateDirectory("Assets/HoverForHire/Resources/Art/Materials");
            const string profilePath = "Assets/HoverForHire/Resources/Art/CoastalGrade.asset";
            var grade = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (grade == null)
            {
                grade = ScriptableObject.CreateInstance<VolumeProfile>();
                IslandAtmosphere.ConfigureGrade(grade);
                AssetDatabase.CreateAsset(grade, profilePath);
                foreach (var component in grade.components)
                    AssetDatabase.AddObjectToAsset(component, grade);
            }
            foreach (var id in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/HoverForHire/Resources/Art/Textures" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(id);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.Contains("_nor_gl");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !normal;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 8;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            var terrain = ArtMaterial("Terrain", "Hover for Hire/Coastal Terrain");
            string[] slots = { "Grass", "Rock", "Sand" };
            string[] sources = { "aerial_grass_rock", "rocky_terrain_02", "aerial_beach_01" };
            for (int i = 0; i < 3; i++)
            {
                terrain.SetTexture("_" + slots[i], Resources.Load<Texture2D>("Art/Textures/" + sources[i] + "_diff_1k"));
                terrain.SetTexture("_" + slots[i] + "Normal", Resources.Load<Texture2D>("Art/Textures/" + sources[i] + "_nor_gl_1k"));
            }
            EditorUtility.SetDirty(terrain);
            ArtMaterial("Ocean", "Hover for Hire/Coastal Ocean");
            ArtMaterial("Sky", "Hover for Hire/Island Sky");
            ArtMaterial("WorldLettering", "Hover for Hire/World Lettering");
            // Runtime palettes need these keyword combinations retained by player shader stripping.
            foreach (string shaderName in new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Particles/Unlit" })
            {
                string prefix = shaderName.Contains("/Particles/") ? "Particles" : shaderName.EndsWith("/Lit") ? "Lit" : "Unlit";
                var translucent = ArtMaterial(prefix + "Transparent", shaderName);
                translucent.SetFloat("_Surface", 1);
                translucent.SetFloat("_Blend", 0);
                translucent.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                translucent.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                translucent.SetFloat("_ZWrite", 0);
                translucent.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                translucent.SetOverrideTag("RenderType", "Transparent");
                translucent.renderQueue = 3000;
                EditorUtility.SetDirty(translucent);
            }
            var emissive = ArtMaterial("LitEmissive", "Universal Render Pipeline/Lit");
            emissive.EnableKeyword("_EMISSION");
            emissive.SetColor("_EmissionColor", Color.white);
            EditorUtility.SetDirty(emissive);
            var normalMapped = ArtMaterial("LitNormalMapped", "Universal Render Pipeline/Lit");
            normalMapped.EnableKeyword("_NORMALMAP");
            normalMapped.SetTexture("_BumpMap", Resources.Load<Texture2D>("Art/Textures/asphalt_02_nor_gl_1k"));
            EditorUtility.SetDirty(normalMapped);
        }
        static Material ArtMaterial(string name, string shaderName)
        {
            string path = "Assets/HoverForHire/Resources/Art/Materials/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException("Missing shader: " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            else
                material.shader = shader;
            return material;
        }
        [MenuItem("Hover for Hire/Build/Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Hover for Hire.exe");
        [MenuItem("Hover for Hire/Build/Windows (development)")]
        public static void BuildWindowsDevelopment() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows-Development/Hover for Hire.exe", true);
        [MenuItem("Hover for Hire/Build/macOS")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/Hover for Hire.app");
        [MenuItem("Hover for Hire/Build/Linux")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/Hover for Hire.x86_64");
        // Release builds by default. The development variant adds the runtime smoke flight, its autopilot and audio
        // capture, and profiler support; Tools/Smoke.ps1 runs it.
        static void Build(BuildTarget target, string path, bool development = false)
        {
            Prepare();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new InvalidOperationException("Install the Unity platform build support module for " + target);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var options = development ? BuildOptions.Development : BuildOptions.None;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = path, target = target, options = options });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Build failed: " + report.summary.result);
            WriteBuildInfo(Path.GetDirectoryName(path), target, development);
            Debug.Log("HOVER_BUILD_OK " + target + (development ? " development " : " release ") + report.summary.totalSize);
        }

        // Records what was built, so Tools/package_builds.py can refuse development builds and builds of other commits.
        static void WriteBuildInfo(string folder, BuildTarget target, bool development)
        {
            var info = new BuildInfo
            {
                Version = Version,
                Target = target.ToString(),
                Development = development,
                BuiltUtc = DateTime.UtcNow.ToString("o"),
                Commit = Git("rev-parse HEAD"),
                UncommittedChanges = Git("status --porcelain").Length > 0,
            };
            File.WriteAllText(Path.Combine(folder, "build-info.json"), JsonUtility.ToJson(info, true));
        }

        static string Git(string arguments)
        {
            try
            {
                var start = new ProcessStartInfo("git", arguments) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using (var process = Process.Start(start))
                {
                    string text = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    return process.ExitCode == 0 ? text : "";
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        [Serializable]
        class BuildInfo
        {
            public string Version, Target, BuiltUtc, Commit;
            public bool Development, UncommittedChanges;
        }
    }
}
