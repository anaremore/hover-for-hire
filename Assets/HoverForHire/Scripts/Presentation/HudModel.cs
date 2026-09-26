using System.Collections.Generic;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>
    /// Everything the HUD shows for one frame: values in the player's units and their formatted text. Refreshed once
    /// per frame in Update, so the several IMGUI events of a frame draw without formatting or allocating strings.
    /// </summary>
    public sealed class HudModel
    {
        public struct Warning
        {
            public string Text;
            public Color Color;
        }

        private static readonly Color Red = new Color(1f, .3f, .24f);
        private static readonly string[] Numbers = BuildNumbers(2000, "0");
        private static readonly string[] Headings = BuildNumbers(360, "000");
        private static readonly string[] PadNumbers = BuildNumbers(100, "00");

        private readonly FlightHUD hud;
        public readonly List<Warning> Warnings = new List<Warning>();

        public UnitSystem Units { get; private set; }
        public bool Cockpit { get; private set; }

        // Mission panel.
        public string ModeHeader = "", Objective = "", Status = "", RightCaption = "", RightValue = "", EarningsLine = "", TrainingLine = "";
        // Compass.
        public string HeadingText = "", RecordingText = "";
        // Flight values in display units and their readouts.
        public float SpeedValue, HeightValue;
        public string SpeedUnit = "", HeightUnit = "", SpeedReadout = "", HeightReadout = "", SpeedTapeReadout = "", HeightTapeReadout = "";
        public string VerticalSpeedText = "";
        public bool VerticalSpeedCaution;
        public float Collective, HoverCollective;
        public string CollectiveText = "", CockpitCollectiveLine = "", PayloadLine = "", CockpitAssistsLine = "";
        // Aircraft status panel.
        public string StatusTitle = "", RotorLine = "", AssistsLine = "", ContextLine = "";
        public bool RotorCaution;
        // Target marker.
        public string TargetName = "", TargetDistance = "", TargetDistanceBehind = "";
        // Crash panel and diagnostics.
        public string CrashTitle = "", CrashDetail = "", CrashAdvice = "", RetryLabel = "", DebugText = "", FooterHint = "";

        public HudModel(FlightHUD hud) { this.hud = hud; }

        public static string Number(int value) => value >= 0 && value < Numbers.Length ? Numbers[value] : value.ToString();
        public static string HeadingLabel(int degrees) => Headings[((degrees % 360) + 360) % 360];
        public static string PadNumber(int index) => index >= 0 && index < PadNumbers.Length ? PadNumbers[index] : index.ToString("00");
        public static string TimeText(float seconds) => $"{Mathf.FloorToInt(Mathf.Max(0, seconds) / 60):00}:{Mathf.FloorToInt(Mathf.Max(0, seconds) % 60):00}";

        public void Refresh()
        {
            HelicopterController aircraft = hud.Aircraft;
            MissionDirector missions = hud.Missions;
            if (aircraft == null || missions == null) return;
            Units = hud.Settings != null ? hud.Settings.Units : UnitSystem.Metric;
            Cockpit = hud.Game.CameraRig != null && hud.Game.CameraRig.IsCockpit;

            RefreshMission(missions);
            HeadingText = $"{aircraft.Heading:000}°";
            FlightRecorder recorder = hud.Game.Recorder;
            RecordingText = recorder != null && recorder.IsRecording ? $"● REC  {TimeText(Time.time - recorder.RecordingStartTime)}" : "";

            SpeedValue = UnitFormat.Speed(aircraft.Airspeed, Units);
            HeightValue = UnitFormat.Height(aircraft.AltitudeAGL, Units);
            SpeedUnit = UnitFormat.SpeedUnit(Units);
            HeightUnit = UnitFormat.HeightUnit(Units);
            SpeedReadout = Units == UnitSystem.Aviation ? $"{SpeedValue:0} {SpeedUnit}" : $"{SpeedValue:0.0} {SpeedUnit}";
            HeightReadout = UnitFormat.FormatHeight(aircraft.AltitudeAGL, Units);
            SpeedTapeReadout = SpeedValue < 10f ? SpeedValue.ToString("0.0") : SpeedValue.ToString("0");
            HeightTapeReadout = HeightValue < 10f ? HeightValue.ToString("0.0") : HeightValue.ToString("0");
            VerticalSpeedText = UnitFormat.FormatVerticalSpeed(aircraft.VerticalSpeed, Units);
            VerticalSpeedCaution = aircraft.VerticalSpeed < -4f && aircraft.AltitudeAGL < 15f;

            Collective = hud.Input.Command.Collective;
            HoverCollective = aircraft.HoverCollective;
            CollectiveText = $"COLLECTIVE  {Collective * 100f:0.0}%";
            CockpitCollectiveLine = $"COLLECTIVE  {Collective * 100f:0.0}%   HOVER  {HoverCollective * 100f:0.0}%";
            PayloadLine = $"PAYLOAD  {aircraft.PayloadKg:0} kg";
            CockpitAssistsLine = "ASSIST  /  " + aircraft.Assists.Summary;

            RefreshStatus(aircraft, missions);
            RefreshWarnings(aircraft);
            RefreshTarget(aircraft, missions);
            RefreshCrash(aircraft);
            DebugText = hud.DebugVisible ? BuildDebug(aircraft) : "";
            FooterHint = BuildFooterHint(missions);
        }

        private void RefreshMission(MissionDirector missions)
        {
            ModeHeader = missions.Mode == GameMode.DeliveryShift ? "PORT MERIDIAN  /  DELIVERY SHIFT"
                : missions.Mode == GameMode.Training ? "PORT MERIDIAN  /  FLIGHT TRAINING" : "PORT MERIDIAN  /  FREE FLIGHT";
            Objective = missions.CurrentObjective;
            Status = missions.Mode == GameMode.FreeFlight ? "10 LANDING SITES  /  UNRESTRICTED" : missions.StatusText;
            bool shift = missions.Mode == GameMode.DeliveryShift;
            RightCaption = shift ? "SHIFT REMAINING" : "MERIDIAN AIR SERVICE";
            RightValue = shift ? TimeText(missions.RemainingSeconds) : "M–04";
            EarningsLine = $"${missions.Earnings:0}  /  {missions.DeliveriesThisShift:00} DELIVERIES";
            TrainingLine = missions.Mode == GameMode.Training ? missions.TrainingName.ToUpperInvariant() : "";
        }

        private void RefreshStatus(HelicopterController aircraft, MissionDirector missions)
        {
            StatusTitle = aircraft.Crashed ? "M–04   /   RECOVERY" : aircraft.Grounded ? "M–04   /   ON GROUND" : "M–04   /   AIRBORNE";
            RotorCaution = aircraft.Realism.PowerLimits && (aircraft.LowRotorSpeed || aircraft.Overtorque);
            RotorLine = aircraft.Realism.PowerLimits
                ? $"NR  {aircraft.RotorSpeed01 * 100f:0}%   TQ  {aircraft.TorqueFraction * 100f:0}%   /   {aircraft.PayloadKg:0} kg"
                : $"ROTOR  {aircraft.RotorRpm:0} rpm   /   {aircraft.PayloadKg:0} kg";
            AssistSettings assists = aircraft.Assists;
            string aids = (assists.RateStabilization ? "RATE  " : "") + (assists.AutoLevel ? "LEVEL  " : "")
                + (assists.YawStabilization ? "YAW  " : "") + (assists.TorqueCompensation ? "TORQUE" : "");
            AssistsLine = "ASSIST  /  " + (aids.Length == 0 ? "OFF" : aids);
            Vector3 wind = aircraft.CurrentWind;
            float windSpeed = new Vector2(wind.x, wind.z).magnitude;
            ContextLine = missions.Mode == GameMode.DeliveryShift
                ? $"COMFORT {missions.ComfortPercent:0}%   CARGO {missions.CargoConditionPercent:0}%"
                : windSpeed > .5f
                    ? $"WIND  {Mathf.Repeat(Mathf.Atan2(-wind.x, -wind.z) * Mathf.Rad2Deg, 360f):000}°  {windSpeed * UnitFormat.KnotsPerMetrePerSecond:0} kt   /   {aircraft.Realism.Summary}"
                    : "REALISM  /  " + aircraft.Realism.Summary;
        }

        /// <summary>Cockpit caution stack: only the conditions the active realism can produce.</summary>
        private void RefreshWarnings(HelicopterController aircraft)
        {
            Warnings.Clear();
            if (aircraft.Crashed) return;
            bool flash = Mathf.Repeat(Time.unscaledTime, .8f) < .5f;
            if (aircraft.EngineFailed) Add("ENGINE FAILURE  ·  AUTOROTATE", Red);
            if (aircraft.TailRotorFailed) Add("TAIL ROTOR FAILURE", Red);
            if (aircraft.Realism.PowerLimits && aircraft.LowRotorSpeed && !aircraft.Grounded && flash)
                Add($"LOW ROTOR RPM  {aircraft.RotorSpeed01 * 100f:0}%", Red);
            if (aircraft.Realism.PowerLimits && aircraft.RotorOverspeed) Add($"ROTOR OVERSPEED  {aircraft.RotorSpeed01 * 100f:0}%", FlightHudGraphics.Amber);
            if (aircraft.Realism.PowerLimits && aircraft.Overtorque) Add($"OVERTORQUE  {aircraft.TorqueFraction * 100f:0}%", FlightHudGraphics.Amber);
            if (aircraft.VortexRingSeverity > .3f) Add("SETTLING WITH POWER  ·  FLY OUT FORWARD", FlightHudGraphics.Amber);
        }

        private void Add(string text, Color color) => Warnings.Add(new Warning { Text = text, Color = color });

        private void RefreshTarget(HelicopterController aircraft, MissionDirector missions)
        {
            LandingZone zone = missions.TargetZone;
            if (zone == null && missions.Mode != GameMode.Training) { TargetName = TargetDistance = TargetDistanceBehind = ""; return; }
            Vector3 world = TargetWorldPosition(missions);
            float distance = Vector3.Distance(aircraft.transform.position, world);
            TargetName = zone != null ? zone.DisplayName : "TRAINING TARGET";
            TargetDistance = UnitFormat.FormatDistance(distance, Units);
            TargetDistanceBehind = TargetDistance + "  /  BEHIND";
        }

        public static Vector3 TargetWorldPosition(MissionDirector missions)
            => missions.TargetZone != null ? missions.TargetZone.transform.position + Vector3.up * 3f : missions.ObjectivePosition;

        private void RefreshCrash(HelicopterController aircraft)
        {
            if (!aircraft.Crashed) return;
            CrashCause cause = aircraft.LastCrashCause;
            CrashTitle = CrashReport.Title(cause);
            CrashDetail = CrashReport.Detail(cause, aircraft.CrashValue, aircraft.CrashLimit, aircraft.CrashObstacle);
            CrashAdvice = CrashReport.Advice(cause);
            RetryLabel = $"Retry  /  {hud.Key("Reset")}";
        }

        private string BuildDebug(HelicopterController aircraft)
        {
            Rigidbody b = aircraft.Body;
            FlightRecorder recorder = hud.Game.Recorder;
            return $"DEVELOPMENT / fixed {Time.fixedDeltaTime:0.000}s\nVelocity  {b.linearVelocity.ToString("F2")} m/s\n" +
                $"Angular  {aircraft.LocalAngularRatesDegrees.ToString("F1")} deg/s\nRaw  {aircraft.RawCommand.Cyclic} yaw {aircraft.RawCommand.Yaw:0.00}\n" +
                $"Assisted  {aircraft.AssistedCommand.Cyclic} yaw {aircraft.AssistedCommand.Yaw:0.00}\nLift  {aircraft.LiftNewtons:0} N   Mass  {b.mass:0} kg\n" +
                $"Rotor  {aircraft.RotorRpm:0} rpm   Grounded  {aircraft.Grounded}\nTouchdown  {aircraft.LastTouchdownSpeed:0.00} m/s\n" +
                $"Ground speed {aircraft.GroundSpeed:0.0} m/s   Sideslip {aircraft.SideslipDegrees:0.0}°\n" +
                $"Hover collective {aircraft.HoverCollective * 100f:0.0}%   Recording {(recorder != null && recorder.IsRecording ? "on" : "off")} (F3)";
        }

        private string BuildFooterHint(MissionDirector missions)
        {
            string job = missions.Mode == GameMode.FreeFlight ? "Start shift" : missions.Mode == GameMode.Training ? "Next drill" : "Job";
            return $"{hud.Key("Pause")}  Flight desk    {hud.Key("CollectiveIncrease")} / {hud.Key("CollectiveDecrease")}  Collective    " +
                $"{hud.Key("SwitchCamera")}  Camera    {hud.Key("FreeLook")}  Look    {hud.Key("Interact")}  {job}    {hud.Key("Reset")}  Reset";
        }

        private static string[] BuildNumbers(int count, string format)
        {
            var values = new string[count];
            for (int i = 0; i < count; i++) values[i] = i.ToString(format);
            return values;
        }
    }
}
