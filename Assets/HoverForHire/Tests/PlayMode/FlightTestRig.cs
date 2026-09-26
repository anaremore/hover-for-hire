using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HoverForHire.Tests
{
    /// <summary>
    /// Scripted-physics rig for flight characterization: real Rigidbody and colliders, fixed 50 Hz steps,
    /// and the shipped tuning asset (copied, so tests never modify it). Restores global physics settings on dispose.
    /// </summary>
    public sealed class FlightTestRig : IDisposable
    {
        public const float Dt = 0.02f;
        public static readonly Vector3 Origin = new Vector3(20000f, 0f, 20000f);

        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<FlightTuning> tunings = new List<FlightTuning>();
        private readonly List<HelicopterController> aircraft = new List<HelicopterController>();
        private readonly SimulationMode oldSimulationMode;
        private readonly float oldFixedDeltaTime;
        private readonly Vector3 oldGravity;

        public FlightTestRig()
        {
            oldSimulationMode = Physics.simulationMode;
            oldFixedDeltaTime = Time.fixedDeltaTime;
            oldGravity = Physics.gravity;
            Physics.simulationMode = SimulationMode.Script;
            Time.fixedDeltaTime = Dt;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            var floor = new GameObject("Handling rig ground");
            objects.Add(floor);
            floor.transform.position = Origin + Vector3.down * 0.5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(2000f, 1f, 2000f);
        }

        public static FlightTuning ShippedTuningCopy()
        {
            var shipped = Resources.Load<FlightTuning>("UtilityHelicopter");
            if (shipped == null) throw new InvalidOperationException("Resources/UtilityHelicopter tuning asset is missing.");
            FlightTuning copy = Object.Instantiate(shipped);
            copy.hideFlags = HideFlags.DontSave;
            return copy;
        }

        /// <summary>Production collision boxes, grounded at the rig origin with an independent tuning copy.</summary>
        public HelicopterController Create(AssistPreset preset = AssistPreset.Beginner, float payload = 0f,
            FlightTuning tuning = null)
        {
            var go = new GameObject("Handling rig helicopter " + aircraft.Count);
            objects.Add(go);
            go.SetActive(false);
            go.transform.position = Origin + new Vector3(aircraft.Count * 40f, 1.55f, 0f);
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
            controller.Tuning = tuning != null ? tuning : ShippedTuningCopy();
            tunings.Add(controller.Tuning);
            controller.InputSource = input;
            controller.Assists.SetPreset(preset);
            go.SetActive(true);
            controller.SetPayload(payload);
            controller.Body.sleepThreshold = 0f;
            // Accelerated steps have no rendering loop to interpolate Transform poses.
            controller.Body.interpolation = RigidbodyInterpolation.None;
            aircraft.Add(controller);
            Physics.SyncTransforms();
            return controller;
        }

        public static RuntimePilotInput Input(HelicopterController controller) => (RuntimePilotInput)controller.InputSource;

        public void Step(int steps, Action beforeEachStep = null)
        {
            for (int i = 0; i < steps; i++)
            {
                beforeEachStep?.Invoke();
                foreach (HelicopterController controller in aircraft)
                    controller.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
                Physics.Simulate(Dt);
            }
        }

        /// <summary>
        /// Spin the collective actuator up to the given setting on the ground, then place the aircraft in the air
        /// with the requested attitude and velocity. Mirrors a pilot already established in flight.
        /// </summary>
        public void EstablishInFlight(HelicopterController controller, float collective, float height,
            Quaternion attitude, Vector3 velocity)
        {
            Input(controller).Value = new PilotCommand(Vector2.zero, 0f, collective);
            Step(250);
            Place(controller, height, attitude, velocity);
        }

        /// <summary>Move an already-primed aircraft (see <see cref="EstablishInFlight"/>) without stepping any others.</summary>
        public static void Place(HelicopterController controller, float height, Quaternion attitude, Vector3 velocity)
        {
            controller.Body.position = new Vector3(controller.Body.position.x, Origin.y + height, controller.Body.position.z);
            controller.Body.rotation = attitude;
            controller.Body.linearVelocity = velocity;
            controller.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        /// <summary>Right-bank-positive roll and nose-up-positive pitch, in degrees.</summary>
        public static float BankDegrees(HelicopterController c)
        {
            Quaternion r = c.Body.rotation;
            Vector3 right = r * Vector3.right, up = r * Vector3.up;
            return Mathf.Atan2(-right.y, up.y) * Mathf.Rad2Deg;
        }

        public static float PitchDegrees(HelicopterController c)
            => Mathf.Asin(Mathf.Clamp((c.Body.rotation * Vector3.forward).y, -1f, 1f)) * Mathf.Rad2Deg;

        public void Dispose()
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
    }
}
