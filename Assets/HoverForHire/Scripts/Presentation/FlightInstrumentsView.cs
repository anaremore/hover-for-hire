using UnityEngine;
using Object = UnityEngine.Object;

namespace HoverForHire
{
    /// <summary>The flight HUD: mission panel, compass, tapes, attitude, status, chart, target, warnings and notices.</summary>
    public sealed class FlightInstrumentsView
    {
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
            DrawInstruments();
            DrawMap();
            DrawTarget();
            S.Text(new Rect(26, 690, 960, 20), M.FooterHint, S.HudSmall, FlightHudGraphics.Muted);
            if (hud.NoticeVisible)
            {
                var r = new Rect(Width / 2 - 257, 535, 514, 48);
                S.Box(r, .72f);
                FlightHudGraphics.Fill(new Rect(r.x, r.y, 2, r.height), FlightHudGraphics.Amber);
                GUI.Label(new Rect(r.x + 14, r.y + 9, r.width - 28, 36), hud.Notice, S.Small);
            }
            if (!string.IsNullOrEmpty(Missions.SaveWarning))
            {
                S.Box(new Rect(Width / 2 - 280, 590, 560, 48), .92f);
                GUI.Label(new Rect(Width / 2 - 266, 598, 532, 36), Missions.SaveWarning, S.Small);
            }
            if (Aircraft.Crashed) DrawCrashPanel();
            DrawWarnings();
            if (hud.DebugVisible) DrawDebug();
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

        private void DrawWarnings()
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
        }

        private void DrawMission()
        {
            // The panel grows with the wrapped objective, so longer drill briefs never run into the status line.
            objectiveMeasure.text = M.Objective;
            float objective = Mathf.Max(49, S.Label.CalcHeight(objectiveMeasure, 316)), height = 126 + objective - 49;
            S.Box(new Rect(26, 26, 348, height), .61f);
            FlightHudGraphics.Fill(new Rect(26, 26, 3, height), FlightHudGraphics.Amber);
            S.Text(new Rect(42, 36, 316, 20), M.ModeHeader, S.HudSmall, FlightHudGraphics.Amber);
            GUI.Label(new Rect(42, 65, 316, objective), M.Objective, S.Label);
            GUI.Label(new Rect(42, 65 + objective, 316, 37), M.Status, S.Small);
            float right = Width - 244;
            S.Text(new Rect(right, 28, 216, 20), M.RightCaption, S.HudRight, FlightHudGraphics.Muted);
            S.Text(new Rect(right, 51, 216, 36), M.RightValue, S.HudValueRight, FlightHudGraphics.Paper);
            S.Text(new Rect(right, 91, 216, 22), M.EarningsLine, S.HudRight);
            if (M.TrainingLine.Length > 0) S.Text(new Rect(right - 60, 119, 276, 24), M.TrainingLine, S.HudRight);
            DrawCompass();
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
            if (M.RecordingText.Length > 0) S.Text(new Rect(cx - 70, 118, 140, 22), M.RecordingText, S.HudCenter, new Color(1f, .32f, .25f));
        }

