using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HoverForHire.Tests
{
    public sealed class InputProcessingTests
    {
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void RelativeMouseUsesSamePhysicalMovementAcrossFrameRates(int fps)
        {
            Vector2 actual = Vector2.zero;
            Vector2 velocity = new Vector2(85f, -40f);
            const float sensitivity = 0.0035f, returnRate = 1.6f;
            for (int i = 0; i < fps; i++)
                actual = FlightInputMath.IntegrateMouse(actual, velocity / fps, sensitivity, returnRate, 1f / fps);
            Vector2 expected = velocity * (sensitivity * (1f - Mathf.Exp(-returnRate)) / returnRate);
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.000005f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void VirtualJoystickIsTotalDisplacementAndHoldsWhenMouseStops(int fps)
        {
            Vector2 actual = Vector2.zero;
            Vector2 total = new Vector2(100f, -60f);
            for (int i = 0; i < fps; i++)
                actual = FlightInputMath.IntegrateMouse(actual, total / fps, 0.003f, 0f, 1f / fps);
            Assert.That(Vector2.Distance(actual, total * 0.003f), Is.LessThan(0.000005f));
            Assert.That(FlightInputMath.IntegrateMouse(actual, Vector2.zero, 0.003f, 0f, 4f), Is.EqualTo(actual));
        }

        [TestCase(30)]
        [TestCase(144)]
        public void ReturnToCenterHasSameDecayAcrossFrameRates(int fps)
        {
            Vector2 actual = new Vector2(0.6f, -0.4f);
            for (int i = 0; i < fps; i++)
                actual = FlightInputMath.IntegrateMouse(actual, Vector2.zero, 0f, 2f, 1f / fps);
            Assert.That(Vector2.Distance(actual, new Vector2(0.6f, -0.4f) * Mathf.Exp(-2f)), Is.LessThan(0.000005f));
        }

        [Test]
        public void KeyboardAndMouseCombineContinuouslyInsteadOfTakingOver()
        {
            Vector2 heldMouse = new Vector2(0.35f, 0.2f);
            Vector2 keyboard = FlightInputMath.SmoothKeyboard(Vector2.zero, Vector2.left, 12f, 1f / 144f);
            Vector2 command = FlightInputMath.Combine(heldMouse, keyboard, Vector2.zero);
            Assert.That(command.x, Is.GreaterThan(0.25f).And.LessThan(0.35f));
            Assert.That(command.y, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(FlightInputMath.Combine(Vector2.one, Vector2.one, Vector2.one).magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CollectivePersistsAndOpposingInputsCancel()
        {
            float collective = FlightInputMath.IntegrateCollective(0.4f, 1f, 0f, 0.24f, 0.5f);
            Assert.That(collective, Is.EqualTo(0.52f).Within(0.0001f));
            Assert.That(FlightInputMath.IntegrateCollective(collective, 0f, 0f, 0.24f, 10f), Is.EqualTo(collective));
            Assert.That(FlightInputMath.IntegrateCollective(collective, 1f, 1f, 0.24f, 1f), Is.EqualTo(collective));
            Assert.That(FlightInputMath.IntegrateCollective(0.95f, 1f, 0f, 0.24f, 1f), Is.EqualTo(1f));
        }

        [Test]
        public void AbsoluteCollectiveHandlesSignedUnsignedAndInvertedHardware()
        {
            Assert.That(FlightInputMath.AbsoluteCollective(-1f, true, false), Is.Zero);
            Assert.That(FlightInputMath.AbsoluteCollective(0f, true, false), Is.EqualTo(0.5f));
            Assert.That(FlightInputMath.AbsoluteCollective(0.8f, false, true), Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void DeadzoneAndCurveAreBoundedContinuousAtCenter()
        {
            Assert.That(FlightInputMath.Shape(new Vector2(0.03f, 0f), 0.04f, 1.35f), Is.EqualTo(Vector2.zero));
            Assert.That(FlightInputMath.Shape(new Vector2(0.0401f, 0f), 0.04f, 1.35f).magnitude, Is.LessThan(0.001f));
            Assert.That(FlightInputMath.Shape(new Vector2(7f, 2f), 0.04f, 1.35f).magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void FreeLookConsumesMouseOnlyForCameraAndDiscardsReleaseMovement()
        {
            var settings = new InputPreferences { MouseMode = MouseCyclicMode.VirtualJoystick, FreeLookBehavior = FreeLookCyclicMode.Hold };
            Vector2 held = new Vector2(0.3f, -0.2f);
            Vector2 moved = FlightInputMath.ProcessMouse(held, new Vector2(90f, 50f), settings,
                true, false, false, 1f / 60f, out Vector2 camera);
            Assert.That(moved, Is.EqualTo(held));
            Assert.That(camera.x, Is.GreaterThan(0f));
            Vector2 released = FlightInputMath.ProcessMouse(moved, new Vector2(200f, 80f), settings,
                false, true, false, 1f / 60f, out Vector2 releaseLook);
            Assert.That(released, Is.EqualTo(held));
            Assert.That(releaseLook, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void FreeLookReturnChangesOnlyByConfiguredDecay()
        {
            var settings = new InputPreferences { FreeLookBehavior = FreeLookCyclicMode.ReturnToCenter, MouseReturnRate = 2f };
            Vector2 held = new Vector2(0.3f, -0.2f);
            Vector2 actual = FlightInputMath.ProcessMouse(held, new Vector2(1000f, 300f), settings,
                true, true, false, 0.5f, out _);
            Assert.That(Vector2.Distance(actual, held * Mathf.Exp(-1f)), Is.LessThan(0.000001f));
        }

        [Test]
        public void BindingOverridesSurviveNewProgrammaticActionIds()
        {
            InputActionAsset first = FlightInput.CreateDefaultActions();
            InputActionAsset restarted = FlightInput.CreateDefaultActions();
            try
            {
                first.FindAction("Pause").ApplyBindingOverride(0, "<Keyboard>/p");
                first.FindAction("KeyboardCyclic").ApplyBindingOverride(1, "<Keyboard>/upArrow");
                first.FindAction("AbsoluteCollective").ApplyBindingOverride(0, "<Joystick>/stick/y");
                Assert.That(first.FindAction("Pause").id, Is.Not.EqualTo(restarted.FindAction("Pause").id));
                FlightInput.LoadBindingOverrides(restarted, FlightInput.SerializeBindingOverrides(first));
                Assert.That(restarted.FindAction("Pause").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/p"));
                Assert.That(restarted.FindAction("KeyboardCyclic").bindings[1].effectivePath, Is.EqualTo("<Keyboard>/upArrow"));
                Assert.That(restarted.FindAction("AbsoluteCollective").bindings[0].effectivePath, Is.EqualTo("<Joystick>/stick/y"));
                Assert.That(restarted.FindAction("Pause").bindings[1].effectivePath, Is.EqualTo("<Gamepad>/start"));
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(restarted); }
        }

        [Test]
        public void PreferencesSanitizeCorruptNumbers()
        {
            var settings = new InputPreferences { MouseSensitivity = float.NaN, CameraDistance = -100f, ResponseCurve = 50f };
            settings.Sanitize();
            Assert.That(settings.MouseSensitivity, Is.EqualTo(0.0035f));
            Assert.That(settings.CameraDistance, Is.EqualTo(5f));
            Assert.That(settings.ResponseCurve, Is.EqualTo(3f));
        }
    }
}
