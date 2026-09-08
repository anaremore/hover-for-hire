using NUnit.Framework;

namespace HoverForHire.Tests
{
    public sealed class MissionTrainingTests
    {
        private static readonly ZoneDefinition Home = new ZoneDefinition { Id = "home", X = 0, Y = 0, Z = 0, Radius = 12 };
        private static FlightSample Hover(float z = 0f) => new FlightSample { X = 0, Y = 10, Z = z, Altitude = 8.5f, Grounded = false };

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void EachDrillCanCompleteFromObservableFlightAndRecordsAssists(int index)
        {
            var session = new TrainingSession(index, Home, 0f, "RATE YAW");
            FlightSample sample = Hover();
            switch (index)
            {
                case 2: sample.Heading = 90f; break;
                case 3: sample.Z = 110f; sample.GroundSpeed = 14f; break;
                case 4:
                    sample.Z = 100f; sample.GroundSpeed = 12f;
                    session.Tick(1f, sample);
                    sample.Z = 120f; sample.GroundSpeed = 0f;
                    break;
                case 5:
                case 6:
                    sample.Z = 90f; sample.Y = 15f;
                    session.Tick(1f, sample);
                    sample = new FlightSample { X = 0f, Y = 1.5f, Z = 0f, Grounded = true };
                    session.RecordTouchdown(0.5f);
                    break;
            }
            session.RecordAssists("UNASSISTED");
            for (int i = 0; i < 12; i++) session.Tick(1f, sample);
            Assert.That(session.State, Is.EqualTo(TrainingState.Complete), session.Feedback);
            Assert.That(session.TryClaimResult(out ChallengeResult result), Is.True);
            Assert.That(result.Assists, Does.Contain("RATE YAW").And.Contain("UNASSISTED"));
            Assert.That(result.Payout, Is.Zero);
            Assert.That(session.TryClaimResult(out _), Is.False);
        }

        [TestCase(5)]
        [TestCase(6)]
        public void LandingDrillCannotCompleteBySittingOnTheStartPad(int index)
        {
            var session = new TrainingSession(index, Home, 0f, "RATE");
            var grounded = new FlightSample { Y = 1.5f, Grounded = true };
            for (int i = 0; i < 20; i++) session.Tick(1f, grounded);
            Assert.That(session.State, Is.EqualTo(TrainingState.Active));
            Assert.That(session.Stage, Is.Zero);
            Assert.That(session.Progress, Is.Zero);
        }

        [Test]
        public void HoverRequiresContinuousStability()
        {
            var session = new TrainingSession(1, Home, 0f, "RATE");
            FlightSample sample = Hover();
            session.Tick(11f, sample);
            sample.GroundSpeed = 5f;
            session.Tick(0.1f, sample);
            Assert.That(session.StableSeconds, Is.Zero);
            sample.GroundSpeed = 0f;
            session.Tick(2f, sample);
            Assert.That(session.State, Is.EqualTo(TrainingState.Active));
        }

        [Test]
        public void PrecisionLandingRejectsHardContactEvenWhenAircraftSurvives()
        {
            var session = new TrainingSession(6, Home, 0f, "RATE");
            session.Tick(1f, Hover(25f));
            session.RecordTouchdown(2f);
            session.Tick(1f, new FlightSample { Y = 1.5f, Grounded = true });
            Assert.That(session.State, Is.EqualTo(TrainingState.Failed));
            Assert.That(session.Feedback, Does.Contain("reduce"));
            Assert.That(session.TryClaimResult(out _), Is.False);
        }
    }
}
