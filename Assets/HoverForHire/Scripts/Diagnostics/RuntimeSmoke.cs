using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HoverForHire
{
    /// <summary>Opt-in development-build smoke flight. Never enabled during ordinary play.</summary>
    public sealed class RuntimeSmoke : MonoBehaviour, IFlightInput
    {
        public PilotCommand Command {get;private set;}
        GameBootstrap game; string output; bool artTour;
        int errors,frameLimit=120,measuredFrames;float peakAltitude,peakSpeed,measurementStart;bool everCrashed,startedGrounded;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnableOnRequest()
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]=="-hover-smoke")
            {
                var smoke=new GameObject("Opt-in runtime smoke").AddComponent<RuntimeSmoke>();smoke.output=args[i+1];
                smoke.artTour=Array.IndexOf(args,"-hover-art")>=0;
                for(int n=0;n<args.Length-1;n++)if(args[n]=="-hover-fps"&&int.TryParse(args[n+1],out int fps))smoke.frameLimit=Mathf.Clamp(fps,20,240);
                break;
            }
        }
        IEnumerator Start()
        {
            Application.logMessageReceived+=OnLog;Directory.CreateDirectory(output);Application.targetFrameRate=frameLimit;
            yield return new WaitForSecondsRealtime(2);game=GameBootstrap.Instance;
            if(game==null){File.WriteAllText(Path.Combine(output,"smoke-failed.txt"),"No bootstrap");Application.Quit(1);yield break;}
            game.GetComponent<FlightHUD>().SendMessage("SetPause",false);
            game.Input.enabled=false;game.Input.SetPaused(false);game.Aircraft.InputSource=this;
            yield return new WaitForSeconds(.8f);
            startedGrounded=game.Aircraft.Grounded;
            everCrashed|=game.Aircraft.Crashed;
            measurementStart=Time.unscaledTime;measuredFrames=0;
            yield return Capture("01-home.png");
            Command=new PilotCommand(Vector2.zero,0,.49f);
            game.Input.ResetCommand(.49f);
            yield return new WaitForSeconds(8);
            yield return Capture("02-climb.png");
            Command=new PilotCommand(new Vector2(0,.12f),0,.47f);
            game.Input.ResetCommand(.47f);
            yield return new WaitForSeconds(3);
            Command=new PilotCommand(Vector2.zero,0,.47f);
            yield return new WaitForSeconds(2);
            yield return Capture("03-forward.png");
            game.CameraRig.ToggleCamera();yield return new WaitForSeconds(1);yield return Capture("04-cockpit.png");
            game.CameraRig.ToggleCamera();
            Command=PilotCommand.Neutral;game.Missions.Retry();yield return new WaitForSeconds(2);
            var hud=game.GetComponent<FlightHUD>();hud.SendMessage("TogglePause");yield return null;yield return Capture("05-menu.png");
            hud.SelectMenuPage(1);yield return null;yield return Capture("06-controls.png");
            hud.SelectMenuPage(2);yield return null;yield return Capture("07-bindings.png");
            hud.SelectMenuPage(3);yield return null;yield return Capture("08-assists-camera.png");
            File.WriteAllText(Path.Combine(output,"runtime-smoke.json"),JsonUtility.ToJson(new Report {Errors=errors,FrameLimit=frameLimit,AverageFps=measuredFrames/Mathf.Max(.1f,Time.unscaledTime-measurementStart),PeakAltitude=peakAltitude,PeakSpeed=peakSpeed,StartedGrounded=startedGrounded,ResetGrounded=game.Aircraft.Grounded,EverCrashed=everCrashed,Engine=Application.unityVersion,PostProcessing=game.CameraRig.GetComponent<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing,Tonemapping=VolumeManager.instance.stack.GetComponent<Tonemapping>().mode.value.ToString(),AmbientProbeL0=RenderSettings.ambientProbe[0,0]},true));
            bool passed=errors==0&&peakAltitude>4&&peakSpeed>1&&!everCrashed&&startedGrounded&&game.Aircraft.Grounded;
            if(artTour)yield return Tour();
            Application.Quit(passed&&errors==0?0:1);
        }
        IEnumerator Tour()
        {
            // Fixed photo viewpoints only run after the actual physics smoke result is saved.
            var hud=game.GetComponent<FlightHUD>(); hud.SendMessage("SetPause",false); hud.enabled=false;
            game.CameraRig.enabled=false; game.Aircraft.enabled=false; game.Aircraft.Body.isKinematic=true;
            var camera=game.CameraRig.GetComponent<Camera>(); camera.fieldOfView=48;
            Vector3 origin=game.Aircraft.transform.position;
            camera.transform.SetPositionAndRotation(origin+new Vector3(8,2.6f,10),Quaternion.LookRotation(origin+Vector3.up*.25f-(origin+new Vector3(8,2.6f,10))));
            yield return new WaitForSeconds(.5f); yield return Capture("09-aircraft.png");
            camera.fieldOfView=65; camera.transform.SetPositionAndRotation(new Vector3(-540,130,-680),Quaternion.LookRotation(new Vector3(-180,25,-170)-new Vector3(-540,130,-680)));
            yield return Capture("10-town.png");
            camera.transform.SetPositionAndRotation(new Vector3(-1140,85,-340),Quaternion.LookRotation(new Vector3(-880,5,-205)-new Vector3(-1140,85,-340)));
            yield return Capture("11-harbor.png");
            camera.transform.SetPositionAndRotation(new Vector3(-470,190,260),Quaternion.LookRotation(new Vector3(-350,105,450)-new Vector3(-470,190,260)));
            yield return Capture("12-highlands.png");
            camera.transform.SetPositionAndRotation(new Vector3(-915,115,510),Quaternion.LookRotation(new Vector3(-700,25,420)-new Vector3(-915,115,510)));
            yield return Capture("13-coast.png");
            // Visual effects diagnostics use explicit presentation events, after the flight assertion.
            var effects=game.Aircraft.GetComponent<AircraftEffects>();
            camera.fieldOfView=52;
            Vector3 home=game.Zones[0].transform.position;
            game.Aircraft.Body.isKinematic=false;game.Aircraft.enabled=true;
            game.Aircraft.ResetAt(home+Vector3.up*3.5f,Quaternion.identity);
            Command=new PilotCommand(Vector2.zero,0,.45f);
            camera.transform.SetPositionAndRotation(home+new Vector3(11,4,13),Quaternion.LookRotation(home+Vector3.up*1.5f-(home+new Vector3(11,4,13))));
            yield return new WaitForSeconds(2);yield return Capture("14-rotor-wash.png");
            Command=PilotCommand.Neutral;game.Aircraft.enabled=false;game.Aircraft.Body.isKinematic=true;
            effects.PresentImpact(new AircraftImpact(home+Vector3.up*.1f,Vector3.up,Vector3.down*7,7,1050));
            yield return new WaitForSeconds(.25f);yield return Capture("15-hard-impact.png");
            bool graded=!effects.HasExploded;effects.ResetEffects();
            effects.PresentImpact(new AircraftImpact(home+Vector3.up*.3f,Vector3.up,Vector3.down*25,25,1050));
            game.Aircraft.ReportCrash();
            yield return new WaitForSeconds(.18f);yield return Capture("16-explosion.png");
            bool explosion=effects.HasExploded&&effects.ActiveDebrisCount>0;
            yield return new WaitForSeconds(1.2f);yield return Capture("17-wreckage.png");
            game.Aircraft.ResetAt(home+Vector3.up*1.55f,Quaternion.identity);
            bool restored=!effects.HasExploded&&effects.ActiveDebrisCount==0;
            Vector3 ocean=new Vector3(-1250,-3.5f,-600);
            game.Aircraft.ResetAt(ocean+Vector3.up*.5f,Quaternion.identity);
            camera.transform.SetPositionAndRotation(ocean+new Vector3(11,4,13),Quaternion.LookRotation(ocean-(ocean+new Vector3(11,4,13))));
            effects.PresentImpact(new AircraftImpact(ocean,Vector3.up,Vector3.down*30,30,1050,true));
            yield return new WaitForSeconds(.2f);yield return Capture("18-water-spray.png");
            bool water=!effects.HasExploded;effects.ResetEffects();
            File.WriteAllText(Path.Combine(output,"effects-smoke.json"),JsonUtility.ToJson(new EffectsReport{HardImpactNoExplosion=graded,CatastrophicExplosion=explosion,ResetRestored=restored,WaterNoExplosion=water,Errors=errors},true));
            if(!graded||!explosion||!restored||!water)errors++;
        }
        IEnumerator Capture(string file){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,file));yield return new WaitForSecondsRealtime(.6f);}
        void Update(){if(game==null)return;measuredFrames++;peakAltitude=Mathf.Max(peakAltitude,game.Aircraft.AltitudeAGL);peakSpeed=Mathf.Max(peakSpeed,game.Aircraft.GroundSpeed);everCrashed|=game.Aircraft.Crashed;}
        void OnLog(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        void OnDestroy()=>Application.logMessageReceived-=OnLog;
        [Serializable] class Report { public int Errors,FrameLimit;public float AverageFps,PeakAltitude,PeakSpeed,AmbientProbeL0;public bool StartedGrounded,ResetGrounded,EverCrashed,PostProcessing;public string Engine,Tonemapping; }
        [Serializable] class EffectsReport { public bool HardImpactNoExplosion,CatastrophicExplosion,ResetRestored,WaterNoExplosion;public int Errors; }
    }
}
