using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HoverForHire.Tests
{
    public sealed class RuntimePilotInput : MonoBehaviour, IFlightInput
    {
        public PilotCommand Value;
        public PilotCommand Command => Value;
    }

    /// <summary>
    /// Actual Unity Rigidbody/collision checks, accelerated through scripted physics steps.
    /// Render/input/camera feel remains a separate player-facing validation requirement.
    /// </summary>
    public sealed class FlightRuntimeTests
    {
        private const float Dt = 0.02f;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<FlightTuning> tunings = new List<FlightTuning>();
        private readonly List<HelicopterController> aircraft = new List<HelicopterController>();
        private SimulationMode oldSimulationMode;
        private float oldFixedDeltaTime;
        private Vector3 oldGravity;

        [SetUp]
        public void SetUp()
        {
            oldSimulationMode = Physics.simulationMode;
            oldFixedDeltaTime = Time.fixedDeltaTime;
            oldGravity = Physics.gravity;
            Physics.simulationMode = SimulationMode.Script;
            Time.fixedDeltaTime = Dt;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            var floor = new GameObject("Flight test ground");
            objects.Add(floor);
            floor.transform.position = new Vector3(10000f, -0.5f, 10000f);
            floor.AddComponent<BoxCollider>().size = new Vector3(500f, 1f, 500f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject value in objects) if (value != null) Object.DestroyImmediate(value);
            foreach (FlightTuning value in tunings) if (value != null) Object.DestroyImmediate(value);
            objects.Clear();
            tunings.Clear();
            aircraft.Clear();
            Physics.simulationMode = oldSimulationMode;
            Time.fixedDeltaTime = oldFixedDeltaTime;
            Physics.gravity = oldGravity;
        }

        private HelicopterController Create(float height = 1.55f, float payload = 0f,
            AssistPreset preset = AssistPreset.Beginner)
        {
            var go = new GameObject("Flight test helicopter");
            objects.Add(go);
            go.SetActive(false);
            go.transform.position = new Vector3(10000f + aircraft.Count * 30f, height, 10000f);
            var hull = go.AddComponent<BoxCollider>();
            hull.center = new Vector3(0f, 0.05f, 0f);
            hull.size = new Vector3(2f, 1.7f, 3.1f);
            foreach (float side in new[] { -1f, 1f })
            {
                var skid = go.AddComponent<BoxCollider>();
                skid.center = new Vector3(side, -1.38f, 0.15f);
                skid.size = new Vector3(0.2f, 0.24f, 3.9f);
            }
            var input = go.AddComponent<RuntimePilotInput>();
            go.AddComponent<Rigidbody>();
            var controller = go.AddComponent<HelicopterController>();
            controller.Tuning = FlightTuning.CreateRuntimeDefaults();
            tunings.Add(controller.Tuning);
            controller.InputSource = input;
            controller.Assists.SetPreset(preset);
            go.SetActive(true);
            controller.SetPayload(payload);
            controller.Body.sleepThreshold = 0f;
            // These accelerated steps have no rendering loop to interpolate Transform poses.
            controller.Body.interpolation = RigidbodyInterpolation.None;
            aircraft.Add(controller);
            Physics.SyncTransforms();
            return controller;
        }

        private static RuntimePilotInput Input(HelicopterController controller)
            => (RuntimePilotInput)controller.InputSource;

        private void Step(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                foreach (HelicopterController controller in aircraft)
                    controller.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
                Physics.Simulate(Dt);
            }
        }

        private void PrimeAtHover(HelicopterController controller, float height = 20f)
        {
            Input(controller).Value = new PilotCommand(Vector2.zero, 0f,
                controller.Body.mass * -Physics.gravity.y / controller.Tuning.MaximumLiftNewtons);
            // Spin the collective actuator up while the skids support any remaining weight.
            Step(250);
            Vector3 position = controller.Body.position;
            position.y = height;
            controller.Body.position = position;
            controller.Body.rotation = Quaternion.identity;
            controller.Body.linearVelocity = Vector3.zero;
            controller.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        [Test]
        public void SkidsSettleOnActualContactsWithoutDriftOrCrash()
        {
            HelicopterController controller = Create();
            Assert.That(controller.Grounded, Is.False, "A downward ray is not a ground contact.");
            Step(250);
            Assert.That(controller.Grounded, Is.True);
            Assert.That(controller.Crashed, Is.False);
            Assert.That(controller.Body.position.y, Is.EqualTo(1.5f).Within(0.08f));
            Assert.That(controller.Body.linearVelocity.magnitude, Is.LessThan(0.12f));
            Assert.That(Vector3.Angle(controller.transform.up, Vector3.up), Is.LessThan(2f));
            Assert.That(controller.AltitudeAGL, Is.LessThan(0.08f));
        }

        [TestCase(0f)]
        [TestCase(300f)]
        public void TrimmedLevelRotorSupportsEmptyAndLoadedHover(float payload)
        {
            HelicopterController controller = Create(payload: payload);
            PrimeAtHover(controller);
            Step(250);
            Assert.That(controller.Grounded, Is.False);
            Assert.That(controller.Crashed, Is.False);
            Assert.That(controller.Body.position.y, Is.EqualTo(20f).Within(0.75f));
            Assert.That(Mathf.Abs(controller.VerticalSpeed), Is.LessThan(0.3f));
            Assert.That(controller.LiftNewtons, Is.EqualTo(controller.Body.mass * 9.81f).Within(2f));
        }

        [Test]
        public void RaisingCollectiveTakesOffAndPayloadReducesClimbPerformance()
        {
            HelicopterController empty = Create();
            HelicopterController loaded = Create(payload: 300f);
            Step(80);
            Input(empty).Value = new PilotCommand(Vector2.zero, 0f, 0.6f);
            Input(loaded).Value = Input(empty).Value;
            Step(200);
            Assert.That(empty.Grounded, Is.False);
            Assert.That(loaded.Grounded, Is.False);
            Assert.That(empty.Body.position.y, Is.GreaterThan(5f));
            Assert.That(empty.Body.position.y - loaded.Body.position.y, Is.GreaterThan(3f));
            Assert.That(empty.VerticalSpeed, Is.GreaterThan(loaded.VerticalSpeed));
            Assert.That(empty.Crashed || loaded.Crashed, Is.False);
        }

        [Test]
        public void ReleasingCyclicRetainsHorizontalMomentum()
        {
            HelicopterController controller = Create();
            PrimeAtHover(controller);
            controller.Body.linearVelocity = Vector3.forward * 12f;
            Step(1);
            Assert.That(controller.GroundSpeed, Is.GreaterThan(11.9f));
            Step(24);
            Assert.That(controller.GroundSpeed, Is.GreaterThan(10.5f));
            Assert.That(controller.Body.position.z, Is.GreaterThan(10005f));
        }

        [Test]
        public void AftCyclicProducesDeliberateBraking()
        {
            HelicopterController controller = Create();
            PrimeAtHover(controller, 40f);
            controller.Body.linearVelocity = Vector3.forward * 15f;
            PilotCommand value = Input(controller).Value;
            value.Cyclic = new Vector2(0f, -0.5f);
            Input(controller).Value = value;
            Step(100);
            Assert.That(controller.Body.linearVelocity.z, Is.LessThan(13f));
            Assert.That(controller.transform.up.z, Is.LessThan(-0.15f));
            Assert.That(controller.Crashed, Is.False);
        }

        [Test]
        public void PresetChangeDoesNotTeleportOrCancelMotion()
        {
            HelicopterController controller = Create(preset: AssistPreset.Unassisted);
            // No lift is needed to test continuity during a short airborne interval.
            controller.Body.position += Vector3.up * 30f;
            controller.Body.linearVelocity = new Vector3(7f, -1f, 3f);
            controller.Body.angularVelocity = new Vector3(0.6f, 0.4f, -0.6f);
            Vector3 before = controller.Body.position;
            controller.SetPreset(AssistPreset.Beginner);
            Step(1);
            Assert.That(Vector3.Distance(before, controller.Body.position), Is.LessThan(0.25f));
            Assert.That(controller.Body.linearVelocity.magnitude, Is.GreaterThan(7f));
            Assert.That(controller.Body.angularVelocity.magnitude, Is.GreaterThan(0.7f));
            Assert.That(controller.AssistedCommand.Cyclic.magnitude, Is.LessThan(0.1f));
            Assert.That(Mathf.Abs(controller.AssistedCommand.Yaw), Is.LessThan(0.1f));
        }

        [Test]
        public void RateAssistanceDampsRotationThroughPhysics()
        {
            HelicopterController assisted = Create(height: 40f, preset: AssistPreset.Standard);
            HelicopterController manual = Create(height: 40f, preset: AssistPreset.Unassisted);
            assisted.Body.angularVelocity = Vector3.right * 0.6f;
            manual.Body.angularVelocity = Vector3.right * 0.6f;
            Step(50);
            float assistedPitchRate = Mathf.Abs(assisted.LocalAngularRatesDegrees.x);
            float manualPitchRate = Mathf.Abs(manual.LocalAngularRatesDegrees.x);
            Assert.That(manualPitchRate, Is.GreaterThan(15f));
            Assert.That(assistedPitchRate, Is.LessThan(manualPitchRate * 0.5f));
        }

        [Test]
        public void WorldHazardCrashUsesOneEventAndLeavesRecoveryAvailable()
        {
            HelicopterController controller = Create(height: 40f);
            int crashEvents = 0;
            controller.CrashedEvent += () => crashEvents++;
            controller.Body.linearVelocity = Vector3.down * 2f;
            controller.ReportCrash();
            controller.ReportCrash();
            Assert.That(crashEvents, Is.EqualTo(1));
            Assert.That(controller.Crashed, Is.True);
            Assert.That(controller.Body.linearVelocity, Is.EqualTo(Vector3.down * 2f));
            controller.ResetAt(new Vector3(10000f, 1.55f, 10000f), Quaternion.identity);
            Assert.That(controller.Crashed, Is.False);
            Step(100);
            Assert.That(controller.Grounded, Is.True);
        }

        [Test]
        public void HardTouchdownCrashesOnceAndResetRestoresGroundContact()
        {
            HelicopterController controller = Create(height: 12f);
            int crashEvents = 0;
            controller.CrashedEvent += () => crashEvents++;
            Step(300);
            Assert.That(controller.Crashed, Is.True);
            Assert.That(crashEvents, Is.EqualTo(1));
            Assert.That(controller.LastTouchdownSpeed, Is.GreaterThan(controller.Tuning.CrashVerticalSpeed));
            controller.ResetAt(new Vector3(10000f, 1.55f, 10000f), Quaternion.identity);
            Assert.That(controller.Crashed, Is.False);
            Assert.That(controller.Body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(controller.Body.angularVelocity, Is.EqualTo(Vector3.zero));
            Step(150);
            Assert.That(controller.Grounded, Is.True);
            Assert.That(controller.Crashed, Is.False);
            Assert.That(crashEvents, Is.EqualTo(1));
        }

        [Test]
        public void CollisionPresentationReceivesIncomingImpactAndRecoveryClearsTransientEffects()
        {
            HelicopterController controller = Create(height: 12f);
            float strongest = 0;
            controller.Impact += impact => strongest = Mathf.Max(strongest, impact.Speed);
            Step(170);
            Assert.That(controller.Crashed, Is.True);
            Assert.That(strongest, Is.GreaterThan(controller.Tuning.CrashVerticalSpeed));
            Assert.That(controller.Body.linearVelocity.magnitude, Is.LessThan(strongest), "Impact data must preserve the incoming speed after collision resolution.");

            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Cabin door right";
            door.transform.SetParent(controller.transform, false);
            Object.DestroyImmediate(door.GetComponent<Collider>());
            Renderer visibleDoor = door.GetComponent<Renderer>();
            var originalPaint = new MaterialPropertyBlock();
            originalPaint.SetColor("_BaseColor", new Color(.22f, .44f, .66f, 1));
            visibleDoor.SetPropertyBlock(originalPaint);
            var effects = controller.gameObject.AddComponent<AircraftEffects>();
            effects.Initialize(controller, null);
            effects.PresentImpact(new AircraftImpact(controller.transform.position, Vector3.up, Vector3.down * 3, 3, 1000));
            Assert.That(effects.HasExploded, Is.False);
            Assert.That(visibleDoor.enabled, Is.True);
            effects.ResetEffects();
            effects.PresentImpact(new AircraftImpact(controller.transform.position, Vector3.up, Vector3.down * 25, 25, 1000));
            Assert.That(effects.HasExploded, Is.True);
            Assert.That(effects.ActiveDebrisCount, Is.EqualTo(1));
            Assert.That(visibleDoor.enabled, Is.False);
            Assert.That(effects.EffectParticleCount, Is.GreaterThan(0));
            var scorchedPaint = new MaterialPropertyBlock();
            visibleDoor.GetPropertyBlock(scorchedPaint);
            Assert.That(scorchedPaint.GetColor("_BaseColor").r, Is.LessThan(.15f));

            controller.ResetAt(new Vector3(10000f, 1.55f, 10000f), Quaternion.identity);
            Assert.That(effects.HasExploded, Is.False);
            Assert.That(effects.ActiveDebrisCount, Is.Zero);
            Assert.That(effects.EffectParticleCount, Is.Zero);
            Assert.That(visibleDoor.enabled, Is.True);
            visibleDoor.GetPropertyBlock(scorchedPaint);
            Assert.That(scorchedPaint.GetColor("_BaseColor"), Is.EqualTo(originalPaint.GetColor("_BaseColor")));
            Assert.That(controller.Crashed, Is.False);
        }
    }
}
