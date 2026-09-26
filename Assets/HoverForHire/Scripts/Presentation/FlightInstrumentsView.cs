using UnityEngine;
using Object = UnityEngine.Object;

namespace HoverForHire
{
    /// <summary>
    /// The flight HUD. The chase view keeps the aircraft clear: tapes sit wide, a compact attitude indicator, collective
    /// and power gauges sit in a bottom cluster, and a heading-up hover display takes the chart's place near a pad.
    /// The cockpit view keeps its overlays in the side columns above the instrument panel.
    /// </summary>
    public sealed class FlightInstrumentsView
    {
        private const float TapeCenterY = 330f, TapeHalf = 94f, TapeOffset = 400f, ClusterY = 612f;
        private static readonly Color Green = new Color(.45f, .95f, .55f);
        private static readonly Color Red = new Color(1f, .3f, .24f);
        private static readonly Color Faint = new Color(.8f, .94f, .7f, .22f);

        private readonly FlightHUD hud;
        private readonly GUIContent objectiveMeasure = new GUIContent();
        private Texture2D mapTexture;

        public FlightInstrumentsView(FlightHUD hud) { this.hud = hud; }

        private HudStyles S => hud.Styles;
        private HudModel M => hud.Model;
        private HelicopterController Aircraft => hud.Aircraft;
        private MissionDirector Missions => hud.Missions;
        private float Width => hud.Width;

        public void Draw()
        {
            if (mapTexture == null) mapTexture = BuildMapTexture();
            DrawMission();
            DrawCompass();
            DrawJobInfo();
            DrawWind();
            if (M.Cockpit) DrawCockpitColumn();
            else DrawChaseInstruments();
            DrawCyclicIndicator();
            DrawAircraftStatus();
            if (M.HoverVisible) DrawHoverDisplay();
            else DrawMap();
            DrawTarget();
            S.Text(new Rect(26, 690, 960, 20), M.FooterHint, S.HudSmall, FlightHudGraphics.Muted);
            float stackBottom = DrawWarnings();
            if (hud.NoticeVisible)
            {
                var r = new Rect(Width / 2 - 257, stackBottom + 4, 514, 46);
                S.Box(r, .78f);
                FlightHudGraphics.Fill(new Rect(r.x, r.y, 2, r.height), FlightHudGraphics.Amber);
                GUI.Label(new Rect(r.x + 14, r.y + 7, r.width - 28, 36), hud.Notice, S.Small);
            }
            if (!string.IsNullOrEmpty(Missions.SaveWarning))
            {
                S.Box(new Rect(Width / 2 - 280, 500, 560, 48), .92f);
                GUI.Label(new Rect(Width / 2 - 266, 508, 532, 36), Missions.SaveWarning, S.Small);
            }
            if (Aircraft.Crashed) DrawCrashPanel();
            if (hud.DebugVisible) DrawDebug();
        }

        // ---- Mission, compass, job clock, wind ----

        private void DrawMission()
        {
            // The panel grows with the wrapped objective, so long drill briefs never run into the status line.
            objectiveMeasure.text = M.Objective;
            float objective = Mathf.Max(24, S.Label.CalcHeight(objectiveMeasure, 316));
            bool status = M.Status.Length > 0;
            float height = 39 + objective + (status ? 45 : 10);
            S.Box(new Rect(26, 26, 348, height), .61f);
            FlightHudGraphics.Fill(new Rect(26, 26, 3, height), FlightHudGraphics.Amber);
            S.Text(new Rect(42, 36, 316, 20), M.ModeHeader, S.HudSmall, FlightHudGraphics.Amber);
            GUI.Label(new Rect(42, 62, 316, objective), M.Objective, S.Label);
            if (status) GUI.Label(new Rect(42, 64 + objective, 316, 37), M.Status, S.Small);
        }

