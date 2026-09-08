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
        GUIStyle title,label,small,value,button,panel,hudLabel,hudSmall,hudValue,hudCenter,hudRight,hudValueRight,hudValueCenter,toggle,sliderTrack,sliderThumb,tabButton;
        Texture2D white,mapTexture,buttonIdle,buttonHover,buttonActive,toggleOff,toggleOn;
        string notice="Welcome to Port Meridian. Raise collective gently to lift off.";
        float noticeUntil=12;
        int menuFocus,menuCount,menuIndex,menuAdjust; bool menuActivate,menuScrollToFocus,insideMenuScroll;
        Vector2Int menuDirection; float menuRepeatAt;
        const float Height=720;
        float Width=1280;
        static readonly Vector2[][] ChartRoads = {
            new[]{new Vector2(-850,-320),new Vector2(-690,-350),new Vector2(-490,-350),new Vector2(-190,-350),new Vector2(110,-350),new Vector2(365,-350),new Vector2(401,-395),new Vector2(553,-395)},
            new[]{new Vector2(-690,-350),new Vector2(-655,-140),new Vector2(-643,95),new Vector2(-660,260),new Vector2(-699,370),new Vector2(-701,400)},
            new[]{new Vector2(-190,-350),new Vector2(-190,-248),new Vector2(-149,-242),new Vector2(-144,-151),new Vector2(-190,-151),new Vector2(-190,-64),new Vector2(-190,113)},
            new[]{new Vector2(-54,88),new Vector2(45,148),new Vector2(144,202),new Vector2(241,224),new Vector2(292,318),new Vector2(363,419),new Vector2(440,471)},
            new[]{new Vector2(365,-350),new Vector2(550,-238),new Vector2(649,-127),new Vector2(708,-28),new Vector2(728,72)}
        };
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
            if(title!=null)return;
            white=Texture2D.whiteTexture;
            label=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true,normal={textColor=FlightHudGraphics.Paper},padding=new RectOffset(0,0,2,2)};
            small=new GUIStyle(label){fontSize=13};
            title=new GUIStyle(label){fontSize=29,fontStyle=FontStyle.Bold};
            value=new GUIStyle(label){fontSize=26,fontStyle=FontStyle.Bold};
            hudLabel=new GUIStyle(label){fontSize=15,wordWrap=false,normal={textColor=FlightHudGraphics.Phosphor}};
            hudSmall=new GUIStyle(hudLabel){fontSize=13};
            hudValue=new GUIStyle(hudLabel){fontSize=27,fontStyle=FontStyle.Bold};
            hudValueRight=new GUIStyle(hudValue){alignment=TextAnchor.UpperRight};
            hudValueCenter=new GUIStyle(hudValue){alignment=TextAnchor.MiddleCenter};
            hudCenter=new GUIStyle(hudLabel){alignment=TextAnchor.MiddleCenter};
            hudRight=new GUIStyle(hudSmall){alignment=TextAnchor.UpperRight};
            buttonIdle=FlightHudGraphics.Solid(new Color(.18f,.21f,.18f));
            buttonHover=FlightHudGraphics.Solid(new Color(.28f,.34f,.25f));
            buttonActive=FlightHudGraphics.Solid(new Color(.43f,.52f,.34f));
            toggleOff=FlightHudGraphics.Solid(new Color(.15f,.18f,.15f));
            toggleOn=FlightHudGraphics.Solid(new Color(.29f,.37f,.23f));
            button=new GUIStyle(GUI.skin.button){fontSize=15,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(16,16,10,10),margin=new RectOffset(3,3,4,4),border=new RectOffset(),fixedHeight=44};
            SetControlColors(button,buttonIdle,buttonHover,buttonActive);
            tabButton=new GUIStyle(button){alignment=TextAnchor.MiddleCenter,fixedHeight=43,fontSize=14};
            toggle=new GUIStyle(button){fontSize=14,fixedHeight=36};
            SetControlColors(toggle,toggleOff,buttonHover,toggleOn);
            sliderTrack=new GUIStyle(GUI.skin.horizontalSlider){fixedHeight=7,margin=new RectOffset(5,5,9,9),border=new RectOffset(),normal={background=buttonIdle}};
            sliderThumb=new GUIStyle(GUI.skin.horizontalSliderThumb){fixedWidth=16,fixedHeight=19,border=new RectOffset(),normal={background=buttonActive},hover={background=buttonHover},active={background=buttonActive}};
            panel=new GUIStyle(GUI.skin.box){padding=new RectOffset(18,18,15,15),normal={background=buttonIdle},border=new RectOffset()};
            BuildMapTexture();
        }
        static void SetControlColors(GUIStyle style,Texture2D idle,Texture2D hover,Texture2D active)
        {
            style.normal.background=idle;style.hover.background=hover;style.active.background=active;style.focused.background=hover;
            style.onNormal.background=active;style.onHover.background=active;style.onActive.background=active;style.onFocused.background=active;
            style.normal.textColor=style.hover.textColor=style.active.textColor=style.focused.textColor=FlightHudGraphics.Paper;
            style.onNormal.textColor=style.onHover.textColor=style.onActive.textColor=style.onFocused.textColor=FlightHudGraphics.Paper;
        }
        void OnGUI()
        {
            if(Game==null||Game.Aircraft==null)return;
            Styles();Matrix4x4 previous=GUI.matrix;
            float scale=Screen.height/Height;Width=Screen.width/scale;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            DrawMission();DrawInstruments();DrawMap();DrawTarget();
            Text(new Rect(26,690,760,20),"ESC  Flight desk    V  Camera    ALT / MMB  Look    C  Center    ENTER  Job",hudSmall,FlightHudGraphics.Muted);
            if(Time.unscaledTime<noticeUntil)
            {
                var r=new Rect(Width/2-257,535,514,48);Box(r,.72f);
                FlightHudGraphics.Fill(new Rect(r.x,r.y,2,r.height),FlightHudGraphics.Amber);
                GUI.Label(new Rect(r.x+14,r.y+9,r.width-28,36),notice,small);
            }
            if(!string.IsNullOrEmpty(Missions.SaveWarning)){Box(new Rect(Width/2-280,590,560,48),.92f);GUI.Label(new Rect(Width/2-266,598,532,36),Missions.SaveWarning,small);}
            if(Aircraft.Crashed)
            {
                var r=new Rect(Width/2-255,248,510,190);Box(r,.97f);FlightHudGraphics.Fill(new Rect(r.x,r.y,r.width,3),FlightHudGraphics.Amber);
                GUI.Label(new Rect(r.x+24,r.y+22,462,38),"AIRCRAFT RECOVERY",title);
                GUI.Label(new Rect(r.x+24,r.y+74,462,46),"Flight ended. Reset at the pad and try a slower approach.",label);
                if(GUI.Button(new Rect(r.x+24,r.y+132,462,44),"Retry  /  Backspace",button))Retry();
            }
            if(debug)DrawDebug();
            if(paused)DrawMenu();
            GUI.matrix=previous;
        }
        void Box(Rect rect,float alpha)=>FlightHudGraphics.Fill(rect,new Color(.045f,.065f,.053f,alpha));
        void Bar(Rect rect,float progress,Color color){Box(rect,.8f);var old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width*Mathf.Clamp01(progress),rect.height),white);GUI.color=old;}
        void Text(Rect rect,string text,GUIStyle style,Color? tint=null)
        {
            Color previous=style.normal.textColor;style.normal.textColor=new Color(.015f,.025f,.018f,.85f);
            GUI.Label(new Rect(rect.x+1,rect.y+1,rect.width,rect.height),text,style);
            style.normal.textColor=tint??FlightHudGraphics.Phosphor;GUI.Label(rect,text,style);style.normal.textColor=previous;
        }
        string ModeName=>Missions.Mode==GameMode.DeliveryShift?"DELIVERY SHIFT":Missions.Mode==GameMode.Training?"FLIGHT TRAINING":"FREE FLIGHT";
        void DrawMission()
        {
            Box(new Rect(26,26,348,126),.61f);
            FlightHudGraphics.Fill(new Rect(26,26,3,126),FlightHudGraphics.Amber);
            Text(new Rect(42,36,316,20),"PORT MERIDIAN  /  "+ModeName,hudSmall,FlightHudGraphics.Amber);
            GUI.Label(new Rect(42,65,316,49),Missions.CurrentObjective,label);
            string status=Missions.Mode==GameMode.FreeFlight?"10 LANDING SITES  /  UNRESTRICTED":Missions.StatusText;
            GUI.Label(new Rect(42,114,316,37),status,small);
            float right=Width-244;
            Text(new Rect(right,28,216,20),Missions.Mode==GameMode.DeliveryShift?"SHIFT REMAINING":"MERIDIAN AIR SERVICE",hudRight,FlightHudGraphics.Muted);
            Text(new Rect(right,51,216,36),Missions.Mode==GameMode.DeliveryShift?TimeText(Missions.RemainingSeconds):"M–04",hudValueRight,FlightHudGraphics.Paper);
            Text(new Rect(right,91,216,22),$"${Missions.Earnings:0}  /  {Missions.DeliveriesThisShift:00} DELIVERIES",hudRight);
            if(Missions.Mode==GameMode.Training)Text(new Rect(right-60,119,276,24),Missions.TrainingName.ToUpperInvariant(),hudRight);
            DrawCompass();
        }
        void DrawCompass()
        {
            float cx=Width/2,heading=Aircraft.Heading;
            const float half=186,pixels=3.2f;
            for(int offset=-70;offset<=70;offset++)
            {
                int mark=Mathf.FloorToInt(heading)+offset;if(mark%5!=0)continue;
                float x=cx+Mathf.DeltaAngle(heading,mark)*pixels;if(Mathf.Abs(x-cx)>half)continue;
                bool major=mark%10==0;
                FlightHudGraphics.Line(new Vector2(x,67),new Vector2(x,major?78:73),FlightHudGraphics.Phosphor);
                if(major)
                {
                    int h=(mark%360+360)%360;
                    string caption=h==0?"N":h==90?"E":h==180?"S":h==270?"W":h.ToString("000");
                    Text(new Rect(x-21,44,42,22),caption,hudCenter);
                }
            }
            FlightHudGraphics.Line(new Vector2(cx-5,82),new Vector2(cx,77),FlightHudGraphics.Paper,1.6f);
            FlightHudGraphics.Line(new Vector2(cx,77),new Vector2(cx+5,82),FlightHudGraphics.Paper,1.6f);
            Box(new Rect(cx-35,88,70,27),.48f);
            Text(new Rect(cx-35,87,70,27),$"{heading:000}°",hudCenter,FlightHudGraphics.Paper);
        }
        void DrawInstruments()
        {
            float cx=Width/2;
            bool cockpit=Game.CameraRig.IsCockpit;
            if(cockpit)
            {
                Text(new Rect(30,235,156,22),"AIR SPEED",hudSmall,FlightHudGraphics.Muted);
                Text(new Rect(30,260,170,35),$"{Aircraft.Airspeed*3.6f:0.0} km/h",hudValue);
                Text(new Rect(Width-178,235,150,22),"SKID AGL",hudRight,FlightHudGraphics.Muted);
                Text(new Rect(Width-190,260,162,35),$"{Aircraft.AltitudeAGL:0.0} m",hudValueRight);
            }
            else
            {
                DrawTape(cx-281,Aircraft.Airspeed*3.6f,"AIR SPEED","km/h",true);
                DrawTape(cx+281,Aircraft.AltitudeAGL,"SKID AGL","m",false);
                DrawAttitude(cx);
            }
            float verticalX=cockpit?Width-157:cx+266,verticalY=cockpit?306:446;
            Text(new Rect(verticalX,verticalY,130,22),$"{Aircraft.VerticalSpeed:+0.0;-0.0;0.0} m/s",cockpit?hudRight:hudCenter,
                Aircraft.VerticalSpeed < -4f && Aircraft.AltitudeAGL < 15?FlightHudGraphics.Amber:FlightHudGraphics.Phosphor);
            Text(new Rect(verticalX,verticalY+23,130,20),"VERT SPEED",cockpit?hudRight:hudCenter,FlightHudGraphics.Muted);
            float collective=Input.Command.Collective;
            if(cockpit)
            {
                Text(new Rect(30,376,272,22),$"COLLECTIVE  {collective*100:0}%   /   {Aircraft.PayloadKg:0} kg",hudSmall);
                Text(new Rect(30,401,290,38),"ASSIST  /  "+Aircraft.Assists.Summary,hudSmall,FlightHudGraphics.Muted);
            }
            else
            {
                Text(new Rect(cx-99,624,198,22),$"COLLECTIVE  {collective*100:0}%",hudCenter);
                Bar(new Rect(cx-91,654,182,5),collective,FlightHudGraphics.Phosphor);
                for(int i=0;i<=4;i++)FlightHudGraphics.Fill(new Rect(cx-91+i*45.5f,650,1,13),FlightHudGraphics.Muted);
                DrawAircraftStatus();
            }
            if(Input.Settings.ShowCyclicIndicator || Input.Settings.MouseMode==MouseCyclicMode.VirtualJoystick)
            {
                Vector2 center=cockpit?new Vector2(Width-106,373):new Vector2(cx+169,625);FlightHudGraphics.Circle(center,26,FlightHudGraphics.Muted);
                FlightHudGraphics.Line(center+Vector2.left*30,center+Vector2.right*30,new Color(.8f,.9f,.7f,.32f));
                FlightHudGraphics.Line(center+Vector2.up*30,center+Vector2.down*30,new Color(.8f,.9f,.7f,.32f));
                Vector2 c=Input.Command.Cyclic;FlightHudGraphics.Circle(center+new Vector2(c.x,-c.y)*23,3,FlightHudGraphics.Amber,12,2);
                Text(new Rect(center.x-49,center.y+33,98,21),Input.IsFreeLooking?"FREE LOOK":"CYCLIC",hudCenter,FlightHudGraphics.Muted);
            }
        }
        void DrawTape(float x,float number,string caption,string unit,bool left)
        {
            const float cy=340,half=94,pixels=3.8f;
            Text(new Rect(x-54,218,108,22),caption,hudCenter,FlightHudGraphics.Muted);
            Text(new Rect(x-44,241,88,20),unit,hudCenter);
            int baseMark=Mathf.FloorToInt(number/5)*5;
            for(int i=-6;i<=6;i++)
            {
                int n=baseMark+i*5;if(n<0)continue;float y=cy-(n-number)*pixels;if(Mathf.Abs(y-cy)>half)continue;
                bool major=n%10==0;
                FlightHudGraphics.Line(new Vector2(x,y),new Vector2(x+(left?-1:1)*(major?14:7),y),FlightHudGraphics.Phosphor);
                if(major)Text(new Rect(left?x-54:x+20,y-10,36,20),n.ToString(),left?hudRight:hudSmall);
            }
            FlightHudGraphics.Line(new Vector2(x,cy-half),new Vector2(x,cy+half),new Color(.8f,.94f,.7f,.35f));
            var readout=new Rect(left?x-100:x+6,cy-21,94,43);Box(readout,.67f);
            FlightHudGraphics.Frame(readout,FlightHudGraphics.Phosphor);
            Text(readout,number<10?number.ToString("0.0"):number.ToString("0"),hudValueCenter,FlightHudGraphics.Paper);
            FlightHudGraphics.Line(new Vector2(x+(left?-6:6),cy-5),new Vector2(x,cy),FlightHudGraphics.Phosphor);
            FlightHudGraphics.Line(new Vector2(x,cy),new Vector2(x+(left?-6:6),cy+5),FlightHudGraphics.Phosphor);
        }
        void DrawAttitude(float cx)
        {
            Quaternion attitude=Aircraft.Body!=null?Aircraft.Body.rotation:Aircraft.transform.rotation;
            Vector3 forward=attitude*Vector3.forward,right=attitude*Vector3.right,up=attitude*Vector3.up;
            float pitch=Mathf.Asin(Mathf.Clamp(forward.y,-1,1))*Mathf.Rad2Deg;
            float roll=Mathf.Atan2(right.y,up.y)*Mathf.Rad2Deg;
            // Attitude comes from the aircraft relative to world up, even in an orbiting chase view.
            // Keep attitude in the same logical coordinate space as the tapes. A nested GUI
            // clip would add its own pivot offset when the screen is scaled above 720p.
            Matrix4x4 previous=GUI.matrix;
            Vector2 center=new Vector2(cx,340);GUI.matrix=previous*FlightHudGraphics.RotationAround(center,roll);
            for(int mark=-80;mark<=80;mark+=10)
            {
                float y=center.y+(pitch-mark)*3.1f;if(Mathf.Abs(y-center.y)>100)continue;
                float inner=mark==0?39:53,outer=mark==0?152:100;
                Color color=mark==0?FlightHudGraphics.Phosphor:new Color(.83f,.96f,.70f,.60f);
                for(int side=-1;side<=1;side+=2)
                {
                    if(mark<0)
                    {
                        for(float d=inner;d<outer;d+=13)FlightHudGraphics.Line(new Vector2(center.x+side*d,y),new Vector2(center.x+side*Mathf.Min(d+7,outer),y),color);
                    }
                    else FlightHudGraphics.Line(new Vector2(center.x+side*inner,y),new Vector2(center.x+side*outer,y),color);
                    if(mark!=0)
                    {
                        FlightHudGraphics.Line(new Vector2(center.x+side*outer,y),new Vector2(center.x+side*outer,y+Mathf.Sign(mark)*5),color);
                        Text(new Rect(center.x+side*121-15,y-11,30,22),Mathf.Abs(mark).ToString(),hudCenter,color);
                    }
                }
            }
            GUI.matrix=previous;
            Vector2 waterline=new Vector2(cx,340);
            FlightHudGraphics.Line(waterline+new Vector2(-25,0),waterline+new Vector2(-8,0),FlightHudGraphics.Paper,1.8f);
            FlightHudGraphics.Line(waterline+new Vector2(-8,0),waterline+new Vector2(0,6),FlightHudGraphics.Paper,1.8f);
            FlightHudGraphics.Line(waterline+new Vector2(0,6),waterline+new Vector2(8,0),FlightHudGraphics.Paper,1.8f);
            FlightHudGraphics.Line(waterline+new Vector2(8,0),waterline+new Vector2(25,0),FlightHudGraphics.Paper,1.8f);
            if(Aircraft.Airspeed>.8f&&Aircraft.Body!=null)
            {
                Vector3 local=Quaternion.Inverse(attitude)*Aircraft.Body.linearVelocity;
                float yaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
                float climb=Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg;
                // Show the velocity vector only inside the instrument field; off-field flight paths are not pinned as false targets.
                if(Mathf.Abs(yaw)<48&&Mathf.Abs(climb)<30)
                {
                    Vector2 fpm=waterline+new Vector2(yaw*3.1f,-climb*3.1f);
                    FlightHudGraphics.Circle(fpm,7,FlightHudGraphics.Phosphor,24);
                    FlightHudGraphics.Line(fpm+Vector2.left*7,fpm+Vector2.left*17,FlightHudGraphics.Phosphor);
                    FlightHudGraphics.Line(fpm+Vector2.right*7,fpm+Vector2.right*17,FlightHudGraphics.Phosphor);
                    FlightHudGraphics.Line(fpm+Vector2.up*7,fpm+Vector2.up*13,FlightHudGraphics.Phosphor);
                }
            }
            Text(new Rect(cx-115,462,230,21),Input.IsFreeLooking?"FREE LOOK":Game.CameraRig.IsCockpit?"COCKPIT VIEW":"CHASE VIEW",hudCenter,FlightHudGraphics.Muted);
        }
        void DrawAircraftStatus()
        {
            Box(new Rect(26,554,266,123),.58f);
            FlightHudGraphics.Fill(new Rect(26,554,266,1),new Color(.8f,.94f,.7f,.45f));
            Text(new Rect(39,566,242,22),Aircraft.Crashed?"M–04   /   RECOVERY":Aircraft.Grounded?"M–04   /   ON GROUND":"M–04   /   AIRBORNE",hudSmall,
                Aircraft.Crashed?FlightHudGraphics.Amber:FlightHudGraphics.Paper);
            Text(new Rect(39,597,240,22),$"ROTOR  {Aircraft.RotorRpm:0} rpm   /   {Aircraft.PayloadKg:0} kg",hudSmall);
            string aids=(Aircraft.Assists.RateStabilization?"RATE  ":"")+(Aircraft.Assists.AutoLevel?"LEVEL  ":"")+(Aircraft.Assists.YawStabilization?"YAW  ":"")+(Aircraft.Assists.TorqueCompensation?"TORQUE":"");
            Text(new Rect(39,625,242,20),"ASSIST  /  "+(aids.Length==0?"OFF":aids),hudSmall,FlightHudGraphics.Muted);
            Text(new Rect(39,651,242,20),Missions.Mode==GameMode.DeliveryShift?$"COMFORT {Missions.ComfortPercent:0}%   CARGO {Missions.CargoConditionPercent:0}%":"UTILITY  /  MERIDIAN AIR SERVICE",hudSmall,FlightHudGraphics.Muted);
        }
        void DrawMap()
        {
            var frame=new Rect(Width-246,429,220,248);Box(frame,.79f);
            var r=new Rect(frame.x+9,frame.y+31,202,202);GUI.DrawTexture(r,mapTexture);
            Color grid=new Color(.79f,.92f,.69f,.10f);
            for(int i=1;i<4;i++){FlightHudGraphics.Fill(new Rect(r.x+i*r.width/4,r.y,1,r.height),grid);FlightHudGraphics.Fill(new Rect(r.x,r.y+i*r.height/4,r.width,1),grid);}
            FlightHudGraphics.Frame(frame,new Color(.8f,.94f,.7f,.42f));
            Text(new Rect(frame.x+11,frame.y+6,180,22),"MERIDIAN  /  NAV",hudSmall);
            Text(new Rect(frame.x+189,frame.y+6,23,22),"N ↑",hudSmall,FlightHudGraphics.Paper);
            foreach(Vector2[] road in ChartRoads)for(int i=1;i<road.Length;i++)
                MapRoad(r,new Vector3(road[i-1].x,0,road[i-1].y),new Vector3(road[i].x,0,road[i].y));
            Vector2 here=MapPosition(r,Aircraft.transform.position);
            if(Missions.TargetZone!=null)
            {
                Vector2 destination=MapPosition(r,Missions.TargetZone.transform.position);
                float length=Vector2.Distance(here,destination);
                for(float d=0;d<length;d+=8)FlightHudGraphics.Line(Vector2.Lerp(here,destination,d/Mathf.Max(1,length)),Vector2.Lerp(here,destination,Mathf.Min(d+4,length)/Mathf.Max(1,length)),new Color(1,.76f,.35f,.7f));
            }
            for(int i=0;i<Game.Zones.Length;i++)
            {
                var zone=Game.Zones[i];if(zone==null)continue;
                Vector2 p=MapPosition(r,zone.transform.position);Color color=zone==Missions.TargetZone?FlightHudGraphics.Amber:FlightHudGraphics.Phosphor;
                if(zone==Missions.TargetZone)FlightHudGraphics.Diamond(p,7,color);else FlightHudGraphics.Frame(new Rect(p.x-2,p.y-2,4,4),color);
                Text(new Rect(p.x+5,p.y-12,23,19),(i+1).ToString("00"),hudSmall,color);
            }
            FlightHudGraphics.Circle(here,12,new Color(.94f,.96f,.89f,.30f),28);
            FlightHudGraphics.Aircraft(here,Aircraft.Heading,FlightHudGraphics.Paper,.85f);
            FlightHudGraphics.Fill(new Rect(r.x+8,r.yMax-12,r.width*500/2400,2),FlightHudGraphics.Muted);
            Text(new Rect(r.x+8,r.yMax-34,70,20),"500 m",hudSmall,FlightHudGraphics.Muted);
        }
        Vector2 MapPosition(Rect rect,Vector3 position)=>new Vector2(Mathf.Clamp(rect.center.x+position.x/2400*rect.width,rect.x+9,rect.xMax-9),Mathf.Clamp(rect.center.y-position.z/2400*rect.height,rect.y+9,rect.yMax-9));
        void MapRoad(Rect rect,Vector3 from,Vector3 to)=>FlightHudGraphics.Line(MapPosition(rect,from),MapPosition(rect,to),new Color(.80f,.84f,.67f,.35f),1.5f);
        void BuildMapTexture()
        {
            const int size=256;mapTexture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Meridian navigation chart",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float wx=(x/(float)(size-1)-.5f)*2400,wz=(y/(float)(size-1)-.5f)*2400;
                float height=IslandWorld.Height(wx,wz);
                if(height<0)pixels[y*size+x]=new Color(.075f,.13f,.13f);
                else
                {
                    float slope=(IslandWorld.Height(wx-8,wz+8)-height)*.013f;
                    Color land=Color.Lerp(new Color(.22f,.29f,.20f),new Color(.46f,.49f,.30f),Mathf.Clamp01(height/150));
                    land*=Mathf.Clamp(1+slope,.66f,1.35f);land.a=1;
                    if(height<3)land=new Color(.49f,.51f,.33f);
                    else if(Mathf.FloorToInt(height/25)!=Mathf.FloorToInt(IslandWorld.Height(wx+9,wz)/25))land*=.74f;
                    pixels[y*size+x]=land;
                }
            }
            mapTexture.SetPixels(pixels);mapTexture.Apply();
        }
        static string TimeText(float seconds)=>$"{Mathf.FloorToInt(Mathf.Max(0,seconds)/60):00}:{Mathf.FloorToInt(Mathf.Max(0,seconds)%60):00}";
        void DrawTarget()
        {
            var zone=Missions.TargetZone;if(zone==null&&Missions.Mode!=GameMode.Training)return;
            var cam=Camera.main;if(cam==null)return;
            Vector3 world=zone!=null?zone.transform.position+Vector3.up*3:Missions.ObjectivePosition;
            Vector3 local=cam.transform.InverseTransformPoint(world);Vector3 point=cam.WorldToViewportPoint(world);
            Rect safe=new Rect(118,178,Width-390,334);
            Vector2 raw=new Vector2(point.x*Width,(1-point.y)*Height),center=new Vector2(Width/2,340);
            bool offscreen=point.z<=0||!safe.Contains(raw);
            Vector2 direction=point.z>0?raw-center:new Vector2(local.x,-local.y);
            if(direction.sqrMagnitude<.01f)direction=Vector2.right;
            Vector2 location=raw;
            if(offscreen)
            {
                direction.Normalize();
                float tx=direction.x>0?(safe.xMax-center.x)/direction.x:direction.x<0?(safe.xMin-center.x)/direction.x:float.PositiveInfinity;
                float ty=direction.y>0?(safe.yMax-center.y)/direction.y:direction.y<0?(safe.yMin-center.y)/direction.y:float.PositiveInfinity;
                location=center+direction*Mathf.Min(tx,ty);
                Vector2 tangent=new Vector2(-direction.y,direction.x),tip=location+direction*9;
                FlightHudGraphics.Line(tip,location-direction*5+tangent*6,FlightHudGraphics.Amber,2);
                FlightHudGraphics.Line(tip,location-direction*5-tangent*6,FlightHudGraphics.Amber,2);
            }
            else FlightHudGraphics.Diamond(location,12,FlightHudGraphics.Amber);
            float labelX=Mathf.Clamp(location.x-108,38,Width-260),labelY=location.y+17;
            Text(new Rect(labelX,labelY,216,21),zone!=null?zone.DisplayName:"TRAINING TARGET",hudCenter,FlightHudGraphics.Amber);
            float distance=Vector3.Distance(Aircraft.transform.position,world);
            Text(new Rect(labelX,labelY+22,216,21),(distance>=1000?$"{distance/1000:0.00} km":$"{distance:0} m")+(point.z<0?"  /  BEHIND":""),hudCenter,FlightHudGraphics.Paper);
            if(Missions.DwellProgress>0)Bar(new Rect(labelX+53,labelY+47,110,3),Missions.DwellProgress,FlightHudGraphics.Amber);
        }
        void DrawDebug()
        {
            Box(new Rect(25,231,440,240),.95f);var b=Aircraft.Body;
            GUI.Label(new Rect(40,240,410,225),$"DEVELOPMENT / fixed {Time.fixedDeltaTime:0.000}s\nVelocity  {b.linearVelocity.ToString("F2")} m/s\nAngular  {Aircraft.LocalAngularRatesDegrees.ToString("F1")} deg/s\nRaw  {Aircraft.RawCommand.Cyclic} yaw {Aircraft.RawCommand.Yaw:0.00}\nAssisted  {Aircraft.AssistedCommand.Cyclic} yaw {Aircraft.AssistedCommand.Yaw:0.00}\nLift  {Aircraft.LiftNewtons:0} N   Mass  {b.mass:0} kg\nRotor  {Aircraft.RotorRpm:0} rpm   Grounded  {Aircraft.Grounded}\nTouchdown  {Aircraft.LastTouchdownSpeed:0.00} m/s\nGround speed {Aircraft.GroundSpeed:0.0} m/s",small);
        }
        void DrawMenu()
        {
            menuIndex=0;
            float left=Width/2-450;
            Box(new Rect(0,0,Width,Height),.72f);Box(new Rect(left,26,900,668),.98f);
            FlightHudGraphics.Fill(new Rect(left,26,900,3),FlightHudGraphics.Amber);
            Text(new Rect(left+26,44,600,22),"MERIDIAN AIR SERVICE  /  OPERATIONS",hudSmall,FlightHudGraphics.Amber);
            GUI.Label(new Rect(left+26,73,600,40),"Flight desk",title);
            Text(new Rect(left+654,76,220,25),"FLIGHT PAUSED",hudRight,FlightHudGraphics.Phosphor);
            GUI.Label(new Rect(left+26,116,850,25),"D-pad / stick  Navigate      Left / right  Adjust      A  Select      B / Escape  Resume",small);
            GUILayout.BeginArea(new Rect(left+24,151,852,518));
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
                GUILayout.Space(9);GUILayout.Label(ReadableName(action.name),label);
                for(int i=0;i<action.bindings.Count;i++)
                {
                    var binding=action.bindings[i];if(binding.isComposite)continue;int index=i;var id=action.id;
                    GUI.enabled=!Input.IsRebinding;
                    if(MenuButton((binding.isPartOfComposite?ReadableName(binding.name)+": ":"")+action.GetBindingDisplayString(i)))Input.BeginRebind(id,index,_=>Save());
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
            if(id==menuFocus)GUI.backgroundColor=new Color(1.28f,1.4f,1.1f);
            return previous;
        }
        bool MenuActivate(int id)=>id==menuFocus&&menuActivate&&GUI.enabled&&Event.current.type==EventType.Layout;
        int MenuAdjustment(int id)=>id==menuFocus&&GUI.enabled&&Event.current.type==EventType.Layout?menuAdjust:0;
        void TrackMenuControl(int id)
        {
            if(id==menuFocus&&Event.current.type==EventType.Repaint)
            {
                var focusedRect=GUILayoutUtility.GetLastRect();FlightHudGraphics.Frame(focusedRect,new Color(.83f,.96f,.70f,.60f));
                FlightHudGraphics.Fill(new Rect(focusedRect.x,focusedRect.y,3,focusedRect.height),FlightHudGraphics.Amber);
            }
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
            int id=menuIndex++;Color color=MenuHighlight(id);bool result=GUILayout.Toggle(state,(state?"ON    ":"OFF   ")+text,toggle);GUI.backgroundColor=color;TrackMenuControl(id);
            if(MenuActivate(id)){menuActivate=false;result=!result;}else if(MenuAdjustment(id)!=0)result=menuAdjust>0;
            return result;
        }
        int MenuToolbar(int selected,string[] options)
        {
            int id=menuIndex++;Color color=MenuHighlight(id);int result=GUILayout.Toolbar(selected,options,tabButton);GUI.backgroundColor=color;TrackMenuControl(id);
            int adjust=MenuAdjustment(id);if(MenuActivate(id)){menuActivate=false;adjust=1;}
            if(adjust!=0)result=(result+adjust+options.Length)%options.Length;return result;
        }
        void Slider(string caption,ref float number,float min,float max,string format="F2")
        {
            int id=menuIndex++;GUILayout.Label((id==menuFocus?"› ":"")+caption+"   "+number.ToString(format),small);Color color=MenuHighlight(id);
            number=GUILayout.HorizontalSlider(number,min,max,sliderTrack,sliderThumb);GUI.backgroundColor=color;TrackMenuControl(id);
            number=Mathf.Clamp(number+MenuAdjustment(id)*(max-min)/40f,min,max);GUILayout.Space(5);
        }
        static string ReadableName(string text)
        {
            if(string.IsNullOrEmpty(text))return "Control";
            var result=new System.Text.StringBuilder();
            for(int i=0;i<text.Length;i++){if(i>0&&char.IsUpper(text[i])&&!char.IsUpper(text[i-1]))result.Append(' ');result.Append(i==0?char.ToUpperInvariant(text[i]):text[i]);}
            return result.ToString();
        }
        void OnDestroy()
        {
            if(Game!=null&&Game.Input!=null)
            {
                Input.PauseRequested-=TogglePause;Input.ResetRequested-=Retry;Input.InteractRequested-=Interact;Input.DebugRequested-=ToggleDebug;Input.AssistRequested-=CycleAssists;Input.HoverRequested-=HoverNotice;
                if(Aircraft!=null)Aircraft.ResetPerformed-=ResetInput;if(Game.Missions!=null)Missions.FeedbackEvent-=MissionFeedback;
            }
            foreach(var texture in new[]{mapTexture,buttonIdle,buttonHover,buttonActive,toggleOff,toggleOn})if(texture!=null)Destroy(texture);
        }
    }
}
