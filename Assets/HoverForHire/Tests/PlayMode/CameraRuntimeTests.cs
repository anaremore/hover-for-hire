using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HoverForHire.Tests
{
    public sealed class CameraRuntimeTests
    {
        private GameObject _target, _cameraObject, _wall, _ground;
        private static readonly Vector3 Origin = new Vector3(10000f, 100f, 10000f);

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_cameraObject != null) Object.Destroy(_cameraObject);
            if (_target != null) Object.Destroy(_target);
            if (_wall != null) Object.Destroy(_wall);
            if (_ground != null) Object.Destroy(_ground);
            yield return null;
        }

        private ChaseCamera CreateRig(out FlightInput input, out Rigidbody body)
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.SetPositionAndRotation(Origin + Vector3.down * 0.5f, Quaternion.identity);
            _ground.transform.localScale = new Vector3(50f, 1f, 50f);
            _target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _target.name = "Camera test aircraft";
            _target.layer = 8;
            _target.transform.position = Origin + Vector3.up;
            _target.transform.localScale = new Vector3(3f, 2f, 7f);
            body = _target.AddComponent<Rigidbody>();
            body.isKinematic = true;
            input = _target.AddComponent<FlightInput>();
            input.enabled = false; // Tests retain a known command without depending on real devices.
            input.Settings = new InputPreferences();
            input.ResetCommand(0.63f);
            _cameraObject = new GameObject("Camera test camera");
            _cameraObject.AddComponent<Camera>().enabled = false;
            ChaseCamera camera = _cameraObject.AddComponent<ChaseCamera>();
            camera.Target = _target.transform;
            camera.Input = input;
            return camera;
        }

        [UnityTest]
        public IEnumerator ChaseSweepIgnoresOwnAircraftButNotOtherSolids()
        {
            CreateRig(out _, out _);
            // A tail fin placed across the camera's sweep path, not overlapping the sweep's start.
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.name = "Own tail fin";
            _wall.layer = WorldConstants.AircraftLayer;
            _wall.transform.position = Origin + new Vector3(0f, 3.9f, -5f);
            _wall.transform.localScale = new Vector3(1f, 2f, 4f);
            Physics.SyncTransforms();
            yield return null;
            Assert.That(_cameraObject.transform.position.z - Origin.z, Is.EqualTo(-12f).Within(0.03f), "The aircraft's own fin must not pull the camera in.");
            Assert.That(_cameraObject.transform.position.y, Is.GreaterThan(Origin.y + 1f));
            // Control: the same solid on an ordinary layer is in the sweep path and does pull the camera in.
            _wall.layer = 0;
            _cameraObject.GetComponent<ChaseCamera>().SnapToTarget();
            yield return null;
            Assert.That(_cameraObject.transform.position.z - Origin.z, Is.GreaterThan(-5f), "The fin lies across the sweep, so the check above is meaningful.");
        }

        [UnityTest]
        public IEnumerator ChaseSweepPassesThroughTreeFoliage()
        {
            CreateRig(out _, out _);
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.name = "Tree crown";
            _wall.layer = WorldConstants.VegetationLayer;
            _wall.transform.position = Origin + new Vector3(0f, 3.9f, -6f);
            _wall.transform.localScale = new Vector3(6f, 4f, 3f);
            Physics.SyncTransforms();
            yield return null;
            Assert.That(_cameraObject.transform.position.z - Origin.z, Is.EqualTo(-12f).Within(0.03f), "Foliage should not make the camera jump.");
        }

        [UnityTest]
        public IEnumerator SolidBuildingShortensCameraAndKeepsItOutsideWall()
        {
            CreateRig(out _, out _);
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = Origin + new Vector3(0f, 4f, -6f);
            _wall.transform.localScale = new Vector3(12f, 8f, 1f);
            Physics.SyncTransforms();
            yield return null;
            Assert.That(_cameraObject.transform.position.z - Origin.z, Is.GreaterThan(-5.5f));
            Assert.That(_cameraObject.transform.position.z - Origin.z, Is.LessThan(-1f));
            Assert.That(_wall.GetComponent<Collider>().bounds.Contains(_cameraObject.transform.position), Is.False);
        }

        [UnityTest]
        public IEnumerator CameraSwitchPreservesAircraftAndPersistentCollective()
        {
            ChaseCamera camera = CreateRig(out FlightInput input, out Rigidbody body);
            var mount = new GameObject("Cockpit mount");
            mount.transform.SetParent(_target.transform, false);
            mount.transform.localPosition = new Vector3(0f, 0.6f, 0.5f);
            camera.CockpitMount = mount.transform;
            Vector3 position = body.position;
            Quaternion rotation = body.rotation;
            PilotCommand command = input.Command;
            yield return null;
            camera.ToggleCamera();
            yield return null;
            Assert.That(camera.IsCockpit, Is.True);
            Assert.That(Vector3.Distance(_cameraObject.transform.position, mount.transform.position), Is.LessThan(0.001f));
            camera.ToggleCamera();
            yield return null;
            Assert.That(camera.IsCockpit, Is.False);
            Assert.That(body.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(body.rotation, rotation), Is.LessThan(0.001f));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(input.Command.Cyclic, Is.EqualTo(command.Cyclic));
            Assert.That(input.Command.Yaw, Is.EqualTo(command.Yaw));
            Assert.That(input.Command.Collective, Is.EqualTo(command.Collective));
        }
    }
}
