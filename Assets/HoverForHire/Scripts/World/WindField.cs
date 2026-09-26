using System;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>
    /// Deterministic island wind: a mean wind with a log-law height profile, slow drift in direction and strength,
    /// gusts that vary in time and space, and faster turbulence for buffeting. Seeded, so tests are repeatable.
    /// </summary>
    public sealed class WindField : IWindSource
    {
        /// <summary>Surface roughness length (m): open grass and low scrub.</summary>
        public const float Roughness = 0.1f;
        /// <summary>Meteorological direction the wind blows from, in degrees (0 = from +Z, 90 = from +X).</summary>
        public float FromDegrees;
        /// <summary>Mean wind speed (m/s) at 10 m above the surface.</summary>
        public float ReferenceSpeed;
        [Range(0f, 1f)] public float Gustiness;
        /// <summary>Surface height under a world XZ position; wind strengthens with height above it.</summary>
        public Func<float, float, float> GroundHeight = (x, z) => 0f;
        public float Time { get; private set; }
        private readonly float seed;

        public WindField(int seed = 1) { this.seed = Mathf.Repeat(seed * 37.713f, 997f); }

        public static float SpeedFor(WindStrength strength)
        {
            switch (strength)
            {
                case WindStrength.Light: return 4f;     // ≈ 8 kt
                case WindStrength.Moderate: return 8f;  // ≈ 16 kt
                case WindStrength.Strong: return 13f;   // ≈ 25 kt
                default: return 0f;
            }
        }

        public void Configure(WindStrength strength, float gustiness, float fromDegrees)
        {
            ReferenceSpeed = SpeedFor(strength);
            Gustiness = Mathf.Clamp01(gustiness);
            FromDegrees = fromDegrees;
        }

        public void Advance(float deltaSeconds) => Time += Mathf.Max(0f, deltaSeconds);

        public bool IsCalm => ReferenceSpeed <= 0.01f && Gustiness <= 0.001f;

        /// <summary>Height multiplier of the mean wind: 1 at 10 m, weaker near the surface, stronger aloft.</summary>
        public static float HeightProfile(float heightAboveSurface)
        {
            float h = Mathf.Clamp(heightAboveSurface, 0.5f, 300f);
            return Mathf.Clamp(Mathf.Log(h / Roughness) / Mathf.Log(10f / Roughness), 0.35f, 1.6f);
        }

        public Vector3 WindAt(Vector3 position)
        {
            if (IsCalm) return Vector3.zero;
            float height = position.y - GroundHeight(position.x, position.z);
            float profile = HeightProfile(height);
            // Slow drift over minutes: ±12° in direction, ±15% in strength.
            float direction = (FromDegrees + (Mathf.PerlinNoise(seed, Time * 0.004f) - 0.5f) * 24f) * Mathf.Deg2Rad;
            float swell = 1f + (Mathf.PerlinNoise(seed + 31.7f, Time * 0.006f) - 0.5f) * 0.3f;
            Vector3 mean = -new Vector3(Mathf.Sin(direction), 0f, Mathf.Cos(direction)) * (ReferenceSpeed * swell * profile);
            float gust = Gustiness * Mathf.Max(2f, ReferenceSpeed) * 0.8f;
            Vector3 gusts = new Vector3(Channel(0, position, 0.45f), Channel(1, position, 0.45f) * 0.35f, Channel(2, position, 0.45f)) * gust;
            return mean + gusts;
        }

        public Vector3 TurbulenceAt(Vector3 position)
        {
            if (IsCalm) return Vector3.zero;
            float strength = Gustiness * Mathf.Clamp01(Mathf.Max(2f, ReferenceSpeed) / 10f);
            return new Vector3(Channel(3, position, 2.3f), Channel(4, position, 2.1f), Channel(5, position, 2.7f)) * strength;
        }

        private float Channel(int channel, Vector3 position, float rate)
            => (Mathf.PerlinNoise(seed + channel * 71.31f + Time * rate, position.x * 0.006f + position.z * 0.004f + channel * 13.7f) - 0.5f) * 2f;
    }

    /// <summary>Advances the shared wind with the physics clock and exposes it to scenery such as windsocks.</summary>
    public sealed class WindSystem : MonoBehaviour
    {
        public static WindField Current { get; private set; }
        public WindField Field = new WindField();

        private void OnEnable() => Current = Field;
        private void OnDisable() { if (Current == Field) Current = null; }
        private void FixedUpdate() => Field.Advance(UnityEngine.Time.fixedDeltaTime);
    }

    /// <summary>Points a windsock downwind and lifts it with wind strength; limp in calm air.</summary>
    public sealed class WindsockAnimator : MonoBehaviour
    {
        private float droop = 80f;
        private Quaternion heading = Quaternion.identity;

        private void Update()
        {
            WindField wind = WindSystem.Current;
            Vector3 flow = wind != null ? wind.WindAt(transform.position) : Vector3.zero;
            Vector3 horizontal = new Vector3(flow.x, 0f, flow.z);
            float speed = horizontal.magnitude;
            if (speed > 0.2f) heading = Quaternion.Slerp(heading, Quaternion.LookRotation(horizontal / speed, Vector3.up), 1f - Mathf.Exp(-3f * UnityEngine.Time.deltaTime));
            float target = Mathf.Lerp(80f, 6f, Mathf.Clamp01(speed / 10f));
            droop = Mathf.Lerp(droop, target, 1f - Mathf.Exp(-2f * UnityEngine.Time.deltaTime));
            transform.rotation = heading * Quaternion.Euler(droop, 0f, 0f);
        }
    }
}
