using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>
    /// Everything the HUD shows for one frame: values in the player's units and their formatted text. Refreshed once
    /// per frame in Update, so the several IMGUI events of a frame draw without formatting strings. Each readout is
    /// formatted again only when the value it shows changes, so steady flight allocates nothing here.
    /// </summary>
    public sealed class HudModel
    {
        public struct Warning
        {
            public string Text;
            public Color Color;
        }

        /// <summary>A readout's text, formatted again only when its key (the value as shown) or its variant changes.</summary>
        private sealed class CachedText
        {
            private long key = long.MinValue;
            private int variant = int.MinValue;
            private string source;
            private string text = "";

            public string Get(long newKey, int newVariant, Func<long, int, string> format)
            {
                if (newKey != key || newVariant != variant)
                {
                    key = newKey;
                    variant = newVariant;
                    text = format(newKey, newVariant);
                }
                return text;
            }

            public string Get<T>(long newKey, int newVariant, T state, Func<T, string> format)
            {
                if (newKey != key || newVariant != variant)
                {
                    key = newKey;
                    variant = newVariant;
                    text = format(state);
                }
                return text;
            }

            /// <summary>Text derived from another string, rebuilt only when that string changes.</summary>
            public string Get(string from, Func<string, string> format)
            {
                if (!ReferenceEquals(from, source))
                {
                    source = from;
                    text = format(from);
                }
                return text;
            }
        }

        /// <summary>The hover display appears below this height and ground speed, or this close to the target pad.</summary>
        public const float HoverDisplayHeight = 40f, HoverDisplaySpeed = 8f, HoverDisplayPadRange = 60f;
        private const float DefaultServiceSpeed = 0.8f, ServiceVerticalSpeed = 0.5f, ServiceTilt = 8f;
        // Mission text is rebuilt by the director on every read; ten times a second keeps the dwell countdown current.
        private const float MissionTextInterval = 0.1f, KeyHintInterval = 2f;
        // Tape readouts: tenths below 10, whole numbers from 10.
        private const long TapeWhole = 1L << 40;

        private static readonly Color Red = new Color(1f, .3f, .24f);
        private static readonly string[] Numbers = BuildNumbers(2000, "0");
        private static readonly string[] Headings = BuildNumbers(360, "000");
        private static readonly string[] PadNumbers = BuildNumbers(100, "00");

        private readonly FlightHUD hud;
        private Vector3 previousGroundVelocity;
        private Vector2 smoothedAcceleration;
        private float windPeak, missionTextAt, keyHintsAt;
        private bool hasPreviousVelocity, crashTextReady;
        private int keyHintsFor = -1;
        private UnitSystem crashUnits;
        private readonly CachedText heading = new CachedText(), recording = new CachedText(), shiftValue = new CachedText(),
            earnings = new CachedText(), training = new CachedText(), jobClock = new CachedText(), speedTape = new CachedText(),
            heightTape = new CachedText(), speedReadout = new CachedText(), heightReadout = new CachedText(), groundSpeed = new CachedText(),
            verticalSpeed = new CachedText(), verticalSpeedLine = new CachedText(), collective = new CachedText(), torque = new CachedText(),
            rotor = new CachedText(), wind = new CachedText(), windPeakText = new CachedText(), pad = new CachedText(), drift = new CachedText(),
            rotorLine = new CachedText(), assists = new CachedText(), context = new CachedText(), lowRotor = new CachedText(),
            overspeed = new CachedText(), overtorque = new CachedText(), targetDistance = new CachedText(), targetBehind = new CachedText();
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
        public bool HoverVisible, PadVisible, ServiceReady, HoverHoldEngaged;
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
            HeadingText = heading.Get(UnitFormat.Round(aircraft.Heading), 0, static (k, _) => $"{k:000}°");
            FlightRecorder recorder = hud.Game.Recorder;
            long recorded = recorder != null && recorder.IsRecording ? Mathf.FloorToInt(Time.time - recorder.RecordingStartTime) : -1;
            RecordingText = recording.Get(recorded, 0, static (k, _) => k < 0 ? "" : "● REC  " + TimeText(k));
            RefreshFlight(aircraft);
            RefreshPower(aircraft);
            RefreshWind(aircraft);
            RefreshHover(aircraft, missions);
            RefreshStatus(aircraft, missions);
            RefreshWarnings(aircraft);
            RefreshTarget(aircraft, missions);
            RefreshCrash(aircraft);
            RefreshKeyHints(missions);
            DebugText = hud.DebugVisible ? BuildDebug(aircraft) : "";
        }

        private void RefreshMission(MissionDirector missions)
        {
            ModeHeader = missions.Mode == GameMode.DeliveryShift ? "PORT MERIDIAN  /  DELIVERY SHIFT"
                : missions.Mode == GameMode.Training ? "PORT MERIDIAN  /  FLIGHT TRAINING" : "PORT MERIDIAN  /  FREE FLIGHT";
            if (Time.unscaledTime >= missionTextAt || hud.Paused)
            {
                missionTextAt = Time.unscaledTime + MissionTextInterval;
                Objective = missions.CurrentObjective;
                Status = missions.Mode == GameMode.FreeFlight ? "" : missions.StatusText;
            }
            bool shift = missions.Mode == GameMode.DeliveryShift;
            ShiftCaption = shift ? "SHIFT REMAINING" : "";
            ShiftValue = shiftValue.Get(shift ? Mathf.FloorToInt(Mathf.Max(0, missions.RemainingSeconds)) : -1, 0,
                static (k, _) => k < 0 ? "" : TimeText(k));
            EarningsLine = earnings.Get(shift ? UnitFormat.Round(missions.Earnings) : -1, missions.DeliveriesThisShift,
                static (k, deliveries) => k < 0 ? "" : $"${k}  /  {deliveries:00} DELIVERIES");
            TrainingLine = training.Get(missions.Mode == GameMode.Training ? missions.TrainingName : "", static name => name.ToUpperInvariant());
            MissionSession job = missions.CurrentMission;
            bool flying = shift && job != null && (job.State == MissionState.Accepted || job.State == MissionState.Pickup || job.State == MissionState.Transport);
            if (!flying) { JobClock = ""; return; }
            float elapsed = job.ElapsedSeconds, target = job.Contract.ExpectedSeconds, deadline = job.Contract.DeadlineSeconds;
            JobClock = jobClock.Get(Mathf.FloorToInt(Mathf.Max(0, elapsed)), Mathf.FloorToInt(Mathf.Max(0, target)),
                static (e, t) => $"JOB {TimeText(e)}  /  PAR {TimeText(t)}");
            JobClockColor = elapsed > deadline * 0.8f ? Red : elapsed > target ? FlightHudGraphics.Amber : FlightHudGraphics.Phosphor;
        }

        private void RefreshFlight(HelicopterController aircraft)
        {
            int units = (int)Units;
            SpeedValue = UnitFormat.Speed(aircraft.HorizontalAirspeed, Units);
            HeightValue = UnitFormat.Height(aircraft.AltitudeAGL, Units);
            SpeedUnit = UnitFormat.SpeedUnit(Units);
            HeightUnit = UnitFormat.HeightUnit(Units);
            SpeedTapeReadout = speedTape.Get(TapeKey(SpeedValue), 0, static (k, _) => TapeText(k));
            HeightTapeReadout = heightTape.Get(TapeKey(HeightValue), 0, static (k, _) => TapeText(k));
            SpeedReadout = speedReadout.Get(UnitFormat.SpeedKey(aircraft.HorizontalAirspeed, Units), units,
                static (k, u) => UnitFormat.SpeedText(k, (UnitSystem)u));
            HeightReadout = heightReadout.Get(UnitFormat.HeightKey(aircraft.AltitudeAGL, Units), units,
                static (k, u) => UnitFormat.HeightText(k, (UnitSystem)u));
            GroundSpeedText = groundSpeed.Get(UnitFormat.SpeedKey(aircraft.GroundSpeed, Units), units,
                static (k, u) => "GS  " + UnitFormat.SpeedText(k, (UnitSystem)u));
            long vertical = UnitFormat.VerticalSpeedKey(aircraft.VerticalSpeed, Units);
            VerticalSpeedText = verticalSpeed.Get(vertical, units, static (k, u) => UnitFormat.VerticalSpeedText(k, (UnitSystem)u));
            VerticalSpeedLine = verticalSpeedLine.Get(vertical, units, static (k, u) => "V/S  " + UnitFormat.VerticalSpeedText(k, (UnitSystem)u));
            VerticalSpeedCaution = aircraft.VerticalSpeed < -4f && aircraft.AltitudeAGL < 15f;
        }

        private static long TapeKey(float value) => value < 10f ? UnitFormat.Round(value * 10.0) : TapeWhole + UnitFormat.Round(value);
        private static string TapeText(long key) => key >= TapeWhole ? (key - TapeWhole).ToString() : (key / 10.0).ToString("0.0");

        private void RefreshPower(HelicopterController aircraft)
        {
            // What the aircraft flies: the pilot's lever in normal play, and right under any other input source.
            Collective = aircraft.RawCommand.Collective;
            HoverCollective = aircraft.HoverCollective;
            GroundEffectShown = aircraft.Realism.GroundEffect && aircraft.HoverCollectiveHere < HoverCollective - 0.002f;
            HoverCollectiveInGroundEffect = aircraft.HoverCollectiveHere;
            CollectiveText = collective.Get(UnitFormat.Round(Collective * 1000.0), 0, static (k, _) => $"COLLECTIVE  {k / 10.0:0.0}%");
            PowerLimits = aircraft.Realism.PowerLimits;
            Torque = aircraft.TorqueFraction;
            RotorSpeed = aircraft.RotorSpeed01;
            TorqueText = torque.Get(UnitFormat.Round(Torque * 100.0), 0, static (k, _) => $"TQ  {k}%");
            RotorText = rotor.Get(UnitFormat.Round(RotorSpeed * 100.0), 0, static (k, _) => $"NR  {k}%");
        }

        private void RefreshWind(HelicopterController aircraft)
        {
            Vector3 air = aircraft.CurrentWind;
            float speed = new Vector2(air.x, air.z).magnitude, dt = Time.deltaTime;
            // A slowly decaying peak shows the gusts around the mean.
            windPeak = Mathf.Max(speed, windPeak - Mathf.Max(0f, dt) * 0.4f);
            // Shown while the air is moving; a leftover peak alone (for example after leaving a windy drill) is not wind.
            WindVisible = speed > 0.5f;
            if (!WindVisible) { WindText = WindPeakText = ""; return; }
            float from = Mathf.Atan2(-air.x, -air.z) * Mathf.Rad2Deg;
            WindFromRelative = Mathf.DeltaAngle(aircraft.Heading, from);
            WindText = wind.Get(UnitFormat.SpeedKey(speed, Units), (int)Units,
                static (k, u) => $"WIND  {k} {UnitFormat.SpeedUnit((UnitSystem)u)}");
            WindPeakText = windPeakText.Get(windPeak > speed + 1f ? UnitFormat.SpeedKey(windPeak, Units) : -1, (int)Units,
                static (k, _) => k < 0 ? "" : $"PEAK  {k}");
        }

        private void RefreshHover(HelicopterController aircraft, MissionDirector missions)
        {
            Rigidbody body = aircraft.Body;
            if (body == null) { HoverVisible = false; return; }
            Vector3 velocity = body.linearVelocity, ground = new Vector3(velocity.x, 0f, velocity.z);
            float headingRadians = aircraft.Heading * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(headingRadians), 0f, Mathf.Cos(headingRadians)), right = new Vector3(forward.z, 0f, -forward.x);
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
            Vector3? padOfInterest = PadOfInterest(missions, position);
            PadVisible = padOfInterest.HasValue;
            float padDistance = float.PositiveInfinity;
            if (padOfInterest.HasValue)
            {
                Vector3 offset = padOfInterest.Value - position;
                offset.y = 0f;
                padDistance = offset.magnitude;
                PadOffset = new Vector2(Vector3.Dot(offset, right), Vector3.Dot(offset, forward));
                PadText = pad.Get(UnitFormat.ShortDistanceKey(padDistance, Units), (int)Units,
                    static (k, u) => "PAD  " + UnitFormat.ShortDistanceText(k, (UnitSystem)u));
            }
            else PadText = "";
            HoverVisible = !aircraft.Crashed && (aircraft.AltitudeAGL < HoverDisplayHeight && aircraft.GroundSpeed < HoverDisplaySpeed
                || padDistance < HoverDisplayPadRange && aircraft.AltitudeAGL < HoverDisplayHeight * 2f);
            MissionSession job = missions.CurrentMission;
            ServiceSpeed = missions.Mode == GameMode.DeliveryShift && job != null ? job.Contract.MaximumGroundSpeed : DefaultServiceSpeed;
            float tilt = Vector3.Angle(body.rotation * Vector3.up, Vector3.up);
            ServiceReady = aircraft.GroundSpeed <= ServiceSpeed && Mathf.Abs(aircraft.VerticalSpeed) <= ServiceVerticalSpeed && tilt <= ServiceTilt;
            DriftText = drift.Get(UnitFormat.DriftKey(aircraft.GroundSpeed, Units), (int)Units,
                static (k, u) => "DRIFT  " + UnitFormat.DriftText(k, (UnitSystem)u));
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
            int payload = (int)UnitFormat.Round(aircraft.PayloadKg);
            RotorLine = aircraft.Realism.PowerLimits
                ? rotorLine.Get(UnitFormat.Round(aircraft.RotorSpeed01 * 100.0) * 100000 + UnitFormat.Round(aircraft.TorqueFraction * 100.0), payload * 2 + 1,
                    static (k, v) => $"NR  {k / 100000}%   TQ  {k % 100000}%   /   {v / 2} kg")
                : rotorLine.Get(UnitFormat.Round(aircraft.RotorRpm), payload * 2, static (k, v) => $"ROTOR  {k} rpm   /   {v / 2} kg");
            AssistSettings aids = aircraft.Assists;
            HoverHoldEngaged = aircraft.HoverHold.Engaged;
            int flags = (aids.RateStabilization ? 1 : 0) | (aids.AutoLevel ? 2 : 0) | (aids.YawStabilization ? 4 : 0)
                | (aids.TorqueCompensation ? 8 : 0) | (aids.AttitudeCommand ? 16 : 0) | (HoverHoldEngaged ? 32 : 0);
            AssistsLine = assists.Get(flags, 0, static (k, _) => AssistsText((int)k));
            ContextLine = missions.Mode == GameMode.DeliveryShift
                ? context.Get(UnitFormat.Round(missions.ComfortPercent) * 1000 + UnitFormat.Round(missions.CargoConditionPercent), 1,
                    static (k, _) => $"COMFORT {k / 1000}%   CARGO {k % 1000}%")
                : context.Get(aircraft.Realism.SummaryKey, 2, aircraft.Realism, static realism => "REALISM  /  " + realism.Summary);
        }

        private static string AssistsText(int flags)
        {
            string aids = ((flags & 1) != 0 ? "RATE  " : "") + ((flags & 2) != 0 ? "LEVEL  " : "") + ((flags & 4) != 0 ? "YAW  " : "")
                + ((flags & 8) != 0 ? "TORQUE  " : "") + ((flags & 16) != 0 ? "ATT  " : "") + ((flags & 32) != 0 ? "HOLD" : "");
            aids = aids.TrimEnd();
            return "ASSIST  /  " + (aids.Length == 0 ? "OFF" : aids);
        }

        /// <summary>Cockpit caution stack: only the conditions the active realism can produce.</summary>
        private void RefreshWarnings(HelicopterController aircraft)
        {
            Warnings.Clear();
            if (aircraft.Crashed) return;
            bool flash = Mathf.Repeat(Time.unscaledTime, .8f) < .5f;
            long rotorPercent = UnitFormat.Round(aircraft.RotorSpeed01 * 100.0);
            if (aircraft.EngineFailed) Add("ENGINE FAILURE  ·  AUTOROTATE", Red);
            if (aircraft.TailRotorFailed) Add("TAIL ROTOR FAILURE", Red);
            if (aircraft.Realism.PowerLimits && aircraft.LowRotorSpeed && !aircraft.Grounded && flash)
                Add(lowRotor.Get(rotorPercent, 0, static (k, _) => $"LOW ROTOR RPM  {k}%"), Red);
            if (aircraft.Realism.PowerLimits && aircraft.RotorOverspeed)
                Add(overspeed.Get(rotorPercent, 0, static (k, _) => $"ROTOR OVERSPEED  {k}%"), FlightHudGraphics.Amber);
            if (aircraft.Realism.PowerLimits && aircraft.Overtorque)
                Add(overtorque.Get(UnitFormat.Round(aircraft.TorqueFraction * 100.0), 0, static (k, _) => $"OVERTORQUE  {k}%"), FlightHudGraphics.Amber);
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
            TargetDistance = targetDistance.Get(UnitFormat.DistanceKey(distance, Units), (int)Units,
                static (k, u) => UnitFormat.DistanceText(k, (UnitSystem)u));
            TargetDistanceBehind = targetBehind.Get(TargetDistance, static text => text + "  /  BEHIND");
        }

        public static Vector3 TargetWorldPosition(MissionDirector missions)
            => missions.TargetZone != null ? missions.TargetZone.transform.position + Vector3.up * 3f : missions.ObjectivePosition;

        private void RefreshCrash(HelicopterController aircraft)
        {
            if (!aircraft.Crashed) { crashTextReady = false; return; }
            // The report describes one crash; build it when the crash happens (or the units change), not every frame.
            if (crashTextReady && crashUnits == Units) return;
            crashTextReady = true;
            crashUnits = Units;
            CrashCause cause = aircraft.LastCrashCause;
            CrashTitle = CrashReport.Title(cause);
            CrashDetail = CrashReport.Detail(cause, aircraft.CrashValue, aircraft.CrashLimit, aircraft.CrashObstacle, Units);
            CrashAdvice = CrashReport.Advice(cause, Units);
        }

        /// <summary>Key hints follow the bindings and the device used last; rebuilding them allocates, so only on a change.</summary>
        private void RefreshKeyHints(MissionDirector missions)
        {
            int hintsFor = (int)missions.Mode * 2 + (hud.Input.LastInputWasGamepad ? 1 : 0);
            // Bindings change in the Flight Desk: refresh while paused, and every few seconds in case.
            if (hintsFor == keyHintsFor && Time.unscaledTime < keyHintsAt && !hud.Paused) return;
            keyHintsFor = hintsFor;
            keyHintsAt = Time.unscaledTime + KeyHintInterval;
            FooterHint = BuildFooterHint(missions);
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
