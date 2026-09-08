using NUnit.Framework;
using UnityEngine;

namespace HoverForHire.Tests
{
    public sealed class AircraftImpactTests
    {
        [TestCase(.5f, ImpactSeverity.Contact)]
        [TestCase(3f, ImpactSeverity.Scrape)]
        [TestCase(7f, ImpactSeverity.HardHit)]
        [TestCase(14f, ImpactSeverity.Structural)]
        [TestCase(25f, ImpactSeverity.Catastrophic)]
        public void IncomingImpactEnergyProducesDistinctPresentationTiers(float speed, ImpactSeverity expected)
        {
            var impact = new AircraftImpact(Vector3.zero, Vector3.up, Vector3.down * speed, speed, 1000);
            Assert.That(impact.Severity, Is.EqualTo(expected));
            Assert.That(impact.EnergyJoules, Is.EqualTo(.5f * 1000 * speed * speed).Within(.1f));
        }

        [Test]
        public void FireballRequiresBothHighSpeedAndHighEnergyAndNeverWater()
        {
            Assert.That(AircraftImpact.Classify(19, 500000), Is.EqualTo(ImpactSeverity.Structural));
            Assert.That(AircraftImpact.Classify(25, 100000), Is.EqualTo(ImpactSeverity.Structural));
            Assert.That(AircraftImpact.Classify(30, 450000, true), Is.EqualTo(ImpactSeverity.Structural));
            Assert.That(AircraftImpact.Classify(20, 180000), Is.EqualTo(ImpactSeverity.Catastrophic));
        }

        [Test]
        public void InvalidIncomingNumbersCannotCreateAnExplosion()
        {
            var impact = new AircraftImpact(Vector3.zero, Vector3.zero, Vector3.zero, float.NaN, float.PositiveInfinity);
            Assert.That(impact.Speed, Is.Zero);
            Assert.That(impact.EnergyJoules, Is.Zero);
            Assert.That(impact.Normal, Is.EqualTo(Vector3.up));
            Assert.That(impact.Severity, Is.EqualTo(ImpactSeverity.Contact));
        }
    }
}
