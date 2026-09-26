using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HoverForHire.Tests
{
    /// <summary>
    /// A complete passenger job flown by physics over the production island: no relocation between pads.
    /// The diagnostic autopilot supplies ordinary bounded pilot commands to the real aircraft and colliders.
    /// </summary>
    public sealed class FlownDeliveryTests
    {
        private const float Dt = 0.02f;
        private SimulationMode oldSimulation;
        private float oldFixedDelta;
        private Vector3 oldGravity;
        private LandingZone[] zones;
        private GameObject world, helicopter, missionObject;
        private FlightTuning tuning;
        private readonly HashSet<Material> materials = new HashSet<Material>();
        private string directory;

        [SetUp]
        public void Setup()
        {
            oldSimulation = Physics.simulationMode;
            oldFixedDelta = Time.fixedDeltaTime;
            oldGravity = Physics.gravity;
            Physics.simulationMode = SimulationMode.Script;
            Time.fixedDeltaTime = Dt;
            Physics.gravity = Vector3.down * 9.81f;
            zones = IslandWorld.Build();
            world = zones[0].transform.parent.gameObject;
            foreach (Renderer renderer in world.GetComponentsInChildren<Renderer>())
                foreach (Material material in renderer.sharedMaterials) if (material != null) materials.Add(material);
            directory = Path.Combine(Path.GetTempPath(), "HoverForHire-Flown-" + Guid.NewGuid().ToString("N"));
            Physics.SyncTransforms();
        }

        [TearDown]
        public void Cleanup()
        {
            if (missionObject != null) Object.DestroyImmediate(missionObject);
            if (helicopter != null) Object.DestroyImmediate(helicopter);
            if (world != null) Object.DestroyImmediate(world);
            if (tuning != null) Object.DestroyImmediate(tuning);
            foreach (Material material in materials)
            {
                if (material == null) continue;
#if UNITY_EDITOR
                if (UnityEditor.EditorUtility.IsPersistent(material)) continue;
#endif
                Object.DestroyImmediate(material);
            }
            materials.Clear();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            Physics.simulationMode = oldSimulation;
            Time.fixedDeltaTime = oldFixedDelta;
            Physics.gravity = oldGravity;
        }

        [Test]
        public void PassengerJobIsFlownAndLandedByPhysicsFromHomeToTown()
        {
            helicopter = new GameObject("Flown delivery aircraft");
            helicopter.SetActive(false);
            helicopter.layer = WorldConstants.AircraftLayer;
            helicopter.transform.position = zones[0].transform.position + Vector3.up * 1.55f;
            helicopter.AddComponent<Rigidbody>();
            var pilot = helicopter.AddComponent<Autopilot>();
            pilot.AutoTick = false;
            var aircraft = helicopter.AddComponent<HelicopterController>();
            tuning = FlightTestRig.ShippedTuningCopy();
            aircraft.Tuning = tuning;
            aircraft.InputSource = pilot;
            aircraft.RotorClearanceMask = WorldConstants.RotorClearanceMask;
            aircraft.WaterSurfaceHeight = WorldConstants.SeaLevel;
            aircraft.Assists.SetPreset(AssistPreset.Standard);
            pilot.Aircraft = aircraft;
            helicopter.AddComponent<HelicopterVisual>().Build(aircraft);
            helicopter.SetActive(true);
            aircraft.Body.interpolation = RigidbodyInterpolation.None;
            aircraft.Body.sleepThreshold = 0f;

            missionObject = new GameObject("Flown delivery director");
            missionObject.SetActive(false);
            var director = missionObject.AddComponent<MissionDirector>();
            director.ProgressionPathOverride = Path.Combine(directory, "progression.json");
            director.Aircraft = aircraft;
            director.Zones = zones;
            missionObject.SetActive(true);
            director.StartShift();
            Physics.SyncTransforms();
            Assert.That(director.CurrentContract.Pickup.Id, Is.EqualTo(zones[0].Definition.Id), "The first job starts at home base.");

            void Step(int count)
            {
                for (int i = 0; i < count && !aircraft.Crashed; i++)
                {
                    pilot.Tick(Dt);
                    aircraft.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
                    Physics.Simulate(Dt);
                    director.Tick(Dt);
                }
            }

            director.AcceptNextJob();
            Step(250); // Seated on the pickup pad: the service dwell loads the passengers.
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Transport));
            Assert.That(aircraft.PayloadKg, Is.GreaterThan(0f));

            LandingZone destination = null;
            foreach (LandingZone zone in zones) if (zone.Definition.Id == director.CurrentContract.Destination.Id) destination = zone;
            Assert.That(destination, Is.Not.Null);
            pilot.FlyTo(destination.transform.position);
            float peakHeight = 0f;
            for (int second = 0; second < 150 && director.MissionState == MissionState.Transport && !aircraft.Crashed; second++)
            {
                Step(50);
                peakHeight = Mathf.Max(peakHeight, aircraft.AltitudeAGL);
            }

            string summary = $"phase {pilot.Current}, crash {aircraft.LastCrashCause} {aircraft.CrashObstacle}, " +
                $"offset {Vector3.Distance(aircraft.Body.position, destination.transform.position):0.0} m, touchdown {pilot.TouchdownSpeed:0.00} m/s, " +
                $"flight {pilot.FlightSeconds:0} s, peak AGL {peakHeight:0} m";
            Assert.That(aircraft.Crashed, Is.False, summary);
            Assert.That(director.MissionState, Is.EqualTo(MissionState.Delivered), summary);
            Assert.That(director.Earnings, Is.GreaterThan(0), summary);
            Assert.That(director.LastResult.TouchdownMetresPerSecond, Is.LessThan(1f), summary);
            Assert.That(director.LastResult.AccuracyMetres, Is.LessThan(3f), summary);
            Assert.That(peakHeight, Is.GreaterThan(15f), "The route was flown, not hopped: " + summary);
            TestContext.WriteLine("Flown delivery: " + summary + $", grade {director.LastResult.Grade}, score {director.LastResult.Score:0}");
        }
    }
}
