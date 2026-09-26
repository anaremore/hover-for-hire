using System;

namespace HoverForHire
{
    /// <summary>Turns a recorded crash cause and its measurements into short, specific pilot feedback.</summary>
    public static class CrashReport
    {
        public static string Title(CrashCause cause)
        {
            switch (cause)
            {
                case CrashCause.HardLanding: return "HARD LANDING";
                case CrashCause.TipOver: return "ROLLOVER";
                case CrashCause.ObstacleImpact: return "COLLISION";
                case CrashCause.RotorStrike: return "MAIN ROTOR STRIKE";
                case CrashCause.TailRotorStrike: return "TAIL ROTOR STRIKE";
                case CrashCause.Ditching: return "DITCHED";
                case CrashCause.OutOfBounds: return "AIRCRAFT LOST";
                default: return "FLIGHT ENDED";
            }
        }

        public static string Detail(CrashCause cause, float value, float limit, string obstacle)
        {
            switch (cause)
            {
                case CrashCause.HardLanding: return $"Touchdown at {value:0.0} m/s. The limit is {limit:0.0} m/s.";
                case CrashCause.TipOver: return $"Tilted {value:0}° while on the ground. The limit is {limit:0}°.";
                case CrashCause.ObstacleImpact: return $"Hit {Describe(obstacle)} at {value:0.0} m/s. The limit is {limit:0.0} m/s.";
                case CrashCause.RotorStrike: return $"The main rotor struck {Describe(obstacle)}.";
                case CrashCause.TailRotorStrike: return $"The tail rotor struck {Describe(obstacle)}.";
                case CrashCause.Ditching: return "The skids went into the sea.";
                case CrashCause.OutOfBounds: return "The aircraft left the island area.";
                default: return "The flight ended.";
            }
        }

        public static string Advice(CrashCause cause)
        {
            switch (cause)
            {
                case CrashCause.HardLanding: return "Arrive in a steady hover, then lower collective until the descent is under 1 m/s.";
                case CrashCause.TipOver: return "Touch down level with no sideways drift; drift and slopes roll the aircraft over.";
                case CrashCause.ObstacleImpact: return "Slow down near structures and keep clear of the ground.";
                case CrashCause.RotorStrike: return "Keep the rotor disc clear: the blades reach 4.6 m from the mast.";
                case CrashCause.TailRotorStrike: return "The tail sits low behind you: avoid nose-high flares close to the ground.";
                case CrashCause.Ditching: return "Watch skid height over water; the sea is below the shoreline.";
                default: return "Reset at the pad and try a slower approach.";
            }
        }

        /// <summary>Readable name for a struck collider: merged scenery collision is named after its layer.</summary>
        public static string Describe(string obstacle)
        {
            if (string.IsNullOrWhiteSpace(obstacle)) return "an obstacle";
            if (obstacle.IndexOf("layer " + WorldConstants.VegetationLayer, StringComparison.Ordinal) >= 0) return "a tree";
            if (obstacle.IndexOf("Collision / layer", StringComparison.Ordinal) >= 0) return "a structure";
            if (obstacle.IndexOf("terrain", StringComparison.OrdinalIgnoreCase) >= 0) return "the ground";
            if (obstacle.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0) return "the water";
            if (obstacle.IndexOf("building", StringComparison.OrdinalIgnoreCase) >= 0 || obstacle.IndexOf("hangar", StringComparison.OrdinalIgnoreCase) >= 0
                || obstacle.IndexOf("tower", StringComparison.OrdinalIgnoreCase) >= 0) return "a building";
            if (obstacle.IndexOf("Windsock", StringComparison.OrdinalIgnoreCase) >= 0) return "the windsock";
            if (obstacle.IndexOf(" / ", StringComparison.Ordinal) > 0 && obstacle.Length > 3 && char.IsDigit(obstacle[0])) return "the landing pad";
            return "an obstacle";
        }
    }
}
