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
        /// <summary>Extent of the island terrain in metres, centred on the world origin.</summary>
        public const float WorldSizeX = 2400f, WorldSizeZ = 2200f;
        /// <summary>The coast-height texture stores (height - CoastHeightMin) / CoastHeightRange in 0..1.</summary>
        public const float CoastHeightMin = -40f, CoastHeightRange = 240f;
        /// <summary>Global shader property carrying <see cref="SeaLevel"/> to the ocean shader.</summary>
        public static readonly int SeaLevelShaderId = Shader.PropertyToID("_HFH_SeaLevel");
        /// <summary>Global shader property (x, z) carrying the world size.</summary>
        public static readonly int WorldSizeShaderId = Shader.PropertyToID("_HFH_WorldSize");
        /// <summary>Global shader property (range, minimum) decoding the coast-height texture.</summary>
        public static readonly int CoastEncodingShaderId = Shader.PropertyToID("_HFH_CoastEncoding");

        /// <summary>Push the world facts the shaders share, so the numbers live only here.</summary>
        public static void PublishShaderGlobals()
        {
            Shader.SetGlobalFloat(SeaLevelShaderId, SeaLevel);
            Shader.SetGlobalVector(WorldSizeShaderId, new Vector4(WorldSizeX, WorldSizeZ, 0f, 0f));
            Shader.SetGlobalVector(CoastEncodingShaderId, new Vector4(CoastHeightRange, CoastHeightMin, 0f, 0f));
        }

        /// <summary>Everything the rotors can strike: all layers except the aircraft and Unity's Ignore Raycast.</summary>
        public static int RotorClearanceMask => ~((1 << AircraftLayer) | (1 << 2));
    }
}
