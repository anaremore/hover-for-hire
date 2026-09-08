using UnityEngine;
using UnityEngine.Rendering;

namespace HoverForHire
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance {get;private set;}
        public HelicopterController Aircraft {get;private set;}
        public FlightInput Input {get;private set;}
        public MissionDirector Missions {get;private set;}
        public ChaseCamera CameraRig {get;private set;}
        public LandingZone[] Zones {get;private set;}
        void Awake()
        {
            Instance=this;Time.fixedDeltaTime=.02f;Time.maximumDeltaTime=.1f;Application.targetFrameRate=120;
            Physics.defaultSolverIterations=12;Physics.defaultSolverVelocityIterations=6;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.66f,.78f,.83f);RenderSettings.ambientEquatorColor=new Color(.40f,.48f,.48f);RenderSettings.ambientGroundColor=new Color(.18f,.23f,.25f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.00024f;RenderSettings.fogColor=new Color(.58f,.74f,.78f);
            var sun=new GameObject("Late afternoon sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2.2f;sun.color=new Color(1,.91f,.76f);sun.transform.rotation=Quaternion.Euler(38,-35,0);sun.shadows=LightShadows.Soft;
            Zones=IslandWorld.Build();
            var go=new GameObject("M-04 / utility helicopter");go.SetActive(false);go.layer=8;go.transform.position=Zones[0].transform.position+Vector3.up*1.55f;
            go.AddComponent<Rigidbody>();Input=go.AddComponent<FlightInput>();Aircraft=go.AddComponent<HelicopterController>();Aircraft.InputSource=Input;Aircraft.Tuning=Resources.Load<FlightTuning>("UtilityHelicopter");
            var visual=go.AddComponent<HelicopterVisual>();visual.Build(Aircraft);go.SetActive(true);
            var cameraObject=new GameObject("Pilot camera",typeof(Camera),typeof(AudioListener));var camera=cameraObject.GetComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=7000;camera.backgroundColor=RenderSettings.fogColor;camera.clearFlags=CameraClearFlags.SolidColor;cameraObject.tag="MainCamera";
            CameraRig=cameraObject.AddComponent<ChaseCamera>();CameraRig.Target=go.transform;CameraRig.Input=Input;CameraRig.CockpitMount=visual.CockpitMount;
            Missions=gameObject.AddComponent<MissionDirector>();Missions.Aircraft=Aircraft;Missions.Zones=Zones;
            var audio=go.AddComponent<FlightAudio>();audio.Aircraft=Aircraft;
            var hud=gameObject.AddComponent<FlightHUD>();hud.Game=this;hud.Audio=audio;
        }
        void FixedUpdate()
        {
            if(Aircraft!=null && !Aircraft.Crashed && Aircraft.Body.position.y < -5f) Aircraft.ReportCrash();
        }
        void OnDestroy(){if(Instance==this)Instance=null;Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
}
