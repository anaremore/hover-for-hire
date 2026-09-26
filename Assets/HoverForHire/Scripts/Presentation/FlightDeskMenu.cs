using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HoverForHire
{
    /// <summary>
    /// The pause menu (Flight Desk) and the first-launch welcome page. Every control is reachable by mouse or by
    /// gamepad/keyboard focus navigation: D-pad/stick moves focus, left/right adjusts, submit activates.
    /// </summary>
    public sealed class FlightDeskMenu
    {
        public const int PageFly = 0, PageControls = 1, PageBindings = 2, PageAssists = 3, PageView = 4;
        private static readonly string[] Tabs = { "Fly", "Controls", "Bindings", "Assists / realism", "View / sound" };
        private static readonly string[] DrillLabels = BuildDrillLabels();

        private readonly FlightHUD hud;
        private int page, focus, count, index, adjust;
        private bool welcome, activate, scrollToFocus, insideScroll;
        private Vector2 scroll;
        private Vector2Int direction;
        private float repeatAt;

        public FlightDeskMenu(FlightHUD hud) { this.hud = hud; }

        private HudStyles S => hud.Styles;
        private FlightInput Input => hud.Input;
        private MissionDirector Missions => hud.Missions;
        private HelicopterController Aircraft => hud.Aircraft;

        public bool ShowingWelcome => welcome;

        public void OpenWelcome() { welcome = true; scroll = Vector2.zero; focus = 0; }
        public void CloseWelcome() => welcome = false;

        public void SelectPage(int value)
        {
            welcome = false;
            page = Mathf.Clamp(value, 0, Tabs.Length - 1);
            scroll = Vector2.zero;
            focus = 0;
        }

        /// <summary>Clear pending navigation whenever the menu opens or closes.</summary>
        public void ResetNavigation() { activate = false; adjust = 0; direction = Vector2Int.zero; }

        public void UpdateNavigation()
        {
            if (!hud.Paused || Input.IsRebinding) return;
            if (Input.MenuBackPressed) { hud.SetPause(false); return; }
            if (Input.MenuSubmitPressed) activate = true;
            Vector2 move = Input.MenuMove;
            Vector2Int next = Mathf.Abs(move.y) > .5f ? new Vector2Int(0, move.y > 0 ? -1 : 1)
                : Mathf.Abs(move.x) > .5f ? new Vector2Int(move.x > 0 ? 1 : -1, 0) : Vector2Int.zero;
            if (next == Vector2Int.zero) { direction = next; return; }
            bool changed = next != direction;
            if (changed || Time.unscaledTime >= repeatAt)
            {
                if (next.y != 0 && count > 0) { focus = (focus + next.y + count) % count; scrollToFocus = true; }
                if (next.x != 0) adjust = next.x;
                repeatAt = Time.unscaledTime + (changed ? .38f : .12f);
            }
            direction = next;
        }

        public void Draw()
        {
            index = 0;
            float width = hud.Width, left = width / 2 - 450;
            S.Box(new Rect(0, 0, width, FlightHUD.Height), .72f);
            S.Box(new Rect(left, 26, 900, 668), .98f);
            FlightHudGraphics.Fill(new Rect(left, 26, 900, 3), FlightHudGraphics.Amber);
            S.Text(new Rect(left + 26, 44, 600, 22), "MERIDIAN AIR SERVICE  /  OPERATIONS", S.HudSmall, FlightHudGraphics.Amber);
            GUI.Label(new Rect(left + 26, 73, 600, 40), "Flight desk", S.Title);
            S.Text(new Rect(left + 654, 76, 220, 25), "FLIGHT PAUSED", S.HudRight, FlightHudGraphics.Phosphor);
            GUI.Label(new Rect(left + 26, 116, 850, 25), "D-pad / stick  Navigate      Left / right  Adjust      A  Select      B / Escape  Resume", S.Small);
            GUILayout.BeginArea(new Rect(left + 24, 151, 852, 518));
            if (welcome) WelcomePage();
            else
            {
                int selected = Toolbar(page, Tabs);
                if (selected != page) { page = selected; scroll = Vector2.zero; focus = 0; }
                GUILayout.Space(10);
                scroll = GUILayout.BeginScrollView(scroll);
                insideScroll = true;
                switch (page)
                {
                    case PageFly: FlyPage(); break;
                    case PageControls: ControlsPage(); break;
                    case PageBindings: BindingsPage(); break;
                    case PageAssists: AssistsPage(); break;
                    default: ViewPage(); break;
                }
                insideScroll = false;
                GUILayout.EndScrollView();
                GUILayout.Space(7);
                if (Button("Resume flight  /  Escape")) hud.SetPause(false);
            }
            GUILayout.EndArea();
            count = index;
            focus = Mathf.Clamp(focus, 0, Mathf.Max(0, count - 1));
            if (Event.current.type == EventType.Layout) { activate = false; adjust = 0; }
        }

        private void FlyPage()
        {
            GUILayout.Label("A compact island. One helicopter. Room to get better.", S.Label);
            GUILayout.BeginHorizontal();
            if (Button("Free flight")) { Missions.StartFreeFlight(); Input.ResetCommand(); hud.SetPause(false); }
            if (Button("Start 15-minute shift")) { Missions.StartShift(); Input.ResetCommand(); hud.SetPause(false); }
            GUILayout.EndHorizontal();
            if (Button("Accept next contract / continue service")) { Missions.Interact(); hud.SetPause(false); }
            if (Missions.Mode == GameMode.DeliveryShift && !Missions.ShiftFinished
                && (Missions.MissionState == MissionState.Available || Missions.MissionState == MissionState.Delivered))
            {
                GUILayout.Label(Missions.CurrentObjective, S.Label);
                if (Button("Browse next offer")) Missions.BrowseNextJob();
            }
            GUILayout.Space(10);
            GUILayout.Label("TRAINING  /  one skill at a time", S.Label);
            for (int i = 0; i < DrillLabels.Length; i++)
            {
                if (i % 2 == 0) GUILayout.BeginHorizontal();
                if (Button(DrillLabels[i])) { Missions.StartTraining(i); Input.ResetCommand(); hud.SetPause(false); }
                if (i % 2 == 1 || i == DrillLabels.Length - 1) GUILayout.EndHorizontal();
            }
            GUILayout.Space(6);
            if (Button("Reset at helipad / retry current job")) { hud.Retry(); hud.SetPause(false); }
            GUILayout.Label("Raise collective gradually. Around 45% is empty hover power. Tilt forward to accelerate; tilt back early to brake. Lower collective after touchdown.", S.Small);
            if (Button("Quit game")) { hud.Save(); Application.Quit(); }
        }

        private void ControlsPage()
        {
            InputPreferences s = Input.Settings;
            GUILayout.Label("Mouse cyclic · keyboard adds smoothly, combined command is bounded", S.Label);
            s.MouseMode = (MouseCyclicMode)Toolbar((int)s.MouseMode, new[] { "Relative + return", "Virtual joystick" });
            Slider("Mouse sensitivity", ref s.MouseSensitivity, .0005f, .02f, "F4");
            Slider("Return toward center / s", ref s.MouseReturnRate, 0, 8);
            Slider("Deadzone", ref s.Deadzone, 0, .4f);
            Slider("Response curve", ref s.ResponseCurve, .5f, 3);
            s.InvertPitch = Toggle(s.InvertPitch, "Invert cyclic pitch");
            s.InvertRoll = Toggle(s.InvertRoll, "Invert cyclic roll");
            Slider("Keyboard response", ref s.KeyboardResponse, 2, 30);
            Slider("Collective change / s", ref s.CollectiveRate, .05f, 1);
            Slider("Collective fine trim / s (key tap)", ref s.CollectiveFineRate, .01f, .5f, "F3");
            Slider("Collective ramp to full rate / s", ref s.CollectiveRampSeconds, 0, 1.5f);
            Slider("Pedal tap strength", ref s.YawFineFraction, .05f, 1);
            Slider("Pedal ramp to full / s", ref s.YawRampSeconds, 0, 1.5f);
            GUILayout.Label("Gamepad layout · Sim pedals puts analog pedals on the triggers and collective on the shoulders", S.Label);
            int layout = Toolbar((int)s.GamepadLayout, new[] { "Classic", "Sim pedals" });
            if (layout != (int)s.GamepadLayout) Input.ApplyGamepadLayout((GamepadLayout)layout);
            GUILayout.Label("Cyclic while holding free look", S.Label);
            s.FreeLookBehavior = (FreeLookCyclicMode)Toolbar((int)s.FreeLookBehavior, new[] { "Hold command", "Return to neutral" });
            s.ShowCyclicIndicator = Toggle(s.ShowCyclicIndicator, "Show cyclic indicator");
            s.UseAbsoluteCollective = Toggle(s.UseAbsoluteCollective, "Use absolute collective axis (bind below first)");
            s.AbsoluteAxisSigned = Toggle(s.AbsoluteAxisSigned, "Absolute axis range is -1 to +1");
            s.InvertAbsoluteCollective = Toggle(s.InvertAbsoluteCollective, "Invert absolute collective");
            GUILayout.Label("MMB duplicates Alt free look on systems that intercept Alt. C recenters cyclic; R recenters only the view. Bindings can be changed on the next tab.", S.Small);
            if (Button("Restore control defaults")) Input.RestoreDefaults();
        }

        private void BindingsPage()
        {
            GUILayout.Label(Input.IsRebinding ? "Move or press a control. Escape / menu Back cancels." : "Choose a binding, then press a key, button, or move an axis.", S.Label);
            GUI.enabled = Input.IsRebinding;
            if (Button("Cancel binding")) Input.CancelRebind();
            GUI.enabled = true;
            foreach (var action in Input.Actions)
            {
                GUILayout.Space(9);
                GUILayout.Label(ReadableName(action.name), S.Label);
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.isComposite) continue;
                    int bindingIndex = i;
                    Guid id = action.id;
                    GUI.enabled = !Input.IsRebinding;
                    if (Button((binding.isPartOfComposite ? ReadableName(binding.name) + ": " : "") + action.GetBindingDisplayString(i)))
                        Input.BeginRebind(id, bindingIndex, _ => hud.Save());
                    GUI.enabled = true;
                }
            }
        }

        private void AssistsPage()
        {
            GUILayout.Label("Flight assists · same aircraft, bounded control commands", S.Label);
            GUILayout.BeginHorizontal();
            foreach (AssistPreset preset in Enum.GetValues(typeof(AssistPreset)))
                if (Button(preset.ToString())) Aircraft.SetPreset(preset);
            GUILayout.EndHorizontal();
            AssistSettings a = Aircraft.Assists;
            a.RateStabilization = Toggle(a.RateStabilization, "Pitch / roll rate stabilization");
            a.AutoLevel = Toggle(a.AutoLevel, "Auto-level with centered cyclic");
            a.YawStabilization = Toggle(a.YawStabilization, "Yaw stabilization");
            a.TorqueCompensation = Toggle(a.TorqueCompensation, "Main rotor torque compensation");
            GUILayout.Label("Hover hold is deferred pending flight tuning. Level assist does not stop drift or hold altitude.", S.Small);
            RealismSection();
        }

        /// <summary>Realism: the physical challenge, separate from control assists. Changes apply at once and are saved.</summary>
        private void RealismSection()
        {
            RealismSettings r = Missions.PlayerRealism;
            GUILayout.Space(8);
            GUILayout.Label("Flight realism · " + r.Summary + " · the helicopter and weather push back; assists above are control help", S.Label);
            GUILayout.BeginHorizontal();
            foreach (RealismPreset preset in Enum.GetValues(typeof(RealismPreset)))
                if (Button(preset.ToString())) { r.SetPreset(preset); hud.ApplyRealismChange(); }
            GUILayout.EndHorizontal();
            bool changed = false;
            r.GroundEffect = Changed(r.GroundEffect, Toggle(r.GroundEffect, "Ground effect cushion"), ref changed);
            r.TranslationalLift = Changed(r.TranslationalLift, Toggle(r.TranslationalLift, "Translational lift"), ref changed);
            r.SpeedStability = Changed(r.SpeedStability, Toggle(r.SpeedStability, "Speed stability (nose rises with speed)"), ref changed);
            r.VortexRingState = Changed(r.VortexRingState, Toggle(r.VortexRingState, "Settling with power (vortex ring state)"), ref changed);
            r.PowerLimits = Changed(r.PowerLimits, Toggle(r.PowerLimits, "Engine power limits, rotor droop, autorotation"), ref changed);
            r.TailRotorFailures = Changed(r.TailRotorFailures, Toggle(r.TailRotorFailures, "Tail rotor strike causes a failure, not a crash"), ref changed);
            GUILayout.Label("Wind", S.Small);
            int wind = Toolbar((int)r.Wind, new[] { "Calm", "Light", "Moderate", "Strong" });
            if (wind != (int)r.Wind) { r.Wind = (WindStrength)wind; changed = true; }
            float gust = r.Gustiness;
            Slider("Gustiness", ref r.Gustiness, 0, 1);
            changed |= !Mathf.Approximately(gust, r.Gustiness);
            GUILayout.Label("Engine failures", S.Small);
            int failures = Toolbar((int)r.EngineFailures, new[] { "Off", "Drills only", "Random (rare)" });
            if (failures != (int)r.EngineFailures) { r.EngineFailures = (FailureMode)failures; changed = true; }
            if (changed) hud.ApplyRealismChange();
        }

        private static bool Changed(bool before, bool after, ref bool changed)
        {
            changed |= before != after;
            return after;
        }

        private void ViewPage()
        {
            PilotSettings settings = hud.Settings;
            GUILayout.Label("Units · Metric uses km/h, m/s and metres; Aviation uses knots, feet per minute and feet", S.Label);
            int units = Toolbar((int)settings.Units, new[] { "Metric", "Aviation" });
            if (units != (int)settings.Units) hud.SetUnits((UnitSystem)units);
            InputPreferences s = Input.Settings;
            GUILayout.Label("Camera", S.Label);
            Slider("Camera distance / m", ref s.CameraDistance, 5, 25);
            Slider("Camera height / m", ref s.CameraHeight, 1, 10);
            Slider("Field of view / deg", ref s.CameraFov, 45, 100);
            Slider("Camera smoothing / s", ref s.CameraSmoothing, .02f, .8f);
            Slider("Look sensitivity", ref s.LookSensitivity, .02f, .5f);
            Slider("Gamepad look / deg/s", ref s.GamepadLookSpeed, 30, 240);
            s.InvertLook = Toggle(s.InvertLook, "Invert camera look");
            s.AutoRecenterView = Toggle(s.AutoRecenterView, "Automatically recenter view");
            Slider("Recenter delay / s", ref s.RecenterDelay, 0, 5);
            Slider("Recenter speed", ref s.RecenterSpeed, 1, 12);
            GUILayout.Label("Sound", S.Label);
            Slider("Audio volume", ref hud.Audio.Volume, 0, 1);
        }

        private void WelcomePage()
        {
            RealismSettings r = Missions.PlayerRealism;
            GUILayout.Label("Welcome to Port Meridian", S.Title);
            GUILayout.Label($"Collective sets lift: hold {hud.Key("CollectiveIncrease")} to raise it gently and {hud.Key("CollectiveDecrease")} to lower it. " +
                "Choose how hard the helicopter and weather push back; you can change this any time in the flight desk.", S.Label);
            GUILayout.Space(6);
            RealismPreset? current = r.MatchingPreset;
            if (Button((current == RealismPreset.Relaxed ? "●  " : "    ") + "Relaxed  ·  calm air, unlimited power, forgiving touchdowns"))
                { r.SetPreset(RealismPreset.Relaxed); hud.ApplyRealismChange(); }
            if (Button((current == RealismPreset.Realistic ? "●  " : "    ") + "Realistic  ·  light wind, power limits, settling with power"))
                { r.SetPreset(RealismPreset.Realistic); hud.ApplyRealismChange(); }
            if (Button((current == RealismPreset.Expert ? "●  " : "    ") + "Expert  ·  gusty wind, earlier settling, rare engine failures"))
                { r.SetPreset(RealismPreset.Expert); hud.ApplyRealismChange(); }
            GUILayout.Space(10);
            if (Button("Start the Takeoff drill  ·  recommended for new pilots"))
                { hud.DismissFirstRun(); Missions.StartTraining(0); Input.ResetCommand(); hud.SetPause(false); }
            if (Button("Just fly  ·  free flight from home base")) { hud.DismissFirstRun(); hud.SetPause(false); }
        }

        // ---- Focus-navigable controls ----

        private Color Highlight(int id)
        {
            Color previous = GUI.backgroundColor;
            if (id == focus) GUI.backgroundColor = new Color(1.28f, 1.4f, 1.1f);
            return previous;
        }

        private bool Activated(int id) => id == focus && activate && GUI.enabled && Event.current.type == EventType.Layout;
        private int Adjustment(int id) => id == focus && GUI.enabled && Event.current.type == EventType.Layout ? adjust : 0;

        private void Track(int id)
        {
            if (id == focus && Event.current.type == EventType.Repaint)
            {
                Rect focused = GUILayoutUtility.GetLastRect();
                FlightHudGraphics.Frame(focused, new Color(.83f, .96f, .70f, .60f));
                FlightHudGraphics.Fill(new Rect(focused.x, focused.y, 3, focused.height), FlightHudGraphics.Amber);
            }
            if (id != focus || !insideScroll || !scrollToFocus || Event.current.type != EventType.Repaint) return;
            Rect rect = GUILayoutUtility.GetLastRect();
            if (rect.yMin < scroll.y) scroll.y = Mathf.Max(0, rect.yMin - 15);
            else if (rect.yMax > scroll.y + 360) scroll.y = rect.yMax - 345;
            scrollToFocus = false;
        }

        private bool Button(string text)
        {
            int id = index++;
            Color color = Highlight(id);
            bool clicked = GUILayout.Button(text, S.Button);
            GUI.backgroundColor = color;
            Track(id);
            bool activated = Activated(id);
            if (activated) activate = false;
            return clicked || activated;
        }

        private bool Toggle(bool state, string text)
        {
            int id = index++;
            Color color = Highlight(id);
            bool result = GUILayout.Toggle(state, (state ? "ON    " : "OFF   ") + text, S.Toggle);
            GUI.backgroundColor = color;
            Track(id);
            if (Activated(id)) { activate = false; result = !result; }
            else if (Adjustment(id) != 0) result = adjust > 0;
            return result;
        }

        private int Toolbar(int selected, string[] options)
        {
            int id = index++;
            Color color = Highlight(id);
            int result = GUILayout.Toolbar(selected, options, S.TabButton);
            GUI.backgroundColor = color;
            Track(id);
            int step = Adjustment(id);
            if (Activated(id)) { activate = false; step = 1; }
            if (step != 0) result = (result + step + options.Length) % options.Length;
            return result;
        }

        private void Slider(string caption, ref float number, float min, float max, string format = "F2")
        {
            int id = index++;
            GUILayout.Label((id == focus ? "› " : "") + caption + "   " + number.ToString(format), S.Small);
            Color color = Highlight(id);
            number = GUILayout.HorizontalSlider(number, min, max, S.SliderTrack, S.SliderThumb);
            GUI.backgroundColor = color;
            Track(id);
            number = Mathf.Clamp(number + Adjustment(id) * (max - min) / 40f, min, max);
            GUILayout.Space(5);
        }

        private static string ReadableName(string text)
        {
            if (string.IsNullOrEmpty(text)) return "Control";
            var result = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                if (i > 0 && char.IsUpper(text[i]) && !char.IsUpper(text[i - 1])) result.Append(' ');
                result.Append(i == 0 ? char.ToUpperInvariant(text[i]) : text[i]);
            }
            return result.ToString();
        }

        private static string[] BuildDrillLabels()
        {
            var labels = new string[TrainingSession.Names.Length];
            for (int d = 0; d < labels.Length; d++) labels[d] = (d + 1).ToString("00") + " " + TrainingSession.Names[d];
            return labels;
        }
    }
}
