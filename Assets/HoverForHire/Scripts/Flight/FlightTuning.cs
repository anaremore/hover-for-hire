using UnityEngine;

namespace HoverForHire
{
    [CreateAssetMenu(fileName = "UtilityHelicopter", menuName = "Hover for Hire/Flight tuning")]
    public sealed class FlightTuning : ScriptableObject
    {
        [Header("Mass and rotor")]
        [Min(100f)] public float EmptyMassKg = 1050f;
        [Min(0f)] public float MaximumPayloadKg = 450f;
        public Vector3 CenterOfMass = new Vector3(0f, -0.3f, 0f);
        [Tooltip("Principal moments at empty mass, body axes: pitch (X), yaw (Y), roll (Z). Explicit so collision shapes " +
            "never change handling. Scaled by current mass / empty mass, matching internal payload near the center of mass.")]
        public Vector3 InertiaKgM2 = new Vector3(1174f, 1233f, 690f);
        [Min(100f)] public float MaximumLiftNewtons = 23000f;
        [Min(0.01f)] public float CollectiveResponseSeconds = 0.35f;
        [Range(0f, 10f)] public float RotorDiskTiltDegrees = 3f;
        [Min(0f)] public float RotorTorqueArmMeters = 0.045f;
        [Min(0f)] public float GovernedRotorRpm = 395f;

        [Header("Control authority")]
        [Min(1f)] public float PitchTorqueNm = 2900f;
        [Min(1f)] public float RollTorqueNm = 2400f;
        [Min(1f)] public float YawTorqueNm = 3500f;
        [Min(1f)] public float MaximumCyclicRateDegrees = 34f;
        [Min(1f)] public float MaximumYawRateDegrees = 48f;
        [Min(0f)] public float RateFeedbackGain = 2.2f;
        [Min(0f)] public float YawFeedbackGain = 2.3f;
        [Min(0.01f)] public float ControlResponseSeconds = 0.12f;
        [Min(0.01f)] public float AssistBlendSeconds = 0.55f;
        [Min(1f)] public float LevelAngleForFullCommand = 24f;
        [Range(0.01f, 1f)] public float LevelCyclicThreshold = 0.22f;
        [Range(0f, 1f)] public float LevelAuthority = 0.6f;

        [Header("Turn coordination assist (part of yaw stabilization)")]
        [Tooltip("Forward airspeed where yaw stabilization starts adding the coordinated-turn rate.")]
        [Min(0f)] public float CoordinationStartSpeed = 8f;
        [Tooltip("Forward airspeed where the coordinated-turn rate is fully applied.")]
        [Min(0f)] public float CoordinationFullSpeed = 18f;

        [Header("Aerodynamic drag in body axes: right, up, forward")]
        public Vector3 LinearDrag = new Vector3(34f, 72f, 12f);
        public Vector3 QuadraticDrag = new Vector3(5.5f, 9f, 1.7f);
        [Tooltip("Passive airframe drag remains in Unassisted; this is not a rate controller.")]
        public Vector3 AngularDrag = new Vector3(180f, 230f, 190f);

        [Header("Rotor aerodynamics (0 disables each term)")]
        [Tooltip("Rotor inflow damping of vertical motion through the air, N per m/s. Makes collective settle " +
            "toward a climb or descent rate instead of accelerating indefinitely.")]
        [Min(0f)] public float HeaveDampingNsPerM = 400f;
        [Tooltip("Rotor rate damping per second, body axes: main-rotor flapping for pitch (X) and roll (Z), " +
            "tail rotor for yaw (Y). Multiplied by the axis inertia; present in every assist mode.")]
        public Vector3 RotorRateDampingPerSecond = new Vector3(2.5f, 1f, 2.5f);
        [Tooltip("Vertical-fin yaw moment per (lateral airspeed × airspeed), N·m/(m/s)². Turns the nose into the relative wind.")]
        [Min(0f)] public float WeathervaneCoefficient = 3f;

        [Header("Ground and damage")]
        [Min(0f)] public float SkidClearanceMeters = 1.5f;
        [Min(1f)] public float AltitudeRayLengthMeters = 3000f;
        [Range(0.1f, 1f)] public float GroundNormalThreshold = 0.65f;
        [Min(0f)] public float CrashVerticalSpeed = 5.5f;
        [Min(0f)] public float CrashImpactSpeed = 8f;
        [Range(30f, 90f)] public float MaximumLandingTiltDegrees = 65f;

        public static FlightTuning CreateRuntimeDefaults()
        {
            FlightTuning value = CreateInstance<FlightTuning>();
            value.name = "Utility helicopter runtime tuning";
            value.hideFlags = HideFlags.DontSave;
            return value;
        }
    }
}
