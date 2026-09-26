using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HoverForHire.Tests
{
    /// <summary>Attitude command and hover hold, flown with the real Rigidbody and the shipped tuning.</summary>
    public sealed class BeginnerAidsRuntimeTests
    {
        private FlightTestRig rig;

        [SetUp] public void SetUp() => rig = new FlightTestRig();
        [TearDown] public void TearDown() => rig.Dispose();

        private static int Steps(float seconds) => Mathf.RoundToInt(seconds / FlightTestRig.Dt);

        [Test]
        public void AttitudeCommandHoldsTheCommandedBankAndLevelsWhenReleased()
        {
            HelicopterController aircraft = rig.Create(AssistPreset.Standard);
            aircraft.Assists.AttitudeCommand = true;
            float hover = aircraft.HoverCollective;
            rig.EstablishInFlight(aircraft, hover, 300f, Quaternion.identity, Vector3.zero);
            aircraft.PrimeCollective(hover);
            RuntimePilotInput input = FlightTestRig.Input(aircraft);
            input.Value = new PilotCommand(Vector2.right, 0f, hover);
            rig.Step(Steps(5f));
            float limit = aircraft.Tuning.MaximumCommandedAttitudeDegrees;
            Assert.That(FlightTestRig.BankDegrees(aircraft), Is.EqualTo(limit).Within(3f), "Full right stick holds the commanded bank.");
            Assert.That(Mathf.Abs(aircraft.LocalAngularRatesDegrees.z), Is.LessThan(3f), "It settles there instead of rolling on.");
            input.Value = new PilotCommand(new Vector2(0f, 0.5f), 0f, hover);
            rig.Step(Steps(4f));
            Assert.That(FlightTestRig.PitchDegrees(aircraft), Is.EqualTo(-limit * 0.5f).Within(3f), "Half forward stick: half the attitude, nose down.");
            input.Value = new PilotCommand(Vector2.zero, 0f, hover);
            rig.Step(Steps(4f));
            Assert.That(Mathf.Abs(FlightTestRig.BankDegrees(aircraft)), Is.LessThan(3f));
            Assert.That(Mathf.Abs(FlightTestRig.PitchDegrees(aircraft)), Is.LessThan(3f), "Centered stick flies level.");
            Assert.That(aircraft.Assists.Summary, Does.Contain("ATTITUDE"));
        }

        [Test]
        public void HoverHoldArrestsDriftAndSinkHoldsHeadingAndHandsBackOnInput()
        {
            HelicopterController aircraft = rig.Create(AssistPreset.Beginner);
            float collective = aircraft.HoverCollective - 0.03f;
            rig.EstablishInFlight(aircraft, collective, 40f, Quaternion.Euler(0f, 30f, 0f), new Vector3(2f, -0.8f, 2.5f));
            aircraft.PrimeCollective(collective);
            rig.Step(1); // The teleport's ground contacts clear on the next physics step.
            float heading = aircraft.Heading;
            var changes = new List<bool>();
            aircraft.HoverHold.Changed += (engaged, reason) => changes.Add(engaged);
            Assert.That(aircraft.HoverHold.TryEngage(aircraft, collective, out string why), Is.True, why);
            rig.Step(Steps(12f));
            Assert.That(aircraft.GroundSpeed, Is.LessThan(0.4f), "The drift is arrested.");
            Assert.That(Mathf.Abs(aircraft.VerticalSpeed), Is.LessThan(0.25f), "The sink is arrested, though the pilot's collective was low.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(aircraft.Heading, heading)), Is.LessThan(3f));
            Assert.That(aircraft.HoverHold.Engaged, Is.True);

            FlightTestRig.Input(aircraft).Value = new PilotCommand(new Vector2(0f, 0.5f), 0f, collective);
            rig.Step(1);
            Assert.That(aircraft.HoverHold.Engaged, Is.False, "Moving the stick hands control back.");
            Assert.That(changes, Is.EqualTo(new[] { true, false }));
            Assert.That(aircraft.Crashed, Is.False);
        }

        [Test]
        public void HoverHoldNeedsASlowAirborneAircraft()
        {
            HelicopterController aircraft = rig.Create(AssistPreset.Beginner);
            rig.Step(10);
            Assert.That(aircraft.HoverHold.TryEngage(aircraft, 0f, out string grounded), Is.False);
            Assert.That(grounded, Does.Contain("air"));
            rig.EstablishInFlight(aircraft, aircraft.HoverCollective, 80f, Quaternion.identity, new Vector3(0f, 0f, 12f));
            rig.Step(1);
            Assert.That(aircraft.HoverHold.TryEngage(aircraft, aircraft.HoverCollective, out string fast), Is.False);
            Assert.That(fast, Does.Contain("5 m/s"));
            Assert.That(aircraft.HoverHold.Engaged, Is.False);
        }
    }
}
