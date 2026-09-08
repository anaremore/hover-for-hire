using UnityEngine;

namespace HoverForHire
{
    /// <summary>Presentation only. The aircraft's Rigidbody is never moved or rotated by a camera.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        public Transform Target;
        public FlightInput Input;
        public Transform CockpitMount;
        public LayerMask ObstacleMask = ~(1 << 8);
        [Min(0.05f)] public float CollisionRadius = 0.4f;
        public bool IsCockpit { get; private set; }

        private Camera _camera;
        private FlightInput _subscribedInput;
        private Vector3 _positionVelocity;
        private float _lookYaw, _lookPitch, _heading, _lookIdleTime;
        private bool _positionReady;
        private readonly InputPreferences _fallback = new InputPreferences();

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.nearClipPlane = 0.08f;
            _camera.farClipPlane = 5000f;
        }

        private void OnEnable() { Subscribe(); }
        private void OnDisable() { Unsubscribe(); }
        private void Subscribe()
        {
            if (_subscribedInput == Input) return;
            Unsubscribe();
            _subscribedInput = Input;
            if (_subscribedInput == null) return;
            _subscribedInput.CameraRequested += ToggleCamera;
            _subscribedInput.RecenterRequested += Recenter;
        }

        private void Unsubscribe()
        {
            if (_subscribedInput == null) return;
            _subscribedInput.CameraRequested -= ToggleCamera;
            _subscribedInput.RecenterRequested -= Recenter;
            _subscribedInput = null;
        }

        public void ToggleCamera()
        {
            IsCockpit = !IsCockpit;
            _positionReady = false;
            // Input command and collective remain untouched when changing the viewpoint.
        }

        public void Recenter() { _lookIdleTime = 100f; }

        public void SnapToTarget()
        {
            _positionReady = false;
            _positionVelocity = Vector3.zero;
            _lookYaw = _lookPitch = 0f;
        }

        private void LateUpdate()
        {
            Subscribe(); // The bootstrap may assign Input after AddComponent invokes OnEnable.
            if (Target == null || (Input != null && Input.IsPaused)) return;
            InputPreferences settings = Input != null ? Input.Settings : _fallback;
            float dt = Time.deltaTime;
            Vector2 look = Input != null ? Input.CameraLookDelta : Vector2.zero;
            if (look.sqrMagnitude > 0.000001f)
            {
                _lookYaw = Mathf.Repeat(_lookYaw + look.x + 180f, 360f) - 180f;
                _lookPitch = Mathf.Clamp(_lookPitch - look.y, -55f, 65f);
                _lookIdleTime = 0f;
            }
            else _lookIdleTime += dt;

            if ((settings.AutoRecenterView || _lookIdleTime >= 100f) &&
                !(Input != null && Input.IsFreeLooking) && _lookIdleTime >= settings.RecenterDelay)
            {
                float blend = 1f - Mathf.Exp(-settings.RecenterSpeed * dt);
                _lookYaw = Mathf.LerpAngle(_lookYaw, 0f, blend);
                _lookPitch = Mathf.Lerp(_lookPitch, 0f, blend);
            }

            _camera.fieldOfView = settings.CameraFov;
            if (IsCockpit)
            {
                Transform mount = CockpitMount != null ? CockpitMount : Target;
                Vector3 position = CockpitMount != null ? mount.position : Target.TransformPoint(new Vector3(0f, 1.05f, 1.15f));
                transform.SetPositionAndRotation(position, mount.rotation * Quaternion.Euler(_lookPitch, _lookYaw, 0f));
                _positionReady = false;
                return;
            }

            Vector3 horizontalForward = Vector3.ProjectOnPlane(Target.forward, Vector3.up);
            float targetHeading = horizontalForward.sqrMagnitude > 0.001f
                ? Mathf.Atan2(horizontalForward.x, horizontalForward.z) * Mathf.Rad2Deg : _heading;
            if (!_positionReady) _heading = targetHeading;
            else _heading = Mathf.LerpAngle(_heading, targetHeading, 1f - Mathf.Exp(-dt / settings.CameraSmoothing));

            Vector3 pivot = Target.position + Vector3.up * 1.2f;
            Quaternion orbit = Quaternion.Euler(_lookPitch, _heading + _lookYaw, 0f);
            Vector3 desired = pivot + orbit * new Vector3(0f, settings.CameraHeight, -settings.CameraDistance);
            desired = ResolveCollision(pivot, desired);
            Vector3 cameraPosition = _positionReady
                ? Vector3.SmoothDamp(transform.position, desired, ref _positionVelocity,
                    settings.CameraSmoothing, Mathf.Infinity, dt)
                : desired;
            // A clear destination alone is insufficient: the damped position may cut across a wall.
            Vector3 resolved = ResolveCollision(pivot, cameraPosition);
            if ((resolved - cameraPosition).sqrMagnitude > 0.0001f) _positionVelocity = Vector3.zero;
            Vector3 viewDirection = pivot - resolved;
            if (viewDirection.sqrMagnitude > 0.0001f)
                transform.SetPositionAndRotation(resolved, Quaternion.LookRotation(viewDirection, Vector3.up));
            _positionReady = true;
        }

        private Vector3 ResolveCollision(Vector3 origin, Vector3 desired)
        {
            Vector3 offset = desired - origin;
            float distance = offset.magnitude;
            if (distance < 0.001f) return desired;
            if (Physics.SphereCast(origin, CollisionRadius, offset / distance, out RaycastHit hit,
                    distance, ObstacleMask, QueryTriggerInteraction.Ignore))
                return origin + offset / distance * Mathf.Max(0.05f, hit.distance - 0.1f);
            return desired;
        }
    }
}
