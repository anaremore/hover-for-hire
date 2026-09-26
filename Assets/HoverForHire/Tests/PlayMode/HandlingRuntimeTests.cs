using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HoverForHire.Tests
{
    /// <summary>
    /// Handling characterization with the shipped tuning, real Rigidbody dynamics and fixed physics steps.
    /// These pin response metrics (settling, rates, sideslip) so tuning changes are measurable, not a feel verdict.
    /// </summary>
    public sealed class HandlingRuntimeTests
    {
        private FlightTestRig rig;

        [SetUp] public void SetUp() => rig = new FlightTestRig();
        [TearDown] public void TearDown() => rig.Dispose();

        private static int Steps(float seconds) => Mathf.RoundToInt(seconds / FlightTestRig.Dt);

        private List<float> ClimbAfterOnePercentStep(float payload)
        {
            HelicopterController aircraft = rig.Create(AssistPreset.Beginner, payload);
            float hover = aircraft.HoverCollective;
            rig.EstablishInFlight(aircraft, hover, 200f, Quaternion.identity, Vector3.zero);
            FlightTestRig.Input(aircraft).Value = new PilotCommand(Vector2.zero, 0f, hover + 0.01f);
            var verticalSpeeds = new List<float>();
            rig.Step(Steps(15f), () => verticalSpeeds.Add(aircraft.VerticalSpeed));
            Assert.That(aircraft.Crashed, Is.False);
            return verticalSpeeds;
        }

        private static float NinetyPercentTime(List<float> series)
        {
            float final = series[series.Count - 1];
            for (int i = 0; i < series.Count; i++) if (series[i] >= 0.9f * final) return i * FlightTestRig.Dt;
            return float.PositiveInfinity;
        }

        [Test]
        public void OnePercentAboveHoverSettlesToAGentleClimbRate()
        {
            List<float> empty = ClimbAfterOnePercentStep(0f);
            List<float> loaded = ClimbAfterOnePercentStep(300f);
            float emptyFinal = empty[empty.Count - 1], loadedFinal = loaded[loaded.Count - 1];
            Assert.That(emptyFinal, Is.InRange(0.40f, 0.58f), "Heave damping should turn +1% collective into roughly half a metre per second.");
            Assert.That(loadedFinal, Is.InRange(0.40f, 0.58f));
            Assert.That(NinetyPercentTime(empty), Is.LessThanOrEqualTo(6.5f), "Vertical speed should settle within seconds, not tens of seconds.");
            Assert.That(NinetyPercentTime(loaded), Is.GreaterThan(NinetyPercentTime(empty)), "A heavier aircraft responds more slowly.");
        }

        [Test]
        public void StandardAssistStillReachesTheMaximumPitchRate()
        {
            HelicopterController aircraft = rig.Create(AssistPreset.Standard);
            rig.EstablishInFlight(aircraft, aircraft.HoverCollective, 300f, Quaternion.identity, Vector3.zero);
            FlightTestRig.Input(aircraft).Value = new PilotCommand(Vector2.up, 0f, aircraft.HoverCollective);
            rig.Step(Steps(1f));
            Assert.That(aircraft.LocalAngularRatesDegrees.x, Is.EqualTo(aircraft.Tuning.MaximumCyclicRateDegrees).Within(1.7f),
                "Rate feed-forward must sustain the requested rate against passive rotor damping.");
        }

        [Test]
        public void UnassistedRollTapSettlesLikeARotorInsteadOfRollingOn()
        {
            HelicopterController aircraft = rig.Create(AssistPreset.Unassisted);
            rig.EstablishInFlight(aircraft, aircraft.HoverCollective, 300f, Quaternion.identity, Vector3.zero);
            RuntimePilotInput input = FlightTestRig.Input(aircraft);
            float hover = aircraft.HoverCollective;
            input.Value = new PilotCommand(Vector2.right, 0f, hover);
            rig.Step(13);
            input.Value = new PilotCommand(Vector2.zero, 0f, hover);
            rig.Step(Steps(1.5f) - 13);
            float rollRate = -aircraft.LocalAngularRatesDegrees.z;
            Assert.That(Mathf.Abs(rollRate), Is.LessThan(5f), "Rotor damping should stop the roll shortly after the input ends.");
            Assert.That(FlightTestRig.BankDegrees(aircraft), Is.InRange(8f, 35f), "A brief input leaves a bank, not a roll-over.");
        }

        [Test]
        public void DisabledRotorTermsRestoreTheUndampedResponse()
        {
            FlightTuning tuning = FlightTestRig.ShippedTuningCopy();
            tuning.RotorRateDampingPerSecond = Vector3.zero;
            tuning.HeaveDampingNsPerM = 0f;
            tuning.WeathervaneCoefficient = 0f;
            HelicopterController aircraft = rig.Create(AssistPreset.Unassisted, tuning: tuning);
            rig.EstablishInFlight(aircraft, aircraft.HoverCollective, 300f, Quaternion.identity, Vector3.zero);
            RuntimePilotInput input = FlightTestRig.Input(aircraft);
            input.Value = new PilotCommand(Vector2.right, 0f, aircraft.HoverCollective);
            rig.Step(13);
            input.Value = new PilotCommand(Vector2.zero, 0f, aircraft.HoverCollective);
            rig.Step(Steps(1.5f) - 13);
            Assert.That(-aircraft.LocalAngularRatesDegrees.z, Is.GreaterThan(20f), "Each new term is an explicit, reversible tuning value.");
        }

        [Test]
        public void BankedTurnAtCruiseIsCoordinatedWithStandardAssists()
        {
            HelicopterController aircraft = rig.Create(AssistPreset.Standard);
            float hover = aircraft.HoverCollective;
            float turnCollective = hover * 1.02f / (Mathf.Cos(20f * Mathf.Deg2Rad) * Mathf.Cos(10f * Mathf.Deg2Rad));
            rig.EstablishInFlight(aircraft, turnCollective, 300f, Quaternion.Euler(10f, 0f, 0f), Vector3.forward * 30f);
            RuntimePilotInput input = FlightTestRig.Input(aircraft);
            // Test pilot: hold 20° right bank and 10° nose-down with proportional cyclic; pedals stay centered.
            void HoldAttitude()
            {
                float roll = Mathf.Clamp((20f - FlightTestRig.BankDegrees(aircraft)) * 0.08f, -1f, 1f);
                float pitch = Mathf.Clamp((FlightTestRig.PitchDegrees(aircraft) + 10f) * 0.08f, -1f, 1f);
                input.Value = new PilotCommand(new Vector2(roll, pitch), 0f, turnCollective);
            }
            rig.Step(Steps(1.5f), HoldAttitude);
            float startHeading = aircraft.Heading, worstSideslip = 0f;
            rig.Step(Steps(4f), () => { HoldAttitude(); worstSideslip = Mathf.Max(worstSideslip, Mathf.Abs(aircraft.SideslipDegrees)); });
            Assert.That(FlightTestRig.BankDegrees(aircraft), Is.InRange(15f, 25f));
            Assert.That(worstSideslip, Is.LessThan(5f), "Yaw stabilization should follow the turn instead of letting the aircraft slide.");
            Assert.That(Mathf.DeltaAngle(startHeading, aircraft.Heading), Is.GreaterThan(15f), "The nose should turn with the bank.");
            Assert.That(aircraft.Crashed, Is.False);
        }

        [Test]
        public void UnassistedAircraftWeathervanesIntoItsRelativeWind()
        {
            // Mirrored sideslips: main-rotor torque yaws both aircraft alike, so only the fin separates them.
            HelicopterController right = rig.Create(AssistPreset.Unassisted);
            HelicopterController left = rig.Create(AssistPreset.Unassisted);
            float cruise = right.HoverCollective * 1.02f / Mathf.Cos(10f * Mathf.Deg2Rad);
            FlightTestRig.Input(right).Value = FlightTestRig.Input(left).Value = new PilotCommand(Vector2.zero, 0f, cruise);
            rig.Step(250); // Prime both collective actuators together, then place both aircraft.
            FlightTestRig.Place(right, 300f, Quaternion.Euler(10f, 0f, 0f), Quaternion.Euler(0f, 12f, 0f) * Vector3.forward * 30f);
            FlightTestRig.Place(left, 300f, Quaternion.Euler(10f, 0f, 0f), Quaternion.Euler(0f, -12f, 0f) * Vector3.forward * 30f);
            Assert.That(right.SideslipDegrees, Is.GreaterThan(10f));
            Assert.That(left.SideslipDegrees, Is.LessThan(-10f));
            float rightStart = right.Heading, leftStart = left.Heading;
            void HoldLevel(HelicopterController aircraft)
            {
                float roll = Mathf.Clamp(-FlightTestRig.BankDegrees(aircraft) * 0.08f, -1f, 1f);
                float pitch = Mathf.Clamp((FlightTestRig.PitchDegrees(aircraft) + 10f) * 0.08f, -1f, 1f);
                FlightTestRig.Input(aircraft).Value = new PilotCommand(new Vector2(roll, pitch), 0f, cruise);
            }
            rig.Step(Steps(1f), () => { HoldLevel(right); HoldLevel(left); });
            float rightTurn = Mathf.DeltaAngle(rightStart, right.Heading), leftTurn = Mathf.DeltaAngle(leftStart, left.Heading);
            Assert.That(rightTurn - leftTurn, Is.GreaterThan(6f), "The fin should yaw each nose toward the side its air arrives from.");
        }
    }
}
