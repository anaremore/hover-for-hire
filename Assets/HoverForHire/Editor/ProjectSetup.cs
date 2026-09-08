using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HoverForHire.Editor
{
    public static class ProjectSetup
    {
        public const string ScenePath="Assets/HoverForHire/Scenes/PortMeridian.unity";
        [MenuItem("Hover for Hire/Prepare project")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/HoverForHire/Scenes");Directory.CreateDirectory("Assets/HoverForHire/Resources");Directory.CreateDirectory("Assets/HoverForHire/Settings");
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/HoverForHire/Settings/IslandURP.asset");
            if(pipeline==null)
            {
                var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,"Assets/HoverForHire/Settings/IslandRenderer.asset");
                pipeline=UniversalRenderPipelineAsset.Create(renderer);pipeline.name="Island URP";pipeline.msaaSampleCount=4;pipeline.shadowDistance=220;pipeline.renderScale=1;AssetDatabase.CreateAsset(pipeline,"Assets/HoverForHire/Settings/IslandURP.asset");
            }
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=0;QualitySettings.shadows=UnityEngine.ShadowQuality.All;
            PlayerSettings.companyName="Hover for Hire";PlayerSettings.productName="Hover for Hire";PlayerSettings.bundleVersion="0.1.0";PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;PlayerSettings.runInBackground=true;
            var ps=new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));ps.FindProperty("activeInputHandler").intValue=2;ps.ApplyModifiedPropertiesWithoutUndo();
            if(AssetDatabase.LoadAssetAtPath<FlightTuning>("Assets/HoverForHire/Resources/UtilityHelicopter.asset")==null) AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<FlightTuning>(),"Assets/HoverForHire/Resources/UtilityHelicopter.asset");
            // A referenced material keeps the runtime palette shader in builds without every Lit variant.
            if(AssetDatabase.LoadAssetAtPath<Material>("Assets/HoverForHire/Resources/Palette.asset")==null) AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Lit")),"Assets/HoverForHire/Resources/Palette.asset");
            if(!File.Exists(ScenePath)) { var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Hover for Hire").AddComponent<GameBootstrap>();EditorSceneManager.SaveScene(scene,ScenePath); }
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};AssetDatabase.SaveAssets();Debug.Log("HOVER_SETUP_OK");
        }
        [MenuItem("Hover for Hire/Build/Windows")]
        public static void BuildWindows()=>Build(BuildTarget.StandaloneWindows64,"Builds/Windows/Hover for Hire.exe");
        [MenuItem("Hover for Hire/Build/macOS")]
        public static void BuildMac()=>Build(BuildTarget.StandaloneOSX,"Builds/macOS/Hover for Hire.app");
        [MenuItem("Hover for Hire/Build/Linux")]
        public static void BuildLinux()=>Build(BuildTarget.StandaloneLinux64,"Builds/Linux/Hover for Hire.x86_64");
        static void Build(BuildTarget target,string path)
        {
            Prepare();if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,target))throw new InvalidOperationException("Install the Unity platform build support module for "+target);
            Directory.CreateDirectory(Path.GetDirectoryName(path));var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{ScenePath},locationPathName=path,target=target,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);Debug.Log("HOVER_BUILD_OK "+target+" "+report.summary.totalSize);
        }
    }
}
