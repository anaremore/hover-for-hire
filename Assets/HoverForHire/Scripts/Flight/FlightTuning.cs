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

        [Header("Aerodynamic drag in body axes: right, up, forward")]
        public Vector3 LinearDrag = new Vector3(34f, 72f, 12f);
        public Vector3 QuadraticDrag = new Vector3(5.5f, 9f, 1.7f);
        [Tooltip("Passive airframe drag remains in Unassisted; this is not a rate controller.")]
        public Vector3 AngularDrag = new Vector3(180f, 230f, 190f);

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
