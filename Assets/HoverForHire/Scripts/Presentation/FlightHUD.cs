using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HoverForHire
{
    public sealed class FlightHUD : MonoBehaviour
    {
        public GameBootstrap Game;
        public FlightAudio Audio;
        bool paused,debug; int page,preset; Vector2 scroll;
        GUIStyle title,label,small,value,button,panel;
        Texture2D white; string notice="Welcome to Port Meridian. Raise collective gently to lift off.";
        float noticeUntil=12;
        int menuFocus,menuCount,menuIndex,menuAdjust; bool menuActivate,menuScrollToFocus,insideMenuScroll;
        Vector2Int menuDirection; float menuRepeatAt;
        const float Width=1280,Height=720;
        HelicopterController Aircraft=>Game.Aircraft;
        FlightInput Input=>Game.Input;
        MissionDirector Missions=>Game.Missions;
        void Start()
        {
            Input.PauseRequested+=TogglePause;Input.ResetRequested+=Retry;Input.InteractRequested+=Interact;Input.DebugRequested+=ToggleDebug;Input.AssistRequested+=CycleAssists;Input.HoverRequested+=HoverNotice;
            Aircraft.ResetPerformed+=ResetInput;
            Missions.FeedbackEvent+=MissionFeedback;
            try { if(PlayerPrefs.HasKey("hfh.assists"))JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString("hfh.assists"),Aircraft.Assists); }catch(Exception){ }
            Audio.Volume=PlayerPrefs.GetFloat("hfh.volume",.65f);
            Missions.StartFreeFlight();
        }
        void ResetInput(){Input.ResetCommand();Game.CameraRig.SnapToTarget();}
        void Update()
        {
            UpdateMenuNavigation();
        }
        void MissionFeedback(string message)
        {
            notice=message;noticeUntil=Time.unscaledTime+6;
            if(message.StartsWith("Loaded",StringComparison.Ordinal)||message.StartsWith("Delivered",StringComparison.Ordinal)||message.StartsWith("Drill complete",StringComparison.Ordinal))Audio.ServiceChime();
        }
        void ToggleDebug()=>debug=!debug;
        void HoverNotice(){notice="Hover hold is deferred. Use rate / level assist and practice a steady collective.";noticeUntil=Time.unscaledTime+6;}
        void CycleAssists(){preset=(preset+1)%3;Aircraft.SetPreset((AssistPreset)preset);Save();}
        void TogglePause()=>SetPause(!paused);
        public void SelectMenuPage(int index){page=Mathf.Clamp(index,0,3);scroll=Vector2.zero;menuFocus=0;}
        void SetPause(bool state){paused=state;Input.SetPaused(state);Time.timeScale=state?0:1;menuActivate=false;menuAdjust=0;menuDirection=Vector2Int.zero;if(!state)Save();}
        void Retry(){Missions.Retry();Input.ResetCommand();notice="Reset complete. Collective is at 0%.";noticeUntil=Time.unscaledTime+4;}
        void Interact(){if(!paused)Missions.Interact();}
        void Save(){Input.SaveSettings();PlayerPrefs.SetString("hfh.assists",JsonUtility.ToJson(Aircraft.Assists));PlayerPrefs.SetFloat("hfh.volume",Audio.Volume);PlayerPrefs.Save();}
        void Styles()
        {
            if(title!=null)return;white=Texture2D.whiteTexture;
            label=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true,normal={textColor=new Color(.91f,.94f,.91f)}};
            small=new GUIStyle(label){fontSize=13};title=new GUIStyle(label){fontSize=28,fontStyle=FontStyle.Bold};value=new GUIStyle(label){fontSize=27,fontStyle=FontStyle.Bold};
            button=new GUIStyle(GUI.skin.button){fontSize=16,padding=new RectOffset(12,12,9,9),margin=new RectOffset(3,3,3,3),fixedHeight=40};
            panel=new GUIStyle(GUI.skin.box){padding=new RectOffset(18,18,15,15)};
        }
        void OnGUI()
        {
            if(Game==null||Game.Aircraft==null)return;Styles();GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/Width,Screen.height/Height,1));
            Box(new Rect(24,22,460,98),.91f);GUI.Label(new Rect(42,34,400,20),"HOVER FOR HIRE  /  PORT MERIDIAN",small);
            GUI.Label(new Rect(42,59,426,50),Missions.CurrentObjective,label);
            Box(new Rect(24,131,390,83),.83f);GUI.Label(new Rect(40,142,358,60),Missions.StatusText,small);
            DrawMap(); DrawInstruments(); DrawTarget();
            Box(new Rect(24,683,1232,29),.9f);
            GUI.Label(new Rect(36,Height-31,1190,24),"ESC  Menu     V  Camera     ALT / MMB  Look     C  Center cyclic     BACKSPACE  Retry     ENTER  Job     F1  Telemetry",small);
            if(Time.unscaledTime<noticeUntil){Box(new Rect(330,490,620,54),.88f);GUI.Label(new Rect(347,501,586,40),notice,small);}
            if(!string.IsNullOrEmpty(Missions.SaveWarning)){Box(new Rect(330,530,620,38),.95f);GUI.Label(new Rect(342,534,596,31),Missions.SaveWarning,small);}
            if(Aircraft.Crashed){Box(new Rect(390,255,500,180),.97f);GUI.Label(new Rect(416,279,450,38),"AIRCRAFT RECOVERY",title);GUI.Label(new Rect(416,323,450,44),"Flight ended. Reset at the pad and try a slower approach.",label);if(GUI.Button(new Rect(416,377,450,40),"Retry  /  Backspace",button))Retry();}
            if(debug)DrawDebug();
            if(paused)DrawMenu();
        }
        void Box(Rect rect,float alpha){var old=GUI.color;GUI.color=new Color(.035f,.085f,.105f,alpha);GUI.DrawTexture(rect,white);GUI.color=old;}
        void Bar(Rect rect,float progress,Color color){Box(rect,.8f);var old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width*Mathf.Clamp01(progress),rect.height),white);GUI.color=old;}
        void DrawInstruments()
        {
            Box(new Rect(24,570,1232,105),.94f);
            Instrument(43,"AIR SPEED",Aircraft.Airspeed*3.6f,"km/h");Instrument(208,"VERTICAL",Aircraft.VerticalSpeed,"m/s");Instrument(373,"SKID AGL",Aircraft.AltitudeAGL,"m");Instrument(538,"HEADING",Aircraft.Heading,"deg");
            Instrument(703,"COLLECTIVE",Input.Command.Collective*100,"%");Bar(new Rect(704,650,129,5),Input.Command.Collective,IslandWorld.Orange);
            GUI.Label(new Rect(875,582,340,21),"ASSISTS  /  "+Aircraft.Assists.Summary,small);GUI.Label(new Rect(875,610,340,27),$"PAYLOAD  {Aircraft.PayloadKg:0} kg     ${Missions.Earnings:0}",label);
            GUI.Label(new Rect(875,641,340,20),Aircraft.Grounded?"GROUNDED  /  AGL ray beneath skids":"AIRBORNE  /  AGL ray beneath skids",small);
            if(Input.Settings.ShowCyclicIndicator || Input.Settings.MouseMode==MouseCyclicMode.VirtualJoystick)
            {
                var r=new Rect(1127,444,82,82);Box(r,.8f);Bar(new Rect(r.x+40,r.y,1,82),1,new Color(.8f,.9f,.9f,.3f));Bar(new Rect(r.x,r.y+40,82,1),1,new Color(.8f,.9f,.9f,.3f));
                Vector2 c=Input.Command.Cyclic;Bar(new Rect(r.center.x+c.x*36-4,r.center.y-c.y*36-4,8,8),1,IslandWorld.Orange);GUI.Label(new Rect(r.x,r.y+85,120,20),Input.IsFreeLooking?"FREE LOOK":"CYCLIC",small);
            }
        }
        void Instrument(float x,string caption,float number,string unit){GUI.Label(new Rect(x,581,156,21),caption,small);GUI.Label(new Rect(x,610,155,36),$"{number:0.0} <size=14>{unit}</size>",value);}
        void DrawMap()
        {
            var r=new Rect(1056,22,200,200);Box(r,.9f);GUI.Label(new Rect(1068,30,176,21),"PORT MERIDIAN    N ↑",small);
            foreach(var z in Game.Zones){var p=z.transform.position;Vector2 m=new Vector2(r.center.x+p.x*.082f,r.center.y-p.z*.082f+10);Bar(new Rect(m.x-3,m.y-3,6,6),1,z==Missions.TargetZone?IslandWorld.Orange:new Color(.7f,.82f,.77f));}
            var a=Aircraft.transform.position;Vector2 here=new Vector2(r.center.x+a.x*.082f,r.center.y-a.z*.082f+10);Bar(new Rect(here.x-4,here.y-4,8,8),1,Color.white);
            GUI.Label(new Rect(1056,229,200,55),Missions.Mode==GameMode.DeliveryShift?$"SHIFT  {TimeText(Missions.RemainingSeconds)}\n{Missions.Grade}":Missions.Mode.ToString(),small);
        }
        static string TimeText(float seconds)=>$"{Mathf.FloorToInt(Mathf.Max(0,seconds)/60):00}:{Mathf.FloorToInt(Mathf.Max(0,seconds)%60):00}";
        void DrawTarget()
        {
            var zone=Missions.TargetZone;if(zone==null&&Missions.Mode!=GameMode.Training)return;var cam=Camera.main;if(cam==null)return;var world=zone!=null?zone.transform.position+Vector3.up*3:Missions.ObjectivePosition;var point=cam.WorldToViewportPoint(world);
            float x=Mathf.Clamp(point.x*Width,100,Width-240),y=Mathf.Clamp((1-point.y)*Height,230,390);if(point.z<0){x=Width/2;y=240;}
            Box(new Rect(x-90,y,220,56),.85f);GUI.Label(new Rect(x-80,y+5,200,23),zone!=null?zone.DisplayName:"TRAINING TARGET",small);GUI.Label(new Rect(x-80,y+28,200,23),$"{Vector3.Distance(Aircraft.transform.position,world):0} m  {(point.z<0?"BEHIND":"")}",small);
            if(Missions.DwellProgress>0)Bar(new Rect(x-90,y+54,220,4),Missions.DwellProgress,IslandWorld.Orange);
        }
        void DrawDebug()
        {
            Box(new Rect(25,231,440,240),.95f);var b=Aircraft.Body;
            GUI.Label(new Rect(40,240,410,225),$"DEVELOPMENT / fixed {Time.fixedDeltaTime:0.000}s\nVelocity  {b.linearVelocity.ToString("F2")} m/s\nAngular  {Aircraft.LocalAngularRatesDegrees.ToString("F1")} deg/s\nRaw  {Aircraft.RawCommand.Cyclic} yaw {Aircraft.RawCommand.Yaw:0.00}\nAssisted  {Aircraft.AssistedCommand.Cyclic} yaw {Aircraft.AssistedCommand.Yaw:0.00}\nLift  {Aircraft.LiftNewtons:0} N   Mass  {b.mass:0} kg\nRotor  {Aircraft.RotorRpm:0} rpm   Grounded  {Aircraft.Grounded}\nTouchdown  {Aircraft.LastTouchdownSpeed:0.00} m/s\nGround speed {Aircraft.GroundSpeed:0.0} m/s",small);
        }
        void DrawMenu()
        {
            menuIndex=0;
            Box(new Rect(0,0,Width,Height),.77f);Box(new Rect(190,35,900,650),.99f);
            GUI.Label(new Rect(217,55,600,40),"FLIGHT DESK",title);GUI.Label(new Rect(217,96,850,25),"Paused · D-pad / stick: navigate · Left/right: adjust · A: select · B: resume",small);
            GUILayout.BeginArea(new Rect(215,132,850,515));
            int selectedPage=MenuToolbar(page,new[]{"Fly","Controls","Bindings","Camera / assists"});
            if(selectedPage!=page){page=selectedPage;scroll=Vector2.zero;menuFocus=0;}
            GUILayout.Space(10);scroll=GUILayout.BeginScrollView(scroll);insideMenuScroll=true;
            if(page==0)ModeMenu();else if(page==1)ControlMenu();else if(page==2)BindingMenu();else CameraMenu();
            insideMenuScroll=false;GUILayout.EndScrollView();GUILayout.Space(7);if(MenuButton("Resume flight  /  Escape"))SetPause(false);GUILayout.EndArea();
            menuCount=menuIndex;menuFocus=Mathf.Clamp(menuFocus,0,Mathf.Max(0,menuCount-1));
            if(Event.current.type==EventType.Layout){menuActivate=false;menuAdjust=0;}
        }
        void ModeMenu()
        {
            GUILayout.Label("A compact island. One helicopter. Room to get better.",label);
            GUILayout.BeginHorizontal();if(MenuButton("Free flight")){Missions.StartFreeFlight();Input.ResetCommand();SetPause(false);}if(MenuButton("Start 15-minute shift")){Missions.StartShift();Input.ResetCommand();SetPause(false);}GUILayout.EndHorizontal();
            if(MenuButton("Accept next contract / continue service")){Missions.Interact();SetPause(false);}
            if(Missions.Mode==GameMode.DeliveryShift&&!Missions.ShiftFinished&&(Missions.MissionState==MissionState.Available||Missions.MissionState==MissionState.Delivered))
            {
                GUILayout.Label(Missions.CurrentObjective,label);
                if(MenuButton("Browse next offer"))Missions.BrowseNextJob();
            }
            GUILayout.Space(10);GUILayout.Label("TRAINING  /  one skill at a time",label);
            string[] drills={"01 Takeoff","02 Hover","03 Yaw control","04 Forward flight","05 Braking","06 Approach","07 Precision landing"};
            for(int i=0;i<drills.Length;i++){if(i%2==0)GUILayout.BeginHorizontal();if(MenuButton(drills[i])){Missions.StartTraining(i);Input.ResetCommand();SetPause(false);}if(i%2==1||i==drills.Length-1)GUILayout.EndHorizontal();}
            GUILayout.Space(6);if(MenuButton("Reset at helipad / retry current job")){Retry();SetPause(false);}
            GUILayout.Label("Raise collective gradually. Around 45% is empty hover power. Tilt forward to accelerate; tilt back early to brake. Lower collective after touchdown.",small);
            if(MenuButton("Quit game")){Save();Application.Quit();}
        }
        void ControlMenu()
        {
            var s=Input.Settings;GUILayout.Label("Mouse cyclic · keyboard adds smoothly, combined command is bounded",label);
            s.MouseMode=(MouseCyclicMode)MenuToolbar((int)s.MouseMode,new[]{"Relative + return","Virtual joystick"});
            Slider("Mouse sensitivity",ref s.MouseSensitivity,.0005f,.02f,"F4");Slider("Return toward center / s",ref s.MouseReturnRate,0,8);Slider("Deadzone",ref s.Deadzone,0,.4f);Slider("Response curve",ref s.ResponseCurve,.5f,3);
            s.InvertPitch=MenuToggle(s.InvertPitch,"Invert cyclic pitch");s.InvertRoll=MenuToggle(s.InvertRoll,"Invert cyclic roll");Slider("Keyboard response",ref s.KeyboardResponse,2,30);Slider("Collective change / s",ref s.CollectiveRate,.05f,1);
            GUILayout.Label("Cyclic while holding free look",label);s.FreeLookBehavior=(FreeLookCyclicMode)MenuToolbar((int)s.FreeLookBehavior,new[]{"Hold command","Return to neutral"});
            s.ShowCyclicIndicator=MenuToggle(s.ShowCyclicIndicator,"Show cyclic indicator");s.UseAbsoluteCollective=MenuToggle(s.UseAbsoluteCollective,"Use absolute collective axis (bind below first)");s.AbsoluteAxisSigned=MenuToggle(s.AbsoluteAxisSigned,"Absolute axis range is -1 to +1");s.InvertAbsoluteCollective=MenuToggle(s.InvertAbsoluteCollective,"Invert absolute collective");
            GUILayout.Label("MMB duplicates Alt free look on systems that intercept Alt. C recenters cyclic; R recenters only the view. Bindings can be changed on the next tab.",small);
            if(MenuButton("Restore control defaults"))Input.RestoreDefaults();
        }
        void BindingMenu()
        {
            GUILayout.Label(Input.IsRebinding?"Move or press a control. Escape / menu Back cancels.":"Choose a binding, then press a key, button, or move an axis.",label);
            GUI.enabled=Input.IsRebinding;if(MenuButton("Cancel binding"))Input.CancelRebind();GUI.enabled=true;
            foreach(var action in Input.Actions)
            {
                GUILayout.Space(6);GUILayout.Label(action.name,label);
                for(int i=0;i<action.bindings.Count;i++)
                {
                    var binding=action.bindings[i];if(binding.isComposite)continue;int index=i;var id=action.id;
                    GUI.enabled=!Input.IsRebinding;
                    if(MenuButton((binding.isPartOfComposite?binding.name+": ":"")+action.GetBindingDisplayString(i)))Input.BeginRebind(id,index,_=>Save());
                    GUI.enabled=true;
                }
            }
        }
        void CameraMenu()
        {
            GUILayout.Label("Flight assists · same aircraft, bounded control commands",label);
            GUILayout.BeginHorizontal();foreach(AssistPreset p in Enum.GetValues(typeof(AssistPreset)))if(MenuButton(p.ToString())){preset=(int)p;Aircraft.SetPreset(p);}GUILayout.EndHorizontal();
            var a=Aircraft.Assists;a.RateStabilization=MenuToggle(a.RateStabilization,"Pitch / roll rate stabilization");a.AutoLevel=MenuToggle(a.AutoLevel,"Auto-level with centered cyclic");a.YawStabilization=MenuToggle(a.YawStabilization,"Yaw stabilization");a.TorqueCompensation=MenuToggle(a.TorqueCompensation,"Main rotor torque compensation");
            GUILayout.Label("Hover hold is deferred pending flight tuning. Level assist does not stop drift or hold altitude.",small);
            var s=Input.Settings;Slider("Camera distance / m",ref s.CameraDistance,5,25);Slider("Camera height / m",ref s.CameraHeight,1,10);Slider("Field of view / deg",ref s.CameraFov,45,100);Slider("Camera smoothing / s",ref s.CameraSmoothing,.02f,.8f);Slider("Look sensitivity",ref s.LookSensitivity,.02f,.5f);Slider("Gamepad look / deg/s",ref s.GamepadLookSpeed,30,240);
            s.InvertLook=MenuToggle(s.InvertLook,"Invert camera look");s.AutoRecenterView=MenuToggle(s.AutoRecenterView,"Automatically recenter view");Slider("Recenter delay / s",ref s.RecenterDelay,0,5);Slider("Recenter speed",ref s.RecenterSpeed,1,12);Slider("Audio volume",ref Audio.Volume,0,1);
        }
        void UpdateMenuNavigation()
        {
            if(!paused||Input.IsRebinding)return;
            if(Input.MenuBackPressed){SetPause(false);return;}
            if(Input.MenuSubmitPressed)menuActivate=true;
            Vector2 move=Input.MenuMove;
            Vector2Int direction=Mathf.Abs(move.y)>.5f?new Vector2Int(0,move.y>0?-1:1):Mathf.Abs(move.x)>.5f?new Vector2Int(move.x>0?1:-1,0):Vector2Int.zero;
            if(direction==Vector2Int.zero){menuDirection=direction;return;}
            bool changed=direction!=menuDirection;
            if(changed||Time.unscaledTime>=menuRepeatAt)
            {
                if(direction.y!=0&&menuCount>0){menuFocus=(menuFocus+direction.y+menuCount)%menuCount;menuScrollToFocus=true;}
                if(direction.x!=0)menuAdjust=direction.x;
                menuRepeatAt=Time.unscaledTime+(changed ? .38f : .12f);
            }
            menuDirection=direction;
        }
        Color MenuHighlight(int id)
        {
            Color previous=GUI.backgroundColor;
            if(id==menuFocus)GUI.backgroundColor=new Color(1f,.64f,.32f);
            return previous;
        }
        bool MenuActivate(int id)=>id==menuFocus&&menuActivate&&GUI.enabled&&Event.current.type==EventType.Layout;
        int MenuAdjustment(int id)=>id==menuFocus&&GUI.enabled&&Event.current.type==EventType.Layout?menuAdjust:0;
        void TrackMenuControl(int id)
        {
            if(id!=menuFocus||!insideMenuScroll||!menuScrollToFocus||Event.current.type!=EventType.Repaint)return;
            Rect rect=GUILayoutUtility.GetLastRect();
            if(rect.yMin<scroll.y)scroll.y=Mathf.Max(0,rect.yMin-15);
            else if(rect.yMax>scroll.y+360)scroll.y=rect.yMax-345;
            menuScrollToFocus=false;
        }
        bool MenuButton(string text)
        {
            int id=menuIndex++;Color color=MenuHighlight(id);bool clicked=GUILayout.Button(text,button);GUI.backgroundColor=color;TrackMenuControl(id);
            bool activate=MenuActivate(id);if(activate)menuActivate=false;return clicked||activate;
        }
        bool MenuToggle(bool state,string text)
        {
            int id=menuIndex++;Color color=MenuHighlight(id);bool result=GUILayout.Toggle(state,(id==menuFocus?"› ":"")+text);GUI.backgroundColor=color;TrackMenuControl(id);
            if(MenuActivate(id)){menuActivate=false;result=!result;}else if(MenuAdjustment(id)!=0)result=menuAdjust>0;
            return result;
        }
        int MenuToolbar(int selected,string[] options)
        {
            int id=menuIndex++;Color color=MenuHighlight(id);int result=GUILayout.Toolbar(selected,options,button);GUI.backgroundColor=color;TrackMenuControl(id);
            int adjust=MenuAdjustment(id);if(MenuActivate(id)){menuActivate=false;adjust=1;}
            if(adjust!=0)result=(result+adjust+options.Length)%options.Length;return result;
        }
        void Slider(string caption,ref float number,float min,float max,string format="F2")
        {
            int id=menuIndex++;GUILayout.Label((id==menuFocus?"› ":"")+caption+"   "+number.ToString(format),small);Color color=MenuHighlight(id);
            number=GUILayout.HorizontalSlider(number,min,max);GUI.backgroundColor=color;TrackMenuControl(id);
            number=Mathf.Clamp(number+MenuAdjustment(id)*(max-min)/40f,min,max);GUILayout.Space(5);
        }
        void OnDestroy(){if(Game==null||Game.Input==null)return;Input.PauseRequested-=TogglePause;Input.ResetRequested-=Retry;Input.InteractRequested-=Interact;Input.DebugRequested-=ToggleDebug;Input.AssistRequested-=CycleAssists;Input.HoverRequested-=HoverNotice;if(Aircraft!=null)Aircraft.ResetPerformed-=ResetInput;if(Game.Missions!=null)Missions.FeedbackEvent-=MissionFeedback;}
    }
}
