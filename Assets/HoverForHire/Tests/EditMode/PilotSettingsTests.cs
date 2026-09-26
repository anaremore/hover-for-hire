using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HoverForHire.Tests
{
    /// <summary>Unit conversion and the versioned player-settings store (with migration), using in-memory storage.</summary>
    public sealed class PilotSettingsTests
    {
        private sealed class MemoryStorage : IPreferenceStorage
        {
            public readonly Dictionary<string, string> Strings = new Dictionary<string, string>();
            public readonly Dictionary<string, float> Floats = new Dictionary<string, float>();
            public int Saves;
            public bool HasKey(string key) => Strings.ContainsKey(key) || Floats.ContainsKey(key);
            public string GetString(string key) => Strings.TryGetValue(key, out string value) ? value : "";
            public float GetFloat(string key, float fallback) => Floats.TryGetValue(key, out float value) ? value : fallback;
            public void SetString(string key, string value) => Strings[key] = value;
            public void DeleteKey(string key) { Strings.Remove(key); Floats.Remove(key); }
            public void Save() => Saves++;
        }

        [Test]
        public void UnitsConvertAndFormatConsistently()
        {
            Assert.That(UnitFormat.Speed(10f, UnitSystem.Metric), Is.EqualTo(36f).Within(0.001f));
            Assert.That(UnitFormat.Speed(10f, UnitSystem.Aviation), Is.EqualTo(19.438f).Within(0.01f));
            Assert.That(UnitFormat.VerticalSpeed(1f, UnitSystem.Aviation), Is.EqualTo(196.85f).Within(0.01f));
            Assert.That(UnitFormat.Height(30.48f, UnitSystem.Aviation), Is.EqualTo(100f).Within(0.001f));
            Assert.That(UnitFormat.FormatSpeed(15f, UnitSystem.Metric), Is.EqualTo("54 km/h"));
            Assert.That(UnitFormat.FormatSpeed(15f, UnitSystem.Aviation), Is.EqualTo("29 kt"));
            Assert.That(UnitFormat.FormatVerticalSpeed(-1.25f, UnitSystem.Metric), Is.EqualTo("-1.3 m/s").Or.EqualTo("-1.2 m/s"));
            Assert.That(UnitFormat.FormatVerticalSpeed(1.22f, UnitSystem.Aviation), Is.EqualTo("+240 ft/min"));
            Assert.That(UnitFormat.FormatHeight(12.34f, UnitSystem.Metric), Is.EqualTo("12.3 m"));
            Assert.That(UnitFormat.FormatHeight(12.192f, UnitSystem.Aviation), Is.EqualTo("40 ft"));
            Assert.That(UnitFormat.FormatHeightBand(8f, 12f, UnitSystem.Aviation), Is.EqualTo("26–39 ft"));
            Assert.That(UnitFormat.FormatDistance(850f, UnitSystem.Metric), Is.EqualTo("850 m"));
            Assert.That(UnitFormat.FormatDistance(1200f, UnitSystem.Metric), Is.EqualTo("1.20 km"));
            Assert.That(UnitFormat.FormatDistance(150f, UnitSystem.Aviation), Is.EqualTo("492 ft"));
            Assert.That(UnitFormat.FormatDistance(2593f, UnitSystem.Aviation), Is.EqualTo("1.4 nm"));
            Assert.That(UnitFormat.FormatSlowSpeed(0.8f, UnitSystem.Metric), Is.EqualTo("0.8 m/s"));
        }

        [Test]
        public void DrillAndCrashTextFollowTheChosenUnits()
        {
            var home = new ZoneDefinition { Id = "home", Radius = 17f };
            var metric = new TrainingSession(3, home, 0f, "RATE");
            Assert.That(metric.Objective, Does.Contain("km/h").And.Contain(" m "), metric.Objective);
            var aviation = new TrainingSession(3, home, 0f, "RATE", null, UnitSystem.Aviation);
            Assert.That(aviation.Objective, Does.Contain(" ft").And.Contain(" kt").And.Not.Contain("m/s"), aviation.Objective);
            var settling = new TrainingSession(TrainingSession.SettlingWithPower, home, 0f, "RATE", null, UnitSystem.Aviation);
            Assert.That(settling.Objective, Does.Contain("ft/min"), settling.Objective);
            Assert.That(CrashReport.Detail(CrashCause.HardLanding, 7.24f, 5.5f, "", UnitSystem.Aviation), Does.Contain("ft/min"));
            Assert.That(CrashReport.Advice(CrashCause.RotorStrike, UnitSystem.Aviation), Does.Contain("15 ft"));
        }

        [Test]
        public void FreshInstallGetsDefaultsWithoutWriting()
        {
            var storage = new MemoryStorage();
            PilotSettings settings = PilotSettingsStore.Load(storage);
            Assert.That(settings.Units, Is.EqualTo(UnitSystem.Metric));
            Assert.That(settings.FirstRunComplete, Is.False);
            Assert.That(settings.Realism.MatchingPreset, Is.EqualTo(RealismPreset.Relaxed));
            Assert.That(storage.Strings, Is.Empty, "Nothing to migrate, nothing written.");
        }

        [Test]
        public void LegacyKeysMigrateOnceAndAreRemoved()
        {
            var storage = new MemoryStorage();
            var assists = new AssistSettings();
            assists.SetPreset(AssistPreset.Unassisted);
            var realism = new RealismSettings();
            realism.SetPreset(RealismPreset.Expert);
            storage.Strings[PilotSettingsStore.LegacyAssistsKey] = JsonUtility.ToJson(assists);
            storage.Floats[PilotSettingsStore.LegacyVolumeKey] = 0.3f;
            storage.Strings[PilotSettingsStore.LegacyRealismKey] = JsonUtility.ToJson(realism);
            storage.Strings[PilotSettingsStore.LegacyFirstRunKey] = "1";

            PilotSettings settings = PilotSettingsStore.Load(storage);
            Assert.That(settings.Assists.MatchingPreset, Is.EqualTo(AssistPreset.Unassisted));
            Assert.That(settings.Volume, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(settings.Realism.MatchingPreset, Is.EqualTo(RealismPreset.Expert));
            Assert.That(settings.FirstRunComplete, Is.True);
            Assert.That(storage.HasKey(PilotSettingsStore.Key), Is.True);
            foreach (string legacy in new[] { PilotSettingsStore.LegacyAssistsKey, PilotSettingsStore.LegacyVolumeKey,
                PilotSettingsStore.LegacyRealismKey, PilotSettingsStore.LegacyFirstRunKey })
                Assert.That(storage.HasKey(legacy), Is.False, legacy + " is removed after migration.");

            PilotSettings reloaded = PilotSettingsStore.Load(storage);
            Assert.That(reloaded.Assists.MatchingPreset, Is.EqualTo(AssistPreset.Unassisted));
            Assert.That(reloaded.Realism.MatchingPreset, Is.EqualTo(RealismPreset.Expert));
        }

        [Test]
        public void SavedSettingsRoundTripAndCorruptionFallsBackToDefaults()
        {
            var storage = new MemoryStorage();
            var settings = new PilotSettings { Units = UnitSystem.Aviation, Volume = 0.9f, FirstRunComplete = true };
            settings.Realism.SetPreset(RealismPreset.Realistic);
            PilotSettingsStore.Save(storage, settings);
            PilotSettings loaded = PilotSettingsStore.Load(storage);
            Assert.That(loaded.Units, Is.EqualTo(UnitSystem.Aviation));
            Assert.That(loaded.Volume, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(loaded.Realism.MatchingPreset, Is.EqualTo(RealismPreset.Realistic));

            storage.Strings[PilotSettingsStore.Key] = "{\"Units\": 7, \"Volume\": 42, \"Realism\": {\"Gustiness\": -3}}";
            PilotSettings sanitized = PilotSettingsStore.Load(storage);
            Assert.That(sanitized.Units, Is.EqualTo(UnitSystem.Metric));
            Assert.That(sanitized.Volume, Is.EqualTo(1f));
            Assert.That(sanitized.Realism.Gustiness, Is.Zero);
        }
    }
}
