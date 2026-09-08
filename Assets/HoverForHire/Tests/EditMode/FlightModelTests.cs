using NUnit.Framework;
using UnityEngine;

namespace HoverForHire.Tests
{
    public sealed class FlightModelTests
    {
        private FlightTuning tuning;

        [SetUp] public void SetUp() => tuning = FlightTuning.CreateRuntimeDefaults();
        [TearDown] public void TearDown() => Object.DestroyImmediate(tuning);

        [Test]
        public void PilotCommandRejectsInvalidAndExcessiveDemand()
        {
            PilotCommand input = new PilotCommand(new Vector2(9f, 9f), 7f, -2f).Clamped();
            Assert.That(input.Cyclic.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(input.Yaw, Is.EqualTo(1f));
            Assert.That(input.Collective, Is.Zero);
            PilotCommand invalid = new PilotCommand(new Vector2(float.NaN, float.PositiveInfinity), float.NaN, float.NegativeInfinity).Clamped();
            Assert.That(invalid.Cyclic, Is.EqualTo(Vector2.zero));
            Assert.That(invalid.Yaw, Is.Zero);
            Assert.That(invalid.Collective, Is.Zero);
        }

        [TestCase(15f, 3f, -8f)]
        [TestCase(-20f, -5f, 30f)]
        [TestCase(0f, 0f, 0f)]
        public void DragNeverAddsKineticEnergy(float x, float y, float z)
        {
            Vector3 velocity = new Vector3(x, y, z);
            Vector3 drag = FlightMath.AerodynamicDrag(velocity, tuning.LinearDrag, tuning.QuadraticDrag);
            Assert.That(Vector3.Dot(velocity, drag), Is.LessThanOrEqualTo(0f));
            if (velocity == Vector3.zero) Assert.That(drag, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void PayloadReducesAccelerationWithoutChangingLiftAtSameCollective()
        {
            float lift = FlightMath.Lift(0.6f, tuning);
            float emptyAcceleration = lift / FlightMath.Mass(0f, tuning) - 9.81f;
            float loadedAcceleration = lift / FlightMath.Mass(300f, tuning) - 9.81f;
            Assert.That(loadedAcceleration, Is.LessThan(emptyAcceleration));
            Assert.That(FlightMath.Mass(9999f, tuning), Is.EqualTo(tuning.EmptyMassKg + tuning.MaximumPayloadKg));
            Assert.That(FlightMath.Lift(0f, tuning), Is.Zero);
        }

        [Test]
        public void BankingReducesVerticalLiftAndCreatesHorizontalAcceleration()
        {
            Vector3 rotor = FlightMath.LocalThrustDirection(Vector2.zero, tuning.RotorDiskTiltDegrees);
            Vector3 banked = Quaternion.Euler(0f, 0f, -30f) * rotor;
            Assert.That(banked.y, Is.EqualTo(Mathf.Cos(30f * Mathf.Deg2Rad)).Within(0.0001f));
            Assert.That(banked.x, Is.GreaterThan(0f));
            Assert.That(banked.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void UnassistedNeutralDoesNotCounterAngularMotionOrRotorTorque()
        {
            var settings = new AssistSettings();
            settings.SetPreset(AssistPreset.Unassisted);
            var solver = new AssistSolver();
            solver.Reset(settings, PilotCommand.Neutral);
            PilotCommand output = solver.Step(PilotCommand.Neutral, new Vector3(1f, 1f, 1f),
                new Vector3(0.5f, 0.7f, 0.5f), 900f, tuning, settings, 0.02f);
            Assert.That(output.Cyclic, Is.EqualTo(Vector2.zero));
            Assert.That(output.Yaw, Is.Zero);
        }

        [Test]
        public void RateAssistOpposesAngularVelocityWithBoundedActuatorCommands()
        {
            var settings = new AssistSettings { AutoLevel = false, TorqueCompensation = false };
            var solver = new AssistSolver();
            solver.Reset(settings, PilotCommand.Neutral);
            PilotCommand output = solver.Step(PilotCommand.Neutral, new Vector3(20f, 20f, -20f),
                Vector3.up, 900f, tuning, settings, 2f);
            Assert.That(output.Cyclic.x, Is.LessThan(0f));
            Assert.That(output.Cyclic.y, Is.LessThan(0f));
            Assert.That(output.Cyclic.magnitude, Is.LessThanOrEqualTo(1.00001f));
            Assert.That(output.Yaw, Is.InRange(-1f, 0f));
        }

        [Test]
        public void AutoLevelCanActWithoutRateOrYawAssistance()
        {
            var settings = new AssistSettings { RateStabilization = false, AutoLevel = true,
                YawStabilization = false, TorqueCompensation = false };
            var solver = new AssistSolver();
            solver.Reset(settings, PilotCommand.Neutral);
            Vector3 upInBankedAircraft = Quaternion.Inverse(Quaternion.Euler(15f, 0f, -20f)) * Vector3.up;
            PilotCommand output = solver.Step(PilotCommand.Neutral, Vector3.zero, upInBankedAircraft,
                900f, tuning, settings, 1f);
            Assert.That(output.Cyclic.x, Is.LessThan(0f), "Right bank should demand left cyclic.");
            Assert.That(output.Cyclic.y, Is.LessThan(0f), "Nose-down should demand aft cyclic.");
            Assert.That(output.Yaw, Is.Zero);
        }

        [Test]
        public void TorqueCompensationIsIndependentAndDoesNotAlterCollective()
        {
            var settings = new AssistSettings { RateStabilization = false, AutoLevel = false,
                YawStabilization = false, TorqueCompensation = true };
            var solver = new AssistSolver();
            var raw = new PilotCommand(Vector2.zero, 0f, 0.5f);
            solver.Reset(settings, raw);
            PilotCommand output = solver.Step(raw, Vector3.zero, Vector3.up, 700f, tuning, settings, 3f);
            Assert.That(output.Yaw, Is.EqualTo(-700f / tuning.YawTorqueNm).Within(0.0001f));
            Assert.That(output.Collective, Is.EqualTo(raw.Collective));
        }

        [Test]
        public void PresetChangesBlendWithoutOneStepControlJump()
        {
            var settings = new AssistSettings();
            settings.SetPreset(AssistPreset.Unassisted);
            var solver = new AssistSolver();
            solver.Reset(settings, PilotCommand.Neutral);
            settings.SetPreset(AssistPreset.Beginner);
            PilotCommand first = solver.Step(PilotCommand.Neutral, new Vector3(1f, 1f, -1f),
                Vector3.up, 700f, tuning, settings, 0.02f);
            Assert.That(first.Cyclic.magnitude, Is.LessThan(0.1f));
            Assert.That(Mathf.Abs(first.Yaw), Is.LessThan(0.1f));
        }

        [Test]
        public void ExponentialActuatorResponseDependsOnElapsedTime()
        {
            float Simulate(float dt)
            {
                float value = 0f;
                for (int i = 0; i < Mathf.RoundToInt(1f / dt); i++)
                    value = Mathf.Lerp(value, 1f, FlightMath.ResponseFraction(dt, tuning.CollectiveResponseSeconds));
                return value;
            }
            Assert.That(Simulate(1f / 30f), Is.EqualTo(Simulate(1f / 144f)).Within(0.00001f));
        }
    }
}
