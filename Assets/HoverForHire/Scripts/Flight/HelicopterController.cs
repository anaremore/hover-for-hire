using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoverForHire
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HelicopterController : MonoBehaviour
    {
        public FlightTuning Tuning;
        [Tooltip("Any MonoBehaviour implementing IFlightInput.")]
        public MonoBehaviour InputSource;
        public Rigidbody Body;
        public AssistSettings Assists = new AssistSettings();

        public bool Grounded => supports.Count > 0 && (Body == null ? transform.up : Body.rotation * Vector3.up).y > 0.4f;
        public bool Crashed { get; private set; }
        /// <summary>Vertical clearance beneath the fuselage origin, less upright skid clearance.</summary>
        public float AltitudeAGL { get; private set; }
        public float GroundSpeed => Body == null ? 0f : Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up).magnitude;
        public float Airspeed => Body == null ? 0f : Body.linearVelocity.magnitude; // Calm atmosphere in this slice.
        public float VerticalSpeed => Body == null ? 0f : Body.linearVelocity.y;
        public float Heading => Body == null ? transform.eulerAngles.y : Body.rotation.eulerAngles.y;
        public float LiftNewtons { get; private set; }
        public float PayloadKg { get; private set; }
        public float LastTouchdownSpeed { get; private set; }
        public PilotCommand RawCommand { get; private set; }
        public PilotCommand AssistedCommand { get; private set; }
        public Vector3 LocalAngularRatesDegrees => Body == null ? Vector3.zero :
            Quaternion.Inverse(Body.rotation) * Body.angularVelocity * Mathf.Rad2Deg;
        public float RotorSpeed01 { get; private set; } = 1f;
        public float RotorRpm => RotorSpeed01 * (Tuning == null ? 395f : Tuning.GovernedRotorRpm);

        public event Action CrashedEvent;
        public event Action<float> Touchdown;
        public event Action<AircraftImpact> Impact;
        public event Action ResetPerformed;

        private readonly HashSet<Collider> supports = new HashSet<Collider>();
        private readonly RaycastHit[] altitudeHits = new RaycastHit[32];
        private readonly AssistSolver solver = new AssistSolver();
        private Vector3 prePhysicsVelocity;
        private bool ownsTuning;

        private void Awake()
        {
            if (Tuning == null)
            {
                Tuning = FlightTuning.CreateRuntimeDefaults();
                ownsTuning = true;
            }
            if (Assists == null) Assists = new AssistSettings();
            if (Body == null) Body = GetComponent<Rigidbody>();
            Body.useGravity = true;
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // Only explicit aerodynamic drag is used; assists cannot hide inside Rigidbody damping.
            Body.linearDamping = 0f;
            Body.angularDamping = 0f;
            Body.maxAngularVelocity = 6f;
            Body.solverIterations = 12;
            Body.solverVelocityIterations = 6;
            Body.centerOfMass = Tuning.CenterOfMass;
            SetPayload(PayloadKg);
            solver.Reset(Assists, PilotCommand.Neutral);
            UpdateAltitude();
        }

        public void SetPreset(AssistPreset preset)
        {
            if (Assists == null) Assists = new AssistSettings();
            Assists.SetPreset(preset);
            // Solver fades toward these flags during fixed updates; toggles do not reset the actuators.
        }

        public void SetPayload(float kilograms)
        {
            if (Tuning == null) return;
            PayloadKg = float.IsNaN(kilograms) || float.IsInfinity(kilograms) ? 0f :
                Mathf.Clamp(kilograms, 0f, Mathf.Max(0f, Tuning.MaximumPayloadKg));
            if (Body != null)
            {
                Body.mass = FlightMath.Mass(PayloadKg, Tuning);
                Body.ResetInertiaTensor();
                Body.centerOfMass = Tuning.CenterOfMass;
            }
        }

        private void FixedUpdate()
        {
            if (Tuning == null || Body == null) return;
            supports.RemoveWhere(IsInvalidSupport);
            UpdateAltitude();
            float dt = Time.fixedDeltaTime;
            prePhysicsVelocity = Body.linearVelocity;
            RawCommand = !Crashed && InputSource is IFlightInput input ? input.Command.Clamped() : PilotCommand.Neutral;
            // The interpolated Transform is for rendering. Forces read the current physics pose.
            Quaternion rotation = Body.rotation;
            Quaternion inverseRotation = Quaternion.Inverse(rotation);
            Vector3 localAngular = inverseRotation * Body.angularVelocity;
            float rotorReactionNm = LiftNewtons * Mathf.Max(0f, Tuning.RotorTorqueArmMeters);
            AssistedCommand = solver.Step(RawCommand, localAngular, inverseRotation * Vector3.up,
                rotorReactionNm, Tuning, Assists, dt);
            RotorSpeed01 = Mathf.MoveTowards(RotorSpeed01, Crashed ? 0f : 1f, dt * (Crashed ? 0.35f : 1f));
            LiftNewtons = FlightMath.Lift(AssistedCommand.Collective, Tuning) * RotorSpeed01 * RotorSpeed01;

            Vector3 localThrust = FlightMath.LocalThrustDirection(AssistedCommand.Cyclic, Tuning.RotorDiskTiltDegrees);
            Body.AddForce(rotation * localThrust * LiftNewtons, ForceMode.Force);
            float authority = RotorSpeed01 * RotorSpeed01;
            Vector3 controlTorque = new Vector3(AssistedCommand.Cyclic.y * Tuning.PitchTorqueNm,
                AssistedCommand.Yaw * Tuning.YawTorqueNm, -AssistedCommand.Cyclic.x * Tuning.RollTorqueNm) * authority;
            controlTorque.y += LiftNewtons * Mathf.Max(0f, Tuning.RotorTorqueArmMeters);
            Body.AddRelativeTorque(controlTorque, ForceMode.Force);

            Vector3 localVelocity = inverseRotation * Body.linearVelocity;
            Body.AddRelativeForce(FlightMath.AerodynamicDrag(localVelocity, Tuning.LinearDrag, Tuning.QuadraticDrag), ForceMode.Force);
            Body.AddRelativeTorque(FlightMath.AerodynamicDrag(localAngular, Tuning.AngularDrag, Vector3.zero), ForceMode.Force);
        }

        private static bool IsInvalidSupport(Collider support) => support == null || !support.enabled || !support.gameObject.activeInHierarchy;

        private void UpdateAltitude()
        {
            // Ignore the aircraft's colliders and triggers; a roof is valid ground beneath the aircraft.
            const float rayOriginOffset = 0.2f;
            Vector3 origin = (Body == null ? transform.position : Body.position) + Vector3.up * rayOriginOffset;
            float length = Mathf.Max(1f, Tuning.AltitudeRayLengthMeters);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, altitudeHits, length, ~0, QueryTriggerInteraction.Ignore);
            float closest = length;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = altitudeHits[i];
                if (hit.rigidbody == Body || hit.collider.transform.IsChildOf(transform)) continue;
                closest = Mathf.Min(closest, hit.distance);
            }
            AltitudeAGL = Mathf.Max(0f, closest - rayOriginOffset - Tuning.SkidClearanceMeters);
        }

        private void OnCollisionEnter(Collision collision) => ProcessContact(collision, true);
        private void OnCollisionStay(Collision collision) => ProcessContact(collision, false);
        private void OnCollisionExit(Collision collision) => supports.Remove(collision.collider);

        private void ProcessContact(Collision collision, bool entering)
        {
            if (Tuning == null || Body == null) return;
            bool supported = false;
            float normalImpact = 0f;
            Vector3 impactPoint = Body.position, impactNormal = Vector3.up;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                supported |= Vector3.Dot(contact.normal, Vector3.up) >= Tuning.GroundNormalThreshold;
                float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
                if (speed >= normalImpact) { normalImpact = speed; impactPoint = contact.point; impactNormal = contact.normal; }
            }

            bool previouslySupported = supports.Count > 0;
            if (supported) supports.Add(collision.collider);
            else supports.Remove(collision.collider);

            bool newTouchdown = supported && !previouslySupported;
            if (newTouchdown)
            {
                // Rigidbody velocity after resolution understates impact; retain the incoming velocity.
                LastTouchdownSpeed = Mathf.Max(0f, -prePhysicsVelocity.y, -collision.relativeVelocity.y);
                Touchdown?.Invoke(LastTouchdownSpeed);
            }

            if (entering || newTouchdown)
                Impact?.Invoke(new AircraftImpact(impactPoint, impactNormal, prePhysicsVelocity,
                    Mathf.Max(normalImpact, newTouchdown ? LastTouchdownSpeed : 0), Body.mass));

            if (Crashed) return;
            bool hardLanding = newTouchdown && LastTouchdownSpeed > Tuning.CrashVerticalSpeed;
            bool tipOver = supported && Vector3.Angle(Body.rotation * Vector3.up, Vector3.up) > Tuning.MaximumLandingTiltDegrees;
            bool obstacleImpact = entering && normalImpact > Tuning.CrashImpactSpeed;
            if (hardLanding || tipOver || obstacleImpact) ReportCrash();
        }

        /// <summary>World hazards can report a crash without embedding map rules in flight physics.</summary>
        public void ReportCrash()
        {
            if (Crashed) return;
            Crashed = true;
            CrashedEvent?.Invoke();
        }

        /// <summary>Explicit respawn only. Ordinary flight never writes position, rotation, or velocity.</summary>
        public void ResetAt(Vector3 position, Quaternion rotation)
        {
            supports.Clear();
            Crashed = false;
            LastTouchdownSpeed = 0f;
            LiftNewtons = 0f;
            RotorSpeed01 = 1f;
            RawCommand = PilotCommand.Neutral;
            AssistedCommand = PilotCommand.Neutral;
            prePhysicsVelocity = Vector3.zero;
            if (Body != null)
            {
                Body.position = position;
                Body.rotation = rotation;
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
                Body.WakeUp();
            }
            if (Assists != null) solver.Reset(Assists, PilotCommand.Neutral);
            if (Tuning != null) UpdateAltitude();
            ResetPerformed?.Invoke();
        }

        private void OnDisable() => supports.Clear();

        private void OnDestroy()
        {
            if (ownsTuning && Tuning != null)
            {
                if (Application.isPlaying) Destroy(Tuning);
                else DestroyImmediate(Tuning);
            }
        }
    }
}
