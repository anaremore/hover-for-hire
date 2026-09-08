using System;
using System.IO;
using NUnit.Framework;

namespace HoverForHire.Tests
{
    public sealed class MissionPersistenceTests
    {
        private string directory;
        private ProgressionStore store;

        [SetUp]
        public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "HoverForHire-Test-" + Guid.NewGuid().ToString("N"));
            store = new ProgressionStore(Path.Combine(directory, "progression.json"));
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(store.Path + suffix)) File.Delete(store.Path + suffix);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }

        private static ChallengeResult Delivery(string id) => new ChallengeResult
        {
            AttemptId = id, ContractId = "cargo", Title = "Provisions", Mode = "Delivery Shift", Payout = 210,
            Grade = "B", Score = 85f, Assists = "RATE YAW → UNASSISTED", CompletedUtc = DateTime.UtcNow.ToString("O")
        };

        [Test]
        public void SaveReloadPreservesProgressAndRejectsDuplicatePayout()
        {
            var data = new ProgressionData();
            ChallengeResult result = Delivery("unique-attempt");
            Assert.That(data.Apply(result), Is.True);
            Assert.That(store.Save(data), Is.True, store.LastError);
            ProgressionData reloaded = store.Load();
            Assert.That(reloaded.CompletedDeliveries, Is.EqualTo(1));
            Assert.That(reloaded.TotalEarnings, Is.EqualTo(210));
            Assert.That(reloaded.Results[0].Assists, Is.EqualTo(result.Assists));
            Assert.That(reloaded.Apply(result), Is.False);
            Assert.That(reloaded.TotalEarnings, Is.EqualTo(210));
        }

        [Test]
        public void CorruptPrimaryRecoversLastGoodBackupAndCanSaveAgain()
        {
            var data = new ProgressionData();
            data.Apply(Delivery("first"));
            Assert.That(store.Save(data), Is.True, store.LastError);
            data.Apply(Delivery("second"));
            Assert.That(store.Save(data), Is.True, store.LastError);
            File.WriteAllText(store.Path, "{ interrupted");
            ProgressionData recovered = store.Load();
            Assert.That(store.RecoveredBackup, Is.True);
            Assert.That(recovered.CompletedDeliveries, Is.EqualTo(1));
            recovered.Apply(Delivery("replacement"));
            Assert.That(store.Save(recovered), Is.True, store.LastError);
            Assert.That(store.Load().CompletedDeliveries, Is.EqualTo(2));
        }

        [Test]
        public void MissingPrimaryDuringReplacementLoadsBackup()
        {
            var data = new ProgressionData();
            data.Apply(Delivery("first"));
            store.Save(data);
            data.Apply(Delivery("second"));
            store.Save(data);
            File.Delete(store.Path);
            File.WriteAllText(store.Path + ".tmp", "{ partial");
            Assert.That(store.Load().CompletedDeliveries, Is.EqualTo(1));
            Assert.That(store.RecoveredBackup, Is.True);
        }

        [Test]
        public void TrainingCompletionIsPersistedWithoutPaymentOrJobUnlocks()
        {
            var data = new ProgressionData();
            var result = new ChallengeResult { AttemptId = "drill", ContractId = "training-6", Mode = "Training", Grade = "A", Payout = 0 };
            Assert.That(data.Apply(result), Is.True);
            Assert.That(store.Save(data), Is.True, store.LastError);
            ProgressionData loaded = store.Load();
            Assert.That(loaded.CompletedTrainingMask, Is.EqualTo(1 << 6));
            Assert.That(loaded.CompletedDeliveries, Is.Zero);
            Assert.That(loaded.TotalEarnings, Is.Zero);
            Assert.That(loaded.Apply(result), Is.False);
        }

        [Test]
        public void UnsupportedVersionDoesNotLoadAsValidProgression()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(store.Path, "{\"Version\":99,\"CompletedDeliveries\":999,\"TotalEarnings\":100000}");
            Assert.That(store.Load().CompletedDeliveries, Is.Zero);
            Assert.That(store.LastError, Is.Not.Empty);
        }
    }
}