        private void DrawCompass()
        {
            float cx = Width / 2, heading = Aircraft.Heading;
            const float half = 186, pixels = 3.2f;
            for (int offset = -70; offset <= 70; offset++)
            {
                int mark = Mathf.FloorToInt(heading) + offset;
                if (mark % 5 != 0) continue;
                float x = cx + Mathf.DeltaAngle(heading, mark) * pixels;
                if (Mathf.Abs(x - cx) > half) continue;
                bool major = mark % 10 == 0;
                FlightHudGraphics.Line(new Vector2(x, 67), new Vector2(x, major ? 78 : 73), FlightHudGraphics.Phosphor);
                if (!major) continue;
                int h = (mark % 360 + 360) % 360;
                string caption = h == 0 ? "N" : h == 90 ? "E" : h == 180 ? "S" : h == 270 ? "W" : HudModel.HeadingLabel(h);
                S.Text(new Rect(x - 21, 44, 42, 22), caption, S.HudCenter);
            }
            FlightHudGraphics.Line(new Vector2(cx - 5, 82), new Vector2(cx, 77), FlightHudGraphics.Paper, 1.6f);
            FlightHudGraphics.Line(new Vector2(cx, 77), new Vector2(cx + 5, 82), FlightHudGraphics.Paper, 1.6f);
            S.Box(new Rect(cx - 35, 88, 70, 27), .48f);
            S.Text(new Rect(cx - 35, 87, 70, 27), M.HeadingText, S.HudCenter, FlightHudGraphics.Paper);
            if (M.RecordingText.Length > 0) S.Text(new Rect(cx - 70, 118, 140, 22), M.RecordingText, S.HudCenter, Red);
        }

        private void DrawJobInfo()
        {
            float right = Width - 244;
            if (M.ShiftCaption.Length > 0)
            {
                S.Text(new Rect(right, 28, 216, 20), M.ShiftCaption, S.HudRight, FlightHudGraphics.Muted);
                S.Text(new Rect(right, 48, 216, 36), M.ShiftValue, S.HudValueRight, FlightHudGraphics.Paper);
                S.Text(new Rect(right, 86, 216, 22), M.EarningsLine, S.HudRight);
                if (M.JobClock.Length > 0) S.Text(new Rect(right - 60, 108, 276, 22), M.JobClock, S.HudRight, M.JobClockColor);
            }
            else if (M.TrainingLine.Length > 0)
            {
                S.Text(new Rect(right, 28, 216, 20), "DRILL", S.HudRight, FlightHudGraphics.Muted);
                S.Text(new Rect(right - 60, 48, 276, 24), M.TrainingLine, S.HudRight, FlightHudGraphics.Paper);
            }
        }

        /// <summary>Wind arrow, heading-up: it enters from the side the wind comes from and points downwind.</summary>
        private void DrawWind()
        {
            if (!M.WindVisible) return;
            var center = new Vector2(Width - 50, M.Cockpit ? 150 : 162);
            const float radius = 18;
            FlightHudGraphics.Circle(center, radius, FlightHudGraphics.Muted, 28);
            FlightHudGraphics.Line(center + Vector2.up * -radius, center + Vector2.up * (-radius - 5), FlightHudGraphics.Paper);
            float angle = M.WindFromRelative * Mathf.Deg2Rad;
            var from = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
            Vector2 tail = center + from * (radius + 6), tip = center - from * (radius - 6);
            FlightHudGraphics.Line(tail, tip, FlightHudGraphics.Amber, 2f);
            Vector2 side = new Vector2(-from.y, from.x);
            FlightHudGraphics.Line(tip, tip + from * 7 + side * 5, FlightHudGraphics.Amber, 2f);
            FlightHudGraphics.Line(tip, tip + from * 7 - side * 5, FlightHudGraphics.Amber, 2f);
            S.Text(new Rect(Width - 300, center.y - 20, 222, 20), M.WindText, S.HudRight);
            if (M.WindPeakText.Length > 0) S.Text(new Rect(Width - 300, center.y + 1, 222, 20), M.WindPeakText, S.HudRight, FlightHudGraphics.Amber);
        }

        // ---- Chase view ----

