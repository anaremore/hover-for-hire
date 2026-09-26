using NUnit.Framework;
using UnityEngine;

namespace HoverForHire.Tests
{
    /// <summary>Realism presets, each aerodynamic effect's curve, the rotor power model, the wind field and the new drills.</summary>
    public sealed class RealismEffectsTests
    {
        private FlightTuning tuning;

        [SetUp]
        public void SetUp() => tuning = Object.Instantiate(Resources.Load<FlightTuning>("UtilityHelicopter"));

        [TearDown] public void TearDown() => Object.DestroyImmediate(tuning);

        private float LandedHubHeight => tuning.SkidClearanceMeters + tuning.MainRotorHub.y;

        [Test]
        public void PresetsEnableProgressivelyHarderPhysics()
        {
            var r = new RealismSettings();
            r.SetPreset(RealismPreset.Relaxed);
            Assert.That(r.GroundEffect && r.TranslationalLift, Is.True);
            Assert.That(r.VortexRingState || r.PowerLimits || r.SpeedStability || r.TailRotorFailures, Is.False, "Relaxed forces nothing challenging on.");
            Assert.That(r.Wind, Is.EqualTo(WindStrength.Calm));
            r.SetPreset(RealismPreset.Realistic);
            Assert.That(r.VortexRingState && r.PowerLimits && r.SpeedStability && r.TailRotorFailures, Is.True);
            Assert.That(r.Wind, Is.EqualTo(WindStrength.Light));
            Assert.That(r.EngineFailures, Is.EqualTo(FailureMode.DrillsOnly));
            r.SetPreset(RealismPreset.Expert);
            Assert.That(r.EngineFailures, Is.EqualTo(FailureMode.Random));
            Assert.That(r.VortexOnsetScale, Is.LessThan(1f), "Expert settles with power earlier.");
            Assert.That(r.RolloverLimitDegrees, Is.LessThan(40f));
            foreach (RealismPreset preset in System.Enum.GetValues(typeof(RealismPreset)))
            {
                r.SetPreset(preset);
                Assert.That(r.MatchingPreset, Is.EqualTo(preset));
                Assert.That(r.Summary, Is.EqualTo(preset.ToString().ToUpperInvariant()));
            }
            r.Wind = WindStrength.Strong;
            Assert.That(r.MatchingPreset, Is.Null);
            Assert.That(r.Summary, Does.StartWith("CUSTOM"));
            var none = RealismSettings.None();
            Assert.That(none.GroundEffect || none.TranslationalLift || none.PowerLimits, Is.False);
        }

        [Test]
        public void GroundEffectCushionsNearTheSurfaceAndFadesWithHeightAndSpeed()
        {
            float landed = FlightMath.GroundEffectFactor(LandedHubHeight, 0f, tuning);
            Assert.That(landed, Is.EqualTo(1.118f).Within(0.01f), "About 12% more thrust with the skids on the ground.");
            Assert.That(FlightMath.GroundEffectFactor(2f * tuning.MainRotorRadius, 0f, tuning), Is.LessThan(1.02f));
            Assert.That(FlightMath.GroundEffectFactor(50f, 0f, tuning), Is.EqualTo(1f).Within(0.002f));
            Assert.That(FlightMath.GroundEffectFactor(0.1f, 0f, tuning), Is.EqualTo(1f + tuning.GroundEffectMaximumGain).Within(0.0001f), "Capped close to the ground.");
            Assert.That(FlightMath.GroundEffectFactor(LandedHubHeight, tuning.GroundEffectFadeSpeed, tuning), Is.EqualTo(1f).Within(0.0001f), "Gone at speed.");
            Assert.That(FlightMath.HoverCollective(1050f, 9.81f, tuning) / landed, Is.EqualTo(0.40f).Within(0.01f), "In-ground-effect hover near 40% collective.");
        }

