using System;
using Unity.Profiling;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>
    /// Presentation coordinator: owns the player's settings, pause state and notices, reacts to input and mission
    /// events, refreshes the HUD model once per frame and hands drawing to the instruments view and the Flight Desk.
    /// </summary>
    public sealed class FlightHUD : MonoBehaviour
    {
        public GameBootstrap Game;
        public FlightAudio Audio;

        /// <summary>Logical GUI height; the HUD scales to the screen and widens with its aspect ratio.</summary>
        public const float Height = 720f;
        public float Width { get; private set; } = 1280f;
        public HelicopterController Aircraft => Game.Aircraft;
        public FlightInput Input => Game.Input;
        public MissionDirector Missions => Game.Missions;
        public PilotSettings Settings { get; private set; }
        public HudStyles Styles { get; } = new HudStyles();
        public HudModel Model { get; private set; }
        public bool Paused { get; private set; }
        public bool DebugVisible { get; private set; }
        public string Notice { get; private set; } = "Welcome to Port Meridian. Raise collective gently to lift off.";
        public bool NoticeVisible => Time.unscaledTime < noticeUntil;

        private readonly IPreferenceStorage storage = new PlayerPrefsStorage();
        private FlightInstrumentsView instruments;
        private FlightDeskMenu menu;
        private float noticeUntil = 12f, shiftConfirmUntil;
        private bool firstRun;

        private void Start()
        {
            Settings = PilotSettingsStore.Load(storage);
            Aircraft.Assists = Settings.Assists;
            Missions.PlayerRealism = Settings.Realism;
            Missions.Units = Settings.Units;
            Audio.Volume = Settings.Volume;
            ApplyGraphics();
            firstRun = !Settings.FirstRunComplete;
            Model = new HudModel(this);
            instruments = new FlightInstrumentsView(this);
            menu = new FlightDeskMenu(this);

            Input.PauseRequested += TogglePause;
            Input.ResetRequested += Retry;
            Input.InteractRequested += Interact;
            Input.DebugRequested += ToggleDebug;
            Input.AssistRequested += CycleAssists;
            Input.HoverRequested += ToggleHoverHold;
            Input.RecordRequested += ToggleRecording;
            Aircraft.ResetPerformed += ResetInput;
            Aircraft.SystemFailed += OnSystemFailure;
            Aircraft.HoverHold.Changed += OnHoverHoldChanged;
            Missions.FeedbackEvent += MissionFeedback;
            Missions.StartFreeFlight();
            ApplyLivery();
            if (firstRun) OpenWelcome();
        }

        private void Update()
        {
            if (Model == null) return;
            menu.UpdateNavigation();
            using (ModelMarker.Auto()) Model.Refresh();
            // In flight nothing on the HUD takes input, so IMGUI skips its layout pass and input events and draws on
            // repaint only. The Flight Desk (GUILayout) and the crash panel's Retry button need every event.
            useGUILayout = Paused || Aircraft.Crashed;
        }

        internal static readonly ProfilerMarker ModelMarker = new ProfilerMarker("HUD.Model"), GuiMarker = new ProfilerMarker("HUD.OnGUI");

        private void OnGUI()
        {
            if (!useGUILayout && Event.current.type != EventType.Repaint) return;
            using var scope = GuiMarker.Auto();
            if (Game == null || Game.Aircraft == null || Model == null) return;
            Styles.Build();
            Matrix4x4 previous = GUI.matrix;
            float scale = Screen.height / Height;
            Width = Screen.width / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            instruments.Draw();
            if (Paused && useGUILayout) menu.Draw();
            GUI.matrix = previous;
        }

        /// <summary>The current binding for an action, named for the device used last.</summary>
        public string Key(string action) => Input.BindingLabel(action, Input.LastInputWasGamepad);

        public void Notify(string text, float seconds)
        {
            Notice = text;
            noticeUntil = Time.unscaledTime + seconds;
        }

        public void TogglePause() { DismissFirstRun(); SetPause(!Paused); }

        public void SetPause(bool state)
        {
            Paused = state;
            Input.SetPaused(state);
            Time.timeScale = state ? 0f : 1f;
            menu?.ResetNavigation();
            if (!state) Save();
        }

        public void SelectMenuPage(int index) => menu.SelectPage(index);

        /// <summary>Diagnostics only: show the new-pilot panel for a capture without reading or writing preferences.</summary>
        public void PreviewFirstRun() { firstRun = true; OpenWelcome(); }

        /// <summary>Hide the new-pilot panel; remember=false (diagnostics) leaves the player's first launch untouched.</summary>
        public void DismissFirstRun(bool remember = true)
        {
            if (!firstRun) return;
            firstRun = false;
            menu.CloseWelcome();
            if (remember) { Settings.FirstRunComplete = true; Save(); }
        }

        /// <summary>Apply the graphics preset, VSync and frame-rate cap from the pilot's settings.</summary>
        public void ApplyGraphics()
            => GraphicsQuality.Apply(Settings.Graphics, Game.CameraRig != null ? Game.CameraRig.GetComponent<Camera>() : null);

        public void Save()
        {
            if (Settings == null) return;
            Settings.Volume = Audio.Volume;
            PilotSettingsStore.Save(storage, Settings);
            Input.SaveSettings();
        }

        /// <summary>Paint the aircraft in the pilot's selected livery.</summary>
        public void ApplyLivery()
        {
            var visual = Aircraft != null ? Aircraft.GetComponent<HelicopterVisual>() : null;
            if (visual != null && Missions.Progression != null) visual.ApplyLivery(Liveries.Get(Missions.Progression.SelectedLivery));
        }

        /// <summary>Switch display units; objectives already shown keep theirs until the next drill or job.</summary>
        public void SetUnits(UnitSystem units)
        {
            Settings.Units = units;
            Missions.Units = units;
            if (Missions.CurrentMission != null) Missions.CurrentMission.Units = units;
            Save();
        }

        /// <summary>Apply edited realism outside drills (drills layer their own effects) and remember it.</summary>
        public void ApplyRealismChange()
        {
            Missions.PlayerRealism.Sanitize();
            if (Missions.Mode != GameMode.Training) Missions.ApplyPlayerRealism();
            Save();
        }

        public void Retry()
        {
            DismissFirstRun();
            Missions.Retry();
            Input.ResetCommand();
            Notify("Reset complete. Collective is at 0%.", 4f);
        }

        private void OpenWelcome()
        {
            SetPause(true);
            menu.OpenWelcome();
        }

        private void Interact()
        {
            if (Paused) return;
            if (Missions.Mode == GameMode.FreeFlight)
            {
                // Starting a shift returns the aircraft to home base: immediate when already there, confirmed otherwise.
                LandingZone home = Game.Zones != null && Game.Zones.Length > 0 ? Game.Zones[0] : null;
                bool atHome = home != null && Aircraft.Grounded && Vector3.Distance(Aircraft.transform.position, home.transform.position) < home.Radius + 2;
                if (atHome || Time.unscaledTime < shiftConfirmUntil)
                {
                    shiftConfirmUntil = 0f;
                    Missions.StartShift();
                    Input.ResetCommand();
                    return;
                }
                shiftConfirmUntil = Time.unscaledTime + 4f;
                Notify($"Press {Key("Interact")} again to start a delivery shift from home base.", 4f);
                return;
            }
            Missions.Interact();
        }

        private void OnSystemFailure(SystemFailure failure)
        {
            UnitSystem units = Settings != null ? Settings.Units : UnitSystem.Metric;
            Notify(failure == SystemFailure.EngineOut
                ? $"ENGINE FAILURE. Lower collective now: autorotate, hold {UnitFormat.FormatSpeedBand(20f, 25f, units)}, flare near {UnitFormat.FormatRoundHeight(30f, units)}."
                : "TAIL ROTOR FAILURE. Reduce collective to cut torque; keep forward speed and land running.", 8f);
        }

        private void ResetInput()
        {
            Input.ResetCommand();
            Game.CameraRig.SnapToTarget();
        }

        private void MissionFeedback(string message)
        {
            Notify(message, 6f);
            if (message.StartsWith("Loaded", StringComparison.Ordinal) || message.StartsWith("Delivered", StringComparison.Ordinal)
                || message.StartsWith("Drill complete", StringComparison.Ordinal))
                Audio.ServiceChime();
        }

        private void ToggleDebug() => DebugVisible = !DebugVisible;

        private void ToggleRecording()
        {
            FlightRecorder recorder = Game.Recorder;
            if (recorder == null) return;
            recorder.Toggle();
            Notify(recorder.IsRecording ? "Flight recording started. Press F3 again to save."
                : "Flight recording saved: " + System.IO.Path.GetFileName(recorder.CurrentPath), 6f);
        }

        private void ToggleHoverHold()
        {
            if (Paused) return;
            if (Aircraft.HoverHold.Engaged) Aircraft.HoverHold.Disengage("Hover hold off.");
            else if (!Aircraft.HoverHold.TryEngage(Aircraft, Input.Command.Collective, out string reason)) Notify(reason, 4f);
        }

        private void OnHoverHoldChanged(bool engaged, string reason) => Notify(reason, engaged ? 4f : 3f);

        private void CycleAssists()
        {
            AssistPreset? current = Aircraft.Assists.MatchingPreset;
            var next = (AssistPreset)(current.HasValue ? ((int)current.Value + 1) % 3 : 0);
            Aircraft.SetPreset(next);
            Save();
            Notify($"Assists: {next}  ·  {Aircraft.Assists.Summary}", 4f);
        }

        private void OnDestroy()
        {
            if (Game != null && Game.Input != null)
            {
                Input.PauseRequested -= TogglePause;
                Input.ResetRequested -= Retry;
                Input.InteractRequested -= Interact;
                Input.DebugRequested -= ToggleDebug;
                Input.AssistRequested -= CycleAssists;
                Input.HoverRequested -= ToggleHoverHold;
                Input.RecordRequested -= ToggleRecording;
                if (Aircraft != null)
                {
                    Aircraft.ResetPerformed -= ResetInput;
                    Aircraft.SystemFailed -= OnSystemFailure;
                    Aircraft.HoverHold.Changed -= OnHoverHoldChanged;
                }
                if (Game.Missions != null) Missions.FeedbackEvent -= MissionFeedback;
            }
            instruments?.Dispose();
            Styles.Dispose();
            FlightHudGraphics.ReleaseRings();
            GraphicsQuality.Release();
        }
    }
}
