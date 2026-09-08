using UnityEngine;

namespace HoverForHire
{
    /// <summary>Small pure model functions, shared by simulation and invariant tests.</summary>
    public static class FlightMath
    {
        public static float ResponseFraction(float deltaTime, float timeConstant)
            => 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / Mathf.Max(0.001f, timeConstant));

        public static Vector3 AerodynamicDrag(Vector3 localVelocity, Vector3 linear, Vector3 quadratic)
        {
            return new Vector3(DragAxis(localVelocity.x, linear.x, quadratic.x),
                DragAxis(localVelocity.y, linear.y, quadratic.y),
                DragAxis(localVelocity.z, linear.z, quadratic.z));
        }

        private static float DragAxis(float velocity, float linear, float quadratic)
            => -velocity * (Mathf.Max(0f, linear) + Mathf.Abs(velocity) * Mathf.Max(0f, quadratic));

        public static Vector3 LocalThrustDirection(Vector2 cyclic, float tiltDegrees)
        {
            Vector2 bounded = Vector2.ClampMagnitude(cyclic, 1f);
            float tilt = Mathf.Tan(Mathf.Clamp(tiltDegrees, 0f, 15f) * Mathf.Deg2Rad);
            return new Vector3(bounded.x * tilt, 1f, bounded.y * tilt).normalized;
        }

        public static float Lift(float collective, FlightTuning tuning)
            => Mathf.Clamp01(collective) * Mathf.Max(0f, tuning.MaximumLiftNewtons);

        public static float Mass(float payloadKg, FlightTuning tuning)
            => Mathf.Max(100f, tuning.EmptyMassKg) + Mathf.Clamp(payloadKg, 0f, Mathf.Max(0f, tuning.MaximumPayloadKg));
    }

    /// <summary>
    /// All assists alter the same bounded actuator demands. They do not apply hidden forces,
    /// overwrite velocity, hold position, or interpret collective as vertical speed.
    /// </summary>
    public sealed class AssistSolver
    {
        private Vector4 weights;
        private PilotCommand output;

        public void Reset(AssistSettings settings, PilotCommand initial)
        {
            weights = settings.Weights;
            output = initial.Clamped();
        }

        public PilotCommand Step(PilotCommand raw, Vector3 angularVelocityLocal, Vector3 worldUpLocal,
            float rotorReactionNm, FlightTuning tuning, AssistSettings settings, float dt)
        {
            raw = raw.Clamped();
            weights = Vector4.Lerp(weights, settings.Weights, FlightMath.ResponseFraction(dt, tuning.AssistBlendSeconds));

            // Unity's positive local Z rotation banks left; pilot X is right bank.
            Vector2 actualRate = new Vector2(-angularVelocityLocal.z, angularVelocityLocal.x);
            float centered = 1f - Mathf.SmoothStep(0f, 1f,
                raw.Cyclic.magnitude / Mathf.Max(0.01f, tuning.LevelCyclicThreshold));
            Vector2 level = new Vector2(Mathf.Atan2(worldUpLocal.x, worldUpLocal.y),
                Mathf.Atan2(worldUpLocal.z, worldUpLocal.y)) * Mathf.Rad2Deg;
            level = Vector2.ClampMagnitude(level / Mathf.Max(1f, tuning.LevelAngleForFullCommand), 1f)
                * tuning.LevelAuthority * centered * weights.y;

            Vector2 requested = Vector2.ClampMagnitude(raw.Cyclic + level, 1f);
            Vector2 rateDemand = (requested * tuning.MaximumCyclicRateDegrees * Mathf.Deg2Rad - actualRate)
                * tuning.RateFeedbackGain;
            Vector2 cyclic = Vector2.Lerp(requested, rateDemand, weights.x);
            float yawRateDemand = (raw.Yaw * tuning.MaximumYawRateDegrees * Mathf.Deg2Rad - angularVelocityLocal.y)
                * tuning.YawFeedbackGain;
            float yaw = Mathf.Lerp(raw.Yaw, yawRateDemand, weights.z);
            yaw -= rotorReactionNm / Mathf.Max(1f, tuning.YawTorqueNm) * weights.w;

            PilotCommand target = new PilotCommand(cyclic, yaw, raw.Collective).Clamped();
            float response = FlightMath.ResponseFraction(dt, tuning.ControlResponseSeconds);
            output.Cyclic = Vector2.Lerp(output.Cyclic, target.Cyclic, response);
            output.Yaw = Mathf.Lerp(output.Yaw, target.Yaw, response);
            output.Collective = Mathf.Lerp(output.Collective, target.Collective,
                FlightMath.ResponseFraction(dt, tuning.CollectiveResponseSeconds));
            return output.Clamped();
        }
    }
}
