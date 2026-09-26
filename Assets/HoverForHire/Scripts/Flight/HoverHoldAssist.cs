using System;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>
    /// Hover hold: nulls ground drift and vertical speed and keeps the heading, the way a pilot would, by adding small
    /// bounded cyclic, collective and pedal inputs to the pilot's own. Any deliberate pilot input hands control back.
    /// It flies the same aircraft through the same controls: no hidden forces, no position teleports.
    /// </summary>
    public sealed class HoverHoldAssist
    {
        /// <summary>The hold engages below this ground speed (m/s), in the air.</summary>
        public const float MaximumEngageSpeed = 5f;
        /// <summary>Pilot inputs beyond these hand control back.</summary>
        public const float CyclicOverride = 0.2f, CollectiveOverride = 0.03f, PedalOverride = 0.3f;
        /// <summary>Largest cyclic, collective and pedal the hold adds.</summary>
        public const float CyclicAuthority = 0.35f, CollectiveAuthority = 0.08f, PedalAuthority = 0.3f;

        public bool Engaged { get; private set; }
        /// <summary>The pilot's collective when the hold engaged; its own corrections are made around it.</summary>
        public float EngagedCollective { get; private set; }
        /// <summary>Raised on engage (true) and hand-back (false), with a short reason.</summary>
        public event Action<bool, string> Changed;

        private float heading, verticalTrim;

        /// <summary>Engage if the aircraft is airborne and slow enough; otherwise explain why not.</summary>
        public bool TryEngage(HelicopterController aircraft, float pilotCollective, out string reason)
        {
            reason = "";
            if (aircraft == null || aircraft.Body == null || aircraft.Crashed) { reason = "Hover hold is unavailable after a crash."; return false; }
            if (aircraft.Grounded) { reason = "Hover hold works in the air: lift into a hover first."; return false; }
            if (aircraft.GroundSpeed > MaximumEngageSpeed) { reason = "Slow below 5 m/s before engaging hover hold."; return false; }
            Engaged = true;
            EngagedCollective = Mathf.Clamp01(pilotCollective);
            heading = aircraft.Heading;
            verticalTrim = 0f;
            Changed?.Invoke(true, "Hover hold engaged. Move any control to take over.");
            return true;
        }

        public void Disengage(string reason)
        {
            if (!Engaged) return;
            Engaged = false;
            Changed?.Invoke(false, reason);
        }

        /// <summary>The command the aircraft flies: the pilot's own, plus the hold's bounded corrections while engaged.</summary>
        public PilotCommand Filter(PilotCommand pilot, HelicopterController aircraft, float dt)
        {
            if (!Engaged) return pilot;
            if (aircraft == null || aircraft.Body == null || aircraft.Crashed) { Disengage("Hover hold off: the flight ended."); return pilot; }
            if (aircraft.Grounded) { Disengage("Hover hold off: on the ground."); return pilot; }
            if (pilot.Cyclic.magnitude > CyclicOverride || Mathf.Abs(pilot.Collective - EngagedCollective) > CollectiveOverride
                || Mathf.Abs(pilot.Yaw) > PedalOverride)
            {
                Disengage("Hover hold off: you have control.");
                return pilot;
            }

            // Horizontal: lean against the drift, as a pilot would, through the attitude the aircraft needs.
            Rigidbody body = aircraft.Body;
            Vector3 velocity = body.linearVelocity;
            float headingRadians = aircraft.Heading * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(headingRadians), 0f, Mathf.Cos(headingRadians));
            var right = new Vector3(forward.z, 0f, -forward.x);
            var drift = new Vector2(Vector3.Dot(velocity, right), Vector3.Dot(velocity, forward));
            Vector2 wanted = Vector2.ClampMagnitude(-drift * 0.8f, 2.5f);
            float pitchTarget = -Mathf.Atan2(wanted.y, FlightMath.StandardGravity) * Mathf.Rad2Deg;
            float bankTarget = Mathf.Atan2(wanted.x, FlightMath.StandardGravity) * Mathf.Rad2Deg;
            Quaternion attitude = body.rotation;
            Vector3 nose = attitude * Vector3.forward, wing = attitude * Vector3.right, up = attitude * Vector3.up;
            float pitch = Mathf.Asin(Mathf.Clamp(nose.y, -1f, 1f)) * Mathf.Rad2Deg;
            float bank = Mathf.Atan2(-wing.y, up.y) * Mathf.Rad2Deg;
            Vector2 correction = Vector2.ClampMagnitude(new Vector2((bankTarget - bank) * 0.07f, (pitch - pitchTarget) * 0.07f), CyclicAuthority);

            // Vertical: hold zero vertical speed around the collective the pilot set.
            float verticalSpeed = velocity.y;
            verticalTrim = Mathf.Clamp(verticalTrim - verticalSpeed * 0.06f * Mathf.Max(0f, dt), -CollectiveAuthority, CollectiveAuthority);
            float collective = EngagedCollective + Mathf.Clamp(-verticalSpeed * 0.05f + verticalTrim, -CollectiveAuthority, CollectiveAuthority);

            // Heading: hold the heading at engagement.
            float pedal = Mathf.Clamp(Mathf.DeltaAngle(aircraft.Heading, heading) * 0.03f, -PedalAuthority, PedalAuthority);
            return new PilotCommand(pilot.Cyclic + correction, pilot.Yaw + pedal, collective).Clamped();
        }
    }
}
