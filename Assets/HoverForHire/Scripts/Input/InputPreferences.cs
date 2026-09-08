using System;
using UnityEngine;

namespace HoverForHire
{
    public enum MouseCyclicMode { Relative, VirtualJoystick }
    public enum FreeLookCyclicMode { Hold, ReturnToCenter }

    [Serializable]
    public sealed class InputPreferences
    {
        public MouseCyclicMode MouseMode = MouseCyclicMode.Relative;
        public FreeLookCyclicMode FreeLookBehavior = FreeLookCyclicMode.Hold;
        [Range(0.0005f, 0.02f)] public float MouseSensitivity = 0.0035f;
        [Range(0f, 8f)] public float MouseReturnRate = 1.6f;
        [Range(0f, 0.4f)] public float Deadzone = 0.04f;
        [Range(0.5f, 3f)] public float ResponseCurve = 1.35f;
        public bool InvertPitch;
        public bool InvertRoll;
        [Range(2f, 30f)] public float KeyboardResponse = 12f;
        [Range(0.05f, 1f)] public float CollectiveRate = 0.24f;
        public bool UseAbsoluteCollective;
        public bool AbsoluteAxisSigned = true;
        public bool InvertAbsoluteCollective;
        [Range(0.02f, 0.5f)] public float LookSensitivity = 0.13f;
        [Range(30f, 240f)] public float GamepadLookSpeed = 115f;
        public bool InvertLook;
        public bool AutoRecenterView = true;
        [Range(0f, 5f)] public float RecenterDelay = 1.1f;
        [Range(1f, 12f)] public float RecenterSpeed = 3.8f;
        [Range(5f, 25f)] public float CameraDistance = 12f;
        [Range(1f, 10f)] public float CameraHeight = 4f;
        [Range(45f, 100f)] public float CameraFov = 68f;
        [Range(0.02f, 0.8f)] public float CameraSmoothing = 0.12f;
        public bool ShowCyclicIndicator = true;

        public void Sanitize()
        {
            MouseSensitivity = Valid(MouseSensitivity, 0.0005f, 0.02f, 0.0035f);
            MouseReturnRate = Valid(MouseReturnRate, 0f, 8f, 1.6f);
            Deadzone = Valid(Deadzone, 0f, 0.4f, 0.04f);
            ResponseCurve = Valid(ResponseCurve, 0.5f, 3f, 1.35f);
            KeyboardResponse = Valid(KeyboardResponse, 2f, 30f, 12f);
            CollectiveRate = Valid(CollectiveRate, 0.05f, 1f, 0.24f);
            LookSensitivity = Valid(LookSensitivity, 0.02f, 0.5f, 0.13f);
            GamepadLookSpeed = Valid(GamepadLookSpeed, 30f, 240f, 115f);
            RecenterDelay = Valid(RecenterDelay, 0f, 5f, 1.1f);
            RecenterSpeed = Valid(RecenterSpeed, 1f, 12f, 3.8f);
            CameraDistance = Valid(CameraDistance, 5f, 25f, 12f);
            CameraHeight = Valid(CameraHeight, 1f, 10f, 4f);
            CameraFov = Valid(CameraFov, 45f, 100f, 68f);
            CameraSmoothing = Valid(CameraSmoothing, 0.02f, 0.8f, 0.12f);
            if (!Enum.IsDefined(typeof(MouseCyclicMode), MouseMode)) MouseMode = MouseCyclicMode.Relative;
            if (!Enum.IsDefined(typeof(FreeLookCyclicMode), FreeLookBehavior)) FreeLookBehavior = FreeLookCyclicMode.Hold;
        }

        private static float Valid(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }
}
