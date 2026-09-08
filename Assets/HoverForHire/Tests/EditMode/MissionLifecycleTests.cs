using NUnit.Framework;

namespace HoverForHire.Tests
{
    public sealed class MissionLifecycleTests
    {
        private static ContractDefinition Contract(ContractType type = ContractType.Passengers) => new ContractDefinition
        {
            Id = "test-job", Title = "Test service", Type = type, PayloadKg = 180f, BasePay = 200,
            Pickup = new ZoneDefinition { Id = "pickup", X = 0f, Y = 10f, Z = 0f, Radius = 10f },
            Destination = new ZoneDefinition { Id = "destination", X = 100f, Y = 40f, Z = 0f, Radius = 8f },
            ExpectedSeconds = 120f, DeadlineSeconds = 300f, DwellSeconds = 3f
        };

        private static FlightSample At(ZoneDefinition zone) => new FlightSample
        {
            X = zone.X, Y = zone.Y + 1.5f, Z = zone.Z, Grounded = true
        };

        private static void Dwell(MissionSession session, FlightSample sample)
        {
            for (int i = 0; i < 3; i++) session.Tick(1f, sample);
        }

        [TestCase(ContractType.Passengers)]
        [TestCase(ContractType.Cargo)]
        public void PickupAndDeliveryPayExactlyOnce(ContractType type)
        {
            var contract = Contract(type);
            var session = new MissionSession(contract);
            Assert.That(session.State, Is.EqualTo(MissionState.Available));
            Assert.That(session.TryClaimPayout(out _), Is.False);
            Assert.That(session.Accept("RATE YAW"), Is.True);
            Assert.That(session.State, Is.EqualTo(MissionState.Accepted));
            Assert.That(session.Accept("UNASSISTED"), Is.False);
            session.Tick(1f, At(contract.Pickup));
            Assert.That(session.State, Is.EqualTo(MissionState.Pickup));
            Assert.That(session.PayloadKg, Is.Zero);
            session.Tick(2f, At(contract.Pickup));
            Assert.That(session.State, Is.EqualTo(MissionState.Transport));
            Assert.That(session.PayloadKg, Is.EqualTo(180f));
            Assert.That(session.TryClaimPayout(out _), Is.False);
            session.RecordAssists("UNASSISTED");
            session.RecordTouchdown(0.7f);
            Dwell(session, At(contract.Destination));
            Assert.That(session.State, Is.EqualTo(MissionState.Delivered));
            Assert.That(session.PayloadKg, Is.Zero);
            Assert.That(session.TryClaimPayout(out ChallengeResult result), Is.True);
            Assert.That(result.Payout, Is.GreaterThan(0));
            Assert.That(result.Assists, Does.Contain("RATE YAW").And.Contain("UNASSISTED"));
            Assert.That(session.TryClaimPayout(out _), Is.False);
            Assert.That(session.Retry("RATE"), Is.False);
            session.Tick(30f, At(contract.Destination));
            session.Fail("reset");
            Assert.That(session.State, Is.EqualTo(MissionState.Delivered));
            Assert.That(session.TryClaimPayout(out _), Is.False);
        }

        [Test]
        public void FlyoverAndContactAtAnotherElevationCannotLoad()
        {
            var contract = Contract();
            var session = new MissionSession(contract);
            session.Accept("RATE");
            FlightSample sample = At(contract.Pickup);
            sample.Grounded = false;
            Dwell(session, sample);
            Assert.That(session.State, Is.EqualTo(MissionState.Pickup));
            sample.Grounded = true;
            sample.Y += 20f;
            Dwell(session, sample);
            Assert.That(session.State, Is.EqualTo(MissionState.Pickup));
            Assert.That(session.DwellSeconds, Is.Zero);
        }

