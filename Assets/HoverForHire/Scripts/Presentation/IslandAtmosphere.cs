using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HoverForHire
{
    /// <summary>Lighting and surface presentation, independent of flight and map collision.</summary>
    public static class IslandAtmosphere
    {
        public static void ConfigureWorld(Light sun)
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            // No baked GI in the generated island: broad sky fill keeps shaded metal and foliage readable.
            RenderSettings.ambientSkyColor=new Color(.80f,.87f,1f);
            RenderSettings.ambientEquatorColor=new Color(.65f,.70f,.74f);
            RenderSettings.ambientGroundColor=new Color(.35f,.39f,.42f);
            RenderSettings.fog=true; RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogDensity=.00038f; RenderSettings.fogColor=new Color(.60f,.68f,.69f);
            sun.intensity=1.7f; sun.color=new Color(1,.96f,.87f);
            sun.transform.rotation=Quaternion.Euler(26,-38,0); sun.shadows=LightShadows.Soft;
            sun.shadowBias=.035f; sun.shadowNormalBias=.25f; sun.shadowStrength=.78f;
            RenderSettings.sun=sun;
            var sky=Resources.Load<Material>("Art/Materials/Sky");
            if(sky!=null) { RenderSettings.skybox=new Material(sky); RenderSettings.skybox.SetVector("_SunDirection",-sun.transform.forward); }
            ApplySurface(IslandWorld.Ground,"Art/Materials/Terrain");
            ApplySurface(IslandWorld.Water,"Art/Materials/Ocean");
            if(IslandWorld.Water.HasProperty("_CoastHeight"))
            {
                const int size=256; var tex=new Texture2D(size,size,TextureFormat.RFloat,false,true) {name="Island shoreline depth",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++) for(int x=0;x<size;x++) pixels[y*size+x]=new Color(Mathf.Clamp01((IslandWorld.Height((x/(float)(size-1)-.5f)*2400,(y/(float)(size-1)-.5f)*2200)+40)/240),0,0,1);
                tex.SetPixels(pixels); tex.Apply(false,true); IslandWorld.Water.SetTexture("_CoastHeight",tex);
            }
            var asphalt=Resources.Load<Texture2D>("Art/Textures/asphalt_02_diff_1k");
            if(asphalt!=null)
            {
                IslandWorld.Asphalt.SetTexture("_BaseMap",asphalt);
                IslandWorld.Asphalt.SetTexture("_BumpMap",Resources.Load<Texture2D>("Art/Textures/asphalt_02_nor_gl_1k"));
                IslandWorld.Asphalt.SetFloat("_BumpScale",.4f); IslandWorld.Asphalt.EnableKeyword("_NORMALMAP");
                IslandWorld.Asphalt.SetColor("_BaseColor",new Color(.60f,.64f,.65f));
            }
            // A small real-time probe gives the aircraft metal and glass a coherent sky reflection.
            var probe=new GameObject("Coastal sky reflection").AddComponent<ReflectionProbe>();
            probe.transform.position=new Vector3(-420,26,-510); probe.mode=ReflectionProbeMode.Realtime;
            probe.refreshMode=ReflectionProbeRefreshMode.OnAwake; probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.resolution=128; probe.size=new Vector3(5000,2000,5000); probe.cullingMask=0;
            probe.clearFlags=ReflectionProbeClearFlags.Skybox; probe.intensity=.85f;
        }

        static void ApplySurface(Material target,string path)
        {
            var material=Resources.Load<Material>(path); if(material==null||target==null)return;
            target.shader=material.shader; target.CopyPropertiesFromMaterial(material);
        }

        public static void ConfigureCamera(Camera camera)
        {
            camera.clearFlags=CameraClearFlags.Skybox; camera.allowHDR=true;
            var data=camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing=true;
            data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality=AntialiasingQuality.High;
            var volume=new GameObject("Coastal film grade").AddComponent<Volume>(); volume.isGlobal=true; volume.priority=1;
            var profile=Resources.Load<VolumeProfile>("Art/CoastalGrade");
            if(profile==null) { profile=ScriptableObject.CreateInstance<VolumeProfile>(); ConfigureGrade(profile); }
            volume.sharedProfile=profile;
        }
        public static void ConfigureGrade(VolumeProfile profile)
        {
            profile.Add<Tonemapping>(true).mode.value=TonemappingMode.ACES;
            var grade=profile.Add<ColorAdjustments>(true); grade.postExposure.value=.2f; grade.contrast.value=9; grade.saturation.value=5;
            var bloom=profile.Add<Bloom>(true); bloom.threshold.value=1.4f; bloom.intensity.value=.12f; bloom.scatter.value=.55f;
            var vignette=profile.Add<Vignette>(true); vignette.intensity.value=.14f; vignette.smoothness.value=.55f;
        }
    }
}
