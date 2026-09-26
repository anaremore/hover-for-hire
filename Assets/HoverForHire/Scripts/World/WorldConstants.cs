using UnityEngine;

namespace HoverForHire
{
    /// <summary>Shared world facts used by physics, presentation and shaders. Change them here only.</summary>
    public static class WorldConstants
    {
        /// <summary>Height of the ocean surface in metres.</summary>
        public const float SeaLevel = -3.5f;
        /// <summary>Layer of the player's aircraft colliders and art; excluded from its own sensors and the camera.</summary>
        public const int AircraftLayer = 8;
        /// <summary>Layer of merged tree and shrub collision. Rotors strike it; the chase camera passes through it.</summary>
        public const int VegetationLayer = 9;
        /// <summary>Global shader property carrying <see cref="SeaLevel"/> to the ocean shader.</summary>
        public static readonly int SeaLevelShaderId = Shader.PropertyToID("_HFH_SeaLevel");

        /// <summary>Everything the rotors can strike: all layers except the aircraft and Unity's Ignore Raycast.</summary>
        public static int RotorClearanceMask => ~((1 << AircraftLayer) | (1 << 2));
    }
}
