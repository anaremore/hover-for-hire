using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HoverForHire.Tests
{
    /// <summary>Checks production-generated geometry, beyond the flat-box flight fixtures.</summary>
    public sealed class WorldRuntimeTests
    {
        private const float Dt = 0.02f;
        private SimulationMode oldSimulation;
        private float oldFixedDelta;
        private Vector3 oldGravity;
        private LandingZone[] zones;
        private GameObject world;
        private GameObject helicopter;
        private FlightTuning tuning;
        private readonly HashSet<Material> materials = new HashSet<Material>();
        private Mesh terrainMesh;

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
            foreach (MeshFilter filter in world.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null && filter.sharedMesh.name == "Port Meridian terrain") terrainMesh = filter.sharedMesh;
            Physics.SyncTransforms();
        }

        [TearDown]
        public void Cleanup()
        {
            if (helicopter != null) Object.DestroyImmediate(helicopter);
            if (world != null) Object.DestroyImmediate(world);
            if (tuning != null) Object.DestroyImmediate(tuning);
            if (terrainMesh != null) Object.DestroyImmediate(terrainMesh);
            foreach (Material material in materials)
            {
                if (material == null) continue;
#if UNITY_EDITOR
                // TextMesh signs share Unity's font asset material; this fixture owns only runtime palettes.
                if (UnityEditor.EditorUtility.IsPersistent(material)) continue;
#endif
                Object.DestroyImmediate(material);
            }
            materials.Clear();
            Physics.simulationMode = oldSimulation;
            Time.fixedDeltaTime = oldFixedDelta;
            Physics.gravity = oldGravity;
        }

        [Test]
        public void AllTenProductionPadsHaveFlatCollidersAtTheirDeclaredElevation()
        {
            Assert.That(zones.Length, Is.EqualTo(10));
            foreach (LandingZone zone in zones)
            {
                Collider surface = null;
                foreach (Collider candidate in world.GetComponentsInChildren<Collider>())
                    if (candidate.enabled && candidate.gameObject.name == zone.DisplayName) surface = candidate;
                Assert.That(surface, Is.Not.Null, zone.DisplayName + " must have a physical pad surface.");
                Assert.That(surface.bounds.size.y, Is.EqualTo(0.24f).Within(0.015f), zone.DisplayName);
                Assert.That(surface.bounds.max.y, Is.EqualTo(zone.transform.position.y).Within(0.015f), zone.DisplayName);

                Vector3 origin = zone.transform.position + Vector3.up * 150f;
                Assert.That(Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f,
                    ~(1 << 8), QueryTriggerInteraction.Ignore), Is.True, zone.DisplayName);
                Assert.That(hit.point.y, Is.EqualTo(zone.transform.position.y).Within(0.03f),
                    zone.DisplayName + " approach ray must reach the visible pad, not an inflated primitive collider.");
                Assert.That(hit.normal.y, Is.GreaterThan(0.99f), zone.DisplayName);
            }
        }

        [Test]
        public void ActualAircraftSettlesUprightAtEveryProductionLandingLocation()
        {
            helicopter = new GameObject("Production geometry spawn test");
            helicopter.SetActive(false);
            helicopter.layer = 8;
            helicopter.transform.position = zones[0].transform.position + Vector3.up * 1.55f;
            helicopter.AddComponent<Rigidbody>();
            var pilot = helicopter.AddComponent<RuntimePilotInput>();
            var controller = helicopter.AddComponent<HelicopterController>();
            tuning = FlightTuning.CreateRuntimeDefaults();
            controller.Tuning = tuning;
            controller.InputSource = pilot;
            helicopter.AddComponent<HelicopterVisual>().Build(controller);
            helicopter.SetActive(true);
            controller.Body.interpolation = RigidbodyInterpolation.None;
            controller.Body.sleepThreshold = 0f;
            int crashes = 0;
            controller.CrashedEvent += () => crashes++;

            foreach (LandingZone zone in zones)
            {
                controller.ResetAt(zone.transform.position + Vector3.up * 1.55f, Quaternion.identity);
                Physics.SyncTransforms();
                for (int step = 0; step < 150; step++)
                {
                    controller.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
                    Physics.Simulate(Dt);
                }
                Assert.That(crashes, Is.Zero, zone.DisplayName + " spawned into colliding geometry.");
                Assert.That(controller.Grounded, Is.True, zone.DisplayName);
                Assert.That(controller.Body.position.y - zone.transform.position.y,
                    Is.EqualTo(tuning.SkidClearanceMeters).Within(0.08f), zone.DisplayName);
                Assert.That(controller.AltitudeAGL, Is.LessThan(0.08f), zone.DisplayName);
                Assert.That(controller.Body.linearVelocity.magnitude, Is.LessThan(0.15f), zone.DisplayName);
                Assert.That(Vector3.Angle(controller.Body.rotation * Vector3.up, Vector3.up), Is.LessThan(2f), zone.DisplayName);
            }
        }
    }
}
