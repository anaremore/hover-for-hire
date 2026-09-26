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

        /// <summary>The hover display appears below this height and ground speed, or this close to the target pad.</summary>
        public const float HoverDisplayHeight = 40f, HoverDisplaySpeed = 8f, HoverDisplayPadRange = 60f;
        private const float DefaultServiceSpeed = 0.8f, ServiceVerticalSpeed = 0.5f, ServiceTilt = 8f;

        private static readonly Color Red = new Color(1f, .3f, .24f);
        private static readonly string[] Numbers = BuildNumbers(2000, "0");
        private static readonly string[] Headings = BuildNumbers(360, "000");
        private static readonly string[] PadNumbers = BuildNumbers(100, "00");

        private readonly FlightHUD hud;
        private Vector3 previousGroundVelocity;
        private Vector2 smoothedAcceleration;
        private float windPeak;
        private bool hasPreviousVelocity;
        public readonly List<Warning> Warnings = new List<Warning>();

        public UnitSystem Units { get; private set; }
        public bool Cockpit { get; private set; }

        // Mission panel and the right-hand job information.
        public string ModeHeader = "", Objective = "", Status = "", ShiftCaption = "", ShiftValue = "", EarningsLine = "", TrainingLine = "", JobClock = "";
        public Color JobClockColor = FlightHudGraphics.Phosphor;
        // Compass.
        public string HeadingText = "", RecordingText = "";
        // Tapes: airspeed is horizontal speed through the air; ground speed is shown beside it.
        public float SpeedValue, HeightValue;
        public string SpeedUnit = "", HeightUnit = "", SpeedTapeReadout = "", HeightTapeReadout = "", GroundSpeedText = "";
        public string SpeedReadout = "", HeightReadout = "", VerticalSpeedText = "", VerticalSpeedLine = "";
        public bool VerticalSpeedCaution;
        // Collective and power.
        public float Collective, HoverCollective, HoverCollectiveInGroundEffect;
        public bool GroundEffectShown, PowerLimits;
        public float Torque, RotorSpeed;
        public string CollectiveText = "", TorqueText = "", RotorText = "";
        // Wind, relative to the nose.
        public bool WindVisible;
        public float WindFromRelative;
        public string WindText = "", WindPeakText = "";
        // Hover display (heading-up): ground drift and its trend in m/s, pad offset in metres.
        public bool HoverVisible, PadVisible, ServiceReady;
        public Vector2 Drift, DriftTrend, PadOffset;
        public float ServiceSpeed = DefaultServiceSpeed;
        public string DriftText = "", PadText = "";
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
            RefreshFlight(aircraft);
            RefreshPower(aircraft);
            RefreshWind(aircraft);
            RefreshHover(aircraft, missions);
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
            Status = missions.Mode == GameMode.FreeFlight ? "" : missions.StatusText;
            bool shift = missions.Mode == GameMode.DeliveryShift;
            ShiftCaption = shift ? "SHIFT REMAINING" : "";
            ShiftValue = shift ? TimeText(missions.RemainingSeconds) : "";
            EarningsLine = shift ? $"${missions.Earnings:0}  /  {missions.DeliveriesThisShift:00} DELIVERIES" : "";
            TrainingLine = missions.Mode == GameMode.Training ? missions.TrainingName.ToUpperInvariant() : "";
            MissionSession job = missions.CurrentMission;
            bool flying = shift && job != null && (job.State == MissionState.Accepted || job.State == MissionState.Pickup || job.State == MissionState.Transport);
            if (!flying) { JobClock = ""; return; }
            float elapsed = job.ElapsedSeconds, target = job.Contract.ExpectedSeconds, deadline = job.Contract.DeadlineSeconds;
            JobClock = $"JOB {TimeText(elapsed)}  /  PAR {TimeText(target)}";
            JobClockColor = elapsed > deadline * 0.8f ? Red : elapsed > target ? FlightHudGraphics.Amber : FlightHudGraphics.Phosphor;
        }

        private void RefreshFlight(HelicopterController aircraft)
        {
            SpeedValue = UnitFormat.Speed(aircraft.HorizontalAirspeed, Units);
            HeightValue = UnitFormat.Height(aircraft.AltitudeAGL, Units);
            SpeedUnit = UnitFormat.SpeedUnit(Units);
            HeightUnit = UnitFormat.HeightUnit(Units);
            SpeedTapeReadout = SpeedValue < 10f ? SpeedValue.ToString("0.0") : SpeedValue.ToString("0");
            HeightTapeReadout = HeightValue < 10f ? HeightValue.ToString("0.0") : HeightValue.ToString("0");
            SpeedReadout = $"{SpeedValue:0} {SpeedUnit}";
            HeightReadout = UnitFormat.FormatHeight(aircraft.AltitudeAGL, Units);
            GroundSpeedText = "GS  " + UnitFormat.FormatSpeed(aircraft.GroundSpeed, Units);
            VerticalSpeedText = UnitFormat.FormatVerticalSpeed(aircraft.VerticalSpeed, Units);
            VerticalSpeedLine = "V/S  " + VerticalSpeedText;
            VerticalSpeedCaution = aircraft.VerticalSpeed < -4f && aircraft.AltitudeAGL < 15f;
        }

        private void RefreshPower(HelicopterController aircraft)
        {
            Collective = hud.Input.Command.Collective;
            HoverCollective = aircraft.HoverCollective;
            GroundEffectShown = aircraft.Realism.GroundEffect && aircraft.HoverCollectiveHere < HoverCollective - 0.002f;
            HoverCollectiveInGroundEffect = aircraft.HoverCollectiveHere;
            CollectiveText = $"COLLECTIVE  {Collective * 100f:0.0}%";
            PowerLimits = aircraft.Realism.PowerLimits;
            Torque = aircraft.TorqueFraction;
            RotorSpeed = aircraft.RotorSpeed01;
            TorqueText = $"TQ  {Torque * 100f:0}%";
            RotorText = $"NR  {RotorSpeed * 100f:0}%";
        }

        private void RefreshWind(HelicopterController aircraft)
        {
            Vector3 wind = aircraft.CurrentWind;
            float speed = new Vector2(wind.x, wind.z).magnitude, dt = Time.deltaTime;
            // A slowly decaying peak shows the gusts around the mean.
            windPeak = Mathf.Max(speed, windPeak - Mathf.Max(0f, dt) * 0.4f);
            // Shown while the air is moving; a leftover peak alone (for example after leaving a windy drill) is not wind.
            WindVisible = speed > 0.5f;
            if (!WindVisible) { WindText = WindPeakText = ""; return; }
            float from = Mathf.Atan2(-wind.x, -wind.z) * Mathf.Rad2Deg;
            WindFromRelative = Mathf.DeltaAngle(aircraft.Heading, from);
            WindText = $"WIND  {UnitFormat.Speed(speed, Units):0} {UnitFormat.SpeedUnit(Units)}";
            WindPeakText = windPeak > speed + 1f ? $"PEAK  {UnitFormat.Speed(windPeak, Units):0}" : "";
        }

        private void RefreshHover(HelicopterController aircraft, MissionDirector missions)
        {
            Rigidbody body = aircraft.Body;
            if (body == null) { HoverVisible = false; return; }
            Vector3 velocity = body.linearVelocity, ground = new Vector3(velocity.x, 0f, velocity.z);
            float heading = aircraft.Heading * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading)), right = new Vector3(forward.z, 0f, -forward.x);
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                Vector3 change = hasPreviousVelocity ? (ground - previousGroundVelocity) / dt : Vector3.zero;
                Vector2 local = new Vector2(Vector3.Dot(change, right), Vector3.Dot(change, forward));
                smoothedAcceleration = Vector2.Lerp(smoothedAcceleration, local, 1f - Mathf.Exp(-dt / 0.3f));
                previousGroundVelocity = ground;
                hasPreviousVelocity = true;
            }
            Drift = new Vector2(Vector3.Dot(ground, right), Vector3.Dot(ground, forward));
            DriftTrend = Drift + smoothedAcceleration;

            Vector3 position = aircraft.transform.position;
            Vector3? pad = PadOfInterest(missions, position);
            PadVisible = pad.HasValue;
            float padDistance = float.PositiveInfinity;
            if (pad.HasValue)
            {
                Vector3 offset = pad.Value - position;
                offset.y = 0f;
                padDistance = offset.magnitude;
                PadOffset = new Vector2(Vector3.Dot(offset, right), Vector3.Dot(offset, forward));
                PadText = "PAD  " + UnitFormat.FormatShortDistance(padDistance, Units);
            }
            else PadText = "";
            HoverVisible = !aircraft.Crashed && (aircraft.AltitudeAGL < HoverDisplayHeight && aircraft.GroundSpeed < HoverDisplaySpeed
                || padDistance < HoverDisplayPadRange && aircraft.AltitudeAGL < HoverDisplayHeight * 2f);
            MissionSession job = missions.CurrentMission;
            ServiceSpeed = missions.Mode == GameMode.DeliveryShift && job != null ? job.Contract.MaximumGroundSpeed : DefaultServiceSpeed;
            float tilt = Vector3.Angle(body.rotation * Vector3.up, Vector3.up);
            ServiceReady = aircraft.GroundSpeed <= ServiceSpeed && Mathf.Abs(aircraft.VerticalSpeed) <= ServiceVerticalSpeed && tilt <= ServiceTilt;
            DriftText = "DRIFT  " + UnitFormat.FormatDriftSpeed(aircraft.GroundSpeed, Units);
        }

        /// <summary>The pad the pilot is working with: the mission target, else the nearest pad within range.</summary>
        private Vector3? PadOfInterest(MissionDirector missions, Vector3 position)
        {
            if (missions.TargetZone != null) return missions.TargetZone.transform.position;
            if (missions.Mode == GameMode.Training) return missions.ObjectivePosition;
            LandingZone[] zones = hud.Game.Zones;
            if (zones == null) return null;
            Vector3? best = null;
            float bestDistance = HoverDisplayPadRange;
            foreach (LandingZone zone in zones)
            {
                if (zone == null) continue;
                Vector3 p = zone.transform.position;
                float distance = new Vector2(p.x - position.x, p.z - position.z).magnitude;
                if (distance < bestDistance) { bestDistance = distance; best = p; }
            }
            return best;
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
            ContextLine = missions.Mode == GameMode.DeliveryShift
                ? $"COMFORT {missions.ComfortPercent:0}%   CARGO {missions.CargoConditionPercent:0}%"
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
            CrashDetail = CrashReport.Detail(cause, aircraft.CrashValue, aircraft.CrashLimit, aircraft.CrashObstacle, Units);
            CrashAdvice = CrashReport.Advice(cause, Units);
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
                $"Hover collective {aircraft.HoverCollective * 100f:0.0}% (here {aircraft.HoverCollectiveHere * 100f:0.0}%)   " +
                $"Recording {(recorder != null && recorder.IsRecording ? "on" : "off")} (F3)";
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