        [TestCase("speed")]
        [TestCase("vertical")]
        [TestCase("tilt")]
        [TestCase("radius")]
        [TestCase("contact")]
        public void AnyUnstableObservationResetsContinuousDwell(string disturbance)
        {
            var contract = Contract();
            var session = new MissionSession(contract);
            session.Accept("RATE");
            FlightSample sample = At(contract.Pickup);
            session.Tick(2.9f, sample);
            switch (disturbance)
            {
                case "speed": sample.GroundSpeed = 1.2f; break;
                case "vertical": sample.VerticalSpeed = -0.8f; break;
                case "tilt": sample.TiltDegrees = 15f; break;
                case "radius": sample.X = 11f; break;
                default: sample.Grounded = false; break;
            }
            session.Tick(0.02f, sample);
            Assert.That(session.DwellSeconds, Is.Zero);
            session.Tick(0.2f, At(contract.Pickup));
            Assert.That(session.State, Is.EqualTo(MissionState.Pickup));
        }

        [Test]
        public void CrashAndRetryClearPayloadDwellAndAttemptMetrics()
        {
            var contract = Contract();
            var session = new MissionSession(contract);
            session.Accept("RATE");
            Dwell(session, At(contract.Pickup));
            session.RecordTouchdown(4f);
            session.Tick(1f, At(contract.Destination));
            string originalAttempt = session.AttemptId;
            FlightSample crashed = At(contract.Destination);
            crashed.Crashed = true;
            session.Tick(1f, crashed);
            Assert.That(session.State, Is.EqualTo(MissionState.Failed));
            Assert.That(session.PayloadKg, Is.Zero);
            Assert.That(session.DwellSeconds, Is.Zero);
            Assert.That(session.TryClaimPayout(out _), Is.False);
            Assert.That(session.Retry("UNASSISTED"), Is.True);
            Assert.That(session.AttemptId, Is.Not.EqualTo(originalAttempt));
            Assert.That(session.ElapsedSeconds, Is.Zero);
            Assert.That(session.LandingSpeed, Is.Zero);
            Assert.That(session.Comfort, Is.EqualTo(100f));
            Assert.That(session.PayloadKg, Is.Zero);
            Dwell(session, At(contract.Destination));
            Assert.That(session.State, Is.EqualTo(MissionState.Pickup), "A retry must pick up again before delivery.");
            Dwell(session, At(contract.Pickup));
            Dwell(session, At(contract.Destination));
            Assert.That(session.TryClaimPayout(out _), Is.True);
        }

        [Test]
        public void DeadlineFailureCannotDeliverOrPayUntilRetried()
        {
            var contract = Contract();
            var session = new MissionSession(contract);
            session.Accept("RATE");
            Dwell(session, At(contract.Pickup));
            session.Tick(301f, At(contract.Destination));
            Assert.That(session.State, Is.EqualTo(MissionState.Failed));
            Assert.That(session.PayloadKg, Is.Zero);
            Assert.That(session.TryClaimPayout(out _), Is.False);
        }

        [Test]
        public void RoughHandlingAndHardLandingReduceConditionAndReward()
        {
            var contract = Contract(ContractType.Cargo);
            var smooth = new MissionSession(contract);
            var rough = new MissionSession(contract);
            smooth.Accept("RATE"); rough.Accept("RATE");
            Dwell(smooth, At(contract.Pickup)); Dwell(rough, At(contract.Pickup));
            FlightSample sample = At(contract.Destination);
            sample.Grounded = false; sample.Acceleration = 20f; sample.AngularSpeedDegrees = 65f;
            rough.Tick(5f, sample);
            rough.RecordTouchdown(4f);
            smooth.RecordTouchdown(0.5f);
            Dwell(smooth, At(contract.Destination)); Dwell(rough, At(contract.Destination));
            Assert.That(rough.CargoCondition, Is.LessThan(smooth.CargoCondition));
            Assert.That(rough.Comfort, Is.LessThan(smooth.Comfort));
            Assert.That(rough.Result.Score, Is.LessThan(smooth.Result.Score));
            Assert.That(rough.Result.Payout, Is.LessThan(smooth.Result.Payout));
        }
    }
}
