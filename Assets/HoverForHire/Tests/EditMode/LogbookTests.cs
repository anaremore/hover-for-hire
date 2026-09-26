using System;
using System.IO;
using NUnit.Framework;

namespace HoverForHire.Tests
{
    /// <summary>Progression version 2: migration from version 1, personal bests and liveries.</summary>
    public sealed class LogbookTests
    {
        private string directory;
        private ProgressionStore store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "HoverForHire-Logbook-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            store = new ProgressionStore(Path.Combine(directory, "progression.json"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private static ChallengeResult Delivery(string attempt, float seconds, float score, string grade = "B")
            => new ChallengeResult { AttemptId = attempt, ContractId = "town-commute", Title = "Town connection", Mode = "Delivery Shift",
                Seconds = seconds, Score = score, Grade = grade, Payout = 200 };

        [Test]
        public void VersionOneSavesMigrateAndRebuildTheirBests()
        {
            const string versionOne = "{\"Version\":1,\"CompletedDeliveries\":1,\"TotalEarnings\":230,\"CompletedTrainingMask\":64," +
                "\"AppliedAttemptIds\":[\"a\",\"b\"],\"Results\":[" +
                "{\"AttemptId\":\"a\",\"ContractId\":\"town-commute\",\"Title\":\"Town connection\",\"Mode\":\"Delivery Shift\",\"Grade\":\"A\",\"Score\":93,\"Seconds\":81,\"Payout\":230}," +
                "{\"AttemptId\":\"b\",\"ContractId\":\"training-6\",\"Title\":\"Precision landing\",\"Mode\":\"Training\",\"Grade\":\"B\",\"Score\":84,\"Seconds\":62,\"Assists\":\"RATE LEVEL\"}]}";
            File.WriteAllText(store.Path, versionOne);

            ProgressionData loaded = store.Load();
            Assert.That(store.LastError, Is.Empty);
            Assert.That(loaded.Version, Is.EqualTo(ProgressionData.CurrentVersion));
            Assert.That(loaded.CompletedDeliveries, Is.EqualTo(1));
            Assert.That(loaded.RouteBest("town-commute").BestSeconds, Is.EqualTo(81f));
            Assert.That(loaded.RouteBest("town-commute").BestGrade, Is.EqualTo("A"));
            Assert.That(loaded.DrillBest(6).BestScore, Is.EqualTo(84f));
            Assert.That(loaded.DrillBest(6).Assists, Is.EqualTo("RATE LEVEL"));
            Assert.That(loaded.OwnsLivery(0) && loaded.SelectedLivery == 0, Is.True, "Everyone flies the standard paint.");

            Assert.That(store.Save(loaded), Is.True, store.LastError);
            Assert.That(File.ReadAllText(store.Path), Does.Contain("\"Version\": 2"));
            ProgressionData reloaded = store.Load();
            Assert.That(reloaded.RouteBests.Count, Is.EqualTo(1), "Migration does not double-count on the next load.");
            Assert.That(reloaded.RouteBest("town-commute").Completions, Is.EqualTo(1));
        }

        [Test]
        public void BestsKeepTheFastestTimeAndTheHighestScore()
        {
            var record = new ProgressionData();
            Assert.That(record.Apply(Delivery("slow-good", 100f, 88f, "B")), Is.True);
            Assert.That(record.Apply(Delivery("fast-rough", 90f, 70f, "C")), Is.True);
            Assert.That(record.Apply(Delivery("fast-rough", 60f, 99f, "A")), Is.False, "A result counts once.");
            RouteRecord route = record.RouteBest("town-commute");
            Assert.That(route.BestSeconds, Is.EqualTo(90f));
            Assert.That(route.BestScore, Is.EqualTo(88f));
            Assert.That(route.BestGrade, Is.EqualTo("B"));
            Assert.That(route.Completions, Is.EqualTo(2));
            record.FlightSeconds = 3725f;
            record.Landings = 12;
            Assert.That(store.Save(record), Is.True, store.LastError);
            ProgressionData reloaded = store.Load();
            Assert.That(reloaded.FlightSeconds, Is.EqualTo(3725f));
            Assert.That(reloaded.Landings, Is.EqualTo(12));
        }

        [Test]
        public void LiveriesAreBoughtWithEarningsAndOnlyOwnedOnesCanBeFlown()
        {
            var record = new ProgressionData { TotalEarnings = 1000 };
            Assert.That(record.BuyLivery(3, 1500), Is.False, "Not enough earned.");
            Assert.That(record.SelectLivery(3), Is.False, "Cannot fly paint you do not own.");
            Assert.That(record.BuyLivery(1, 600), Is.True);
            Assert.That(record.Balance, Is.EqualTo(400));
            Assert.That(record.BuyLivery(1, 600), Is.False, "Bought once.");
            Assert.That(record.SelectLivery(1), Is.True);
            Assert.That(record.IsValid, Is.True);
            Assert.That(store.Save(record), Is.True, store.LastError);
            ProgressionData reloaded = store.Load();
            Assert.That(reloaded.SelectedLivery, Is.EqualTo(1));
            Assert.That(reloaded.Balance, Is.EqualTo(400));
            reloaded.SpentOnLiveries = 5000;
            Assert.That(reloaded.IsValid, Is.False, "Spending more than was earned is a corrupt record.");
            Assert.That(Liveries.All.Length, Is.EqualTo(4));
            Assert.That(Liveries.All[0].Price, Is.Zero);
        }
    }
}
