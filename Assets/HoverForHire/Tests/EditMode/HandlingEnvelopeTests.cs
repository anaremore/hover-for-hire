using System.Globalization;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace HoverForHire.Tests
{
    /// <summary>
    /// Pure characterization of the shipped tuning: each rotor term's sign and scale, the assist solver's
    /// feed-forward and turn coordination, and the flight recorder's CSV contract.
    /// </summary>
    public sealed class HandlingEnvelopeTests
    {
        private FlightTuning tuning;

        [SetUp]
        public void SetUp()
        {
            var shipped = Resources.Load<FlightTuning>("UtilityHelicopter");
            Assert.That(shipped, Is.Not.Null, "Resources/UtilityHelicopter must exist.");
            tuning = Object.Instantiate(shipped);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(tuning);

        /// <summary>Level still-air vertical axis using the same terms as HelicopterController.</summary>
        private static float SteadyClimbAfterStep(FlightTuning t, float massKg, float step, float seconds, out float ninetyPercentTime)
        {
            const float dt = 0.02f;
            float hover = FlightMath.HoverCollective(massKg, 9.81f, t), actuator = hover, v = 0f;
            int steps = Mathf.RoundToInt(seconds / dt);
            var history = new float[steps];
            for (int i = 0; i < steps; i++)
            {
                actuator = Mathf.Lerp(actuator, hover + step, FlightMath.ResponseFraction(dt, t.CollectiveResponseSeconds));
                float force = FlightMath.Lift(actuator, t) - massKg * 9.81f + FlightMath.HeaveDamping(v, 1f, t)
                    + FlightMath.AerodynamicDrag(new Vector3(0f, v, 0f), t.LinearDrag, t.QuadraticDrag).y;
                v += force / massKg * dt;
                history[i] = v;
            }
            ninetyPercentTime = float.PositiveInfinity;
            for (int i = 0; i < steps; i++) if (history[i] >= 0.9f * v) { ninetyPercentTime = i * dt; break; }
            return v;
        }

        [Test]
        public void HoverCollectiveTracksWeightWithPayload()
        {
            Assert.That(FlightMath.HoverCollective(FlightMath.Mass(0f, tuning), 9.81f, tuning), Is.EqualTo(0.4478f).Within(0.001f));
            Assert.That(FlightMath.HoverCollective(FlightMath.Mass(300f, tuning), 9.81f, tuning), Is.EqualTo(0.5758f).Within(0.001f));
            Assert.That(FlightMath.HoverCollective(FlightMath.Mass(0f, tuning), -9.81f, tuning),
                Is.EqualTo(FlightMath.HoverCollective(FlightMath.Mass(0f, tuning), 9.81f, tuning)), "Gravity sign must not matter.");
        }

        [Test]
        public void OnePercentCollectiveGivesAModestSettledClimb()
        {
            float climb = SteadyClimbAfterStep(tuning, FlightMath.Mass(0f, tuning), 0.01f, 20f, out float settle);
            Assert.That(climb, Is.InRange(0.40f, 0.58f));
            Assert.That(settle, Is.LessThanOrEqualTo(6.5f));
            tuning.HeaveDampingNsPerM = 0f;
            float undamped = SteadyClimbAfterStep(tuning, FlightMath.Mass(0f, tuning), 0.01f, 60f, out float slowSettle);
            Assert.That(undamped, Is.GreaterThan(2f), "With the term disabled, +1% collective produces the old runaway climb rate.");
            Assert.That(slowSettle, Is.GreaterThan(15f));
        }

        [Test]
        public void HeaveDampingOpposesVerticalMotionAndNeedsARotor()
        {
            Assert.That(FlightMath.HeaveDamping(2f, 1f, tuning), Is.EqualTo(-2f * tuning.HeaveDampingNsPerM).Within(0.001f));
            Assert.That(FlightMath.HeaveDamping(-3f, 1f, tuning), Is.GreaterThan(0f));
            Assert.That(FlightMath.HeaveDamping(4f, 0f, tuning), Is.Zero);
        }

        [Test]
        public void RotorRateDampingOpposesEachAxisAndScalesWithAuthority()
        {
            Vector3 inertia = FlightMath.Inertia(tuning.EmptyMassKg, tuning);
            Vector3 torque = FlightMath.RotorRateDamping(new Vector3(0.5f, -0.4f, 0.3f), inertia, tuning.RotorRateDampingPerSecond, 1f);
            Assert.That(torque.x, Is.LessThan(0f));
            Assert.That(torque.y, Is.GreaterThan(0f));
            Assert.That(torque.z, Is.LessThan(0f));
            Assert.That(torque.x, Is.EqualTo(-0.5f * tuning.RotorRateDampingPerSecond.x * inertia.x).Within(0.01f));
            Vector3 half = FlightMath.RotorRateDamping(new Vector3(0.5f, -0.4f, 0.3f), inertia, tuning.RotorRateDampingPerSecond, 0.5f);
            Assert.That(half.x, Is.EqualTo(torque.x * 0.5f).Within(0.01f));
            Assert.That(FlightMath.RotorRateDamping(Vector3.one, inertia, Vector3.zero, 1f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void InertiaIsExplicitAndScalesWithMass()
        {
            Vector3 empty = FlightMath.Inertia(tuning.EmptyMassKg, tuning);
            Assert.That(empty, Is.EqualTo(tuning.InertiaKgM2));
            Vector3 loaded = FlightMath.Inertia(tuning.EmptyMassKg * 1.5f, tuning);
            Assert.That(loaded.y, Is.EqualTo(empty.y * 1.5f).Within(0.01f));
        }

        [Test]
        public void WeathervaneTurnsTheNoseTowardArrivingAir()
        {
            Assert.That(FlightMath.WeathervaneYawTorque(new Vector3(3f, 0f, 30f), tuning.WeathervaneCoefficient), Is.GreaterThan(0f));
            Assert.That(FlightMath.WeathervaneYawTorque(new Vector3(-3f, 0f, 30f), tuning.WeathervaneCoefficient), Is.LessThan(0f));
            Assert.That(FlightMath.WeathervaneYawTorque(new Vector3(0f, 0f, 30f), tuning.WeathervaneCoefficient), Is.Zero);
            // Crosswind hover: air from the right with no forward speed still weathervanes.
            Assert.That(FlightMath.WeathervaneYawTorque(new Vector3(6f, 0f, 0f), tuning.WeathervaneCoefficient), Is.GreaterThan(0f));
        }

        [Test]
        public void CoordinatedTurnRateMatchesBankAndSpeed()
        {
            float bank = 20f * Mathf.Deg2Rad;
            Assert.That(FlightMath.CoordinatedTurnRate(bank, 30f, tuning), Is.EqualTo(9.81f * Mathf.Tan(bank) / 30f).Within(0.0005f));
            Assert.That(FlightMath.CoordinatedTurnRate(-bank, 30f, tuning), Is.LessThan(0f));
            Assert.That(FlightMath.CoordinatedTurnRate(bank, tuning.CoordinationStartSpeed - 1f, tuning), Is.Zero, "No coordination in the hover regime.");
            Vector3 rightBankUp = Quaternion.Inverse(Quaternion.Euler(0f, 0f, -20f)) * Vector3.up;
            Assert.That(FlightMath.BankRadians(rightBankUp) * Mathf.Rad2Deg, Is.EqualTo(20f).Within(0.01f), "Right bank is positive.");
        }

        private PilotCommand Settle(AssistSolver solver, PilotCommand raw, Vector3 angular, Vector3 up, Vector3 air, AssistSettings settings)
        {
            PilotCommand output = default;
            Vector3 inertia = FlightMath.Inertia(tuning.EmptyMassKg, tuning);
            for (int i = 0; i < 200; i++) output = solver.Step(raw, angular, up, 0f, air, inertia, tuning, settings, 0.02f);
            return output;
        }

        [Test]
        public void RateFeedForwardHoldsTheRequestedRateWithoutError()
        {
            var settings = new AssistSettings();
            settings.SetPreset(AssistPreset.Standard);
            var solver = new AssistSolver();
            solver.Reset(settings, PilotCommand.Neutral);
            float target = tuning.MaximumCyclicRateDegrees * Mathf.Deg2Rad;
            // Already rotating at the requested pitch rate: the command equals the passive damping at that rate.
            PilotCommand output = Settle(solver, new PilotCommand(Vector2.up, 0f, 0.45f), new Vector3(target, 0f, 0f), Vector3.up, Vector3.zero, settings);
            Vector3 inertia = FlightMath.Inertia(tuning.EmptyMassKg, tuning);
            float expected = target * FlightMath.PassiveRateDamping(0, inertia, tuning) / tuning.PitchTorqueNm;
            Assert.That(output.Cyclic.y, Is.EqualTo(expected).Within(0.002f));
            Assert.That(output.Cyclic.y, Is.LessThan(0.8f), "Feed-forward must leave headroom for error correction.");
            Assert.That(Mathf.Abs(output.Cyclic.x), Is.LessThan(0.001f));
        }

        [Test]
        public void YawStabilizationAddsTheCoordinatedRateOnlyAtSpeed()
        {
            var settings = new AssistSettings();
            settings.SetPreset(AssistPreset.Standard);
            settings.TorqueCompensation = false;
            Vector3 rightBankUp = Quaternion.Inverse(Quaternion.Euler(0f, 0f, -20f)) * Vector3.up;
            var cruise = new AssistSolver();
            cruise.Reset(settings, PilotCommand.Neutral);
            PilotCommand fast = Settle(cruise, new PilotCommand(Vector2.zero, 0f, 0.5f), Vector3.zero, rightBankUp, new Vector3(0f, 0f, 30f), settings);
            var hover = new AssistSolver();
            hover.Reset(settings, PilotCommand.Neutral);
            PilotCommand slow = Settle(hover, new PilotCommand(Vector2.zero, 0f, 0.5f), Vector3.zero, rightBankUp, new Vector3(0f, 0f, 3f), settings);
            Assert.That(fast.Yaw, Is.GreaterThan(0.2f), "A right bank at cruise asks for right yaw.");
            Assert.That(Mathf.Abs(slow.Yaw), Is.LessThan(0.001f), "Hovering in a bank does not command yaw.");
            settings.SetPreset(AssistPreset.Unassisted);
            var manual = new AssistSolver();
            manual.Reset(settings, PilotCommand.Neutral);
            PilotCommand unassisted = Settle(manual, new PilotCommand(Vector2.zero, 0f, 0.5f), Vector3.zero, rightBankUp, new Vector3(0f, 0f, 30f), settings);
            Assert.That(unassisted.Yaw, Is.Zero, "Coordination belongs to yaw stabilization; Unassisted relies on the fin alone.");
        }

        [Test]
        public void FlightRecordRowsMatchTheHeaderInAnyCulture()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var sample = new FlightRecordSample
                {
                    Time = 1.25f, Mode = "DeliveryShift", MissionState = "Transport", Assists = "RATE LEVEL, YAW",
                    Position = new Vector3(1.5f, 20.25f, -3f), Velocity = new Vector3(0.5f, float.NaN, 2f),
                    Raw = new PilotCommand(new Vector2(0.1f, -0.2f), 0.3f, 0.45f), MassKg = 1050f, RotorRpm = 395f
                };
                var builder = new StringBuilder();
                FlightRecordFormat.AppendRow(builder, sample);
                string row = builder.ToString();
                Assert.That(row.EndsWith("\n"), Is.True);
                string unquoted = System.Text.RegularExpressions.Regex.Replace(row.TrimEnd('\n'), "\"[^\"]*\"", "Q");
                Assert.That(unquoted.Split(',').Length, Is.EqualTo(FlightRecordFormat.Header.Split(',').Length));
                Assert.That(row, Does.Contain("1.250"), "Decimal points are invariant.");
                Assert.That(row, Does.Not.Contain("NaN"));
            }
            finally { Thread.CurrentThread.CurrentCulture = previous; }
        }
    }
}
