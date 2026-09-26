using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HoverForHire.Tests
{
    /// <summary>Blade strikes, water, crash causes and thrust tilt with the real Rigidbody and colliders.</summary>
    public sealed class SafetyRuntimeTests
    {
        private FlightTestRig rig;
        private readonly List<GameObject> obstacles = new List<GameObject>();

        [SetUp] public void SetUp() => rig = new FlightTestRig();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject obstacle in obstacles) if (obstacle != null) Object.DestroyImmediate(obstacle);
            obstacles.Clear();
            rig.Dispose();
        }

        private GameObject Obstacle(string name, Vector3 center, Vector3 size, bool trigger = false)
        {
            var obstacle = new GameObject(name);
            obstacles.Add(obstacle);
            obstacle.transform.position = center;
            var box = obstacle.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = trigger;
            Physics.SyncTransforms();
            return obstacle;
        }

        private HelicopterController Hovering(float height = 20f)
        {
            HelicopterController aircraft = rig.Create(AssistPreset.Beginner);
            rig.EstablishInFlight(aircraft, aircraft.HoverCollective, height, Quaternion.identity, Vector3.zero);
            return aircraft;
        }

        [Test]
        public void PoleInsideTheRotorDiscIsAMainRotorStrike()
        {
            HelicopterController aircraft = Hovering();
            Vector3 hub = aircraft.Body.position + aircraft.Tuning.MainRotorHub;
            Obstacle("Test mast", hub + new Vector3(3.6f, -5f, 0f), new Vector3(.3f, 30f, .3f));
            rig.Step(3);
            Assert.That(aircraft.Crashed, Is.True);
            Assert.That(aircraft.LastCrashCause, Is.EqualTo(CrashCause.RotorStrike));
            Assert.That(aircraft.CrashObstacle, Is.EqualTo("Test mast"));
        }

        [Test]
        public void PoleJustOutsideTheDiscAndTriggerVolumesAreNotStrikes()
        {
            HelicopterController aircraft = Hovering();
            Vector3 hub = aircraft.Body.position + aircraft.Tuning.MainRotorHub;
            Obstacle("Clear mast", hub + new Vector3(aircraft.Tuning.MainRotorRadius + .6f, -5f, 0f), new Vector3(.3f, 30f, .3f));
            Obstacle("Mission trigger volume", hub, new Vector3(6f, 1f, 6f), trigger: true);
            rig.Step(25);
            Assert.That(aircraft.Crashed, Is.False, aircraft.LastCrashCause + " " + aircraft.CrashObstacle);
        }

        [Test]
        public void ObstacleAtTheTailRotorIsATailRotorStrike()
        {
            HelicopterController aircraft = Hovering();
            Obstacle("Test post", aircraft.Body.position + aircraft.Tuning.TailRotorHub, new Vector3(.4f, .4f, .4f));
            rig.Step(3);
            Assert.That(aircraft.LastCrashCause, Is.EqualTo(CrashCause.TailRotorStrike));
        }

        [Test]
        public void HardLandingRecordsTheMeasuredSpeedAndLimit()
        {
            HelicopterController aircraft = rig.Create(AssistPreset.Beginner);
            aircraft.Body.position += Vector3.up * 12f;
            Physics.SyncTransforms();
            rig.Step(300);
            Assert.That(aircraft.LastCrashCause, Is.EqualTo(CrashCause.HardLanding));
            Assert.That(aircraft.CrashValue, Is.GreaterThan(aircraft.CrashLimit));
            Assert.That(aircraft.CrashLimit, Is.EqualTo(aircraft.Tuning.CrashVerticalSpeed));
            aircraft.ResetAt(FlightTestRig.Origin + Vector3.up * 1.55f, Quaternion.identity);
            Assert.That(aircraft.LastCrashCause, Is.EqualTo(CrashCause.None), "Reset clears the previous cause.");
        }

        [Test]
        public void WaterIsASurfaceForAltitudeAndDitchingEndsTheFlight()
        {
            HelicopterController aircraft = Hovering(20f);
            rig.Step(1); // Altitude is sampled during the physics step, after the placement.
            float dryAltitude = aircraft.AltitudeAGL;
            Assert.That(dryAltitude, Is.GreaterThan(15f));
            aircraft.WaterSurfaceHeight = FlightTestRig.Origin.y + 5f;
            rig.Step(1);
            Assert.That(aircraft.AltitudeAGL, Is.EqualTo(dryAltitude - 5f).Within(0.3f), "Altitude over water measures to the surface, not the seabed.");
            Assert.That(aircraft.Crashed, Is.False);
            aircraft.WaterSurfaceHeight = aircraft.Body.position.y + 2f;
            rig.Step(1);
            Assert.That(aircraft.LastCrashCause, Is.EqualTo(CrashCause.Ditching));
        }

        [Test]
        public void BankingTiltsThrustSidewaysAndCostsVerticalLift()
        {
            HelicopterController level = rig.Create(AssistPreset.Unassisted);
            HelicopterController banked = rig.Create(AssistPreset.Unassisted);
            float hover = level.HoverCollective;
            FlightTestRig.Input(level).Value = FlightTestRig.Input(banked).Value = new PilotCommand(Vector2.zero, 0f, hover);
            rig.Step(250);
            FlightTestRig.Place(level, 200f, Quaternion.identity, Vector3.zero);
            FlightTestRig.Place(banked, 200f, Quaternion.Euler(0f, 0f, -30f), Vector3.zero);
            rig.Step(50);
            Assert.That(banked.VerticalSpeed, Is.LessThan(-0.5f), "A 30° bank leaves only cos 30° of the thrust vertical.");
            Assert.That(banked.Body.linearVelocity.x, Is.GreaterThan(2f), "A right bank accelerates the aircraft to the right.");
            Assert.That(Mathf.Abs(level.VerticalSpeed), Is.LessThan(0.2f));
            Assert.That(Mathf.Abs(level.Body.linearVelocity.x), Is.LessThan(0.3f));
        }
    }
}
