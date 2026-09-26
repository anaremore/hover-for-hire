using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HoverForHire.Tests
{
    /// <summary>
    /// Realism effects on real Rigidbody dynamics with the shipped tuning. Each test enables only the effects it examines;
    /// a scripted test pilot (attitude, heading and collective loops) flies the techniques the drills teach.
    /// </summary>
    public sealed class RealismRuntimeTests
    {
        private enum Autorotation { Glide, Flare, Level, Cushion, Landed }

        private FlightTestRig rig;

        [SetUp] public void SetUp() => rig = new FlightTestRig();
        [TearDown] public void TearDown() => rig.Dispose();

        private static int Steps(float seconds) => Mathf.RoundToInt(seconds / FlightTestRig.Dt);

        private static RealismSettings Only(bool groundEffect = false, bool translationalLift = false, bool speedStability = false,
            bool vortexRing = false, bool powerLimits = false, bool tailRotorFailures = false)
            => new RealismSettings
            {
                GroundEffect = groundEffect, TranslationalLift = translationalLift, SpeedStability = speedStability,
                VortexRingState = vortexRing, PowerLimits = powerLimits, TailRotorFailures = tailRotorFailures
            };

        private static RealismSettings Preset(RealismPreset preset)
        {
            var settings = new RealismSettings();
            settings.SetPreset(preset);
            return settings;
        }

        /// <summary>Spin up on the ground at hover collective, then place every aircraft level at a height and velocity.</summary>
        private void Establish(float height, Vector3 velocity, params HelicopterController[] aircraft)
        {
            foreach (HelicopterController a in aircraft) FlightTestRig.Input(a).Value = new PilotCommand(Vector2.zero, 0f, a.HoverCollective);
            rig.Step(250);
            foreach (HelicopterController a in aircraft) FlightTestRig.Place(a, height, Quaternion.identity, velocity);
        }

        /// <summary>A pilot flying by the horizon: cyclic holds pitch (nose-up positive) and bank, pedals hold heading.</summary>
        private static void Fly(HelicopterController aircraft, float pitchUp, float collective, float bank = 0f, float heading = 0f)
        {
            float pitchError = FlightTestRig.PitchDegrees(aircraft) - pitchUp, bankError = bank - FlightTestRig.BankDegrees(aircraft);
            var cyclic = new Vector2(Mathf.Clamp(bankError * 0.07f, -1f, 1f), Mathf.Clamp(pitchError * 0.07f, -1f, 1f));
            float pedals = Mathf.Clamp(Mathf.DeltaAngle(aircraft.Heading, heading) * 0.03f, -1f, 1f);
            FlightTestRig.Input(aircraft).Value = new PilotCommand(cyclic, pedals, Mathf.Clamp01(collective));
        }

        /// <summary>Nose-up pitch (degrees) that holds a forward airspeed: the lean that balances drag, plus a speed correction.</summary>
        private static float PitchForSpeed(HelicopterController aircraft, float targetSpeed)
        {
            float speed = aircraft.HorizontalAirspeed;
            float drag = aircraft.Tuning.LinearDrag.z * speed + aircraft.Tuning.QuadraticDrag.z * speed * speed;
            float lean = Mathf.Atan2(drag, aircraft.Body.mass * FlightMath.StandardGravity) * Mathf.Rad2Deg;
            return -Mathf.Clamp(lean + (targetSpeed - speed) * 1.15f, -20f, 20f);
        }

        [Test]
        public void GroundEffectHoldsALowHoverOnLessThanTheFreeAirCollective()
        {
            HelicopterController cushioned = rig.Create(AssistPreset.Beginner, realism: Only(groundEffect: true));
            HelicopterController freeAir = rig.Create(AssistPreset.Beginner);
            float collective = cushioned.HoverCollective * 0.95f;
            rig.Step(Steps(30f), () =>
            {
                Fly(cushioned, 0f, collective);
                Fly(freeAir, 0f, collective);
            });
            TestContext.WriteLine($"Ground effect hover at 95% collective: {cushioned.AltitudeAGL:0.00} m AGL, gain {cushioned.GroundEffectGain:0.000}.");
            Assert.That(freeAir.Grounded, Is.True, "95% of the free-air hover collective cannot lift off without ground effect.");
            Assert.That(cushioned.Grounded, Is.False);
            Assert.That(cushioned.AltitudeAGL, Is.InRange(1.0f, 2.3f), "The cushion holds a low hover (about 1.6 m predicted).");
            Assert.That(Mathf.Abs(cushioned.VerticalSpeed), Is.LessThan(0.2f));
            Assert.That(cushioned.GroundEffectGain, Is.EqualTo(1f / 0.95f - 1f).Within(0.01f), "Lift balances weight through the cushion.");
            Assert.That(cushioned.HoverCollectiveHere, Is.LessThan(cushioned.HoverCollective));
        }

        [Test]
        public void TranslationalLiftClimbsAtConstantCollective()
        {
            HelicopterController lifted = rig.Create(AssistPreset.Standard, realism: Only(translationalLift: true));
            HelicopterController plain = rig.Create(AssistPreset.Standard);
            Establish(200f, new Vector3(0f, 0f, 16f), lifted, plain);
            float collective = lifted.HoverCollective;
            rig.Step(Steps(5f), () =>
            {
                Fly(lifted, 0f, collective);
                Fly(plain, 0f, collective);
            });
            TestContext.WriteLine($"Translational lift: {lifted.VerticalSpeed:0.00} m/s climb at {lifted.HorizontalAirspeed:0.0} m/s (plain {plain.VerticalSpeed:0.00} m/s).");
            Assert.That(lifted.HorizontalAirspeed, Is.GreaterThan(11f), "Still well into translational lift.");
            Assert.That(lifted.TranslationalLiftGain, Is.GreaterThan(0.08f));
            Assert.That(lifted.VerticalSpeed, Is.GreaterThan(1.2f), "Same collective, more lift: the aircraft climbs with airspeed.");
            Assert.That(Mathf.Abs(plain.VerticalSpeed), Is.LessThan(0.4f), "Without it, level flight at hover collective stays level.");
        }

        [Test]
        public void SpeedStabilityRaisesTheNoseWithForwardAirspeed()
        {
            HelicopterController stable = rig.Create(AssistPreset.Standard, realism: Only(speedStability: true));
            HelicopterController plain = rig.Create(AssistPreset.Standard);
            Establish(200f, new Vector3(0f, 0f, 25f), stable, plain);
            rig.Step(Steps(3f), () =>
            {
                FlightTestRig.Input(stable).Value = new PilotCommand(Vector2.zero, 0f, stable.HoverCollective);
                FlightTestRig.Input(plain).Value = new PilotCommand(Vector2.zero, 0f, plain.HoverCollective);
            });
            TestContext.WriteLine($"Speed stability: nose {FlightTestRig.PitchDegrees(stable) - FlightTestRig.PitchDegrees(plain):0.0}° higher after 3 s at 25 m/s.");
            Assert.That(FlightTestRig.PitchDegrees(stable) - FlightTestRig.PitchDegrees(plain), Is.GreaterThan(3f),
                "Flapback pitches the nose up with airspeed unless the pilot holds it down.");
        }

        [Test]
        public void VortexRingStateIsEscapedWithAirspeedNotCollective()
        {
            HelicopterController holding = rig.Create(AssistPreset.Standard, realism: Only(vortexRing: true));
            HelicopterController pulling = rig.Create(AssistPreset.Standard, realism: Only(vortexRing: true));
            HelicopterController flyingOut = rig.Create(AssistPreset.Standard, realism: Only(vortexRing: true));
            Establish(300f, Vector3.zero, holding, pulling, flyingOut);
            float hover = holding.HoverCollective, lowered = hover * 0.76f;

            int entry = 0;
            for (; entry < Steps(30f) && holding.VortexRingSeverity < 0.5f; entry++)
                rig.Step(1, () => { Fly(holding, 0f, lowered); Fly(pulling, 0f, lowered); Fly(flyingOut, 0f, lowered); });
            Assert.That(holding.VortexRingSeverity, Is.GreaterThanOrEqualTo(0.5f), "A steep, slow, powered descent settles into its own wake.");
            Assert.That(holding.AltitudeAGL, Is.GreaterThan(150f));

            float entryHeight = flyingOut.AltitudeAGL, lowest = entryHeight, elapsed = 0f;
            bool clearAir = false;
            rig.Step(Steps(15f), () =>
            {
                elapsed += FlightTestRig.Dt;
                Fly(holding, 0f, lowered);
                Fly(pulling, 0f, Mathf.Min(1f, lowered + elapsed * 0.25f));
                // Recovery: forward cyclic into clean air first, then collective to stop the descent.
                clearAir |= flyingOut.HorizontalAirspeed >= 12f;
                if (!clearAir) Fly(flyingOut, -15f, lowered);
                else Fly(flyingOut, PitchForSpeed(flyingOut, 15f), Mathf.Clamp(hover - flyingOut.VerticalSpeed * 0.04f, 0.1f, 0.7f));
                lowest = Mathf.Min(lowest, flyingOut.AltitudeAGL);
            });

            TestContext.WriteLine($"Vortex ring: entered after {entry * FlightTestRig.Dt:0.0} s at {entryHeight:0} m. After 15 s: holding {holding.VerticalSpeed:0.0} m/s " +
                $"(severity {holding.VortexRingSeverity:0.00}), pulling {pulling.VerticalSpeed:0.0} m/s, flying out {flyingOut.VerticalSpeed:0.0} m/s at " +
                $"{flyingOut.HorizontalAirspeed:0.0} m/s having lost {entryHeight - lowest:0} m.");
            Assert.That(holding.VortexRingSeverity, Is.GreaterThan(0.4f), "Holding the collective stays in the ring.");
            Assert.That(holding.VerticalSpeed, Is.LessThan(-8f));
            Assert.That(pulling.VerticalSpeed, Is.LessThan(holding.VerticalSpeed - 1f), "Pulling collective makes the sink worse.");
            Assert.That(flyingOut.VortexRingSeverity, Is.LessThan(0.05f), "Airspeed carries the rotor into clean air.");
            Assert.That(flyingOut.VerticalSpeed, Is.GreaterThan(-2f), "Then collective stops the descent.");
            Assert.That(entryHeight - lowest, Is.LessThan(120f), $"Height lost in the recovery: {entryHeight - lowest:0} m (about 90 m predicted).");
            Assert.That(holding.Crashed || pulling.Crashed || flyingOut.Crashed, Is.False);
        }

        [Test]
        public void OverpullingAtMaximumWeightDroopsRotorSpeed()
        {
            HelicopterController limited = rig.Create(AssistPreset.Standard, 450f, realism: Only(powerLimits: true));
            HelicopterController unlimited = rig.Create(AssistPreset.Standard, 450f);
            Establish(60f, Vector3.zero, limited, unlimited);
            float hover = limited.HoverCollective;
            rig.Step(Steps(2f), () => { Fly(limited, 0f, hover); Fly(unlimited, 0f, hover); });
            TestContext.WriteLine($"Maximum-weight hover: torque {limited.TorqueFraction * 100f:0}%, rotor {limited.RotorSpeed01 * 100f:0.0}%.");
            Assert.That(limited.TorqueFraction, Is.EqualTo(0.85f).Within(0.05f), "A maximum-weight hover needs about 85% torque.");
            Assert.That(limited.RotorSpeed01, Is.EqualTo(1f).Within(0.01f));

            rig.Step(Steps(8f), () => { Fly(limited, 0f, 0.8f); Fly(unlimited, 0f, 0.8f); });
            TestContext.WriteLine($"80% collective at maximum weight: torque {limited.TorqueFraction * 100f:0}%, rotor {limited.RotorSpeed01 * 100f:0.0}%, " +
                $"climb {limited.VerticalSpeed:0.00} m/s (unlimited power {unlimited.VerticalSpeed:0.00} m/s).");
            Assert.That(limited.TorqueFraction, Is.EqualTo(limited.Tuning.EngineTorqueLimit).Within(0.01f), "The engine is at its torque limit.");
            Assert.That(limited.Overtorque && limited.LowRotorSpeed, Is.True);
            Assert.That(limited.RotorSpeed01, Is.InRange(0.8f, 0.95f), "Overpulling droops the rotor instead of adding lift.");
            Assert.That(limited.VerticalSpeed, Is.LessThan(unlimited.VerticalSpeed - 1f));
            Assert.That(unlimited.RotorSpeed01, Is.EqualTo(1f), "Without power limits the rotor stays governed.");
            Assert.That(limited.Crashed, Is.False);
        }

        [Test]
        public void EngineFailureDecaysRotorSpeedUnlessCollectiveIsLowered()
        {
            HelicopterController held = rig.Create(AssistPreset.Standard, realism: Only(powerLimits: true));
            HelicopterController lowered = rig.Create(AssistPreset.Standard, realism: Only(powerLimits: true));
            Establish(150f, Vector3.zero, held, lowered);
            var failures = new List<SystemFailure>();
            held.SystemFailed += failures.Add;
            float hover = held.HoverCollective;
            rig.Step(Steps(1f), () => { Fly(held, 0f, hover); Fly(lowered, 0f, hover); });

            held.FailEngine();
            held.FailEngine();
            lowered.FailEngine();
            Assert.That(failures, Is.EqualTo(new[] { SystemFailure.EngineOut }), "One failure, reported once.");
            float secondsToNinety = -1f, lowestLowered = 1f;
            for (int i = 1; i <= Steps(5f); i++)
            {
                rig.Step(1, () => { Fly(held, 0f, hover); Fly(lowered, 0f, 0.15f); });
                if (secondsToNinety < 0f && held.RotorSpeed01 < 0.9f) secondsToNinety = i * FlightTestRig.Dt;
                lowestLowered = Mathf.Min(lowestLowered, lowered.RotorSpeed01);
            }
            TestContext.WriteLine($"Engine failure: rotor 90% after {secondsToNinety:0.00} s with collective held; lowered collective kept it at or above {lowestLowered * 100f:0.0}% " +
                $"(now {lowered.RotorSpeed01 * 100f:0.0}%).");
            Assert.That(secondsToNinety, Is.InRange(1.4f, 2.6f), "With collective held, rotor RPM decays to 90% in about two seconds.");
            Assert.That(held.TorqueFraction, Is.Zero);
            Assert.That(lowestLowered, Is.GreaterThan(0.9f), "Lowering collective at once keeps the rotor in the green.");
            Assert.That(lowered.RotorSpeed01, Is.InRange(0.95f, lowered.Tuning.MaximumRotorSpeed01));
            Assert.That(held.Crashed || lowered.Crashed, Is.False);
        }

        [Test]
        public void AutorotationLandingWithTheTaughtTechniqueIsSurvivable()
        {
            HelicopterController aircraft = rig.Create(AssistPreset.Standard, realism: Preset(RealismPreset.Realistic));
            Establish(180f, new Vector3(0f, 0f, 25f), aircraft);
            float hover = aircraft.HoverCollective;
            rig.Step(Steps(2f), () => Fly(aircraft, PitchForSpeed(aircraft, 25f), hover));
            aircraft.FailEngine();

            float touchdown = -1f, landedSeconds = 0f, afterFailure = 0f, glideDescent = 0f, lowestRotor = 1f, highestRotor = 1f;
            int glideSamples = 0;
            float touchdownGroundSpeed = 0f;
            aircraft.Touchdown += speed => { if (touchdown < 0f) { touchdown = speed; touchdownGroundSpeed = aircraft.GroundSpeed; } };
            Autorotation phase = Autorotation.Glide;
            for (int i = 0; i < Steps(90f) && landedSeconds < 3f && !aircraft.Crashed; i++)
            {
                float height = aircraft.AltitudeAGL, rotor = aircraft.RotorSpeed01, speed = aircraft.HorizontalAirspeed;
                if (touchdown >= 0f) phase = Autorotation.Landed;
                else if (phase == Autorotation.Glide && height < 35f) phase = Autorotation.Flare;
                else if (phase == Autorotation.Flare && (height < 6f || speed < 6f)) phase = Autorotation.Level;
                else if (phase == Autorotation.Level && height < 3f) phase = Autorotation.Cushion;
                switch (phase)
                {
                    case Autorotation.Glide:
                        // Collective down at once, then small changes to keep rotor RPM near 100%; glide at 25 m/s.
                        Fly(aircraft, PitchForSpeed(aircraft, 25f), Mathf.Clamp(0.22f + 1.5f * (rotor - 1f), 0.05f, 0.6f));
                        if (afterFailure > 3f) { lowestRotor = Mathf.Min(lowestRotor, rotor); highestRotor = Mathf.Max(highestRotor, rotor); }
                        if (afterFailure > 8f) { glideDescent += -aircraft.VerticalSpeed; glideSamples++; }
                        break;
                    case Autorotation.Flare:
                        // Aft cyclic trades airspeed for a slower descent and stores energy in the rotor.
                        Fly(aircraft, 20f, Mathf.Clamp(0.22f + 1.5f * (rotor - 1.05f), 0.05f, 0.6f));
                        break;
                    case Autorotation.Level:
                        Fly(aircraft, 0f, Mathf.Clamp(0.22f + 1.5f * (rotor - 1.05f), 0.05f, 0.6f));
                        break;
                    case Autorotation.Cushion:
                        // Spend the rotor's stored energy against the remaining descent.
                        Fly(aircraft, 0f, Mathf.Clamp(0.25f - 0.2f * aircraft.VerticalSpeed, 0.05f, 1f));
                        break;
                    default:
                        Fly(aircraft, 0f, 0f);
                        landedSeconds += FlightTestRig.Dt;
                        break;
                }
                rig.Step(1);
                afterFailure += FlightTestRig.Dt;
            }

            TestContext.WriteLine($"Autorotation: glide {glideDescent / Mathf.Max(1, glideSamples):0.0} m/s down, rotor {lowestRotor * 100f:0}–{highestRotor * 100f:0}%, " +
                $"touchdown {touchdown:0.00} m/s at {touchdownGroundSpeed:0.0} m/s ground speed, {afterFailure:0.0} s after the failure.");
            Assert.That(aircraft.Crashed, Is.False, $"Crashed: {aircraft.LastCrashCause} ({aircraft.CrashValue:0.0} vs {aircraft.CrashLimit:0.0}).");
            Assert.That(aircraft.TailRotorFailed, Is.False, "The flare must not drag the tail rotor.");
            Assert.That(phase, Is.EqualTo(Autorotation.Landed));
            Assert.That(glideSamples, Is.GreaterThan(0));
            Assert.That(glideDescent / glideSamples, Is.InRange(7f, 12f), "A steady autorotative glide descends at about 7–12 m/s.");
            Assert.That(lowestRotor, Is.GreaterThan(0.9f), "Rotor RPM held in the green through the glide.");
            Assert.That(highestRotor, Is.LessThan(1.1f));
            Assert.That(touchdown, Is.InRange(0f, 3f), "Flare and cushion bring the touchdown well under the 5.5 m/s crash limit.");
            Assert.That(aircraft.GroundSpeed, Is.LessThan(1f), "The run-on landing slides to a stop.");
        }

        [Test]
        public void AutopilotLandsOnAPadInAGustyCrosswind()
        {
            HelicopterController aircraft = rig.Create(AssistPreset.Standard, realism: Preset(RealismPreset.Realistic));
            var wind = new WindField(11);
            wind.Configure(WindStrength.Moderate, 0.3f, 90f);
            aircraft.Wind = wind;
            var pilot = aircraft.gameObject.AddComponent<Autopilot>();
            pilot.AutoTick = false;
            pilot.Aircraft = aircraft;
            aircraft.InputSource = pilot;
            var pad = new Vector3(aircraft.Body.position.x, FlightTestRig.Origin.y, aircraft.Body.position.z + 150f);
            pilot.FlyTo(pad);
            float strongest = 0f;
            for (int i = 0; i < Steps(150f) && pilot.Current != Autopilot.Phase.Landed && !aircraft.Crashed; i++)
            {
                rig.Step(1, () => { wind.Advance(FlightTestRig.Dt); pilot.Tick(FlightTestRig.Dt); });
                strongest = Mathf.Max(strongest, aircraft.CurrentWind.magnitude);
            }
            Vector3 offset = aircraft.Body.position - pad;
            TestContext.WriteLine($"Crosswind landing: {new Vector2(offset.x, offset.z).magnitude:0.00} m from center, touchdown {pilot.TouchdownSpeed:0.00} m/s, " +
                $"{pilot.FlightSeconds:0} s, strongest wind {strongest:0.0} m/s.");
            Assert.That(aircraft.Crashed, Is.False, aircraft.LastCrashCause.ToString());
            Assert.That(pilot.Current, Is.EqualTo(Autopilot.Phase.Landed), $"Stuck in {pilot.Current}.");
            Assert.That(new Vector2(offset.x, offset.z).magnitude, Is.LessThan(2.5f), "Lands on the pad despite the crosswind.");
            Assert.That(pilot.TouchdownSpeed, Is.LessThan(1.2f));
            Assert.That(strongest, Is.GreaterThan(6f), "The wind really was blowing.");
        }

        [Test]
        public void TailRotorStrikeLeavesAFlyableSpinWhenFailuresAreEnabled()
        {
            HelicopterController spinning = rig.Create(AssistPreset.Standard, realism: Only(tailRotorFailures: true));
            HelicopterController lowered = rig.Create(AssistPreset.Standard, realism: Only(tailRotorFailures: true));
            HelicopterController strict = rig.Create(AssistPreset.Standard);
            Establish(120f, Vector3.zero, spinning, lowered, strict);
            var failures = new List<SystemFailure>();
            spinning.SystemFailed += failures.Add;

            foreach (HelicopterController aircraft in new[] { spinning, lowered, strict })
                aircraft.ReportRotorContact(CrashCause.TailRotorStrike, rig.Ground);
            Assert.That(strict.Crashed, Is.True, "Without tail-rotor failures a strike ends the flight.");
            Assert.That(strict.LastCrashCause, Is.EqualTo(CrashCause.TailRotorStrike));
            Assert.That(spinning.Crashed || lowered.Crashed, Is.False);
            Assert.That(spinning.TailRotorFailed && lowered.TailRotorFailed, Is.True);
            Assert.That(failures, Is.EqualTo(new[] { SystemFailure.TailRotor }));

            float hover = spinning.HoverCollective;
            rig.Step(Steps(3f), () =>
            {
                // Full opposite pedal no longer does anything; only less rotor torque slows the spin.
                FlightTestRig.Input(spinning).Value = new PilotCommand(Vector2.zero, -1f, hover);
                FlightTestRig.Input(lowered).Value = new PilotCommand(Vector2.zero, -1f, hover * 0.45f);
            });
            float spinRate = Mathf.Abs(spinning.LocalAngularRatesDegrees.y), loweredRate = Mathf.Abs(lowered.LocalAngularRatesDegrees.y);
            TestContext.WriteLine($"Tail rotor failure: {spinRate:0} °/s after 3 s at hover collective, {loweredRate:0} °/s with collective lowered.");
            Assert.That(spinRate, Is.GreaterThan(30f), "Main-rotor torque spins the fuselage once anti-torque is lost.");
            Assert.That(loweredRate, Is.LessThan(spinRate * 0.6f), "Lowering collective reduces the torque and the spin.");
            Assert.That(spinning.Crashed || lowered.Crashed, Is.False);
        }

        [Test]
        public void RelaxedMatchesTheBaseModelAwayFromTheGroundAtZeroAirspeed()
        {
            HelicopterController relaxed = rig.Create(AssistPreset.Beginner, realism: Preset(RealismPreset.Relaxed));
            HelicopterController baseline = rig.Create(AssistPreset.Beginner);
            Establish(200f, Vector3.zero, relaxed, baseline);
            float largestDifference = 0f;
            rig.Step(Steps(10f), () =>
            {
                FlightTestRig.Input(relaxed).Value = new PilotCommand(Vector2.zero, 0f, relaxed.HoverCollective + 0.01f);
                FlightTestRig.Input(baseline).Value = new PilotCommand(Vector2.zero, 0f, baseline.HoverCollective + 0.01f);
                largestDifference = Mathf.Max(largestDifference, Mathf.Abs(relaxed.VerticalSpeed - baseline.VerticalSpeed));
            });
            TestContext.WriteLine($"Relaxed vs base model: largest vertical-speed difference {largestDifference:0.0000} m/s; climb {relaxed.VerticalSpeed:0.000} m/s.");
            Assert.That(largestDifference, Is.LessThan(0.01f), "Relaxed adds nothing above two rotor diameters at zero airspeed.");
            Assert.That(relaxed.VerticalSpeed, Is.InRange(0.40f, 0.58f), "The phase-1 climb response is preserved.");
            Assert.That(relaxed.RotorSpeed01, Is.EqualTo(1f), "Relaxed keeps the governed rotor.");
        }
    }
}
