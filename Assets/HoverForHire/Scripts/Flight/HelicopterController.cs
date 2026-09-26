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
        /// <summary>Optional air-mass motion. Null is calm air.</summary>
        public IWindSource Wind;
        /// <summary>World water surface height (m). Negative infinity means no water.</summary>
        public float WaterSurfaceHeight = float.NegativeInfinity;
        [Tooltip("Layers the spinning rotors can strike. The aircraft's own colliders are always ignored.")]
        public LayerMask RotorClearanceMask = ~0;

        public bool Grounded => supports.Count > 0 && (Body == null ? transform.up : Body.rotation * Vector3.up).y > 0.4f;
        public bool Crashed { get; private set; }
        /// <summary>Why the last flight ended, with the measured value and the limit it exceeded (units depend on cause).</summary>
        public CrashCause LastCrashCause { get; private set; }
        public float CrashValue { get; private set; }
        public float CrashLimit { get; private set; }
        /// <summary>Name of the struck object for rotor strikes and obstacle impacts, when known.</summary>
        public string CrashObstacle { get; private set; } = "";
        /// <summary>Vertical clearance beneath the fuselage origin, less upright skid clearance.</summary>
        public float AltitudeAGL { get; private set; }
        public float GroundSpeed => Body == null ? 0f : Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up).magnitude;
        /// <summary>Wind sampled at the aircraft during the latest physics step (world axes, m/s).</summary>
        public Vector3 CurrentWind { get; private set; }
        /// <summary>Velocity relative to the surrounding air (world axes). Equals ground velocity in calm air.</summary>
        public Vector3 AirVelocity => Body == null ? Vector3.zero : Body.linearVelocity - CurrentWind;
        /// <summary>Magnitude of the 3D velocity through the air, including vertical motion.</summary>
        public float Airspeed => AirVelocity.magnitude;
        /// <summary>Angle between the nose and the horizontal air velocity; positive when air arrives from the right.</summary>
        public float SideslipDegrees
        {
            get
            {
                if (Body == null) return 0f;
                Vector3 local = Quaternion.Inverse(Body.rotation) * AirVelocity;
                return new Vector2(local.x, local.z).sqrMagnitude < 0.25f ? 0f : Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            }
        }
        /// <summary>Collective that balances the current weight in level, still air, out of ground effect.</summary>
        public float HoverCollective => Tuning == null || Body == null ? 0f :
            FlightMath.HoverCollective(Body.mass, Physics.gravity.y, Tuning);
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
        private readonly List<Mesh> sensorMeshes = new List<Mesh>();
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
            CreateRotorSensor("Main rotor clearance", Tuning.MainRotorHub, Tuning.MainRotorRadius, false, CrashCause.RotorStrike);
            CreateRotorSensor("Tail rotor clearance", Tuning.TailRotorHub, Tuning.TailRotorRadius, true, CrashCause.TailRotorStrike);
        }

        /// <summary>A convex trigger disc swept by each rotor; physics reports any overlap exactly, with no probe gaps.</summary>
        private void CreateRotorSensor(string name, Vector3 hub, float radius, bool tail, CrashCause cause)
        {
            var sensorObject = new GameObject(name) { layer = gameObject.layer };
            sensorObject.transform.SetParent(transform, false);
            Mesh mesh = RotorDiscSensor.DiscMesh(name, hub, radius, Tuning.RotorDiscThickness, tail);
            sensorMeshes.Add(mesh);
            var volume = sensorObject.AddComponent<MeshCollider>();
            volume.sharedMesh = mesh;
            volume.convex = true;
            volume.isTrigger = true;
            var sensor = sensorObject.AddComponent<RotorDiscSensor>();
            sensor.Owner = this;
            sensor.Cause = cause;
        }

        /// <summary>A spinning rotor touched something solid: a blade strike ends the flight.</summary>
        public void ReportRotorContact(CrashCause cause, Collider other)
        {
            if (Crashed || other == null || other.isTrigger || Tuning == null || RotorSpeed01 < Tuning.RotorStrikeMinimumSpeed01) return;
            if (other.attachedRigidbody == Body || other.transform.IsChildOf(transform)) return;
            if (((1 << other.gameObject.layer) & RotorClearanceMask) == 0) return;
            ReportCrash(cause, 0f, 0f, other.name);
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
                // Explicit moments: adding or reshaping colliders never changes handling.
                Body.inertiaTensor = FlightMath.Inertia(Body.mass, Tuning);
                Body.inertiaTensorRotation = Quaternion.identity;
                Body.centerOfMass = Tuning.CenterOfMass;
            }
        }

        private void FixedUpdate()
        {
            if (Tuning == null || Body == null) return;
            supports.RemoveWhere(IsInvalidSupport);
            UpdateAltitude();
            CheckWater();
            float dt = Time.fixedDeltaTime;
            prePhysicsVelocity = Body.linearVelocity;
            RawCommand = !Crashed && InputSource is IFlightInput input ? input.Command.Clamped() : PilotCommand.Neutral;
            // The interpolated Transform is for rendering. Forces read the current physics pose.
            Quaternion rotation = Body.rotation;
            Quaternion inverseRotation = Quaternion.Inverse(rotation);
            Vector3 localAngular = inverseRotation * Body.angularVelocity;
            CurrentWind = Wind != null ? Wind.WindAt(Body.position) : Vector3.zero;
            Vector3 airVelocity = Body.linearVelocity - CurrentWind;
            Vector3 localAir = inverseRotation * airVelocity;
            Vector3 inertia = Body.inertiaTensor;
            float rotorReactionNm = LiftNewtons * Mathf.Max(0f, Tuning.RotorTorqueArmMeters);
            AssistedCommand = solver.Step(RawCommand, localAngular, inverseRotation * Vector3.up,
                rotorReactionNm, localAir, inertia, Tuning, Assists, dt);
            RotorSpeed01 = Mathf.MoveTowards(RotorSpeed01, Crashed ? 0f : 1f, dt * (Crashed ? 0.35f : 1f));
            LiftNewtons = FlightMath.Lift(AssistedCommand.Collective, Tuning) * RotorSpeed01 * RotorSpeed01;

            Vector3 localThrust = FlightMath.LocalThrustDirection(AssistedCommand.Cyclic, Tuning.RotorDiskTiltDegrees);
            Body.AddForce(rotation * localThrust * LiftNewtons, ForceMode.Force);
            Body.AddForce(Vector3.up * FlightMath.HeaveDamping(airVelocity.y, RotorSpeed01, Tuning), ForceMode.Force);
            float authority = RotorSpeed01 * RotorSpeed01;
            Vector3 controlTorque = new Vector3(AssistedCommand.Cyclic.y * Tuning.PitchTorqueNm,
                AssistedCommand.Yaw * Tuning.YawTorqueNm, -AssistedCommand.Cyclic.x * Tuning.RollTorqueNm) * authority;
            controlTorque.y += LiftNewtons * Mathf.Max(0f, Tuning.RotorTorqueArmMeters);
            controlTorque += FlightMath.RotorRateDamping(localAngular, inertia, Tuning.RotorRateDampingPerSecond, authority);
            controlTorque.y += FlightMath.WeathervaneYawTorque(localAir, Tuning.WeathervaneCoefficient);
            Body.AddRelativeTorque(controlTorque, ForceMode.Force);

            Body.AddRelativeForce(FlightMath.AerodynamicDrag(localAir, Tuning.LinearDrag, Tuning.QuadraticDrag), ForceMode.Force);
            Body.AddRelativeTorque(FlightMath.AerodynamicDrag(localAngular, Tuning.AngularDrag, Vector3.zero), ForceMode.Force);
        }

        private bool HasWater => !float.IsNegativeInfinity(WaterSurfaceHeight);

        private void CheckWater()
        {
            if (Crashed || !HasWater) return;
            float skids = Body.position.y - Tuning.SkidClearanceMeters;
            if (skids < WaterSurfaceHeight - 0.3f) ReportCrash(CrashCause.Ditching, WaterSurfaceHeight - skids, 0.3f, "water");
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
            // Water is a surface too: without this the ray would measure to the seabed.
            if (HasWater) closest = Mathf.Min(closest, Mathf.Max(0f, origin.y - WaterSurfaceHeight));
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
            float tilt = Vector3.Angle(Body.rotation * Vector3.up, Vector3.up);
            if (newTouchdown && LastTouchdownSpeed > Tuning.CrashVerticalSpeed)
                ReportCrash(CrashCause.HardLanding, LastTouchdownSpeed, Tuning.CrashVerticalSpeed, collision.collider.name);
            else if (supported && tilt > Tuning.MaximumLandingTiltDegrees)
                ReportCrash(CrashCause.TipOver, tilt, Tuning.MaximumLandingTiltDegrees, collision.collider.name);
            else if (entering && normalImpact > Tuning.CrashImpactSpeed)
                ReportCrash(CrashCause.ObstacleImpact, normalImpact, Tuning.CrashImpactSpeed, collision.collider.name);
        }

        /// <summary>World hazards can report a crash without embedding map rules in flight physics.</summary>
        public void ReportCrash() => ReportCrash(CrashCause.Hazard);

        /// <summary>End the flight once, recording the cause, the measured value and the limit it exceeded.</summary>
        public void ReportCrash(CrashCause cause, float value = 0f, float limit = 0f, string obstacle = "")
        {
            if (Crashed) return;
            Crashed = true;
            LastCrashCause = cause;
            CrashValue = value;
            CrashLimit = limit;
            CrashObstacle = obstacle ?? "";
            CrashedEvent?.Invoke();
        }

        /// <summary>Explicit respawn only. Ordinary flight never writes position, rotation, or velocity.</summary>
        public void ResetAt(Vector3 position, Quaternion rotation)
        {
            supports.Clear();
            Crashed = false;
            LastCrashCause = CrashCause.None;
            CrashValue = CrashLimit = 0f;
            CrashObstacle = "";
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
            foreach (Mesh mesh in sensorMeshes) if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            if (ownsTuning && Tuning != null)
            {
                if (Application.isPlaying) Destroy(Tuning);
                else DestroyImmediate(Tuning);
            }
        }
    }
}
