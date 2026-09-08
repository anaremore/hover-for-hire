using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>Opt-in development-build smoke flight. Never enabled during ordinary play.</summary>
    public sealed class RuntimeSmoke : MonoBehaviour, IFlightInput
    {
        public PilotCommand Command {get;private set;}
        GameBootstrap game; string output;
        int errors,frameLimit=120,measuredFrames;float peakAltitude,peakSpeed,measurementStart;bool everCrashed,startedGrounded;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnableOnRequest()
        {
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]=="-hover-smoke")
            {
                var smoke=new GameObject("Opt-in runtime smoke").AddComponent<RuntimeSmoke>();smoke.output=args[i+1];
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
            File.WriteAllText(Path.Combine(output,"runtime-smoke.json"),JsonUtility.ToJson(new Report {Errors=errors,FrameLimit=frameLimit,AverageFps=measuredFrames/Mathf.Max(.1f,Time.unscaledTime-measurementStart),PeakAltitude=peakAltitude,PeakSpeed=peakSpeed,StartedGrounded=startedGrounded,ResetGrounded=game.Aircraft.Grounded,EverCrashed=everCrashed,Engine=Application.unityVersion},true));
            Application.Quit(errors==0&&peakAltitude>4&&peakSpeed>1&&!everCrashed&&startedGrounded&&game.Aircraft.Grounded?0:1);
        }
        IEnumerator Capture(string file){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,file));yield return new WaitForSecondsRealtime(.6f);}
        void Update(){if(game==null)return;measuredFrames++;peakAltitude=Mathf.Max(peakAltitude,game.Aircraft.AltitudeAGL);peakSpeed=Mathf.Max(peakSpeed,game.Aircraft.GroundSpeed);everCrashed|=game.Aircraft.Crashed;}
        void OnLog(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        void OnDestroy()=>Application.logMessageReceived-=OnLog;
        [Serializable] class Report { public int Errors,FrameLimit;public float AverageFps,PeakAltitude,PeakSpeed;public bool StartedGrounded,ResetGrounded,EverCrashed;public string Engine; }
    }
}
