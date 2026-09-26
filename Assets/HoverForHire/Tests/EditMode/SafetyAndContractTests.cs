using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HoverForHire.Tests
{
    /// <summary>Crash feedback wording, assist preset matching and the production contract table.</summary>
    public sealed class SafetyAndContractTests
    {
        [Test]
        public void EveryCrashCauseHasSpecificFeedback()
        {
            foreach (CrashCause cause in Enum.GetValues(typeof(CrashCause)))
            {
                Assert.That(CrashReport.Title(cause), Is.Not.Empty, cause.ToString());
                Assert.That(CrashReport.Detail(cause, 7.2f, 5.5f, "Island terrain"), Is.Not.Empty, cause.ToString());
                Assert.That(CrashReport.Advice(cause), Is.Not.Empty, cause.ToString());
            }
            Assert.That(CrashReport.Detail(CrashCause.HardLanding, 7.24f, 5.5f, ""), Does.Contain("7.2").And.Contain("5.5"),
                "A hard landing reports the measured touchdown speed against the limit.");
            Assert.That(CrashReport.Title(CrashCause.RotorStrike), Is.Not.EqualTo(CrashReport.Title(CrashCause.HardLanding)));
        }

        [TestCase("Collision / layer 9", "a tree")]
        [TestCase("Collision / layer 0", "a structure")]
        [TestCase("Island terrain", "the ground")]
        [TestCase("Coastal building", "a building")]
        [TestCase("Windsock pole", "the windsock")]
        [TestCase("", "an obstacle")]
        public void StruckObjectsGetReadableNames(string collider, string expected)
            => Assert.That(CrashReport.Describe(collider), Is.EqualTo(expected));

        [Test]
        public void AssistFlagsReportTheirMatchingPreset()
        {
            var settings = new AssistSettings();
            foreach (AssistPreset preset in Enum.GetValues(typeof(AssistPreset)))
            {
                settings.SetPreset(preset);
                Assert.That(settings.MatchingPreset, Is.EqualTo(preset));
            }
            settings.SetPreset(AssistPreset.Standard);
            settings.YawStabilization = false;
            Assert.That(settings.MatchingPreset, Is.Null, "A custom mix is not reported as a preset.");
        }

        [Test]
        public void ProductionContractsApplyStricterLimitsToAdvancedWork()
        {
            var zones = new LandingZone[10];
            var owner = new GameObject("Contract table fixture");
            owner.SetActive(false);
            string save = Path.Combine(Path.GetTempPath(), "HoverForHire-Contracts-" + Guid.NewGuid().ToString("N"), "progression.json");
            try
            {
                for (int i = 0; i < zones.Length; i++)
                {
                    var zoneObject = new GameObject("Zone " + i);
                    zoneObject.transform.SetParent(owner.transform);
                    zoneObject.transform.position = new Vector3(i * 150f, i, 0f);
                    zones[i] = zoneObject.AddComponent<LandingZone>();
                    zones[i].Id = "pad-" + i;
                }
                var director = owner.AddComponent<MissionDirector>();
                director.ProgressionPathOverride = save;
                director.Zones = zones;
                director.StartShift();
                Assert.That(director.Contracts.Count, Is.EqualTo(9));
                int immediate = 0, advanced = 0;
                foreach (ContractDefinition contract in director.Contracts)
                {
                    Assert.That(contract.Pickup.Id, Is.Not.EqualTo(contract.Destination.Id), contract.Id);
                    Assert.That(contract.ExpectedSeconds, Is.GreaterThan(0f), contract.Id);
                    if (contract.RequiredDeliveries == 0) immediate++;
                    if (contract.RequiredDeliveries >= 4)
                    {
                        advanced++;
                        Assert.That(contract.DwellSeconds, Is.EqualTo(4f), contract.Id);
                        Assert.That(contract.MaximumGroundSpeed, Is.EqualTo(0.5f), contract.Id);
                        Assert.That(contract.MaximumTiltDegrees, Is.EqualTo(6f), contract.Id);
                        Assert.That(contract.DeadlineSeconds, Is.EqualTo(contract.ExpectedSeconds * 2f).Within(0.01f), contract.Id);
                    }
                    else
                    {
                        Assert.That(contract.DwellSeconds, Is.EqualTo(3f), contract.Id);
                        Assert.That(contract.MaximumGroundSpeed, Is.EqualTo(0.8f), contract.Id);
                        Assert.That(contract.MaximumTiltDegrees, Is.EqualTo(8f), contract.Id);
                    }
                }
                Assert.That(immediate, Is.EqualTo(2), "The first helicopter can take its first two jobs immediately.");
                Assert.That(advanced, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                if (Directory.Exists(Path.GetDirectoryName(save))) Directory.Delete(Path.GetDirectoryName(save), true);
            }
        }
    }
}