        private void DrawChaseInstruments()
        {
            float cx = Width / 2;
            bool aviation = M.Units == UnitSystem.Aviation;
            float speedX = cx - TapeOffset, heightX = cx + TapeOffset;
            DrawTape(speedX, M.SpeedValue, M.SpeedTapeReadout, "AIR SPEED", M.SpeedUnit, true, 5, 3.8f);
            DrawTape(heightX, M.HeightValue, M.HeightTapeReadout, "SKID AGL", M.HeightUnit, false,
                aviation ? 10 : 5, aviation ? 3.8f * UnitFormat.MetresPerFoot : 3.8f);
            S.Text(new Rect(speedX + 14, TapeCenterY + 28, 170, 20), M.GroundSpeedText, S.HudSmall);
            S.Text(new Rect(heightX - 184, TapeCenterY + 26, 170, 22), M.VerticalSpeedLine, S.HudRight,
                M.VerticalSpeedCaution ? FlightHudGraphics.Amber : FlightHudGraphics.Phosphor);
            DrawAttitudeIndicator(new Vector2(cx - 250, ClusterY), 40);
            DrawCollective(cx - 195, 568, 180);
            if (M.PowerLimits) DrawPower(cx + 10, 568, 90);
        }

        /// <summary>A moving tape with minor marks every <paramref name="step"/> units and labels every two steps.</summary>
        private void DrawTape(float x, float number, string readoutText, string caption, string unit, bool left, int step, float pixels)
        {
            const float cy = TapeCenterY, half = TapeHalf;
            S.Text(new Rect(x - 54, cy - 122, 108, 22), caption, S.HudCenter, FlightHudGraphics.Muted);
            S.Text(new Rect(x - 44, cy - 101, 88, 20), unit, S.HudCenter);
            int baseMark = Mathf.FloorToInt(number / step) * step;
            int span = Mathf.CeilToInt(half / (pixels * step)) + 1;
            for (int i = -span; i <= span; i++)
            {
                int n = baseMark + i * step;
                if (n < 0) continue;
                float y = cy - (n - number) * pixels;
                if (Mathf.Abs(y - cy) > half) continue;
                bool major = n % (step * 2) == 0;
                FlightHudGraphics.Line(new Vector2(x, y), new Vector2(x + (left ? -1 : 1) * (major ? 14 : 7), y), FlightHudGraphics.Phosphor);
                if (major) S.Text(new Rect(left ? x - 54 : x + 20, y - 10, 36, 20), HudModel.Number(n), left ? S.HudRight : S.HudSmall);
            }
            FlightHudGraphics.Line(new Vector2(x, cy - half), new Vector2(x, cy + half), new Color(.8f, .94f, .7f, .35f));
            var readout = new Rect(left ? x - 100 : x + 6, cy - 21, 94, 43);
            S.Box(readout, .67f);
            FlightHudGraphics.Frame(readout, FlightHudGraphics.Phosphor);
            S.Text(readout, readoutText, S.HudValueCenter, FlightHudGraphics.Paper);
            FlightHudGraphics.Line(new Vector2(x + (left ? -6 : 6), cy - 5), new Vector2(x, cy), FlightHudGraphics.Phosphor);
            FlightHudGraphics.Line(new Vector2(x, cy), new Vector2(x + (left ? -6 : 6), cy + 5), FlightHudGraphics.Phosphor);
        }