        private void DrawInstruments()
        {
            float cx = Width / 2;
            bool cockpit = M.Cockpit;
            if (cockpit)
            {
                S.Text(new Rect(30, 235, 156, 22), "AIR SPEED", S.HudSmall, FlightHudGraphics.Muted);
                S.Text(new Rect(30, 260, 170, 35), M.SpeedReadout, S.HudValue);
                S.Text(new Rect(Width - 178, 235, 150, 22), "SKID AGL", S.HudRight, FlightHudGraphics.Muted);
                S.Text(new Rect(Width - 190, 260, 162, 35), M.HeightReadout, S.HudValueRight);
            }
            else
            {
                bool aviation = M.Units == UnitSystem.Aviation;
                DrawTape(cx - 281, M.SpeedValue, M.SpeedTapeReadout, "AIR SPEED", M.SpeedUnit, true, 5, 3.8f);
                DrawTape(cx + 281, M.HeightValue, M.HeightTapeReadout, "SKID AGL", M.HeightUnit, false, aviation ? 10 : 5, aviation ? 3.8f * UnitFormat.MetresPerFoot : 3.8f);
                DrawAttitude(cx);
            }
            float verticalX = cockpit ? Width - 157 : cx + 266, verticalY = cockpit ? 306 : 446;
            S.Text(new Rect(verticalX, verticalY, 130, 22), M.VerticalSpeedText, cockpit ? S.HudRight : S.HudCenter,
                M.VerticalSpeedCaution ? FlightHudGraphics.Amber : FlightHudGraphics.Phosphor);
            S.Text(new Rect(verticalX, verticalY + 23, 130, 20), "VERT SPEED", cockpit ? S.HudRight : S.HudCenter, FlightHudGraphics.Muted);
            if (cockpit)
            {
                S.Text(new Rect(30, 351, 272, 22), M.PayloadLine, S.HudSmall, FlightHudGraphics.Muted);
                S.Text(new Rect(30, 376, 320, 22), M.CockpitCollectiveLine, S.HudSmall);
                S.Text(new Rect(30, 401, 290, 38), M.CockpitAssistsLine, S.HudSmall, FlightHudGraphics.Muted);
            }
            else
            {
                S.Text(new Rect(cx - 99, 624, 198, 22), M.CollectiveText, S.HudCenter);
                S.Bar(new Rect(cx - 91, 654, 182, 5), M.Collective, FlightHudGraphics.Phosphor);
                for (int i = 0; i <= 4; i++) FlightHudGraphics.Fill(new Rect(cx - 91 + i * 45.5f, 650, 1, 13), FlightHudGraphics.Muted);
                // Hover reference for the current weight: level, still air, out of ground effect.
                float hoverX = cx - 91 + 182 * Mathf.Clamp01(M.HoverCollective);
                FlightHudGraphics.Fill(new Rect(hoverX - 1, 645, 2, 19), FlightHudGraphics.Amber);
                S.Text(new Rect(hoverX - 30, 664, 60, 16), "HOVER", S.HudCenter, FlightHudGraphics.Amber);
                DrawAircraftStatus();
            }
            FlightInput input = hud.Input;
            if (input.Settings.ShowCyclicIndicator || input.Settings.MouseMode == MouseCyclicMode.VirtualJoystick)
            {
                Vector2 center = cockpit ? new Vector2(Width - 106, 373) : new Vector2(cx + 169, 625);
                FlightHudGraphics.Circle(center, 26, FlightHudGraphics.Muted);
                FlightHudGraphics.Line(center + Vector2.left * 30, center + Vector2.right * 30, new Color(.8f, .9f, .7f, .32f));
                FlightHudGraphics.Line(center + Vector2.up * 30, center + Vector2.down * 30, new Color(.8f, .9f, .7f, .32f));
                Vector2 c = input.Command.Cyclic;
                FlightHudGraphics.Circle(center + new Vector2(c.x, -c.y) * 23, 3, FlightHudGraphics.Amber, 12, 2);
                S.Text(new Rect(center.x - 49, center.y + 33, 98, 21), input.IsFreeLooking ? "FREE LOOK" : "CYCLIC", S.HudCenter, FlightHudGraphics.Muted);
            }
        }