        [Test]
        public void TranslationalLiftRisesAcrossTheTransitionBand()
        {
            Assert.That(FlightMath.TranslationalLiftFactor(0f, tuning), Is.EqualTo(1f));
            Assert.That(FlightMath.TranslationalLiftFactor(tuning.TranslationalLiftStart, tuning), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(FlightMath.TranslationalLiftFactor(30f, tuning), Is.EqualTo(1f + tuning.TranslationalLiftGain).Within(0.0001f));
            float previous = 1f;
            for (float v = 0f; v <= 20f; v += 0.5f)
            {
                float factor = FlightMath.TranslationalLiftFactor(v, tuning);
                Assert.That(factor, Is.GreaterThanOrEqualTo(previous));
                previous = factor;
            }
            Assert.That(FlightMath.TranslationalBuffet(9f, tuning), Is.GreaterThan(0.8f), "The shudder peaks mid-transition.");
            Assert.That(FlightMath.TranslationalBuffet(25f, tuning), Is.Zero);
        }

        [Test]
        public void VortexRingNeedsPowerLowSpeedAndASteepDescent()
        {
            Assert.That(FlightMath.VortexRingSeverity(8f, 0f, 0.5f, true, 1f, tuning), Is.GreaterThan(0.95f));
            Assert.That(FlightMath.VortexRingSeverity(-2f, 0f, 0.5f, true, 1f, tuning), Is.Zero, "Climbing never settles.");
            Assert.That(FlightMath.VortexRingSeverity(8f, 15f, 0.5f, true, 1f, tuning), Is.Zero, "Forward airspeed flies out of it.");
            Assert.That(FlightMath.VortexRingSeverity(8f, 0f, 0.15f, true, 1f, tuning), Is.Zero, "Low collective (autorotative) avoids it.");
            Assert.That(FlightMath.VortexRingSeverity(8f, 0f, 0.5f, false, 1f, tuning), Is.LessThanOrEqualTo(0.15f), "An engine-out rotor has upward inflow.");
            float descent = tuning.VortexRingOnset * 0.95f;
            Assert.That(FlightMath.VortexRingSeverity(descent, 0f, 0.5f, true, 1f, tuning), Is.Zero);
            Assert.That(FlightMath.VortexRingSeverity(descent, 0f, 0.5f, true, 0.8f, tuning), Is.GreaterThan(0f), "Expert onset comes sooner.");

            float hover = FlightMath.HoverCollective(1050f, 9.81f, tuning);
            Assert.That(FlightMath.VortexRingThrustLoss(0f, 1f, hover, tuning), Is.Zero);
            Assert.That(FlightMath.VortexRingThrustLoss(1f, hover, hover, tuning), Is.EqualTo(tuning.VortexRingThrustLoss).Within(0.0001f));
            float NetThrust(float collective) => collective * (1f - FlightMath.VortexRingThrustLoss(1f, collective, hover, tuning));
            Assert.That(NetThrust(hover * 1.3f), Is.LessThan(NetThrust(hover)), "In a developed ring, pulling collective makes the sink worse.");
            Assert.That(NetThrust(1f), Is.LessThan(hover), "Even full collective cannot hold height inside the ring.");
        }

        [Test]
        public void PowerModelMatchesTheDesignedTorqueAndAutorotationBalance()
        {
            float rated = tuning.EngineRatedPowerW;
            float emptyHover = FlightMath.PowerRequired(1050f * 9.81f, 0f, 0f, 1f, tuning);
            float heavyHover = FlightMath.PowerRequired(1500f * 9.81f, 0f, 0f, 1f, tuning);
            Assert.That(emptyHover / rated, Is.EqualTo(0.54f).Within(0.03f), "Empty hover needs about 54% torque.");
            Assert.That(heavyHover / rated, Is.EqualTo(0.85f).Within(0.04f), "Maximum-weight hover needs about 85% torque.");
            Assert.That(FlightMath.PowerRequired(1050f * 9.81f, 25f, 0f, 1f, tuning), Is.LessThan(emptyHover), "Forward flight needs less induced power (the power bucket).");
            float autorotation = FlightMath.PowerRequired(1050f * 9.81f, 0f, -10f, 1f, tuning);
            Assert.That(Mathf.Abs(autorotation), Is.LessThan(0.1f * rated), "About 10 m/s of vertical descent can drive the rotor on its own.");
            Assert.That(FlightMath.InducedVelocity(1050f * 9.81f, 0f, tuning), Is.EqualTo(7.9f).Within(0.2f));
            Assert.That(FlightMath.InducedVelocity(1050f * 9.81f, 20f, tuning), Is.LessThan(3.5f));
            Assert.That(FlightMath.RotorStallFactor(1f, tuning), Is.EqualTo(1f));
            Assert.That(FlightMath.RotorStallFactor(tuning.RotorStallSpeed01 - 0.1f, tuning), Is.Zero);
            Assert.That(FlightMath.RatedTorque(tuning), Is.EqualTo(rated / (tuning.GovernedRotorRpm * Mathf.PI / 30f)).Within(0.5f));
        }

        [Test]
        public void WindFieldIsDeterministicStrongerAloftAndBlowsFromItsDirection()
        {
            var calm = new WindField(3);
            Assert.That(calm.WindAt(new Vector3(10f, 30f, 10f)), Is.EqualTo(Vector3.zero));
            Assert.That(calm.TurbulenceAt(Vector3.zero), Is.EqualTo(Vector3.zero));
            var a = new WindField(3); var b = new WindField(3);
            a.Configure(WindStrength.Moderate, 0.4f, 90f); b.Configure(WindStrength.Moderate, 0.4f, 90f);
            a.Advance(12.5f); b.Advance(12.5f);
            Vector3 point = new Vector3(-200f, 40f, 300f);
            Assert.That(a.WindAt(point), Is.EqualTo(b.WindAt(point)), "Same seed and time, same wind.");
            var steady = new WindField(5);
            steady.Configure(WindStrength.Moderate, 0f, 90f);
            Vector3 low = steady.WindAt(new Vector3(0f, 2f, 0f)), high = steady.WindAt(new Vector3(0f, 60f, 0f));
            Assert.That(high.magnitude, Is.GreaterThan(low.magnitude * 1.3f), "Wind strengthens with height above the surface.");
            Assert.That(steady.WindAt(new Vector3(0f, 10f, 0f)).x, Is.LessThan(-5f), "A wind from the east blows toward the west (−X).");
            // Mean wind (with its ±15% slow swell) plus gusts of at most 0.8 × gustiness × speed per channel.
            float bound = 8f * 1.15f * WindField.HeightProfile(point.y) + 0.8f * 0.4f * 8f * 1.6f;
            float lowest = float.MaxValue, highest = 0f;
            for (int i = 0; i < 200; i++)
            {
                a.Advance(0.5f);
                float speed = a.WindAt(point).magnitude;
                Assert.That(speed, Is.LessThan(bound), "Gusts stay bounded.");
                lowest = Mathf.Min(lowest, speed);
                highest = Mathf.Max(highest, speed);
            }
            Assert.That(highest - lowest, Is.GreaterThan(1f), "Gusty wind actually varies.");
            Assert.That(WindField.HeightProfile(10f), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void CrosswindDrillHoversThenLands()
        {
            var home = new ZoneDefinition { Id = "home", X = 0f, Y = 0f, Z = 0f, Radius = 17f };
            var session = new TrainingSession(TrainingSession.Crosswind, home, 0f, "RATE");
            var hover = new FlightSample { X = 2f, Y = 11.5f, Z = 1f, Altitude = 10f };
            for (int i = 0; i < 9; i++) session.Tick(1f, hover);
            Assert.That(session.Stage, Is.EqualTo(1), session.Feedback);
            Assert.That(session.State, Is.EqualTo(TrainingState.Active));
            session.RecordTouchdown(0.8f);
            var landed = new FlightSample { X = 1f, Y = 1.5f, Z = 1f, Grounded = true };
            for (int i = 0; i < 4; i++) session.Tick(1f, landed);
            Assert.That(session.State, Is.EqualTo(TrainingState.Complete), session.Feedback);
        }

        [Test]
        public void HeavyLiftDrillRewardsPowerManagementAndFailsOnSustainedDroop()
        {
            var home = new ZoneDefinition { Id = "home", Radius = 17f };
            var good = new TrainingSession(TrainingSession.HeavyLift, home, 0f, "RATE");
            var sample = new FlightSample { Y = 26.5f, Altitude = 25f, RotorSpeed01 = 1f, TorqueFraction = 0.95f };
            for (int i = 0; i < 6; i++) good.Tick(1f, sample);
            Assert.That(good.State, Is.EqualTo(TrainingState.Complete), good.Feedback);
            var drooping = new TrainingSession(TrainingSession.HeavyLift, home, 0f, "RATE");
            var droop = new FlightSample { Y = 10f, Altitude = 8f, RotorSpeed01 = 0.9f, TorqueFraction = 1.1f };
            for (int i = 0; i < 5; i++) drooping.Tick(1f, droop);
            Assert.That(drooping.State, Is.EqualTo(TrainingState.Failed));
        }

        [Test]
        public void SettlingWithPowerDrillNeedsEntryThenAForwardRecovery()
        {
            var home = new ZoneDefinition { Id = "home", Radius = 17f };
            var session = new TrainingSession(TrainingSession.SettlingWithPower, home, 0f, "RATE");
            session.Tick(1f, new FlightSample { Altitude = 120f, VerticalSpeed = -7f, VortexRing = 0.7f });
            Assert.That(session.Stage, Is.EqualTo(1));
            var recovered = new FlightSample { Altitude = 95f, VerticalSpeed = -1f, VortexRing = 0f, HorizontalAirspeed = 14f };
            for (int i = 0; i < 3; i++) session.Tick(1f, recovered);
            Assert.That(session.State, Is.EqualTo(TrainingState.Complete), session.Feedback);
            var late = new TrainingSession(TrainingSession.SettlingWithPower, home, 0f, "RATE");
            late.Tick(1f, new FlightSample { Altitude = 60f, VerticalSpeed = -8f, VortexRing = 0.8f });
            late.Tick(1f, new FlightSample { Altitude = 15f, VerticalSpeed = -9f, VortexRing = 0.8f });
            Assert.That(late.State, Is.EqualTo(TrainingState.Failed));
        }

        [Test]
        public void AutorotationDrillFailsTheEngineThenScoresTheLanding()
        {
            var home = new ZoneDefinition { Id = "home", Radius = 17f };
            var session = new TrainingSession(TrainingSession.Autorotation, home, 0f, "RATE");
            var cruise = new FlightSample { Altitude = 180f, GroundSpeed = 25f, RotorSpeed01 = 1f };
            session.Tick(2f, cruise);
            Assert.That(session.RequestEngineFailure, Is.False);
            session.Tick(1.5f, cruise);
            Assert.That(session.RequestEngineFailure, Is.True, "The engine fails three seconds in.");
            session.RecordTouchdown(1.4f);
            var stopped = new FlightSample { Grounded = true, RotorSpeed01 = 0.8f };
            for (int i = 0; i < 3; i++) session.Tick(1f, stopped);
            Assert.That(session.State, Is.EqualTo(TrainingState.Complete), session.Feedback);
            Assert.That(session.TryClaimResult(out ChallengeResult result), Is.True);
            Assert.That(result.TouchdownMetresPerSecond, Is.EqualTo(1.4f).Within(0.001f));
        }

        [Test]
        public void ConfinedAreaDrillMeasuresAgainstItsOwnPad()
        {
            var home = new ZoneDefinition { Id = "home", X = 0f, Y = 0f, Z = 0f, Radius = 17f };
            var ridge = new ZoneDefinition { Id = "ridge", X = 900f, Y = 61f, Z = 1000f, Radius = 10f };
            var session = new TrainingSession(TrainingSession.ConfinedArea, home, 0f, "RATE", ridge);
            session.RecordTouchdown(0.6f);
            var landed = new FlightSample { X = 901f, Y = 62.5f, Z = 1000.5f, Grounded = true };
            for (int i = 0; i < 4; i++) session.Tick(1f, landed);
            Assert.That(session.State, Is.EqualTo(TrainingState.Complete), session.Feedback);
            Assert.That(session.PositionError, Is.LessThan(2f), "Distance is measured to the confined pad, not home.");
            var progression = new ProgressionData();
            Assert.That(session.TryClaimResult(out ChallengeResult result), Is.True);
            Assert.That(progression.Apply(result), Is.True);
            Assert.That(progression.CompletedTrainingMask & (1 << TrainingSession.ConfinedArea), Is.Not.Zero, "Every drill can record completion.");
        }
    }
}
