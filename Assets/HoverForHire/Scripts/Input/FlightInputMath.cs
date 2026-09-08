using UnityEngine;

namespace HoverForHire
{
    /// <summary>Pure input processing. This never applies aircraft stabilization or other forces.</summary>
    public static class FlightInputMath
    {
        public static Vector2 ProcessMouse(Vector2 current, Vector2 delta, InputPreferences settings,
            bool freeLook, bool wasFreeLooking, bool discardDelta, float dt, out Vector2 cameraDelta)
        {
            if (discardDelta || (wasFreeLooking && !freeLook)) delta = Vector2.zero;
            cameraDelta = freeLook ? delta * settings.LookSensitivity : Vector2.zero;
            if (freeLook)
                return settings.FreeLookBehavior == FreeLookCyclicMode.Hold ? current :
                    IntegrateMouse(current, Vector2.zero, 0f, Mathf.Max(0.1f, settings.MouseReturnRate), dt);
            if (settings.InvertPitch) delta.y = -delta.y;
            if (settings.InvertRoll) delta.x = -delta.x;
            return IntegrateMouse(current, delta, settings.MouseSensitivity,
                settings.MouseMode == MouseCyclicMode.Relative ? settings.MouseReturnRate : 0f, dt);
        }

        /// <summary>
        /// Exact integration of dc/dt = mouse velocity * sensitivity - returnRate * c.
        /// Mouse delta already contains movement over the frame: never multiply it by deltaTime.
        /// The decay integral assumes constant mouse velocity within each input sample.
        /// </summary>
        public static Vector2 IntegrateMouse(Vector2 current, Vector2 delta, float sensitivity,
            float returnRate, float deltaTime)
        {
            float t = Mathf.Max(0f, returnRate) * Mathf.Max(0f, deltaTime);
            float decay = Mathf.Exp(-t);
            float integral = t > 0.0001f ? (1f - decay) / t : 1f - t * 0.5f;
            return Vector2.ClampMagnitude(current * decay + delta * (sensitivity * integral), 1f);
        }

        public static Vector2 Shape(Vector2 value, float deadzone, float curve)
        {
            float magnitude = value.magnitude;
            float zone = Mathf.Clamp(deadzone, 0f, 0.95f);
            if (magnitude <= zone) return Vector2.zero;
            float response = Mathf.Pow(Mathf.Clamp01((magnitude - zone) / (1f - zone)), Mathf.Max(0.1f, curve));
            return value / magnitude * response;
        }

        public static Vector2 SmoothKeyboard(Vector2 current, Vector2 target, float response, float deltaTime) =>
            Vector2.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0f, response) * Mathf.Max(0f, deltaTime)));

        /// <summary>Add devices continuously; no active-device latch or discontinuous takeover threshold.</summary>
        public static Vector2 Combine(Vector2 mouse, Vector2 keyboard, Vector2 gamepad) =>
            Vector2.ClampMagnitude(mouse + keyboard + gamepad, 1f);

        public static float IntegrateCollective(float current, float increase, float decrease, float rate, float dt) =>
            Mathf.Clamp01(current + (Mathf.Clamp01(increase) - Mathf.Clamp01(decrease)) * Mathf.Max(0f, rate) * Mathf.Max(0f, dt));

        public static float AbsoluteCollective(float axis, bool signed, bool inverted)
        {
            float result = Mathf.Clamp01(signed ? (axis + 1f) * 0.5f : axis);
            return inverted ? 1f - result : result;
        }
    }
}
