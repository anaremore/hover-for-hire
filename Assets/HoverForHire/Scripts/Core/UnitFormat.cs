using System;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>Metric: km/h, m/s and m. Aviation: knots, feet per minute and feet (nautical miles for long distances).</summary>
    public enum UnitSystem { Metric, Aviation }

    /// <summary>
    /// The one place displayed speeds, heights and distances are converted and formatted, so tapes, objectives,
    /// drill cues and feedback always agree. Physics and scoring stay in SI units.
    /// The formats the HUD refreshes every frame are built from a key: the value as shown, as an integer. Equal keys
    /// always give equal text, so a readout is formatted again only when its key changes (see HudModel).
    /// </summary>
    public static class UnitFormat
    {
        public const float MetresPerFoot = 0.3048f;
        public const float MetresPerNauticalMile = 1852f;
        public const float KnotsPerMetrePerSecond = 3600f / MetresPerNauticalMile;
        public const float FeetPerMinutePerMetrePerSecond = 60f / MetresPerFoot;

        public static float Speed(float metresPerSecond, UnitSystem units)
            => units == UnitSystem.Aviation ? metresPerSecond * KnotsPerMetrePerSecond : metresPerSecond * 3.6f;

        public static string SpeedUnit(UnitSystem units) => units == UnitSystem.Aviation ? "kt" : "km/h";

        public static float VerticalSpeed(float metresPerSecond, UnitSystem units)
            => units == UnitSystem.Aviation ? metresPerSecond * FeetPerMinutePerMetrePerSecond : metresPerSecond;

        public static string VerticalSpeedUnit(UnitSystem units) => units == UnitSystem.Aviation ? "ft/min" : "m/s";

        public static float Height(float metres, UnitSystem units) => units == UnitSystem.Aviation ? metres / MetresPerFoot : metres;

        public static string HeightUnit(UnitSystem units) => units == UnitSystem.Aviation ? "ft" : "m";

        /// <summary>Rounds half away from zero, as .NET number formatting does.</summary>
        public static long Round(double value) => (long)Math.Round(value, MidpointRounding.AwayFromZero);

        // Separates a format's precision ranges (for example tenths of a metre, then whole metres) within one key.
        private const long Coarse = 1L << 40;

        private static string Tenths(long tenths) => (tenths / 10.0).ToString("0.0");

        /// <summary>Airspeed or ground speed, e.g. "54 km/h" or "29 kt".</summary>
        public static string FormatSpeed(float metresPerSecond, UnitSystem units) => SpeedText(SpeedKey(metresPerSecond, units), units);
        public static long SpeedKey(float metresPerSecond, UnitSystem units) => Round(Speed(metresPerSecond, units));
        public static string SpeedText(long key, UnitSystem units) => $"{key} {SpeedUnit(units)}";

        /// <summary>A vertical rate that matters to the tenth, e.g. a touchdown or sink: "0.8 m/s" or "160 ft/min".</summary>
        public static string FormatSlowSpeed(float metresPerSecond, UnitSystem units)
            => units == UnitSystem.Aviation ? $"{RoundFeetPerMinute(metresPerSecond):0} ft/min" : $"{metresPerSecond:0.0} m/s";

        /// <summary>A vertical-rate band, e.g. "5–7 m/s" or "980–1380 ft/min".</summary>
        public static string FormatVerticalBand(float low, float high, UnitSystem units)
            => units == UnitSystem.Aviation ? $"{RoundFeetPerMinute(low):0}–{RoundFeetPerMinute(high):0} ft/min" : $"{low:0}–{high:0} m/s";

        /// <summary>A slow horizontal drift in the airspeed units, to the tenth: "2.9 km/h" or "1.6 kt".</summary>
        public static string FormatDriftSpeed(float metresPerSecond, UnitSystem units) => DriftText(DriftKey(metresPerSecond, units), units);
        public static long DriftKey(float metresPerSecond, UnitSystem units) => Round(Speed(metresPerSecond, units) * 10.0);
        public static string DriftText(long key, UnitSystem units) => $"{Tenths(key)} {SpeedUnit(units)}";

        /// <summary>A horizontal speed band, e.g. "29–79 km/h" or "16–43 kt".</summary>
        public static string FormatSpeedBand(float low, float high, UnitSystem units)
            => $"{Speed(low, units):0}–{Speed(high, units):0} {SpeedUnit(units)}";

        /// <summary>A round height for instructions, e.g. "25 m" or "82 ft".</summary>
        public static string FormatRoundHeight(float metres, UnitSystem units)
            => units == UnitSystem.Aviation ? $"{metres / MetresPerFoot:0} ft" : $"{metres:0} m";

        /// <summary>A short distance to the tenth of a metre (feet are whole), e.g. "2.3 m" or "8 ft".</summary>
        public static string FormatShortDistance(float metres, UnitSystem units) => ShortDistanceText(ShortDistanceKey(metres, units), units);
        public static long ShortDistanceKey(float metres, UnitSystem units)
            => units == UnitSystem.Aviation ? Round(metres / MetresPerFoot) : Round(metres * 10.0);
        public static string ShortDistanceText(long key, UnitSystem units) => units == UnitSystem.Aviation ? $"{key} ft" : $"{Tenths(key)} m";

        private static float RoundFeetPerMinute(float metresPerSecond) => Mathf.Round(metresPerSecond * FeetPerMinutePerMetrePerSecond / 10f) * 10f;

        /// <summary>Signed vertical speed, e.g. "+1.2 m/s" or "-240 ft/min".</summary>
        public static string FormatVerticalSpeed(float metresPerSecond, UnitSystem units)
            => VerticalSpeedText(VerticalSpeedKey(metresPerSecond, units), units);
        /// <summary>Tens of feet per minute, or tenths of a metre per second.</summary>
        public static long VerticalSpeedKey(float metresPerSecond, UnitSystem units)
            => units == UnitSystem.Aviation ? Round(metresPerSecond * FeetPerMinutePerMetrePerSecond / 10.0) : Round(metresPerSecond * 10.0);
        public static string VerticalSpeedText(long key, UnitSystem units)
        {
            string sign = key > 0 ? "+" : key < 0 ? "-" : "";
            long size = Math.Abs(key);
            return units == UnitSystem.Aviation ? $"{sign}{size * 10} ft/min" : $"{sign}{Tenths(size)} m/s";
        }

        /// <summary>Height above a surface, e.g. "12.3 m" or "40 ft".</summary>
        public static string FormatHeight(float metres, UnitSystem units) => HeightText(HeightKey(metres, units), units);
        /// <summary>Whole feet; tenths of a metre below 100 m, whole metres above.</summary>
        public static long HeightKey(float metres, UnitSystem units)
            => units == UnitSystem.Aviation ? Round(metres / MetresPerFoot) : metres < 100f ? Round(metres * 10.0) : Coarse + Round(metres);
        public static string HeightText(long key, UnitSystem units)
            => units == UnitSystem.Aviation ? $"{key} ft" : key >= Coarse ? $"{key - Coarse} m" : $"{Tenths(key)} m";

        /// <summary>A height band, e.g. "8–12 m" or "26–39 ft".</summary>
        public static string FormatHeightBand(float lowMetres, float highMetres, UnitSystem units)
            => units == UnitSystem.Aviation
                ? $"{lowMetres / MetresPerFoot:0}–{highMetres / MetresPerFoot:0} ft"
                : $"{lowMetres:0}–{highMetres:0} m";

        /// <summary>Horizontal distance, e.g. "850 m", "1.20 km", "600 ft" or "1.4 nm".</summary>
        public static string FormatDistance(float metres, UnitSystem units) => DistanceText(DistanceKey(metres, units), units);
        /// <summary>Whole feet, then tenths of a nautical mile from half a mile; whole metres, then hundredths of a kilometre.</summary>
        public static long DistanceKey(float metres, UnitSystem units)
        {
            if (units == UnitSystem.Aviation)
                return metres >= MetresPerNauticalMile * 0.5f ? Coarse + Round(metres / MetresPerNauticalMile * 10.0) : Round(metres / MetresPerFoot);
            return metres >= 1000f ? Coarse + Round(metres / 10.0) : Round(metres);
        }
        public static string DistanceText(long key, UnitSystem units)
        {
            if (units == UnitSystem.Aviation)
                return key >= Coarse ? $"{Tenths(key - Coarse)} nm" : $"{key} ft";
            return key >= Coarse ? $"{(key - Coarse) / 100.0:0.00} km" : $"{key} m";
        }
    }
}
