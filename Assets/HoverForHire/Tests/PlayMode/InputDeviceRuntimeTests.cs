using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HoverForHire.Tests
{
    /// <summary>
    /// Exercises real Input System state events and resolved actions through the production sampler.
    /// Each rig sees only its synthetic devices. Existing devices and saved preferences are preserved.
    /// </summary>
    public sealed class InputDeviceRuntimeTests
    {
        private GameObject _rig;
        private FlightInput _input;
        private Keyboard _keyboard, _previousKeyboard;
        private Mouse _mouse, _previousMouse;
        private Gamepad _gamepad, _previousGamepad;
        private InputSettings.UpdateMode _previousUpdateMode;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;
        private string _preferencesBefore, _bindingsBefore;
        private bool _hadPreferences, _hadBindings;
        private const float Frame = 1f / 60f;

        [SetUp]
        public void SetUp()
        {
            _previousUpdateMode = InputSystem.settings.updateMode;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _previousKeyboard = Keyboard.current; _previousMouse = Mouse.current; _previousGamepad = Gamepad.current;
            _hadPreferences = PlayerPrefs.HasKey(FlightInput.PreferencesKey);
            _hadBindings = PlayerPrefs.HasKey(FlightInput.BindingsKey);
            _preferencesBefore = PlayerPrefs.GetString(FlightInput.PreferencesKey);
            _bindingsBefore = PlayerPrefs.GetString(FlightInput.BindingsKey);
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            _gamepad = InputSystem.AddDevice<Gamepad>();
            _rig = new GameObject("Isolated input device rig");
            _rig.SetActive(false);
            _input = _rig.AddComponent<FlightInput>();
            _input.PersistenceEnabled = false;
            _rig.SetActive(true);
            _input.enabled = false; // Explicit input frames below replace MonoBehaviour.Update, not its logic.
            _input.ActionAsset.devices = new InputDevice[] { _keyboard, _mouse, _gamepad };
            _input.ActionAsset.Enable();
            _input.Settings = new InputPreferences { MouseMode = MouseCyclicMode.VirtualJoystick };
            _input.ResetCommand();
            Step(); // Consume the initial lock/reset delta discard.
        }

        [TearDown]
        public void TearDown()
        {
            if (_rig != null) Object.DestroyImmediate(_rig);
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            if (_gamepad != null && _gamepad.added) InputSystem.RemoveDevice(_gamepad);
            if (_previousKeyboard != null && _previousKeyboard.added) _previousKeyboard.MakeCurrent();
            if (_previousMouse != null && _previousMouse.added) _previousMouse.MakeCurrent();
            if (_previousGamepad != null && _previousGamepad.added) _previousGamepad.MakeCurrent();
            InputSystem.settings.updateMode = _previousUpdateMode;
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
            // Suppression makes these read-only checks: the fixture never deletes or overwrites real saves.
            Assert.That(PlayerPrefs.HasKey(FlightInput.PreferencesKey), Is.EqualTo(_hadPreferences));
            Assert.That(PlayerPrefs.HasKey(FlightInput.BindingsKey), Is.EqualTo(_hadBindings));
            Assert.That(PlayerPrefs.GetString(FlightInput.PreferencesKey), Is.EqualTo(_preferencesBefore));
            Assert.That(PlayerPrefs.GetString(FlightInput.BindingsKey), Is.EqualTo(_bindingsBefore));
        }

        private void Step(KeyboardState keyboard = default, GamepadState gamepad = default, MouseState mouse = default)
        {
            InputSystem.QueueStateEvent(_keyboard, keyboard);
            InputSystem.QueueStateEvent(_gamepad, gamepad);
            InputSystem.QueueStateEvent(_mouse, mouse);
            InputSystem.Update();
            _input.SampleFrame(Frame);
        }

        [Test]
        public void KeyboardTakeoffInputsProduceForwardRightYawAndPersistentCollective()
        {
            var held = new KeyboardState(Key.W, Key.D, Key.E, Key.LeftShift);
            for (int i = 0; i < 150; i++) Step(held);
            Assert.That(_input.Command.Collective, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(_input.Command.Cyclic.x, Is.GreaterThan(0.65f));
            Assert.That(_input.Command.Cyclic.y, Is.GreaterThan(0.65f));
            Assert.That(_input.Command.Yaw, Is.EqualTo(1f));
            float collective = _input.Command.Collective;
            for (int i = 0; i < 60; i++) Step();
            Assert.That(_input.Command.Collective, Is.EqualTo(collective));
            Assert.That(_input.Command.Cyclic.magnitude, Is.LessThan(0.001f));
            Assert.That(_input.Command.Yaw, Is.Zero);
        }

        [Test]
        public void GamepadTakeoffInputsProduceCyclicYawAndProportionalPersistentCollective()
        {
            var held = new GamepadState { leftStick = new Vector2(0.6f, 0.8f), rightTrigger = 0.5f }
                .WithButton(GamepadButton.LeftShoulder);
            for (int i = 0; i < 300; i++) Step(gamepad: held);
            Assert.That(_input.Command.Collective, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(_input.Command.Cyclic.x, Is.GreaterThan(0.5f));
            Assert.That(_input.Command.Cyclic.y, Is.GreaterThan(0.7f));
            Assert.That(_input.Command.Yaw, Is.EqualTo(-1f));
            float collective = _input.Command.Collective;
            Step();
            Assert.That(_input.Command.Collective, Is.EqualTo(collective));
            Assert.That(_input.Command.Cyclic, Is.EqualTo(Vector2.zero));
            Step(gamepad: new GamepadState { leftTrigger = 1f });
            Assert.That(_input.Command.Collective, Is.EqualTo(collective - 0.24f * Frame).Within(0.0001f));
        }

        [Test]
        public void MouseFreeLookRoutesMotionToCameraAndDiscardsReleaseFrame()
        {
            Step(mouse: new MouseState { delta = new Vector2(60f, 20f) });
            Vector2 held = _input.Command.Cyclic;
            Assert.That(held.magnitude, Is.GreaterThan(0.05f));
            Step(new KeyboardState(Key.LeftAlt), mouse: new MouseState { delta = new Vector2(120f, -40f) });
            Assert.That(_input.IsFreeLooking, Is.True);
            Assert.That(_input.Command.Cyclic, Is.EqualTo(held));
            Assert.That(_input.CameraLookDelta, Is.EqualTo(new Vector2(120f, -40f) * _input.Settings.LookSensitivity));
            Step(mouse: new MouseState { delta = new Vector2(900f, 900f) });
            Assert.That(_input.IsFreeLooking, Is.False);
            Assert.That(_input.Command.Cyclic, Is.EqualTo(held));
            Assert.That(_input.CameraLookDelta, Is.EqualTo(Vector2.zero));
            Step();
            Assert.That(_input.Command.Cyclic, Is.EqualTo(held), "Free-look motion must never be replayed into cyclic.");
        }

        [Test]
        public void PauseIgnoresFlightMotionAndResetConsumesHeldInputs()
        {
            int resets = 0;
            _input.PauseRequested += () => _input.SetPaused(!_input.IsPaused);
            _input.ResetRequested += () => resets++;
            _input.ResetCommand(0.45f);
            Step(new KeyboardState(Key.Escape, Key.LeftShift), mouse: new MouseState { delta = new Vector2(600f, 600f) });
            Assert.That(_input.IsPaused, Is.True);
            Assert.That(_input.Command.Collective, Is.EqualTo(0.45f));
            Step(new KeyboardState(Key.Backspace, Key.LeftShift));
            Assert.That(resets, Is.Zero, "Aircraft reset is suppressed while a settings menu is open.");
            Step();
            Step(new KeyboardState(Key.Escape), mouse: new MouseState { delta = new Vector2(900f, 900f) });
            Assert.That(_input.IsPaused, Is.False);
            Assert.That(_input.Command.Cyclic, Is.EqualTo(Vector2.zero));
            Assert.That(_input.Command.Collective, Is.EqualTo(0.45f));
            Step(new KeyboardState(Key.Backspace, Key.LeftShift, Key.W), mouse: new MouseState { delta = new Vector2(500f, 500f) });
            Assert.That(resets, Is.EqualTo(1));
            Assert.That(_input.Command.Collective, Is.Zero);
            Assert.That(_input.Command.Cyclic, Is.EqualTo(Vector2.zero));
            Step(mouse: new MouseState { delta = new Vector2(500f, 500f) });
            Assert.That(_input.Command.Cyclic, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void FocusLossPausesClearsLookAndDoesNotReplayAccumulatedMouse()
        {
            Step(mouse: new MouseState { delta = new Vector2(55f, -20f) });
            Vector2 held = _input.Command.Cyclic;
            Step(new KeyboardState(Key.LeftAlt), mouse: new MouseState { delta = new Vector2(100f, 20f) });
            Assert.That(_input.CameraLookDelta.magnitude, Is.GreaterThan(1f));
            _rig.SendMessage("OnApplicationFocus", false);
            Assert.That(_input.IsPaused, Is.True);
            Assert.That(_input.CameraLookDelta, Is.EqualTo(Vector2.zero));
            for (int i = 0; i < 10; i++) Step(mouse: new MouseState { delta = new Vector2(1000f, 1000f) });
            _rig.SendMessage("OnApplicationFocus", true);
            Assert.That(_input.IsPaused, Is.True, "Regaining OS focus must not automatically resume flight.");
            _input.SetPaused(false);
            Step(mouse: new MouseState { delta = new Vector2(1000f, 1000f) });
            Assert.That(_input.Command.Cyclic, Is.EqualTo(held));
            Step();
            Assert.That(_input.Command.Cyclic, Is.EqualTo(held));
        }

        [Test]
        public void GamepadMenuActionsWorkWhileFlightInputsArePaused()
        {
            _input.SetPaused(true);
            _input.ResetCommand(0.42f);
            var state = new GamepadState { leftStick = Vector2.down, rightTrigger = 1f }.WithButton(GamepadButton.South);
            Step(gamepad: state);
            Assert.That(_input.MenuMove.y, Is.LessThan(-0.9f));
            Assert.That(_input.MenuSubmitPressed, Is.True);
            Assert.That(_input.Command.Collective, Is.EqualTo(0.42f));
            Assert.That(_input.Command.Cyclic, Is.EqualTo(Vector2.zero));
            Step();
            Step(gamepad: new GamepadState().WithButton(GamepadButton.East));
            Assert.That(_input.MenuBackPressed, Is.True);
        }

        [Test]
        public void PreferenceSuppressionPreventsAccidentalTestWrites()
        {
            _input.Settings.MouseSensitivity = 0.007f;
            _input.ActionAsset.FindAction("Pause").ApplyBindingOverride(0, "<Keyboard>/p");
            _input.SaveSettings(); // The fixture's teardown verifies both real PlayerPrefs keys are untouched.
        }
    }
}