        /// <summary>Compact attitude indicator: horizon and pitch marks turn with bank; the aircraft symbol stays fixed.</summary>
        private void DrawAttitudeIndicator(Vector2 center, float radius)
        {
            Quaternion attitude = Aircraft.Body != null ? Aircraft.Body.rotation : Aircraft.transform.rotation;
            Vector3 forward = attitude * Vector3.forward, right = attitude * Vector3.right, up = attitude * Vector3.up;
            float pitch = Mathf.Asin(Mathf.Clamp(forward.y, -1, 1)) * Mathf.Rad2Deg;
            float roll = Mathf.Atan2(right.y, up.y) * Mathf.Rad2Deg;
            const float pixelsPerDegree = 1.6f;
            S.Box(new Rect(center.x - radius - 4, center.y - radius - 4, radius * 2 + 8, radius * 2 + 8), .45f);
            FlightHudGraphics.Circle(center, radius, FlightHudGraphics.Muted, 40);
            // Fixed bank scale at the top: 0, 10, 20, 30 and 45 degrees each side.
            foreach (float bank in new[] { -45f, -30f, -20f, -10f, 0f, 10f, 20f, 30f, 45f })
            {
                float a = bank * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                FlightHudGraphics.Line(center + direction * radius, center + direction * (radius + (bank == 0f ? 7 : 4)), FlightHudGraphics.Muted);
            }
            Matrix4x4 previous = GUI.matrix;
            // Keep in the same logical space as the tapes: a nested GUI clip would add its own pivot offset above 720p.
            GUI.matrix = previous * FlightHudGraphics.RotationAround(center, roll);
            for (int mark = -20; mark <= 20; mark += 10)
            {
                float y = pitch * pixelsPerDegree - mark * pixelsPerDegree;
                if (Mathf.Abs(y) >= radius - 2) continue;
                float halfWidth = mark == 0 ? Mathf.Sqrt(radius * radius - y * y) : (mark % 20 == 0 ? 12 : 7);
                FlightHudGraphics.Line(new Vector2(center.x - halfWidth, center.y + y), new Vector2(center.x + halfWidth, center.y + y),
                    mark == 0 ? FlightHudGraphics.Phosphor : Faint, mark == 0 ? 1.8f : 1.2f);
            }
            // Bank pointer turns with the horizon against the fixed scale.
            FlightHudGraphics.Line(new Vector2(center.x, center.y - radius + 2), new Vector2(center.x - 4, center.y - radius + 9), FlightHudGraphics.Amber, 1.6f);
            FlightHudGraphics.Line(new Vector2(center.x, center.y - radius + 2), new Vector2(center.x + 4, center.y - radius + 9), FlightHudGraphics.Amber, 1.6f);
            GUI.matrix = previous;
            FlightHudGraphics.Line(center + new Vector2(-18, 0), center + new Vector2(-6, 0), FlightHudGraphics.Paper, 1.8f);
            FlightHudGraphics.Line(center + new Vector2(-6, 0), center + new Vector2(0, 5), FlightHudGraphics.Paper, 1.8f);
            FlightHudGraphics.Line(center + new Vector2(0, 5), center + new Vector2(6, 0), FlightHudGraphics.Paper, 1.8f);
            FlightHudGraphics.Line(center + new Vector2(6, 0), center + new Vector2(18, 0), FlightHudGraphics.Paper, 1.8f);
        }

        /// <summary>Collective with the hover setting out of ground effect (amber) and, near the ground, in it (green).</summary>
        private void DrawCollective(float x, float y, float width)
        {
            S.Text(new Rect(x - 5, y, width + 10, 22), M.CollectiveText, S.HudCenter);
            var bar = new Rect(x, y + 32, width, 6);
            S.Bar(bar, M.Collective, FlightHudGraphics.Phosphor);
            for (int i = 0; i <= 4; i++) FlightHudGraphics.Fill(new Rect(x + i * width / 4, bar.y - 4, 1, 14), FlightHudGraphics.Muted);
            float hoverX = x + width * Mathf.Clamp01(M.HoverCollective);
            FlightHudGraphics.Fill(new Rect(hoverX - 1, bar.y - 8, 2, 20), FlightHudGraphics.Amber);
            if (M.GroundEffectShown)
            {
                // In ground effect the hover setting is lower: label it to the left of its tick, the free-air one to the right.
                float groundX = x + width * Mathf.Clamp01(M.HoverCollectiveInGroundEffect);
                FlightHudGraphics.Fill(new Rect(groundX - 1, bar.y - 8, 2, 20), Green);
                S.Text(new Rect(groundX - 64, bar.y + 12, 60, 18), "IGE", S.HudRight, Green);
                S.Text(new Rect(hoverX + 4, bar.y + 12, 70, 18), "HOVER", S.HudSmall, FlightHudGraphics.Amber);
            }
            else S.Text(new Rect(hoverX - 40, bar.y + 12, 80, 18), "HOVER", S.HudCenter, FlightHudGraphics.Amber);
        }

