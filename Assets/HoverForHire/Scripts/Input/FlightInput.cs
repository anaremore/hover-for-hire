using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HoverForHire
{
    [DefaultExecutionOrder(-150)]
    public sealed class FlightInput : MonoBehaviour, IFlightInput
    {
        public const string PreferencesKey = "HoverForHire.Controls.v1";
        public const string BindingsKey = "HoverForHire.Bindings.v1";
        public InputPreferences Settings = new InputPreferences();
        /// <summary>Set false before Awake for isolated test/replay rigs that must not touch local preferences.</summary>
        public bool PersistenceEnabled { get; set; } = true;
        public PilotCommand Command { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsFreeLooking { get; private set; }
        public bool IsRebinding => _rebind != null;
        public Vector2 CameraLookDelta { get; private set; }
        public Vector2 MenuMove { get; private set; }
        public bool MenuSubmitPressed { get; private set; }
        public bool MenuBackPressed { get; private set; }
        public Vector2 MouseCyclic => _mouseCyclic;
        public IReadOnlyList<InputAction> Actions => _actions;
        public InputActionAsset ActionAsset => _asset;

        public event Action PauseRequested;
        public event Action ResetRequested;
        public event Action CameraRequested;
        public event Action RecenterRequested;
        public event Action InteractRequested;
        public event Action DebugRequested;
        public event Action AssistRequested;
        public event Action HoverRequested;

        private InputActionAsset _asset;
        private InputActionMap _map;
        private readonly List<InputAction> _actions = new List<InputAction>();
        private InputAction _keyboard, _gamepad, _mouse, _look, _freeLook, _yawLeft, _yawRight;
        private InputAction _increase, _decrease, _absolute;
        private Vector2 _mouseCyclic, _keyboardCyclic;
        private float _collective;
        private bool _skipMouseFrame = true;
        private InputActionRebindingExtensions.RebindingOperation _rebind;

        private void Awake()
        {
            _asset = CreateDefaultActions();
            _map = _asset.FindActionMap("Flight", true);
            foreach (InputAction action in _map.actions) _actions.Add(action);
            _keyboard = _map["KeyboardCyclic"]; _gamepad = _map["GamepadCyclic"];
            _mouse = _map["MouseCyclic"]; _look = _map["CameraLook"]; _freeLook = _map["FreeLook"];
            _yawLeft = _map["YawLeft"]; _yawRight = _map["YawRight"];
            _increase = _map["CollectiveIncrease"]; _decrease = _map["CollectiveDecrease"];
            _absolute = _map["AbsoluteCollective"];
            if (PersistenceEnabled) LoadSettings();
            Command = new PilotCommand(Vector2.zero, 0f, 0f);
        }

        private void OnEnable() { _asset?.Enable(); _skipMouseFrame = true; }
        private void Start() { UpdateCursor(); }
        private void OnDisable() { CancelRebind(); _asset?.Disable(); }
        private void OnDestroy()
        {
            CancelRebind();
            if (_asset != null) Destroy(_asset);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnApplicationFocus(bool focused)
        {
            _skipMouseFrame = true;
            CameraLookDelta = MenuMove = Vector2.zero;
            MenuSubmitPressed = MenuBackPressed = false;
            if (!focused && !IsPaused)
            {
                PauseRequested?.Invoke();
                if (!IsPaused) SetPaused(true);
            }
        }

        private void Update() => SampleFrame(Time.deltaTime);

        /// <summary>Sample the action state once per render/input frame; exposed for deterministic device replay.</summary>
        public void SampleFrame(float deltaSeconds)
        {
            CameraLookDelta = Vector2.zero;
            MenuMove = Vector2.zero;
            MenuSubmitPressed = MenuBackPressed = false;
            if (IsRebinding)
            {
                if (_map["MenuBack"].WasPressedThisFrame()) CancelRebind();
                return;
            }
            if (_map["Pause"].WasPressedThisFrame()) PauseRequested?.Invoke();
            if (IsPaused)
            {
                MenuMove = _map["MenuMove"].ReadValue<Vector2>();
                MenuSubmitPressed = _map["MenuSubmit"].WasPressedThisFrame();
                MenuBackPressed = _map["MenuBack"].WasPressedThisFrame();
                return;
            }
            if (_map["Reset"].WasPressedThisFrame())
            {
                ResetCommand();
                ResetRequested?.Invoke();
                return; // Held controls cannot immediately undo the neutral reset in this frame.
            }
            if (_map["SwitchCamera"].WasPressedThisFrame()) CameraRequested?.Invoke();
            if (_map["RecenterView"].WasPressedThisFrame()) RecenterRequested?.Invoke();
            if (_map["Interact"].WasPressedThisFrame()) InteractRequested?.Invoke();
            if (_map["Debug"].WasPressedThisFrame()) DebugRequested?.Invoke();
            if (_map["AssistPreset"].WasPressedThisFrame()) AssistRequested?.Invoke();
            if (_map["HoverHold"].WasPressedThisFrame()) HoverRequested?.Invoke();
            if (_map["RecenterCyclic"].WasPressedThisFrame()) RecenterCyclic();

            float dt = Mathf.Max(0f, deltaSeconds);
            Vector2 mouseDelta = _mouse.ReadValue<Vector2>();
            bool freeLook = _freeLook.IsPressed();
            // Discard the release frame and every cursor-lock transition. There is no queued mouse state.
            _mouseCyclic = FlightInputMath.ProcessMouse(_mouseCyclic, mouseDelta, Settings,
                freeLook, IsFreeLooking, _skipMouseFrame, dt, out Vector2 mouseLook);
            _skipMouseFrame = false;
            IsFreeLooking = freeLook;
            Vector2 lookDelta = _look.ReadValue<Vector2>() * (Settings.GamepadLookSpeed * dt);
            lookDelta += mouseLook;
            if (Settings.InvertLook) lookDelta.y = -lookDelta.y;
            CameraLookDelta = lookDelta;

            _keyboardCyclic = FlightInputMath.SmoothKeyboard(_keyboardCyclic, _keyboard.ReadValue<Vector2>(),
                Settings.KeyboardResponse, dt);
            Vector2 mouseCommand = FlightInputMath.Shape(_mouseCyclic, Settings.Deadzone, Settings.ResponseCurve);
            Vector2 gamepadCommand = FlightInputMath.Shape(_gamepad.ReadValue<Vector2>(), Settings.Deadzone, Settings.ResponseCurve);
            Vector2 cyclic = FlightInputMath.Combine(mouseCommand, _keyboardCyclic, gamepadCommand);
            if (Settings.UseAbsoluteCollective && _absolute.controls.Count > 0)
                _collective = FlightInputMath.AbsoluteCollective(_absolute.ReadValue<float>(),
                    Settings.AbsoluteAxisSigned, Settings.InvertAbsoluteCollective);
            else
                _collective = FlightInputMath.IntegrateCollective(_collective, _increase.ReadValue<float>(),
                    _decrease.ReadValue<float>(), Settings.CollectiveRate, dt);
            float yaw = Mathf.Clamp(_yawRight.ReadValue<float>() - _yawLeft.ReadValue<float>(), -1f, 1f);
            Command = new PilotCommand(cyclic, yaw, _collective);
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            CameraLookDelta = Vector2.zero;
            _skipMouseFrame = true;
            UpdateCursor();
        }

        private void UpdateCursor()
        {
            Cursor.lockState = IsPaused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = IsPaused;
        }

        public void RecenterCyclic()
        {
            _mouseCyclic = Vector2.zero;
            _skipMouseFrame = true;
        }

        public void ResetCommand(float collective = 0f)
        {
            RecenterCyclic();
            _keyboardCyclic = Vector2.zero;
            _collective = Mathf.Clamp01(collective);
            Command = new PilotCommand(Vector2.zero, 0f, _collective);
        }

        public void SaveSettings()
        {
            Settings.Sanitize();
            if (!PersistenceEnabled) return;
            PlayerPrefs.SetString(PreferencesKey, JsonUtility.ToJson(Settings));
            PlayerPrefs.SetString(BindingsKey, SerializeBindingOverrides(_asset));
            PlayerPrefs.Save();
        }

        private void LoadSettings()
        {
            if (PlayerPrefs.HasKey(PreferencesKey))
            {
                try { JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(PreferencesKey), Settings); }
                catch (Exception) { Debug.LogWarning("Control preferences were unreadable; using defaults."); Settings = new InputPreferences(); }
            }
            Settings.Sanitize();
            if (PlayerPrefs.HasKey(BindingsKey))
            {
                try { LoadBindingOverrides(_asset, PlayerPrefs.GetString(BindingsKey)); }
                catch (Exception) { Debug.LogWarning("Saved control bindings were unreadable; using defaults."); _asset.RemoveAllBindingOverrides(); }
            }
        }

        public void RestoreDefaults()
        {
            CancelRebind();
            Settings = new InputPreferences();
            _asset.RemoveAllBindingOverrides();
            RecenterCyclic();
            SaveSettings();
        }

        public void BeginRebind(Guid actionId, int bindingIndex, Action<bool> completed = null)
        {
            CancelRebind();
            InputAction action = _asset.FindAction(actionId.ToString(), true);
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count || action.bindings[bindingIndex].isComposite)
            {
                completed?.Invoke(false);
                return;
            }
            _map.Disable();
            // Build explicitly: the convenience preset excludes mouse delta, which is a real binding here.
            _rebind = new InputActionRebindingExtensions.RebindingOperation()
                .WithAction(action).WithTargetBinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Pointer>/position")
                .OnMatchWaitForAnother(0.12f)
                .OnPotentialMatch(operation =>
                {
                    bool cancel = false;
                    if (action.name != "MenuBack")
                        foreach (InputControl control in _map["MenuBack"].controls)
                            if (operation.selectedControl == control) cancel = true;
                    if (cancel) operation.Cancel(); else operation.Complete();
                })
                .OnCancel(operation => FinishRebind(operation, false, completed))
                .OnComplete(operation => FinishRebind(operation, true, completed));
            if (action.name != "MouseCyclic") _rebind.WithControlsExcluding("<Pointer>/delta");
            if (action.bindings[bindingIndex].isPartOfComposite) _rebind.WithExpectedControlType("Button");
            _rebind.Start();
            if (action.name != "MenuBack") _map["MenuBack"].Enable();
        }

        private void FinishRebind(InputActionRebindingExtensions.RebindingOperation operation, bool success, Action<bool> completed)
        {
            _rebind = null;
            operation.Dispose();
            if (isActiveAndEnabled) _map.Enable();
            _skipMouseFrame = true;
            if (success) SaveSettings();
            completed?.Invoke(success);
        }

        public void CancelRebind() { _rebind?.Cancel(); }

        public static InputActionAsset CreateDefaultActions()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "Hover for Hire Controls";
            var map = new InputActionMap("Flight");
            asset.AddActionMap(map);
            Add(map, "KeyboardCyclic", InputActionType.Value, "Vector2")
                .AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Add(map, "GamepadCyclic", InputActionType.Value, "Vector2", "<Gamepad>/leftStick");
            Add(map, "MouseCyclic", InputActionType.PassThrough, "Vector2", "<Mouse>/delta");
            Add(map, "YawLeft", InputActionType.Value, "Axis", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            Add(map, "YawRight", InputActionType.Value, "Axis", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            Add(map, "CollectiveIncrease", InputActionType.Value, "Axis", "<Keyboard>/leftShift", "<Gamepad>/rightTrigger");
            Add(map, "CollectiveDecrease", InputActionType.Value, "Axis", "<Keyboard>/leftCtrl", "<Gamepad>/leftTrigger");
            Add(map, "AbsoluteCollective", InputActionType.Value, "Axis", "");
            Add(map, "CameraLook", InputActionType.Value, "Vector2", "<Gamepad>/rightStick");
            Add(map, "FreeLook", InputActionType.Button, "Button", "<Keyboard>/leftAlt", "<Mouse>/middleButton", "<Gamepad>/leftStickPress");
            Add(map, "SwitchCamera", InputActionType.Button, "Button", "<Keyboard>/v", "<Gamepad>/buttonNorth");
            Add(map, "RecenterView", InputActionType.Button, "Button", "<Keyboard>/r", "<Gamepad>/rightStickPress");
            Add(map, "RecenterCyclic", InputActionType.Button, "Button", "<Keyboard>/c", "<Gamepad>/dpad/down");
            Add(map, "Pause", InputActionType.Button, "Button", "<Keyboard>/escape", "<Gamepad>/start");
            Add(map, "Reset", InputActionType.Button, "Button", "<Keyboard>/backspace", "<Gamepad>/select");
            Add(map, "Interact", InputActionType.Button, "Button", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            Add(map, "Debug", InputActionType.Button, "Button", "<Keyboard>/f1", "<Gamepad>/dpad/right");
            Add(map, "AssistPreset", InputActionType.Button, "Button", "<Keyboard>/f2", "<Gamepad>/dpad/up");
            Add(map, "HoverHold", InputActionType.Button, "Button", "<Keyboard>/h", "<Gamepad>/buttonWest");
            Add(map, "MenuMove", InputActionType.Value, "Vector2", "<Gamepad>/dpad", "<Gamepad>/leftStick");
            Add(map, "MenuSubmit", InputActionType.Button, "Button", "<Gamepad>/buttonSouth");
            Add(map, "MenuBack", InputActionType.Button, "Button", "<Gamepad>/buttonEast");
            return asset;
        }

        private static InputAction Add(InputActionMap map, string name, InputActionType type, string controlType, params string[] bindings)
        {
            InputAction action = map.AddAction(name, type);
            action.expectedControlType = controlType;
            foreach (string path in bindings) action.AddBinding(path);
            return action;
        }

        [Serializable] private sealed class SavedBindings { public int Version = 1; public List<SavedBinding> Bindings = new List<SavedBinding>(); }
        [Serializable] private sealed class SavedBinding
        {
            public string Action, DefaultPath, PartName, Path, Interactions, Processors;
            public int Index;
        }

        // Programmatic actions get new GUIDs at launch. Save semantic binding keys instead of transient IDs.
        public static string SerializeBindingOverrides(InputActionAsset asset)
        {
            var saved = new SavedBindings();
            foreach (InputAction action in asset)
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    if (!binding.hasOverrides) continue;
                    saved.Bindings.Add(new SavedBinding { Action = action.name, Index = i,
                        DefaultPath = binding.path, PartName = binding.name, Path = binding.overridePath,
                        Interactions = binding.overrideInteractions, Processors = binding.overrideProcessors });
                }
            return JsonUtility.ToJson(saved);
        }

        public static void LoadBindingOverrides(InputActionAsset asset, string json)
        {
            SavedBindings saved = JsonUtility.FromJson<SavedBindings>(json);
            if (saved == null || saved.Version != 1 || saved.Bindings == null) return;
            foreach (SavedBinding entry in saved.Bindings)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Action)) continue;
                InputAction action = asset.FindAction(entry.Action);
                if (action == null) continue;
                int index = -1;
                for (int i = 0; i < action.bindings.Count; i++)
                    if ((action.bindings[i].path ?? "") == (entry.DefaultPath ?? "") &&
                        (action.bindings[i].name ?? "") == (entry.PartName ?? ""))
                    {
                        index = i;
                        if (i == entry.Index) break;
                    }
                if (index < 0 || action.bindings[index].isComposite) continue;
                action.ApplyBindingOverride(index, new InputBinding { overridePath = entry.Path,
                    overrideInteractions = entry.Interactions, overrideProcessors = entry.Processors });
            }
        }
    }
}
