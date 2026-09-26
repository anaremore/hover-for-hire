using System;
using NUnit.Framework;
using UnityEngine;

namespace HoverForHire.Tests
{
    /// <summary>
    /// HUD readouts are formatted from keys (the value as shown) so they can be cached. They must read exactly as the
    /// direct formats did. Values sit 0.3 of a display step from a whole step, clear of rounding midpoints, where
    /// float-to-decimal detail may round either way.
    /// </summary>
    public sealed class ReadoutFormatTests
    {
        private const UnitSystem Metric = UnitSystem.Metric, Aviation = UnitSystem.Aviation;

        private static void Sweep(int steps, Func<int, float> value, Func<float, string> keyed, Func<float, string> direct)
        {
            for (int k = 0; k < steps; k++)
            {
                float v = value(k);
                Assert.That(keyed(v), Is.EqualTo(direct(v)), $"value {v}");
            }
        }

        [Test]
        public void SpeedsReadAsBefore()
        {
            Sweep(400, k => (k + .3f) / 3.6f, v => UnitFormat.FormatSpeed(v, Metric), v => $"{v * 3.6f:0} km/h");
            Sweep(200, k => (k + .3f) / UnitFormat.KnotsPerMetrePerSecond, v => UnitFormat.FormatSpeed(v, Aviation),
                v => $"{v * UnitFormat.KnotsPerMetrePerSecond:0} kt");
            Sweep(300, k => (k + .3f) / 36f, v => UnitFormat.FormatDriftSpeed(v, Metric), v => $"{v * 3.6f:0.0} km/h");
            Sweep(300, k => (k + .3f) / 10f / UnitFormat.KnotsPerMetrePerSecond, v => UnitFormat.FormatDriftSpeed(v, Aviation),
                v => $"{v * UnitFormat.KnotsPerMetrePerSecond:0.0} kt");
        }

        [Test]
        public void VerticalSpeedsReadAsBefore()
        {
            Sweep(300, k => (k - 150 + (k < 150 ? -.3f : .3f)) / 10f, v => UnitFormat.FormatVerticalSpeed(v, Metric),
                v => $"{v:+0.0;-0.0;0.0} m/s");
            Sweep(300, k => (k - 150 + (k < 150 ? -.3f : .3f)) * 10f / UnitFormat.FeetPerMinutePerMetrePerSecond,
                v => UnitFormat.FormatVerticalSpeed(v, Aviation),
                v => $"{Mathf.Round(v * UnitFormat.FeetPerMinutePerMetrePerSecond / 10f) * 10f:+0;-0;0} ft/min");
            Assert.That(UnitFormat.FormatVerticalSpeed(-0.02f, Metric), Is.EqualTo("0.0 m/s"), "A reading that rounds to zero has no sign.");
            Assert.That(UnitFormat.FormatVerticalSpeed(0.01f, Aviation), Is.EqualTo("0 ft/min"));
        }

        [Test]
        public void HeightsAndDistancesReadAsBefore()
        {
            Sweep(1500, k => (k + .3f) / 10f, v => UnitFormat.FormatHeight(v, Metric), v => v < 100f ? $"{v:0.0} m" : $"{v:0} m");
            Sweep(1000, k => (k + .3f) * UnitFormat.MetresPerFoot, v => UnitFormat.FormatHeight(v, Aviation), v => $"{v / UnitFormat.MetresPerFoot:0} ft");
            Sweep(600, k => (k + .3f) / 10f, v => UnitFormat.FormatShortDistance(v, Metric), v => $"{v:0.0} m");
            Sweep(600, k => (k + .3f) * UnitFormat.MetresPerFoot, v => UnitFormat.FormatShortDistance(v, Aviation), v => $"{v / UnitFormat.MetresPerFoot:0} ft");
            Sweep(3000, k => k + .3f, v => UnitFormat.FormatDistance(v, Metric), v => v >= 1000f ? $"{v / 1000f:0.00} km" : $"{v:0} m");
            // Feet up to 3000 ft (below half a nautical mile), then tenths of a nautical mile.
            Sweep(3000, k => (k + .3f) * UnitFormat.MetresPerFoot, v => UnitFormat.FormatDistance(v, Aviation), v => $"{v / UnitFormat.MetresPerFoot:0} ft");
            Sweep(200, k => (k + 5.3f) / 10f * UnitFormat.MetresPerNauticalMile, v => UnitFormat.FormatDistance(v, Aviation),
                v => $"{v / UnitFormat.MetresPerNauticalMile:0.0} nm");
        }

        [Test]
        public void EqualKeysGiveEqualTextAcrossPrecisionChanges()
        {
            // 99.96 m rounds up into the tenths range ("100.0 m"); 100.2 m is in the whole-metre range. Different keys.
            Assert.That(UnitFormat.HeightKey(99.96f, Metric), Is.Not.EqualTo(UnitFormat.HeightKey(100.2f, Metric)));
            Assert.That(UnitFormat.FormatHeight(99.96f, Metric), Is.EqualTo("100.0 m"));
            Assert.That(UnitFormat.FormatHeight(100.2f, Metric), Is.EqualTo("100 m"));
            Assert.That(UnitFormat.FormatDistance(999.7f, Metric), Is.EqualTo("1000 m"));
            Assert.That(UnitFormat.FormatDistance(1000f, Metric), Is.EqualTo("1.00 km"));
        }
    }
}