        /// <summary>Torque (red line 100%) and rotor RPM (green 95–105%) when power limits are on.</summary>
        private void DrawPower(float x, float y, float width)
        {
            Color torqueColor = M.Torque > 1f ? FlightHudGraphics.Amber : FlightHudGraphics.Phosphor;
            S.Text(new Rect(x, y, width, 18), M.TorqueText, S.HudSmall, torqueColor);
            var torque = new Rect(x, y + 20, width, 5);
            S.Bar(torque, M.Torque / 1.2f, torqueColor);
            FlightHudGraphics.Fill(new Rect(x + width / 1.2f - 1, torque.y - 4, 2, 13), Red);
            Color rotorColor = M.RotorSpeed < 0.9f || M.RotorSpeed > 1.1f ? Red : M.RotorSpeed < 0.95f || M.RotorSpeed > 1.05f ? FlightHudGraphics.Amber : Green;
            S.Text(new Rect(x, y + 32, width, 18), M.RotorText, S.HudSmall, rotorColor);
            var rotor = new Rect(x, y + 52, width, 5);
            // Scale 80–115%, with the governed band marked.
            S.Bar(rotor, (M.RotorSpeed - 0.8f) / 0.35f, rotorColor);
            float low = x + width * (0.95f - 0.8f) / 0.35f, high = x + width * (1.05f - 0.8f) / 0.35f;
            FlightHudGraphics.Fill(new Rect(low, rotor.y + 7, high - low, 2), Green);
        }

        // ---- Cockpit view ----

        private void DrawCockpitColumn()
        {
            const float x = 30;
            S.Text(new Rect(x, 214, 156, 20), "AIR SPEED", S.HudSmall, FlightHudGraphics.Muted);
            S.Text(new Rect(x, 232, 190, 35), M.SpeedReadout, S.HudValue);
            S.Text(new Rect(x, 266, 200, 20), M.GroundSpeedText, S.HudSmall);
            S.Text(new Rect(x, 292, 156, 20), "SKID AGL", S.HudSmall, FlightHudGraphics.Muted);
            S.Text(new Rect(x, 310, 190, 35), M.HeightReadout, S.HudValue);
            S.Text(new Rect(x, 344, 220, 20), M.VerticalSpeedLine, S.HudSmall,
                M.VerticalSpeedCaution ? FlightHudGraphics.Amber : FlightHudGraphics.Phosphor);
            DrawCollective(x + 8, 364, 150);
            if (M.PowerLimits) DrawPower(Width - 130, 204, 100);
        }

        private void DrawCyclicIndicator()
        {
            FlightInput input = hud.Input;
            if (!input.Settings.ShowCyclicIndicator && input.Settings.MouseMode != MouseCyclicMode.VirtualJoystick) return;
            Vector2 center = M.Cockpit ? new Vector2(Width - 80, 300) : new Vector2(Width / 2 + 150, ClusterY);
            FlightHudGraphics.Circle(center, 24, FlightHudGraphics.Muted);
            FlightHudGraphics.Line(center + Vector2.left * 28, center + Vector2.right * 28, new Color(.8f, .9f, .7f, .32f));
            FlightHudGraphics.Line(center + Vector2.up * 28, center + Vector2.down * 28, new Color(.8f, .9f, .7f, .32f));
            Vector2 c = input.Command.Cyclic;
            FlightHudGraphics.Circle(center + new Vector2(c.x, -c.y) * 21, 3, FlightHudGraphics.Amber, 12, 2);
            S.Text(new Rect(center.x - 49, center.y + 30, 98, 21), input.IsFreeLooking ? "FREE LOOK" : "CYCLIC", S.HudCenter, FlightHudGraphics.Muted);
        }

        // ---- Status, chart, hover display ----

        private void DrawAircraftStatus()
        {
            if (M.Cockpit) return;
            S.Box(new Rect(26, 554, 266, 123), .58f);
            FlightHudGraphics.Fill(new Rect(26, 554, 266, 1), new Color(.8f, .94f, .7f, .45f));
            S.Text(new Rect(39, 566, 242, 22), M.StatusTitle, S.HudSmall, Aircraft.Crashed ? FlightHudGraphics.Amber : FlightHudGraphics.Paper);
            S.Text(new Rect(39, 597, 240, 22), M.RotorLine, S.HudSmall, M.RotorCaution ? FlightHudGraphics.Amber : FlightHudGraphics.Phosphor);
            S.Text(new Rect(39, 625, 242, 20), M.AssistsLine, S.HudSmall, FlightHudGraphics.Muted);
            S.Text(new Rect(39, 651, 242, 20), M.ContextLine, S.HudSmall, FlightHudGraphics.Muted);
        }

        private Rect ChartFrame => new Rect(Width - 246, 429, 220, 248);
        /// <summary>The square chart covers the island's larger dimension.</summary>
        private const float ChartExtent = WorldConstants.WorldSizeX;

