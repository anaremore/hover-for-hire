using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HoverForHire.Tests
{
    /// <summary>Graphics presets change a runtime copy of the pipeline asset and the camera, never the project asset.</summary>
    public sealed class GraphicsQualityTests
    {
        private RenderPipelineAsset before;
        private Camera camera;

        [SetUp]
        public void SetUp()
        {
            before = QualitySettings.renderPipeline;
            camera = new GameObject("Graphics test camera").AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            GraphicsQuality.Release();
            Object.Destroy(camera.gameObject);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        [Test]
        public void PresetsChangeARuntimeCopyAndLeaveTheProjectAssetAlone()
        {
            var project = (before != null ? before : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            Assume.That(project, Is.Not.Null, "The project renders with URP.");
            float projectShadows = project.shadowDistance;
            int projectMsaa = project.msaaSampleCount;

            GraphicsQuality.Apply(new GraphicsChoices { Preset = GraphicsPreset.Low, VSync = false, FrameCap = 60 }, camera);
            var runtime = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            Assert.That(runtime, Is.Not.Null.And.Not.SameAs(project));
            Assert.That(runtime.shadowDistance, Is.EqualTo(150f));
            Assert.That(runtime.shadowCascadeCount, Is.EqualTo(2));
            Assert.That(runtime.msaaSampleCount, Is.EqualTo(1));
            Assert.That(runtime.renderScale, Is.EqualTo(.8f).Within(1e-4f));
            Assert.That(camera.layerCullDistances[WorldConstants.VegetationLayer], Is.EqualTo(900f), "Low stops drawing distant trees.");
            Assert.That(QualitySettings.vSyncCount, Is.Zero);
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));

            GraphicsQuality.Apply(new GraphicsChoices { Preset = GraphicsPreset.Ultra }, camera);
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(runtime), "One runtime copy, reused.");
            Assert.That(runtime.shadowDistance, Is.EqualTo(550f));
            Assert.That(runtime.msaaSampleCount, Is.EqualTo(8));
            Assert.That(camera.layerCullDistances[WorldConstants.VegetationLayer], Is.Zero, "Ultra draws every tree.");
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
            Assert.That(Application.targetFrameRate, Is.EqualTo(-1), "VSync paces the frames; no cap.");

            Assert.That(project.shadowDistance, Is.EqualTo(projectShadows), "The project asset is untouched.");
            Assert.That(project.msaaSampleCount, Is.EqualTo(projectMsaa));
            GraphicsQuality.Release();
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(before), "Release puts the project's pipeline back.");
        }
    }
}