        /// <summary>A moving tape with minor marks every <paramref name="step"/> units and labels every two steps.</summary>
        private void DrawTape(float x, float number, string readoutText, string caption, string unit, bool left, int step, float pixels)
        {
            const float cy = 340, half = 94;
            S.Text(new Rect(x - 54, 218, 108, 22), caption, S.HudCenter, FlightHudGraphics.Muted);
            S.Text(new Rect(x - 44, 241, 88, 20), unit, S.HudCenter);
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

        private void DrawAttitude(float cx)
        {
            Quaternion attitude = Aircraft.Body != null ? Aircraft.Body.rotation : Aircraft.transform.rotation;
            Vector3 forward = attitude * Vector3.forward, right = attitude * Vector3.right, up = attitude * Vector3.up;
            float pitch = Mathf.Asin(Mathf.Clamp(forward.y, -1, 1)) * Mathf.Rad2Deg;
            float roll = Mathf.Atan2(right.y, up.y) * Mathf.Rad2Deg;
            // Attitude comes from the aircraft relative to world up, even in an orbiting chase view. Keep it in the same
            // logical coordinate space as the tapes: a nested GUI clip would add its own pivot offset above 720p.
            Matrix4x4 previous = GUI.matrix;
            var center = new Vector2(cx, 340);
            GUI.matrix = previous * FlightHudGraphics.RotationAround(center, roll);
            for (int mark = -80; mark <= 80; mark += 10)
            {
                float y = center.y + (pitch - mark) * 3.1f;
                if (Mathf.Abs(y - center.y) > 100) continue;
                float inner = mark == 0 ? 39 : 53, outer = mark == 0 ? 152 : 100;
                Color color = mark == 0 ? FlightHudGraphics.Phosphor : new Color(.83f, .96f, .70f, .60f);
                for (int side = -1; side <= 1; side += 2)
                {
                    if (mark < 0)
                        for (float d = inner; d < outer; d += 13)
                            FlightHudGraphics.Line(new Vector2(center.x + side * d, y), new Vector2(center.x + side * Mathf.Min(d + 7, outer), y), color);
                    else FlightHudGraphics.Line(new Vector2(center.x + side * inner, y), new Vector2(center.x + side * outer, y), color);
                    if (mark == 0) continue;
                    FlightHudGraphics.Line(new Vector2(center.x + side * outer, y), new Vector2(center.x + side * outer, y + Mathf.Sign(mark) * 5), color);
                    S.Text(new Rect(center.x + side * 121 - 15, y - 11, 30, 22), HudModel.Number(Mathf.Abs(mark)), S.HudCenter, color);
                }
            }
            GUI.matrix = previous;
            var waterline = new Vector2(cx, 340);
            FlightHudGraphics.Line(waterline + new Vector2(-25, 0), waterline + new Vector2(-8, 0), FlightHudGraphics.Paper, 1.8f);
            FlightHudGraphics.Line(waterline + new Vector2(-8, 0), waterline + new Vector2(0, 6), FlightHudGraphics.Paper, 1.8f);
            FlightHudGraphics.Line(waterline + new Vector2(0, 6), waterline + new Vector2(8, 0), FlightHudGraphics.Paper, 1.8f);
            FlightHudGraphics.Line(waterline + new Vector2(8, 0), waterline + new Vector2(25, 0), FlightHudGraphics.Paper, 1.8f);
            if (Aircraft.Airspeed > .8f && Aircraft.Body != null)
            {
                Vector3 local = Quaternion.Inverse(attitude) * Aircraft.Body.linearVelocity;
                float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                float climb = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                // Show the velocity vector only inside the instrument field; off-field flight paths are not pinned as false targets.
                if (Mathf.Abs(yaw) < 48 && Mathf.Abs(climb) < 30)
                {
                    Vector2 fpm = waterline + new Vector2(yaw * 3.1f, -climb * 3.1f);
                    FlightHudGraphics.Circle(fpm, 7, FlightHudGraphics.Phosphor, 24);
                    FlightHudGraphics.Line(fpm + Vector2.left * 7, fpm + Vector2.left * 17, FlightHudGraphics.Phosphor);
                    FlightHudGraphics.Line(fpm + Vector2.right * 7, fpm + Vector2.right * 17, FlightHudGraphics.Phosphor);
                    FlightHudGraphics.Line(fpm + Vector2.up * 7, fpm + Vector2.up * 13, FlightHudGraphics.Phosphor);
                }
            }
            S.Text(new Rect(cx - 115, 462, 230, 21), hud.Input.IsFreeLooking ? "FREE LOOK" : M.Cockpit ? "COCKPIT VIEW" : "CHASE VIEW",
                S.HudCenter, FlightHudGraphics.Muted);
        }

        private void DrawAircraftStatus()
        {
            S.Box(new Rect(26, 554, 266, 123), .58f);
            FlightHudGraphics.Fill(new Rect(26, 554, 266, 1), new Color(.8f, .94f, .7f, .45f));
            S.Text(new Rect(39, 566, 242, 22), M.StatusTitle, S.HudSmall, Aircraft.Crashed ? FlightHudGraphics.Amber : FlightHudGraphics.Paper);
            S.Text(new Rect(39, 597, 240, 22), M.RotorLine, S.HudSmall, M.RotorCaution ? FlightHudGraphics.Amber : FlightHudGraphics.Phosphor);
            S.Text(new Rect(39, 625, 242, 20), M.AssistsLine, S.HudSmall, FlightHudGraphics.Muted);
            S.Text(new Rect(39, 651, 242, 20), M.ContextLine, S.HudSmall, FlightHudGraphics.Muted);
        }

        private void DrawMap()
        {
            var frame = new Rect(Width - 246, 429, 220, 248);
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
            FlightHudGraphics.Fill(new Rect(r.x + 8, r.yMax - 12, r.width * 500 / 2400, 2), FlightHudGraphics.Muted);
            S.Text(new Rect(r.x + 8, r.yMax - 34, 70, 20), M.Units == UnitSystem.Aviation ? "0.27 nm" : "500 m", S.HudSmall, FlightHudGraphics.Muted);
        }

        private static Vector2 MapPosition(Rect rect, Vector3 position)
            => new Vector2(Mathf.Clamp(rect.center.x + position.x / 2400 * rect.width, rect.x + 9, rect.xMax - 9),
                Mathf.Clamp(rect.center.y - position.z / 2400 * rect.height, rect.y + 9, rect.yMax - 9));

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
                float wx = (x / (float)(size - 1) - .5f) * 2400, wz = (y / (float)(size - 1) - .5f) * 2400;
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