        private void DrawMap()
        {
            Rect frame = ChartFrame;
            S.Box(frame, .79f);
            var r = new Rect(frame.x + 9, frame.y + 31, 202, 202);
            GUI.DrawTexture(r, mapTexture);
            var grid = new Color(.79f, .92f, .69f, .10f);
            for (int i = 1; i < 4; i++)
            {
                FlightHudGraphics.Fill(new Rect(r.x + i * r.width / 4, r.y, 1, r.height), grid);
                FlightHudGraphics.Fill(new Rect(r.x, r.y + i * r.height / 4, r.width, 1), grid);
            }
            FlightHudGraphics.Frame(frame, new Color(.8f, .94f, .7f, .42f));
            S.Text(new Rect(frame.x + 11, frame.y + 6, 180, 22), "MERIDIAN  /  NAV", S.HudSmall);
            S.Text(new Rect(frame.x + 189, frame.y + 6, 23, 22), "N ↑", S.HudSmall, FlightHudGraphics.Paper);
            foreach (Vector2[] road in IslandWorld.Roads)
                for (int i = 1; i < road.Length; i++)
                    MapRoad(r, new Vector3(road[i - 1].x, 0, road[i - 1].y), new Vector3(road[i].x, 0, road[i].y));
            Vector2 here = MapPosition(r, Aircraft.transform.position);
            if (Missions.TargetZone != null)
            {
                Vector2 destination = MapPosition(r, Missions.TargetZone.transform.position);
                float length = Vector2.Distance(here, destination);
                for (float d = 0; d < length; d += 8)
                    FlightHudGraphics.Line(Vector2.Lerp(here, destination, d / Mathf.Max(1, length)),
                        Vector2.Lerp(here, destination, Mathf.Min(d + 4, length) / Mathf.Max(1, length)), new Color(1, .76f, .35f, .7f));
            }
            LandingZone[] zones = hud.Game.Zones;
            for (int i = 0; i < zones.Length; i++)
            {
                LandingZone zone = zones[i];
                if (zone == null) continue;
                Vector2 p = MapPosition(r, zone.transform.position);
                Color color = zone == Missions.TargetZone ? FlightHudGraphics.Amber : FlightHudGraphics.Phosphor;
                if (zone == Missions.TargetZone) FlightHudGraphics.Diamond(p, 7, color);
                else FlightHudGraphics.Frame(new Rect(p.x - 2, p.y - 2, 4, 4), color);
                S.Text(new Rect(p.x + 5, p.y - 12, 23, 19), HudModel.PadNumber(i + 1), S.HudSmall, color);
            }
            FlightHudGraphics.Circle(here, 12, new Color(.94f, .96f, .89f, .30f), 28);
            FlightHudGraphics.Aircraft(here, Aircraft.Heading, FlightHudGraphics.Paper, .85f);
            FlightHudGraphics.Fill(new Rect(r.x + 8, r.yMax - 12, r.width * 500 / ChartExtent, 2), FlightHudGraphics.Muted);
            S.Text(new Rect(r.x + 8, r.yMax - 34, 70, 20), M.Units == UnitSystem.Aviation ? "0.27 nm" : "500 m", S.HudSmall, FlightHudGraphics.Muted);
        }

