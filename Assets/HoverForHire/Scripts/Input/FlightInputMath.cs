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

        /// <summary>
        /// Travel of a held digital control over one frame, from heldBefore to heldBefore + dt seconds.
        /// The rate rises smoothly from fineRate to coarseRate over rampSeconds; the exact integral keeps
        /// the result independent of frame rate, so a short tap trims finely and a long hold still moves quickly.
        /// </summary>
        public static float RampedTravel(float heldBefore, float dt, float fineRate, float coarseRate, float rampSeconds)
        {
            float start = Mathf.Max(0f, heldBefore);
            return Mathf.Max(0f, RampedArea(start + Mathf.Max(0f, dt), fineRate, coarseRate, rampSeconds)
                - RampedArea(start, fineRate, coarseRate, rampSeconds));
        }

        /// <summary>Integral of the ramped rate from 0 to t seconds held.</summary>
        public static float RampedArea(float t, float fineRate, float coarseRate, float rampSeconds)
        {
            float fine = Mathf.Max(0f, fineRate), coarse = Mathf.Max(fine, coarseRate), held = Mathf.Max(0f, t);
            if (rampSeconds <= 0.0001f) return coarse * held;
            float u = Mathf.Clamp01(held / rampSeconds);
            // Smoothstep 3u² − 2u³ integrates to u³ − u⁴/2 over the ramp.
            float area = fine * Mathf.Min(held, rampSeconds) + (coarse - fine) * rampSeconds * (u * u * u - 0.5f * u * u * u * u);
            if (held > rampSeconds) area += coarse * (held - rampSeconds);
            return area;
        }

        /// <summary>Deflection of a held digital control: fineFraction on a tap, rising smoothly to full.</summary>
        public static float RampedLevel(float heldSeconds, float fineFraction, float rampSeconds)
        {
            float fine = Mathf.Clamp01(fineFraction);
            if (rampSeconds <= 0.0001f) return 1f;
            float u = Mathf.Clamp01(Mathf.Max(0f, heldSeconds) / rampSeconds);
            return u >= 1f ? 1f : fine + (1f - fine) * u * u * (3f - 2f * u);
        }

        public static float AbsoluteCollective(float axis, bool signed, bool inverted)
        {
            float result = Mathf.Clamp01(signed ? (axis + 1f) * 0.5f : axis);
            return inverted ? 1f - result : result;
        }
    }
}
