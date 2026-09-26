using System;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>
    /// Aircraft sound. The synthesized rotor, turbine, gearbox, airflow, skids and horn play from a child source that is
    /// positional outside and 2D and muffled in the cockpit; touchdown and service cues are one-shots. The sound follows
    /// the flight model: rotor RPM sets the blade-pass pitch, load and descent drive blade slap, and the turbine runs
    /// down after an engine failure. No external audio assets or runtime network access.
    /// </summary>
    public sealed class FlightAudio : MonoBehaviour
    {
        public HelicopterController Aircraft;
        /// <summary>Optional camera rig: the cockpit view switches to interior sound.</summary>
        public ChaseCamera CameraRig;
        public float Volume = 0.65f;

        /// <summary>The parameters most recently sent to the synthesizer (diagnostics and tests).</summary>
        public RotorSoundParameters Parameters { get; private set; }

        private RotorSoundSynth synth;
        private AudioSource engineSource, feedback;
        private AudioLowPassFilter muffler;
        private AudioClip touchdown, chime, carrier;
        private float engineSpeed = 0.94f;

        private void Start()
        {
            var sound = new GameObject("Rotor and engine sound");
            sound.transform.SetParent(transform, false);
            // The source plays a silent carrier; the filter below writes the synthesized sound into its buffer.
            engineSource = sound.AddComponent<AudioSource>();
            int rate = AudioSettings.outputSampleRate;
            carrier = AudioClip.Create("Synthesizer carrier", rate, 1, rate, false);
            carrier.SetData(new float[rate], 0);
            engineSource.clip = carrier;
            engineSource.loop = true;
            engineSource.playOnAwake = false;
            engineSource.spatialBlend = 1f;
            engineSource.dopplerLevel = 0.3f;
            engineSource.rolloffMode = AudioRolloffMode.Logarithmic;
            engineSource.minDistance = 25f;
            engineSource.maxDistance = 900f;
            synth = new RotorSoundSynth(rate);
            sound.AddComponent<RotorAudioFilter>().Synth = synth;
            muffler = sound.AddComponent<AudioLowPassFilter>();
            muffler.cutoffFrequency = 22000f;
            engineSource.Play();

            feedback = gameObject.AddComponent<AudioSource>();
            feedback.spatialBlend = 0f;
            feedback.playOnAwake = false;
            touchdown = Tone("Skid touchdown", .28, (t, n) => n * (float)Math.Exp(-t * 18) * .7f);
            chime = Tone("Service completed", .42, (t, n) => (float)(Math.Sin(t * 2 * Math.PI * (t < .2 ? 660 : 880)) * Math.Sin(Math.PI * t / .42) * .25));
            if (Aircraft != null)
            {
                Aircraft.Touchdown += OnTouchdown;
                Aircraft.ResetPerformed += OnReset;
            }
        }

        private void Update()
        {
            if (synth == null || Aircraft == null) return;
            float dt = Time.deltaTime;
            engineSpeed = RotorSoundSynth.StepEngineSpeed(engineSpeed, !Aircraft.EngineFailed && !Aircraft.Crashed, Aircraft.TorqueFraction, dt);
            float weight = Aircraft.Body != null ? Aircraft.Body.mass * FlightMath.StandardGravity : 10000f;
            float load = Aircraft.LiftNewtons / Mathf.Max(1f, weight);
            bool interior = CameraRig != null && CameraRig.IsCockpit;
            Parameters = new RotorSoundParameters
            {
                RotorSpeed01 = Aircraft.RotorSpeed01,
                Load = load,
                Slap = BladeSlap(-Aircraft.AirVelocity.y, Aircraft.HorizontalAirspeed, load, Aircraft.VortexRingSeverity),
                EngineSpeed01 = engineSpeed,
                Airspeed = Aircraft.Airspeed,
                Gust = Aircraft.Turbulence01,
                Scrape = Aircraft.Grounded ? Mathf.Clamp01((Aircraft.GroundSpeed - 0.3f) / 6f) : 0f,
                Horn = Aircraft.Realism.PowerLimits && Aircraft.LowRotorSpeed && !Aircraft.Grounded ? 1f : 0f,
                // Ducked under the Flight Desk rather than silenced.
                Volume = Volume * (Time.timeScale <= 0f ? 0.25f : 1f),
                Interior = interior ? 1f : 0f
            };
            synth.SetTarget(Parameters);
            engineSource.spatialBlend = interior ? 0f : 1f;
            muffler.cutoffFrequency = Mathf.Lerp(muffler.cutoffFrequency, interior ? 2800f : 22000f, 1f - Mathf.Exp(-Mathf.Max(0f, Time.unscaledDeltaTime) * 8f));
        }

        /// <summary>
        /// Blade slap 0..1: blade-vortex interaction in a moderate descent at low to medium airspeed, a vortex ring, and
        /// high rotor load (pull-ups, steep turns, flares).
        /// </summary>
        public static float BladeSlap(float descentThroughAir, float horizontalAirspeed, float load, float vortexRing)
        {
            float descending = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 3f, descentThroughAir));
            float speedBand = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(6f, 14f, horizontalAirspeed))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(26f, 40f, horizontalAirspeed)));
            float interaction = descending * speedBand * Mathf.Clamp01(load * 1.2f);
            return Mathf.Clamp01(interaction + vortexRing * 0.8f + Mathf.Max(0f, load - 1.15f) * 2.5f);
        }

        private void OnReset() => engineSpeed = 0.94f;

        private void OnTouchdown(float speed)
        {
            if (feedback != null) feedback.PlayOneShot(touchdown, Volume * Mathf.Clamp01(speed / 3 + .15f));
        }

        public void ServiceChime()
        {
            if (feedback != null) feedback.PlayOneShot(chime, Volume);
        }

        private static AudioClip Tone(string name, double length, Func<double, float, float> sample)
        {
            const int rate = 22050;
            var data = new float[(int)(length * rate)];
            var rng = new System.Random(17);
            for (int i = 0; i < data.Length; i++) data[i] = sample(i / (double)rate, (float)rng.NextDouble() * 2 - 1);
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (Aircraft != null)
            {
                Aircraft.Touchdown -= OnTouchdown;
                Aircraft.ResetPerformed -= OnReset;
            }
            foreach (AudioClip clip in new[] { touchdown, chime, carrier })
                if (clip != null) Destroy(clip);
        }
    }
}
