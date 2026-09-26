using System;
using UnityEngine;

namespace HoverForHire
{
    public enum RealismPreset { Relaxed, Realistic, Expert }
    public enum WindStrength { Calm, Light, Moderate, Strong }
    public enum FailureMode { Off, DrillsOnly, Random }

    /// <summary>
    /// Which physical challenges are active. Assists decide how much help the pilot gets; realism decides how much
    /// the helicopter and the weather push back. Both are recorded with every result.
    /// </summary>
    [Serializable]
    public sealed class RealismSettings
    {
        [Tooltip("Air cushion within about one rotor diameter of a surface.")]
        public bool GroundEffect = true;
        [Tooltip("Extra rotor efficiency as the aircraft gains airspeed (effective translational lift).")]
        public bool TranslationalLift = true;
        [Tooltip("Nose rises with forward airspeed (flapback) and the rotor rolls slightly in transition.")]
        public bool SpeedStability;
        [Tooltip("Settling with power: steep powered descents at low airspeed lose rotor thrust.")]
        public bool VortexRingState;
        [Tooltip("Finite engine power: overpulling collective droops rotor RPM; engine failures need autorotation.")]
        public bool PowerLimits;
        [Tooltip("A tail rotor strike removes anti-torque instead of ending the flight.")]
        public bool TailRotorFailures;
        public WindStrength Wind = WindStrength.Calm;
        [Range(0f, 1f)] public float Gustiness;
        public FailureMode EngineFailures = FailureMode.Off;
        [Tooltip("Scales the vortex-ring onset descent rate; below 1 it starts earlier.")]
        [Range(0.5f, 1.5f)] public float VortexOnsetScale = 1f;
        [Tooltip("Largest tilt while resting on the ground before the aircraft rolls over.")]
        [Range(20f, 90f)] public float RolloverLimitDegrees = 65f;

        public void SetPreset(RealismPreset preset)
        {
            GroundEffect = TranslationalLift = true;
            bool challenging = preset != RealismPreset.Relaxed;
            SpeedStability = VortexRingState = PowerLimits = TailRotorFailures = challenging;
            Wind = preset == RealismPreset.Relaxed ? WindStrength.Calm : preset == RealismPreset.Realistic ? WindStrength.Light : WindStrength.Moderate;
            Gustiness = preset == RealismPreset.Relaxed ? 0f : preset == RealismPreset.Realistic ? 0.3f : 0.6f;
            EngineFailures = preset == RealismPreset.Relaxed ? FailureMode.Off : preset == RealismPreset.Realistic ? FailureMode.DrillsOnly : FailureMode.Random;
            VortexOnsetScale = preset == RealismPreset.Expert ? 0.8f : 1f;
            RolloverLimitDegrees = preset == RealismPreset.Relaxed ? 65f : preset == RealismPreset.Realistic ? 40f : 35f;
        }

        /// <summary>The preset these settings match exactly, or null for a custom combination.</summary>
        public RealismPreset? MatchingPreset
        {
            get
            {
                foreach (RealismPreset preset in new[] { RealismPreset.Relaxed, RealismPreset.Realistic, RealismPreset.Expert })
                {
                    var reference = new RealismSettings();
                    reference.SetPreset(preset);
                    if (reference.SameAs(this)) return preset;
                }
                return null;
            }
        }

        public bool SameAs(RealismSettings other) => other != null && GroundEffect == other.GroundEffect
            && TranslationalLift == other.TranslationalLift && SpeedStability == other.SpeedStability
            && VortexRingState == other.VortexRingState && PowerLimits == other.PowerLimits
            && TailRotorFailures == other.TailRotorFailures && Wind == other.Wind
            && Mathf.Approximately(Gustiness, other.Gustiness) && EngineFailures == other.EngineFailures
            && Mathf.Approximately(VortexOnsetScale, other.VortexOnsetScale)
            && Mathf.Approximately(RolloverLimitDegrees, other.RolloverLimitDegrees);

        public RealismSettings Clone() => (RealismSettings)MemberwiseClone();

        /// <summary>Short label for HUD and result records, e.g. "REALISTIC" or "CUSTOM: VRS POWER WIND 2".</summary>
        public string Summary
        {
            get
            {
                RealismPreset? preset = MatchingPreset;
                if (preset.HasValue) return preset.Value.ToString().ToUpperInvariant();
                string text = "CUSTOM:";
                if (GroundEffect) text += " GE";
                if (TranslationalLift) text += " ETL";
                if (SpeedStability) text += " STAB";
                if (VortexRingState) text += " VRS";
                if (PowerLimits) text += " POWER";
                if (TailRotorFailures) text += " TR";
                if (Wind != WindStrength.Calm) text += " WIND " + (int)Wind;
                if (EngineFailures == FailureMode.Random) text += " FAIL";
                return text;
            }
        }

        public void Sanitize()
        {
            if (!Enum.IsDefined(typeof(WindStrength), Wind)) Wind = WindStrength.Calm;
            if (!Enum.IsDefined(typeof(FailureMode), EngineFailures)) EngineFailures = FailureMode.Off;
            Gustiness = float.IsNaN(Gustiness) ? 0f : Mathf.Clamp01(Gustiness);
            VortexOnsetScale = float.IsNaN(VortexOnsetScale) ? 1f : Mathf.Clamp(VortexOnsetScale, 0.5f, 1.5f);
            RolloverLimitDegrees = float.IsNaN(RolloverLimitDegrees) ? 65f : Mathf.Clamp(RolloverLimitDegrees, 20f, 90f);
        }

        /// <summary>Everything off: exactly the phase-1/2 model (used to guard against regressions).</summary>
        public static RealismSettings None() => new RealismSettings { GroundEffect = false, TranslationalLift = false };
    }
}
