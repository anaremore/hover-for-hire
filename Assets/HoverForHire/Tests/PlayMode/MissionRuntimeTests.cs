using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HoverForHire.Tests
{
    /// <summary>
    /// Real pad/skid contacts and the live director. Relocation between pads isolates
    /// mission integrity; the flight runtime suite and human playtest cover the route flying.
    /// </summary>
    public sealed class MissionRuntimeTests
    {
        private const float Dt = 0.02f;
        private readonly List<GameObject> objects = new List<GameObject>();
        private SimulationMode oldSimulation;
        private Vector3 oldGravity;
        private float oldFixedDelta;
        private HelicopterController aircraft;
        private MissionDirector director;
        private LandingZone[] pads;
        private FlightTuning tuning;
        private string directory, savePath;

        [SetUp]
        public void Setup()
        {
            oldSimulation = Physics.simulationMode;
            oldGravity = Physics.gravity;
            oldFixedDelta = Time.fixedDeltaTime;
            Physics.simulationMode = SimulationMode.Script;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            Time.fixedDeltaTime = Dt;
            directory = Path.Combine(Path.GetTempPath(), "HoverForHire-MissionRuntime-" + Guid.NewGuid().ToString("N"));
            savePath = Path.Combine(directory, "progression.json");

            pads = new LandingZone[3];
            for (int i = 0; i < pads.Length; i++)
            {
                var pad = Track(new GameObject("Mission runtime pad " + i));
                pad.transform.position = new Vector3(20000f + i * 100f, i * 5f, 20000f);
                var collider = pad.AddComponent<BoxCollider>();
                collider.center = Vector3.down * 0.5f;
                collider.size = new Vector3(40f, 1f, 40f);
                pads[i] = pad.AddComponent<LandingZone>();
                pads[i].Id = "test-pad-" + i;
                pads[i].DisplayName = "Pad " + i;
                pads[i].Radius = 14f;
            }

            var helicopter = Track(new GameObject("Mission runtime aircraft"));
            helicopter.SetActive(false);
            helicopter.transform.position = pads[0].transform.position + Vector3.up * 1.55f;
            helicopter.AddComponent<Rigidbody>();
            foreach (float side in new[] { -1f, 1f })
            {
                var skid = helicopter.AddComponent<BoxCollider>();
                skid.center = new Vector3(side, -1.38f, 0.15f);
                skid.size = new Vector3(0.2f, 0.24f, 3.9f);
            }
            var hull = helicopter.AddComponent<BoxCollider>();
            hull.center = new Vector3(0f, 0.05f, 0f);
            hull.size = new Vector3(2f, 1.7f, 3.1f);
            var pilot = helicopter.AddComponent<RuntimePilotInput>();
            aircraft = helicopter.AddComponent<HelicopterController>();
            tuning = FlightTuning.CreateRuntimeDefaults();
            aircraft.Tuning = tuning;
            aircraft.InputSource = pilot;
            helicopter.SetActive(true);
            aircraft.Body.interpolation = RigidbodyInterpolation.None;
            aircraft.Body.sleepThreshold = 0f;

            // Awake is deferred while inactive: persistence is isolated before any read/write.
            var missionObject = Track(new GameObject("Mission runtime director"));
            missionObject.SetActive(false);
            director = missionObject.AddComponent<MissionDirector>();
            director.ProgressionPathOverride = savePath;
            director.Aircraft = aircraft;
            director.Zones = pads;
            missionObject.SetActive(true);
            director.StartShift();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void Cleanup()
        {
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            if (tuning != null) Object.DestroyImmediate(tuning);
            Physics.simulationMode = oldSimulation;
            Physics.gravity = oldGravity;
            Time.fixedDeltaTime = oldFixedDelta;
            foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(savePath + suffix)) File.Delete(savePath + suffix);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }

        private GameObject Track(GameObject value) { objects.Add(value); return value; }

        private void Step(int count)
        {
            for (int i = 0; i < count; i++)
            {
                aircraft.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
                Physics.Simulate(Dt);
                director.Tick(Dt);
            }
        }

        private void PlaceOverPad(int index, float height = 1.55f)
        {
            // A fixture relocation is intentionally distinct from the player reset API.
            // The external ResetAt behavior is verified separately below.
            aircraft.Body.position = pads[index].transform.position + Vector3.up * height;
            aircraft.Body.rotation = Quaternion.identity;
            aircraft.Body.linearVelocity = Vector3.zero;
            aircraft.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        [Test]
        public void PassengerThenCargoUseActualGroundDwellAndPersistExactlyTwoPayouts()
        {
            Step(100);
            Assert.That(aircraft.Grounded, Is.True);
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Available));
            Assert.That(aircraft.PayloadKg, Is.Zero);
            director.AcceptNextJob();
            Assert.That(director.CurrentContract.Type, Is.EqualTo(ContractType.Passengers));
            Step(75);
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Pickup));
            Assert.That(aircraft.PayloadKg, Is.Zero, "Brief stable contact must not load passengers.");
            Step(100);
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Transport));
            Assert.That(aircraft.PayloadKg, Is.EqualTo(160f));
            Assert.That(aircraft.Body.mass, Is.EqualTo(tuning.EmptyMassKg + 160f));
            PlaceOverPad(1);
            Step(210);
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Delivered));
            Assert.That(aircraft.PayloadKg, Is.Zero);
            int passengerPay = director.Earnings;
            Assert.That(passengerPay, Is.GreaterThan(0));
            Assert.That(director.CompletedDeliveries, Is.EqualTo(1));
            Step(250);
            director.Retry();
            Step(200);
            Assert.That(director.Earnings, Is.EqualTo(passengerPay), "Post-delivery dwell and reset cannot pay again.");
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Delivered));

            director.AcceptNextJob();
            Assert.That(director.CurrentContract.Type, Is.EqualTo(ContractType.Cargo));
            Step(200);
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Transport));
            Assert.That(aircraft.PayloadKg, Is.EqualTo(230f));
            PlaceOverPad(2);
            Step(210);
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Delivered));
            Assert.That(director.DeliveriesThisShift, Is.EqualTo(2));
            Assert.That(director.Earnings, Is.GreaterThan(passengerPay));
            Assert.That(aircraft.PayloadKg, Is.Zero);
            var reloaded = new ProgressionStore(savePath).Load();
            Assert.That(reloaded.CompletedDeliveries, Is.EqualTo(2));
            Assert.That(reloaded.TotalEarnings, Is.EqualTo(director.Earnings));
            Assert.That(reloaded.Results.Count, Is.EqualTo(2));
            Assert.That(reloaded.Results[0].Assists, Does.Contain("RATE"));
            Assert.That(director.SaveWarning, Is.Empty);
        }

        [Test]
        public void ExternalResetFailsLoadedJobAndRetryRequiresNewPickup()
        {
            director.AcceptNextJob();
            Step(220);
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Transport));
            string attempt = director.CurrentMission.AttemptId;
            aircraft.ResetAt(pads[1].transform.position + Vector3.up * 1.55f, Quaternion.identity);
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Failed));
            Assert.That(aircraft.PayloadKg, Is.Zero);
            Step(200);
            Assert.That(director.Earnings, Is.Zero);
            float remainingBeforeRetry = director.RemainingSeconds;
            director.Retry();
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Accepted));
            Assert.That(director.CurrentMission.AttemptId, Is.Not.EqualTo(attempt));
            Assert.That(director.RemainingSeconds, Is.EqualTo(remainingBeforeRetry));
            Assert.That(Vector3.Distance(aircraft.Body.position, pads[0].transform.position + Vector3.up * 1.55f), Is.LessThan(0.01f));
            Assert.That(aircraft.PayloadKg, Is.Zero);
            Step(220);
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Transport));
            Assert.That(aircraft.PayloadKg, Is.EqualTo(160f));
            Assert.That(director.Earnings, Is.Zero);
        }

        [Test]
        public void ShiftExpiryClearsPayloadAndCannotCompletePendingDelivery()
        {
            director.ShiftDurationSeconds = 8f;
            director.StartShift();
            director.AcceptNextJob();
            Step(210);
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Transport));
            Assert.That(director.RemainingSeconds, Is.EqualTo(3.8f).Within(0.03f));
            // Deliver too late to complete the three-second unload before the shift ends.
            Step(100);
            PlaceOverPad(1);
            Step(150);
            Assert.That(director.ShiftFinished, Is.True);
            Assert.That(director.RemainingSeconds, Is.Zero);
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Failed));
            Assert.That(aircraft.PayloadKg, Is.Zero);
            Assert.That(director.Earnings, Is.Zero);
            Assert.That(director.CompletedDeliveries, Is.Zero);
        }

        [Test]
        public void ContactOnWrongFloorDoesNotSatisfyDestinationElevation()
        {
            director.AcceptNextJob();
            Step(220);
            var overhead = Track(new GameObject("Unrelated overhead roof"));
            overhead.transform.position = pads[1].transform.position + Vector3.up * 20f;
            var collider = overhead.AddComponent<BoxCollider>();
            collider.center = Vector3.down * 0.5f;
            collider.size = new Vector3(40f, 1f, 40f);
            PlaceOverPad(1, 21.55f);
            Step(250);
            Assert.That(aircraft.Grounded, Is.True);
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Transport));
            Assert.That(director.DwellProgress, Is.Zero);
            Assert.That(director.Earnings, Is.Zero);
        }
    }
}
