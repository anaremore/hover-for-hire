namespace HoverForHire
{
    /// <summary>Why a flight ended. Presentation turns the cause and its measured values into pilot feedback.</summary>
    public enum CrashCause
    {
        None,
        /// <summary>Touchdown vertical speed exceeded the landing limit.</summary>
        HardLanding,
        /// <summary>The supported aircraft tilted past the roll-over limit.</summary>
        TipOver,
        /// <summary>The airframe struck something faster than the obstacle limit.</summary>
        ObstacleImpact,
        /// <summary>The main rotor disc contacted terrain, a structure or a tree.</summary>
        RotorStrike,
        /// <summary>The tail rotor contacted terrain, a structure or a tree.</summary>
        TailRotorStrike,
        /// <summary>The skids went below the water surface.</summary>
        Ditching,
        /// <summary>The aircraft left the playable world.</summary>
        OutOfBounds,
        /// <summary>A world hazard reported the crash without a more specific cause.</summary>
        Hazard
    }
}
