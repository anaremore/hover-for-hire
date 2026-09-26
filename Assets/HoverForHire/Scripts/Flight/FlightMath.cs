using UnityEngine;

namespace HoverForHire
{
    /// <summary>Small pure model functions, shared by simulation and invariant tests.</summary>
    public static class FlightMath
    {
        public const float StandardGravity = 9.81f;
        private const float MaximumCoordinatedBankRadians = 60f * Mathf.Deg2Rad;

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

        /// <summary>Principal moments for the current mass. Internal payload sits near the center of mass,
        /// so moments scale with total mass exactly as uniform-density collider inertia did before.</summary>
        public static Vector3 Inertia(float massKg, FlightTuning tuning)
        {
            Vector3 empty = tuning.InertiaKgM2;
            float scale = Mathf.Max(100f, massKg) / Mathf.Max(100f, tuning.EmptyMassKg);
            return new Vector3(Mathf.Max(1f, empty.x), Mathf.Max(1f, empty.y), Mathf.Max(1f, empty.z)) * scale;
        }

        /// <summary>Collective that balances weight in level, still air, out of ground effect.</summary>
        public static float HoverCollective(float massKg, float gravity, FlightTuning tuning)
            => Mathf.Clamp01(Mathf.Max(0f, massKg) * Mathf.Abs(gravity) / Mathf.Max(1f, tuning.MaximumLiftNewtons));

        /// <summary>
        /// Rotor inflow opposes vertical motion through the air (heave damping), in newtons along world up.
        /// Collective therefore settles toward a climb or descent rate instead of accelerating indefinitely.
        /// </summary>
        public static float HeaveDamping(float verticalAirspeed, float rotorSpeed01, FlightTuning tuning)
            => -verticalAirspeed * Mathf.Max(0f, tuning.HeaveDampingNsPerM) * Mathf.Clamp01(rotorSpeed01);

        /// <summary>
        /// Passive rotor damping torque in body axes (N·m): main-rotor flapping resists pitch and roll,
        /// the tail rotor resists yaw. Present in every assist mode; it is aerodynamics, not stabilization.
        /// </summary>
        public static Vector3 RotorRateDamping(Vector3 angularVelocityLocal, Vector3 inertiaKgM2,
            Vector3 dampingPerSecond, float rotorAuthority)
        {
            Vector3 coefficient = Vector3.Scale(new Vector3(Mathf.Max(0f, dampingPerSecond.x),
                Mathf.Max(0f, dampingPerSecond.y), Mathf.Max(0f, dampingPerSecond.z)), inertiaKgM2);
            return -Vector3.Scale(angularVelocityLocal, coefficient) * Mathf.Max(0f, rotorAuthority);
        }

        /// <summary>
        /// Vertical-fin yaw moment (N·m). Positive yaws the nose right, toward air arriving from the right,
        /// so the aircraft weathervanes into its relative wind in forward flight and in a crosswind hover.
        /// </summary>
        public static float WeathervaneYawTorque(Vector3 airVelocityLocal, float coefficient)
            => Mathf.Max(0f, coefficient) * airVelocityLocal.x * airVelocityLocal.magnitude;

        /// <summary>Right-bank-positive bank angle from world up expressed in the aircraft frame.</summary>
        public static float BankRadians(Vector3 worldUpLocal) => -Mathf.Atan2(worldUpLocal.x, worldUpLocal.y);

        /// <summary>Yaw rate (rad/s) of a coordinated turn at this bank and forward airspeed, blended in with speed.</summary>
        public static float CoordinatedTurnRate(float bankRadians, float forwardAirspeed, FlightTuning tuning)
        {
            float speed = Mathf.Max(0f, forwardAirspeed);
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tuning.CoordinationStartSpeed,
                Mathf.Max(tuning.CoordinationStartSpeed + 0.01f, tuning.CoordinationFullSpeed), speed));
            if (blend <= 0f) return 0f;
            float bank = Mathf.Clamp(bankRadians, -MaximumCoordinatedBankRadians, MaximumCoordinatedBankRadians);
            return StandardGravity * Mathf.Tan(bank) / Mathf.Max(1f, speed) * blend;
        }

        /// <summary>Passive rotor and airframe damping about one body axis, N·m per rad/s (0 pitch, 1 yaw, 2 roll).</summary>
        public static float PassiveRateDamping(int axis, Vector3 inertiaKgM2, FlightTuning tuning)
            => Mathf.Max(0f, tuning.RotorRateDampingPerSecond[axis]) * inertiaKgM2[axis] + Mathf.Max(0f, tuning.AngularDrag[axis]);
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

        /// <summary>Still-air step using the tuning's empty-mass inertia.</summary>
        public PilotCommand Step(PilotCommand raw, Vector3 angularVelocityLocal, Vector3 worldUpLocal,
            float rotorReactionNm, FlightTuning tuning, AssistSettings settings, float dt)
            => Step(raw, angularVelocityLocal, worldUpLocal, rotorReactionNm, Vector3.zero,
                FlightMath.Inertia(tuning.EmptyMassKg, tuning), tuning, settings, dt);

        public PilotCommand Step(PilotCommand raw, Vector3 angularVelocityLocal, Vector3 worldUpLocal,
            float rotorReactionNm, Vector3 airVelocityLocal, Vector3 inertiaKgM2, FlightTuning tuning,
            AssistSettings settings, float dt)
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
            Vector2 targetRate = requested * tuning.MaximumCyclicRateDegrees * Mathf.Deg2Rad;
            // Feed-forward sustains the requested rate against passive rotor damping; feedback removes the error.
            Vector2 feedForward = new Vector2(
                targetRate.x * FlightMath.PassiveRateDamping(2, inertiaKgM2, tuning) / tuning.RollTorqueNm,
                targetRate.y * FlightMath.PassiveRateDamping(0, inertiaKgM2, tuning) / tuning.PitchTorqueNm);
            Vector2 rateDemand = feedForward + (targetRate - actualRate) * tuning.RateFeedbackGain;
            Vector2 cyclic = Vector2.Lerp(requested, rateDemand, weights.x);

            // Yaw stabilization tracks pedal rate plus, at speed, the coordinated-turn rate for the current bank.
            float yawTarget = raw.Yaw * tuning.MaximumYawRateDegrees * Mathf.Deg2Rad
                + FlightMath.CoordinatedTurnRate(FlightMath.BankRadians(worldUpLocal), airVelocityLocal.z, tuning);
            float yawRateDemand = yawTarget * FlightMath.PassiveRateDamping(1, inertiaKgM2, tuning) / tuning.YawTorqueNm
                + (yawTarget - angularVelocityLocal.y) * tuning.YawFeedbackGain;
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