        /// <summary>
        /// Heading-up hover display: ground drift as a vector from the aircraft symbol, a cue where the drift is heading,
        /// a ring at the service drift limit (green when every service condition is met) and the pad's position.
        /// </summary>
        private void DrawHoverDisplay()
        {
            Rect frame = ChartFrame;
            S.Box(frame, .82f);
            FlightHudGraphics.Frame(frame, new Color(.8f, .94f, .7f, .42f));
            S.Text(new Rect(frame.x + 11, frame.y + 6, 120, 22), "HOVER  /  DRIFT", S.HudSmall);
            S.Text(new Rect(frame.x + 110, frame.y + 6, 100, 22), M.ServiceReady ? "STEADY" : "", S.HudRight, Green);
            var center = new Vector2(frame.center.x, frame.y + 128);
            const float half = 92, pixelsPerMetrePerSecond = half / 3f;
            FlightHudGraphics.Line(center + Vector2.left * half, center + Vector2.right * half, Faint);
            FlightHudGraphics.Line(center + Vector2.up * -half, center + Vector2.up * half, Faint);
            FlightHudGraphics.Circle(center, pixelsPerMetrePerSecond, Faint, 32);
            FlightHudGraphics.Circle(center, 2 * pixelsPerMetrePerSecond, Faint, 40);
            FlightHudGraphics.Circle(center, Mathf.Max(4f, M.ServiceSpeed * pixelsPerMetrePerSecond), M.ServiceReady ? Green : FlightHudGraphics.Muted, 32, 1.6f);
            if (M.PadVisible)
            {
                float distance = M.PadOffset.magnitude;
                Vector2 direction = distance > 0.01f ? new Vector2(M.PadOffset.x, -M.PadOffset.y) / distance : Vector2.zero;
                // Compressed range: fine near the pad, still on the display at 60 m.
                Vector2 pad = center + direction * (half * distance / (distance + 12f));
                FlightHudGraphics.Line(center, pad, new Color(1, .76f, .35f, .35f));
                FlightHudGraphics.Diamond(pad, 7, FlightHudGraphics.Amber);
            }
            Vector2 drift = Clamp(new Vector2(M.Drift.x, -M.Drift.y) * pixelsPerMetrePerSecond, half);
            Vector2 trend = Clamp(new Vector2(M.DriftTrend.x, -M.DriftTrend.y) * pixelsPerMetrePerSecond, half);
            if (drift.sqrMagnitude > 4f)
            {
                FlightHudGraphics.Line(center, center + drift, FlightHudGraphics.Paper, 2f);
                Vector2 unit = drift.normalized, side = new Vector2(-unit.y, unit.x);
                FlightHudGraphics.Line(center + drift, center + drift - unit * 7 + side * 4, FlightHudGraphics.Paper, 2f);
                FlightHudGraphics.Line(center + drift, center + drift - unit * 7 - side * 4, FlightHudGraphics.Paper, 2f);
            }
            FlightHudGraphics.Circle(center + trend, 4, FlightHudGraphics.Phosphor, 16, 1.6f);
            FlightHudGraphics.Aircraft(center, 0f, FlightHudGraphics.Paper, .85f);
            S.Text(new Rect(frame.x + 11, frame.yMax - 26, 120, 20), M.DriftText, S.HudSmall, M.ServiceReady ? Green : FlightHudGraphics.Phosphor);
            S.Text(new Rect(frame.x + 110, frame.yMax - 26, 100, 20), M.PadText, S.HudRight, FlightHudGraphics.Amber);
        }

        private static Vector2 Clamp(Vector2 value, float radius) => Vector2.ClampMagnitude(value, radius);

        private static Vector2 MapPosition(Rect rect, Vector3 position)
            => new Vector2(Mathf.Clamp(rect.center.x + position.x / ChartExtent * rect.width, rect.x + 9, rect.xMax - 9),
                Mathf.Clamp(rect.center.y - position.z / ChartExtent * rect.height, rect.y + 9, rect.yMax - 9));

        private static void MapRoad(Rect rect, Vector3 from, Vector3 to)
            => FlightHudGraphics.Line(MapPosition(rect, from), MapPosition(rect, to), new Color(.80f, .84f, .67f, .35f), 1.5f);

        private static Texture2D BuildMapTexture()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "Meridian navigation chart", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float wx = (x / (float)(size - 1) - .5f) * ChartExtent, wz = (y / (float)(size - 1) - .5f) * ChartExtent;
                float height = IslandWorld.Height(wx, wz);
                if (height < 0) { pixels[y * size + x] = new Color(.075f, .13f, .13f); continue; }
                float slope = (IslandWorld.Height(wx - 8, wz + 8) - height) * .013f;
                Color land = Color.Lerp(new Color(.22f, .29f, .20f), new Color(.46f, .49f, .30f), Mathf.Clamp01(height / 150));
                land *= Mathf.Clamp(1 + slope, .66f, 1.35f);
                land.a = 1;
                if (height < 3) land = new Color(.49f, .51f, .33f);
                else if (Mathf.FloorToInt(height / 25) != Mathf.FloorToInt(IslandWorld.Height(wx + 9, wz) / 25)) land *= .74f;
                pixels[y * size + x] = land;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // ---- Target, warnings, crash, debug ----

