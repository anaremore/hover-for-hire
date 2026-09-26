using UnityEngine;

namespace HoverForHire
{
    /// <summary>
    /// Diagnostic pilot for automated checks: flies the real flight model through the same bounded pilot
    /// commands a person would use (cyclic, pedals, collective), from one pad to another, and lands.
    /// Cascaded proportional control: position → velocity → attitude → cyclic; height → climb rate → collective;
    /// heading → pedals. It never moves the aircraft directly. Not a player assist.
    /// </summary>
    [DefaultExecutionOrder(-120)]
    public sealed class Autopilot : MonoBehaviour, IFlightInput
    {
        public enum Phase { Idle, Climb, Cruise, Approach, Descend, Touchdown, Landed }

        public HelicopterController Aircraft;
        [Min(1f)] public float CruiseSpeed = 18f;
        [Tooltip("Cruise height above the destination pad, and minimum clearance above terrain and trees.")]
        [Min(5f)] public float CruiseClearance = 35f;
        [Min(0.3f)] public float BrakingDeceleration = 1.2f;
        [Tooltip("Tick from FixedUpdate. Scripted-physics tests tick manually before each step instead.")]
        public bool AutoTick = true;

        public Phase Current { get; private set; } = Phase.Idle;
        public Vector3 Target { get; private set; }
        public PilotCommand Command { get; private set; }
        /// <summary>Vertical speed at the first ground contact of the landing (m/s, positive down).</summary>
        public float TouchdownSpeed { get; private set; }
        public float FlightSeconds { get; private set; }

        private const float SkidOffset = 1.5f;
        private Vector3 start;
        private float trim, touchdownTime;
        private float? holdHeading;

        /// <summary>Fly from the current position to a pad surface point and land on it.</summary>
        public void FlyTo(Vector3 padSurface)
        {
            Target = padSurface;
            start = Aircraft.Body.position;
            Current = Phase.Climb;
            trim = 0f;
            holdHeading = null;
            TouchdownSpeed = 0f;
            FlightSeconds = 0f;
        }

        public void Stop()
        {
            Current = Phase.Idle;
            Command = new PilotCommand(Vector2.zero, 0f, 0f);
        }

        private void FixedUpdate() { if (AutoTick) Tick(Time.fixedDeltaTime); }

        public void Tick(float dt)
        {
            if (Aircraft == null || Aircraft.Body == null || Current == Phase.Idle) return;
            if (Current == Phase.Landed) { Command = new PilotCommand(Vector2.zero, 0f, 0f); return; }
            FlightSeconds += dt;
            Rigidbody body = Aircraft.Body;
            Vector3 position = body.position, velocity = body.linearVelocity;
            Vector3 horizontal = new Vector3(Target.x - position.x, 0f, Target.z - position.z);
            float distance = horizontal.magnitude;
            float groundSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            float groundBelow = position.y - SkidOffset - Aircraft.AltitudeAGL; // Terrain, roofs, trees or water.

            // Phase logic.
            if (Current == Phase.Climb && position.y - start.y > 12f) Current = Phase.Cruise;
            if (Current == Phase.Cruise && distance < groundSpeed * groundSpeed / (2f * BrakingDeceleration) + 25f) Current = Phase.Approach;
            if (Current == Phase.Approach && distance < 1.5f && groundSpeed < 0.6f) Current = Phase.Descend;
            if (Current == Phase.Descend && Aircraft.Grounded)
            {
                Current = Phase.Touchdown;
                touchdownTime = 0f;
                TouchdownSpeed = Aircraft.LastTouchdownSpeed;
            }

            // Horizontal: braking-limited desired ground velocity, then the tilt that produces it.
            Vector3 desiredVelocity = Vector3.zero;
            if (Current != Phase.Climb && distance > 0.05f)
            {
                float speed = Mathf.Min(CruiseSpeed, Mathf.Sqrt(2f * BrakingDeceleration * Mathf.Max(0f, distance - 1f)));
                if (Current == Phase.Descend || Current == Phase.Touchdown) speed = Mathf.Min(speed, 0.6f * distance);
                desiredVelocity = horizontal / distance * speed;
            }
            Vector3 acceleration = Vector3.ClampMagnitude((desiredVelocity - new Vector3(velocity.x, 0f, velocity.z)) * 0.6f, 3f);

            float heading = Aircraft.Heading, desiredHeading;
            if ((Current == Phase.Cruise || Current == Phase.Approach) && distance > 30f)
            {
                desiredHeading = Mathf.Atan2(horizontal.x, horizontal.z) * Mathf.Rad2Deg;
                if (Current == Phase.Cruise) holdHeading = null;
            }
            else
            {
                if (!holdHeading.HasValue) holdHeading = heading;
                desiredHeading = holdHeading.Value;
            }
            float headingRadians = heading * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(headingRadians), 0f, Mathf.Cos(headingRadians));
            Vector3 right = new Vector3(Mathf.Cos(headingRadians), 0f, -Mathf.Sin(headingRadians));
            float pitchTarget = Mathf.Clamp(-Mathf.Atan2(Vector3.Dot(acceleration, forward), FlightMath.StandardGravity) * Mathf.Rad2Deg, -20f, 15f);
            float bankTarget = Mathf.Clamp(Mathf.Atan2(Vector3.Dot(acceleration, right), FlightMath.StandardGravity) * Mathf.Rad2Deg, -20f, 20f);
            Quaternion attitude = body.rotation;
            Vector3 noseAxis = attitude * Vector3.forward, rightAxis = attitude * Vector3.right, upAxis = attitude * Vector3.up;
            float pitch = Mathf.Asin(Mathf.Clamp(noseAxis.y, -1f, 1f)) * Mathf.Rad2Deg;
            float bank = Mathf.Atan2(-rightAxis.y, upAxis.y) * Mathf.Rad2Deg;
            var cyclic = new Vector2(Mathf.Clamp((bankTarget - bank) * 0.07f, -1f, 1f), Mathf.Clamp((pitch - pitchTarget) * 0.07f, -1f, 1f));
            float pedals = Mathf.Clamp(Mathf.DeltaAngle(heading, desiredHeading) * 0.03f, -1f, 1f);

            // Vertical: desired climb rate, then collective around the hover setting.
            float climb;
            if (Current == Phase.Climb) climb = 2.5f;
            else if (Current == Phase.Cruise || Current == Phase.Approach)
            {
                float heightTarget = Mathf.Max(Target.y + SkidOffset + (distance > 60f ? CruiseClearance : 8f + distance * 0.45f),
                    groundBelow + SkidOffset + 20f);
                climb = Mathf.Clamp((heightTarget - position.y) * 0.35f, -2.5f, 3f);
            }
            else if (Current == Phase.Descend)
            {
                float abovePad = position.y - (Target.y + SkidOffset);
                climb = abovePad < 4f ? -0.3f : abovePad < 10f ? -0.8f : -1.5f;
            }
            else climb = -0.5f;

            float hover = Aircraft.HoverCollective, collective;
            if (Current == Phase.Touchdown)
            {
                touchdownTime += dt;
                collective = Mathf.Max(0f, hover * (1f - touchdownTime / 2f));
                if (touchdownTime > 2.5f) Current = Phase.Landed;
            }
            else
            {
                float error = climb - velocity.y;
                trim = Mathf.Clamp(trim + error * 0.2f * dt, -0.1f, 0.1f);
                collective = hover / Mathf.Max(0.7f, upAxis.y) + error * 0.05f + trim;
            }
            Command = new PilotCommand(cyclic, pedals, Mathf.Clamp01(collective));
        }
    }
}
