using System;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>Device-independent pilot demand. Collective persists in the input provider.</summary>
    [Serializable]
    public struct PilotCommand
    {
        public Vector2 Cyclic; // X: roll right; Y: pitch forward. Unit disk.
        public float Yaw; // Positive turns the nose right.
        public float Collective; // Rotor lift demand, never an altitude command.

        public PilotCommand(Vector2 cyclic, float yaw, float collective)
        {
            Cyclic = cyclic;
            Yaw = yaw;
            Collective = collective;
        }

        public static PilotCommand Neutral => new PilotCommand(Vector2.zero, 0f, 0f);

        public PilotCommand Clamped()
        {
            return new PilotCommand(
                Vector2.ClampMagnitude(new Vector2(Finite(Cyclic.x), Finite(Cyclic.y)), 1f),
                Mathf.Clamp(Finite(Yaw), -1f, 1f), Mathf.Clamp01(Finite(Collective)));
        }

        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }

    /// <summary>Implement this for keyboard, gamepad, replay, or future flight hardware.</summary>
    public interface IFlightInput
    {
        PilotCommand Command { get; }
    }
}
