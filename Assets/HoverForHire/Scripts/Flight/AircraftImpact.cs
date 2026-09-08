using UnityEngine;

namespace HoverForHire
{
    public enum ImpactSeverity { Contact, Scrape, HardHit, Structural, Catastrophic }

    /// <summary>Presentation data only. Severity never changes the flight model's crash thresholds.</summary>
    public readonly struct AircraftImpact
    {
        public readonly Vector3 Point, Normal, IncomingVelocity;
        public readonly float Speed, EnergyJoules;
        public readonly bool IsWater;
        public readonly ImpactSeverity Severity;

        public AircraftImpact(Vector3 point, Vector3 normal, Vector3 incomingVelocity, float speed, float mass, bool isWater = false)
        {
            Point = point;
            Normal = normal.sqrMagnitude > .001f ? normal.normalized : Vector3.up;
            IncomingVelocity = incomingVelocity;
            Speed = float.IsNaN(speed) || float.IsInfinity(speed) ? 0 : Mathf.Max(0, speed);
            float safeMass = float.IsNaN(mass) || float.IsInfinity(mass) ? 0 : Mathf.Max(0, mass);
            EnergyJoules = .5f * safeMass * Speed * Speed;
            IsWater = isWater;
            Severity = Classify(Speed, EnergyJoules, isWater);
        }

        public static ImpactSeverity Classify(float speed, float energyJoules, bool water = false)
        {
            if (!water && speed >= 20 && energyJoules >= 180000) return ImpactSeverity.Catastrophic;
            if (speed >= 10) return ImpactSeverity.Structural;
            if (speed >= 5.5f) return ImpactSeverity.HardHit;
            if (speed >= 2) return ImpactSeverity.Scrape;
            return ImpactSeverity.Contact;
        }
    }
}
