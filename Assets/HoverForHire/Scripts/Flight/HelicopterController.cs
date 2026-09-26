using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoverForHire
{
    public enum SystemFailure { EngineOut, TailRotor }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HelicopterController : MonoBehaviour
    {
        public FlightTuning Tuning;
        [Tooltip("Any MonoBehaviour implementing IFlightInput.")]
        public MonoBehaviour InputSource;
        public Rigidbody Body;
        public AssistSettings Assists = new AssistSettings();
        /// <summary>Hover hold: bounded drift, height and heading hold that hands back on any deliberate pilot input.</summary>
        public readonly HoverHoldAssist HoverHold = new HoverHoldAssist();
        /// <summary>Which physical challenges are active. The defaults (ground effect and translational lift only) are Relaxed.</summary>
        public RealismSettings Realism = new RealismSettings();
        /// <summary>Optional air-mass motion. Null is calm air.</summary>
        public IWindSource Wind;
        /// <summary>World water surface height (m). Negative infinity means no water.</summary>
        public float WaterSurfaceHeight = float.NegativeInfinity;
        [Tooltip("Layers the spinning rotors can strike. The aircraft's own colliders are always ignored.")]
        public LayerMask RotorClearanceMask = ~0;
        [Tooltip("Mean flight time between random engine failures when realism enables them.")]
        [Min(60f)] public float MeanSecondsBetweenEngineFailures = 1800f;
        public int FailureSeed = 7;

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
        /// <summary>Horizontal speed through the air: what translational lift and ground-effect fade respond to.</summary>
        public float HorizontalAirspeed { get { Vector3 air = AirVelocity; return new Vector2(air.x, air.z).magnitude; } }
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
        /// <summary>Hover collective at the current height, including ground effect when enabled.</summary>
        public float HoverCollectiveHere => Realism != null && Realism.GroundEffect && Tuning != null
            ? HoverCollective / FlightMath.GroundEffectFactor(AltitudeAGL + Tuning.SkidClearanceMeters + Tuning.MainRotorHub.y, 0f, Tuning)
            : HoverCollective;
        public float VerticalSpeed => Body == null ? 0f : Body.linearVelocity.y;
        public float Heading => Body == null ? transform.eulerAngles.y : Body.rotation.eulerAngles.y;
        public float LiftNewtons { get; private set; }
        public float PayloadKg { get; private set; }
        public float LastTouchdownSpeed { get; private set; }
        public PilotCommand RawCommand { get; private set; }
        public PilotCommand AssistedCommand { get; private set; }
        public Vector3 LocalAngularRatesDegrees => Body == null ? Vector3.zero :
            Quaternion.Inverse(Body.rotation) * Body.angularVelocity * Mathf.Rad2Deg;
        /// <summary>Rotor speed as a fraction of governed speed (NR). Held at 1 unless power limits are on.</summary>
        public float RotorSpeed01 { get; private set; } = 1f;
        public float RotorRpm => RotorSpeed01 * (Tuning == null ? 395f : Tuning.GovernedRotorRpm);

        // ---- Realism observations (for the HUD, audio, recorder and drills) ----
        public bool EngineFailed { get; private set; }
        public bool TailRotorFailed { get; private set; }
        /// <summary>Engine torque as a fraction of its 100% rating (TQ).</summary>
        public float TorqueFraction { get; private set; }
        public float PowerRequiredW { get; private set; }
        public float VortexRingSeverity { get; private set; }
        public float GroundEffectGain { get; private set; }
        public float TranslationalLiftGain { get; private set; }
        /// <summary>0..1 shudder while passing through translational lift.</summary>
        public float TransitionBuffet01 { get; private set; }
        /// <summary>0..1 current turbulence buffet.</summary>
        public float Turbulence01 { get; private set; }
        public bool LowRotorSpeed => !Crashed && RotorSpeed01 < 0.95f;
        public bool RotorOverspeed => !Crashed && RotorSpeed01 > 1.08f;
        public bool Overtorque => !Crashed && TorqueFraction > 1.0f;
        /// <summary>Rollover limit in force: the tuning's structural limit or the realism setting, whichever is lower.</summary>
        public float RolloverLimitDegrees => Tuning == null ? 65f : Mathf.Min(Tuning.MaximumLandingTiltDegrees,
            Realism != null ? Realism.RolloverLimitDegrees : Tuning.MaximumLandingTiltDegrees);

        public event Action CrashedEvent;
        public event Action<float> Touchdown;
        public event Action<AircraftImpact> Impact;
        public event Action ResetPerformed;
        public event Action<SystemFailure> SystemFailed;

        private readonly HashSet<Collider> supports = new HashSet<Collider>();
        private readonly RaycastHit[] altitudeHits = new RaycastHit[32];
        private readonly List<Mesh> sensorMeshes = new List<Mesh>();
        private readonly AssistSolver solver = new AssistSolver();
        private Vector3 prePhysicsVelocity;
        private bool ownsTuning;
        private float heaveForce, demandedLift, engineTorqueShare = 1f, simulationTime;
        private System.Random failureRandom;

        private void Awake()
        {
            if (Tuning == null)
            {
                Tuning = FlightTuning.CreateRuntimeDefaults();
                ownsTuning = true;
            }
            if (Assists == null) Assists = new AssistSettings();
            if (Realism == null) Realism = new RealismSettings();
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
            failureRandom = new System.Random(FailureSeed);
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

        /// <summary>
        /// A spinning rotor touched something solid. A main-rotor strike ends the flight; a tail-rotor strike does too,
        /// unless realism allows tail-rotor failures, in which case anti-torque is lost and the pilot must fly it down.
        /// </summary>
        public void ReportRotorContact(CrashCause cause, Collider other)
        {
            if (Crashed || other == null || other.isTrigger || Tuning == null || RotorSpeed01 < Tuning.RotorStrikeMinimumSpeed01) return;
            if (other.attachedRigidbody == Body || other.transform.IsChildOf(transform)) return;
            if (((1 << other.gameObject.layer) & RotorClearanceMask) == 0) return;
            if (cause == CrashCause.TailRotorStrike && Realism != null && Realism.TailRotorFailures)
            {
                if (!TailRotorFailed) { TailRotorFailed = true; SystemFailed?.Invoke(SystemFailure.TailRotor); }
                return;
            }
            ReportCrash(cause, 0f, 0f, other.name);
        }

        /// <summary>Stop the engine: the rotor is then driven only by the air (autorotation).</summary>
        public void FailEngine()
        {
            if (Crashed || EngineFailed) return;
            EngineFailed = true;
            SystemFailed?.Invoke(SystemFailure.EngineOut);
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
            simulationTime += dt;
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
            float horizontalAir = new Vector2(airVelocity.x, airVelocity.z).magnitude;
            // The fuselage reacts to the torque the engine delivers into the rotor: none once it has failed.
            float rotorReactionNm = LiftNewtons * Mathf.Max(0f, Tuning.RotorTorqueArmMeters) * engineTorqueShare;
            PilotCommand flown = HoverHold.Filter(RawCommand, this, dt);
            AssistedCommand = solver.Step(flown, localAngular, inverseRotation * Vector3.up,
                rotorReactionNm, localAir, inertia, Tuning, Assists, dt);

            Vector3 localThrust = FlightMath.LocalThrustDirection(AssistedCommand.Cyclic, Tuning.RotorDiskTiltDegrees);
            Vector3 thrustDirection = rotation * localThrust;
            UpdateRotorSpeed(dt, airVelocity, thrustDirection);
            MaybeFailEngine(dt);
            float rotor = RotorSpeed01, authority = rotor * rotor;

            // Thrust: collective demand at the current rotor speed, shaped by the enabled aerodynamic effects.
            float hubHeight = AltitudeAGL + Tuning.SkidClearanceMeters + Tuning.MainRotorHub.y;
            float groundEffect = Realism.GroundEffect ? FlightMath.GroundEffectFactor(hubHeight, horizontalAir, Tuning) : 1f;
            float translational = Realism.TranslationalLift ? FlightMath.TranslationalLiftFactor(horizontalAir, Tuning) : 1f;
            VortexRingSeverity = Realism.VortexRingState && !Crashed
                ? FlightMath.VortexRingSeverity(-airVelocity.y, horizontalAir, AssistedCommand.Collective, !EngineFailed, Realism.VortexOnsetScale, Tuning) : 0f;
            float stall = Realism.PowerLimits ? FlightMath.RotorStallFactor(rotor, Tuning) : 1f;
            GroundEffectGain = groundEffect - 1f;
            TranslationalLiftGain = translational - 1f;
            TransitionBuffet01 = Realism.TranslationalLift ? FlightMath.TranslationalBuffet(horizontalAir, Tuning) : 0f;
            // The rotor works for the demanded thrust; a vortex ring wastes part of it.
            demandedLift = FlightMath.Lift(AssistedCommand.Collective, Tuning) * authority * groundEffect * translational * stall;
            LiftNewtons = demandedLift * (1f - FlightMath.VortexRingThrustLoss(VortexRingSeverity, AssistedCommand.Collective, HoverCollective, Tuning));

            Body.AddForce(thrustDirection * LiftNewtons, ForceMode.Force);
            // Upflow through a descending rotor adds thrust; in a vortex ring the rotor re-ingests its own wake instead.
            heaveForce = FlightMath.HeaveDamping(airVelocity.y, rotor, Tuning) * (1f - VortexRingSeverity);
            Body.AddForce(Vector3.up * heaveForce, ForceMode.Force);

            float tail = TailRotorFailed ? 0f : 1f;
            Vector3 controlTorque = new Vector3(AssistedCommand.Cyclic.y * Tuning.PitchTorqueNm * authority,
                AssistedCommand.Yaw * Tuning.YawTorqueNm * authority * tail, -AssistedCommand.Cyclic.x * Tuning.RollTorqueNm * authority);
            controlTorque.y += LiftNewtons * Mathf.Max(0f, Tuning.RotorTorqueArmMeters) * engineTorqueShare;
            Vector3 damping = FlightMath.RotorRateDamping(localAngular, inertia, Tuning.RotorRateDampingPerSecond, authority);
            damping.y *= tail;
            controlTorque += damping;
            controlTorque.y += FlightMath.WeathervaneYawTorque(localAir, Tuning.WeathervaneCoefficient);
            if (Realism.SpeedStability)
            {
                // Flapback: the disc tilts back with forward airspeed, raising the nose unless the pilot holds it down.
                controlTorque.x -= Tuning.SpeedStabilityNmPerMs * Mathf.Max(0f, localAir.z) * authority;
                float transition = FlightMath.TranslationalBuffet(horizontalAir * 1.6f, Tuning);
                controlTorque.z -= Tuning.TransverseFlowNm * transition * authority;
            }
            if (VortexRingSeverity > 0f)
            {
                float buffet = Tuning.VortexRingBuffetNm * VortexRingSeverity;
                controlTorque.x += (Mathf.PerlinNoise(simulationTime * 3.1f, 0.3f) - 0.5f) * 2f * buffet;
                controlTorque.z += (Mathf.PerlinNoise(simulationTime * 2.7f, 5.1f) - 0.5f) * 2f * buffet;
            }
            Vector3 turbulence = Wind != null ? Wind.TurbulenceAt(Body.position) : Vector3.zero;
            Turbulence01 = Mathf.Clamp01(turbulence.magnitude);
            if (Turbulence01 > 0f)
            {
                float exposure = 0.4f + 0.6f * Mathf.Clamp01(horizontalAir / 20f);
                controlTorque.x += turbulence.x * Tuning.TurbulenceTorqueNm * exposure;
                controlTorque.z += turbulence.z * Tuning.TurbulenceTorqueNm * exposure;
            }
            Body.AddRelativeTorque(controlTorque, ForceMode.Force);

            Body.AddRelativeForce(FlightMath.AerodynamicDrag(localAir, Tuning.LinearDrag, Tuning.QuadraticDrag), ForceMode.Force);
            Body.AddRelativeTorque(FlightMath.AerodynamicDrag(localAngular, Tuning.AngularDrag, Vector3.zero), ForceMode.Force);
        }

        /// <summary>
        /// Rotor speed and engine torque. With power limits the rotor is a flywheel: the governed engine supplies torque
        /// up to its limit, overpulling droops the rotor, and with the engine out only the air can drive it.
        /// Without power limits the rotor stays governed and the engine supplies whatever is needed.
        /// </summary>
        private void UpdateRotorSpeed(float dt, Vector3 airVelocity, Vector3 thrustDirection)
        {
            if (Crashed)
            {
                RotorSpeed01 = Mathf.MoveTowards(RotorSpeed01, 0f, dt * 0.35f);
                TorqueFraction = 0f;
                engineTorqueShare = 0f;
                return;
            }
            float axial = Vector3.Dot(airVelocity, thrustDirection);
            float edgewise = (airVelocity - thrustDirection * axial).magnitude;
            // Inside a vortex ring the rotor re-ingests its own wake: the descent brings no upflow to help it.
            axial *= 1f - VortexRingSeverity;
            float rotor = Mathf.Max(0.05f, RotorSpeed01);
            float omega0 = FlightMath.GovernedOmega(Tuning);
            PowerRequiredW = FlightMath.PowerRequired(demandedLift + heaveForce, edgewise, axial, rotor, Tuning);
            float required = PowerRequiredW / (omega0 * rotor);
            float rated = FlightMath.RatedTorque(Tuning);
            float engine;
            if (Realism.PowerLimits)
            {
                float omega = RotorSpeed01 * omega0;
                engine = EngineFailed ? 0f : Mathf.Clamp(required + Tuning.RotorInertiaKgM2 * Tuning.GovernorGain * (omega0 - omega),
                    0f, rated * Tuning.EngineTorqueLimit);
                omega += (engine - required) / Mathf.Max(1f, Tuning.RotorInertiaKgM2) * dt;
                RotorSpeed01 = Mathf.Clamp(omega / omega0, 0f, Tuning.MaximumRotorSpeed01);
            }
            else
            {
                RotorSpeed01 = Mathf.MoveTowards(RotorSpeed01, 1f, dt);
                engine = Mathf.Max(0f, required);
            }
            TorqueFraction = engine / Mathf.Max(1f, rated);
            engineTorqueShare = required > 1f ? Mathf.Clamp01(engine / required) : (EngineFailed ? 0f : 1f);
        }

        private void MaybeFailEngine(float dt)
        {
            if (Realism.EngineFailures != FailureMode.Random || !Realism.PowerLimits || EngineFailed || Crashed || Grounded || AltitudeAGL < 30f) return;
            if (failureRandom.NextDouble() < dt / MeanSecondsBetweenEngineFailures) FailEngine();
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
            else if (supported && tilt > RolloverLimitDegrees)
                ReportCrash(CrashCause.TipOver, tilt, RolloverLimitDegrees, collision.collider.name);
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
        public void ResetAt(Vector3 position, Quaternion rotation) => ResetAt(position, rotation, Vector3.zero);

        /// <summary>Respawn with an initial velocity, e.g. for drills that begin in flight. Systems are restored.</summary>
        public void ResetAt(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            supports.Clear();
            HoverHold.Disengage("Hover hold off: aircraft reset.");
            Crashed = false;
            LastCrashCause = CrashCause.None;
            CrashValue = CrashLimit = 0f;
            CrashObstacle = "";
            LastTouchdownSpeed = 0f;
            LiftNewtons = demandedLift = 0f;
            RotorSpeed01 = 1f;
            EngineFailed = TailRotorFailed = false;
            engineTorqueShare = 1f;
            heaveForce = 0f;
            TorqueFraction = VortexRingSeverity = 0f;
            RawCommand = PilotCommand.Neutral;
            AssistedCommand = PilotCommand.Neutral;
            prePhysicsVelocity = velocity;
            if (Body != null)
            {
                Body.position = position;
                Body.rotation = rotation;
                Body.linearVelocity = velocity;
                Body.angularVelocity = Vector3.zero;
                Body.WakeUp();
            }
            if (Assists != null) solver.Reset(Assists, PilotCommand.Neutral);
            if (Tuning != null) UpdateAltitude();
            ResetPerformed?.Invoke();
        }

        /// <summary>Start the collective actuator at a setting (airborne drill starts) instead of zero.</summary>
        public void PrimeCollective(float collective)
        {
            var primed = new PilotCommand(Vector2.zero, 0f, Mathf.Clamp01(collective));
            solver.Reset(Assists, primed);
            AssistedCommand = primed;
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