        private void DrawTarget()
        {
            LandingZone zone = Missions.TargetZone;
            if (zone == null && Missions.Mode != GameMode.Training) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 world = HudModel.TargetWorldPosition(Missions);
            Vector3 local = cam.transform.InverseTransformPoint(world);
            Vector3 point = cam.WorldToViewportPoint(world);
            var safe = new Rect(118, 178, Width - 390, 334);
            Vector2 raw = new Vector2(point.x * Width, (1 - point.y) * FlightHUD.Height), center = new Vector2(Width / 2, 340);
            bool offscreen = point.z <= 0 || !safe.Contains(raw);
            Vector2 direction = point.z > 0 ? raw - center : new Vector2(local.x, -local.y);
            if (direction.sqrMagnitude < .01f) direction = Vector2.right;
            Vector2 location = raw;
            if (offscreen)
            {
                direction.Normalize();
                float tx = direction.x > 0 ? (safe.xMax - center.x) / direction.x : direction.x < 0 ? (safe.xMin - center.x) / direction.x : float.PositiveInfinity;
                float ty = direction.y > 0 ? (safe.yMax - center.y) / direction.y : direction.y < 0 ? (safe.yMin - center.y) / direction.y : float.PositiveInfinity;
                location = center + direction * Mathf.Min(tx, ty);
                Vector2 tangent = new Vector2(-direction.y, direction.x), tip = location + direction * 9;
                FlightHudGraphics.Line(tip, location - direction * 5 + tangent * 6, FlightHudGraphics.Amber, 2);
                FlightHudGraphics.Line(tip, location - direction * 5 - tangent * 6, FlightHudGraphics.Amber, 2);
            }
            else FlightHudGraphics.Diamond(location, 12, FlightHudGraphics.Amber);
            float labelX = Mathf.Clamp(location.x - 108, 38, Width - 260), labelY = location.y + 17;
            S.Text(new Rect(labelX, labelY, 216, 21), M.TargetName, S.HudCenter, FlightHudGraphics.Amber);
            S.Text(new Rect(labelX, labelY + 22, 216, 21), point.z < 0 ? M.TargetDistanceBehind : M.TargetDistance, S.HudCenter, FlightHudGraphics.Paper);
            if (Missions.DwellProgress > 0) S.Bar(new Rect(labelX + 53, labelY + 47, 110, 3), Missions.DwellProgress, FlightHudGraphics.Amber);
        }

        /// <summary>Caution stack under the compass; returns the y below it for the next notice.</summary>
        private float DrawWarnings()
        {
            float y = 146;
            foreach (HudModel.Warning warning in M.Warnings)
            {
                var r = new Rect(Width / 2 - 170, y, 340, 26);
                S.Box(r, .8f);
                FlightHudGraphics.Fill(new Rect(r.x, r.y, 3, r.height), warning.Color);
                S.Text(new Rect(r.x, r.y + 2, r.width, 22), warning.Text, S.HudCenter, warning.Color);
                y += 30;
            }
            return y;
        }

        private void DrawCrashPanel()
        {
            var r = new Rect(Width / 2 - 255, 248, 510, 190);
            S.Box(r, .97f);
            FlightHudGraphics.Fill(new Rect(r.x, r.y, r.width, 3), FlightHudGraphics.Amber);
            GUI.Label(new Rect(r.x + 24, r.y + 18, 462, 38), M.CrashTitle, S.Title);
            GUI.Label(new Rect(r.x + 24, r.y + 58, 462, 26), M.CrashDetail, S.Label);
            GUI.Label(new Rect(r.x + 24, r.y + 86, 462, 44), M.CrashAdvice, S.Small);
            if (GUI.Button(new Rect(r.x + 24, r.y + 132, 462, 44), M.RetryLabel, S.Button)) hud.Retry();
        }

        private void DrawDebug()
        {
            S.Box(new Rect(25, 231, 440, 280), .95f);
            GUI.Label(new Rect(40, 240, 410, 265), M.DebugText, S.Small);
        }

        public void Dispose()
        {
            if (mapTexture != null) Object.Destroy(mapTexture);
        }
    }
}
