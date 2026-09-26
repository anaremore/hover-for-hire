using System;

namespace HoverForHire
{
    /// <summary>What the synthesizer should sound like now. Written by the main thread once per frame.</summary>
    public struct RotorSoundParameters
    {
        /// <summary>Rotor speed as a fraction of governed speed (NR).</summary>
        public float RotorSpeed01;
        /// <summary>Rotor thrust relative to the aircraft's weight: about 1 in a hover, more in a climb or a turn.</summary>
        public float Load;
        /// <summary>0..1 blade slap: blade-vortex interaction in descents, a vortex ring, or high load.</summary>
        public float Slap;
        /// <summary>Gas generator speed as a fraction of flight idle-to-maximum (0 is stopped).</summary>
        public float EngineSpeed01;
        /// <summary>Speed through the air, m/s.</summary>
        public float Airspeed;
        /// <summary>0..1 turbulence buffet.</summary>
        public float Gust;
        /// <summary>0..1 skids sliding on the ground.</summary>
        public float Scrape;
        /// <summary>1 while the low-rotor-RPM horn sounds.</summary>
        public float Horn;
        /// <summary>Master level, 0..1.</summary>
        public float Volume;
        /// <summary>1 in the cockpit (more low rotor and gearbox, less turbine), 0 outside.</summary>
        public float Interior;
    }

    /// <summary>
    /// Real-time helicopter sound from first principles: a four-blade rotor's blade-pass pulses and swish, impulsive
    /// blade slap, turbine and gearbox whine, airflow noise, skid scrape and the low-rotor horn. Runs on the audio
    /// thread without allocating; parameters ramp across each buffer so changes never click.
    /// </summary>
    public sealed class RotorSoundSynth
    {
        public const int BladeCount = 4;
        public const float GovernedRotorHz = 395f / 60f;
        public const float TurbineWhineHz = 4100f, GearMeshHz = 1180f, HornHz = 520f, HornPulseHz = 2.5f;
        private const int SineTableSize = 4096;
        private static readonly float[] SineTable = BuildSineTable();

        private readonly float sampleRate;
        private readonly object gate = new object();
        private RotorSoundParameters target, current;
        private double bladePhase, turbinePhase, turbinePhase2, gearPhase, hornPhase, hornGatePhase;
        private uint noiseState;
        private float thumpFilter, swishHigh, swishLow, windFilter1, windFilter2, scrapeHigh, scrapeLow, hissFilter;

        public RotorSoundSynth(float sampleRate, uint seed = 0x9E3779B9u)
        {
            this.sampleRate = Math.Max(8000f, sampleRate);
            noiseState = seed == 0 ? 1u : seed;
        }

        public float SampleRate => sampleRate;

        /// <summary>Main thread: the next buffer ramps from the current state to these values.</summary>
        public void SetTarget(RotorSoundParameters parameters)
        {
            lock (gate) target = parameters;
        }

        /// <summary>Audio thread: add the synthesized sound to every channel of an interleaved buffer.</summary>
        public void Render(float[] data, int channels)
        {
            if (data == null || channels <= 0) return;
            RotorSoundParameters goal;
            lock (gate) goal = target;
            RotorSoundParameters start = current;
            int frames = data.Length / channels;
            if (frames == 0) return;
            float step = 1f / frames;
            for (int frame = 0; frame < frames; frame++)
            {
                float t = (frame + 1) * step;
                float sample = Sample(
                    Lerp(start.RotorSpeed01, goal.RotorSpeed01, t), Lerp(start.Load, goal.Load, t), Lerp(start.Slap, goal.Slap, t),
                    Lerp(start.EngineSpeed01, goal.EngineSpeed01, t), Lerp(start.Airspeed, goal.Airspeed, t), Lerp(start.Gust, goal.Gust, t),
                    Lerp(start.Scrape, goal.Scrape, t), Lerp(start.Horn, goal.Horn, t), Lerp(start.Interior, goal.Interior, t));
                sample = SoftClip(sample * Clamp01(Lerp(start.Volume, goal.Volume, t)));
                int offset = frame * channels;
                for (int c = 0; c < channels; c++) data[offset + c] += sample;
            }
            current = goal;
        }

        private float Sample(float rotor, float load, float slap, float engine, float airspeed, float gust, float scrape, float horn, float interior)
        {
            rotor = Math.Max(0f, rotor);
            float white = Noise();
            float loadFactor = Clamp(load, 0f, 1.6f);
            float rotorLevel = rotor * rotor * (0.35f + 0.65f * Clamp01(loadFactor));

            // Blade passes: a sharp pressure pulse per blade, low-passed into the "whop", with band-passed swish.
            bladePhase += BladeCount * GovernedRotorHz * rotor / sampleRate;
            if (bladePhase >= 1.0) bladePhase -= Math.Floor(bladePhase);
            float remaining = 1f - (float)bladePhase;
            float r2 = remaining * remaining, r4 = r2 * r2, r8 = r4 * r4;
            thumpFilter += (r8 - 1f / 9f - thumpFilter) * Coefficient(160f + 120f * loadFactor);
            float thump = thumpFilter * rotorLevel * (1.1f + 0.8f * interior);

            swishHigh += (white - swishHigh) * Coefficient(1900f);
            swishLow += (swishHigh - swishLow) * Coefficient(420f);
            float swish = (swishHigh - swishLow) * remaining * r2 * rotorLevel * (1.6f - 0.9f * interior);

            // Blade slap: the tip vortex strikes the following blade, a crack at every blade pass.
            float r16 = r8 * r8, crack = r16 * r16;
            float slapSound = (crack * 1.4f + white * r16 * 2.4f + (swishHigh - swishLow) * r8 * 0.8f)
                * Clamp01(slap) * rotor * (1f - 0.5f * interior);

            // Turbine: two compressor tones and a hiss, falling in pitch as the gas generator spools down.
            float engineLevel = Clamp01(engine);
            turbinePhase += TurbineWhineHz * engineLevel / sampleRate;
            turbinePhase2 += TurbineWhineHz * 1.5f * engineLevel / sampleRate;
            if (turbinePhase >= 1.0) turbinePhase -= Math.Floor(turbinePhase);
            if (turbinePhase2 >= 1.0) turbinePhase2 -= Math.Floor(turbinePhase2);
            hissFilter += (white - hissFilter) * Coefficient(3000f);
            float turbine = (Sine(turbinePhase) * 0.6f + Sine(turbinePhase2) * 0.4f) * engineLevel * engineLevel * (0.06f - 0.04f * interior)
                + (white - hissFilter) * engineLevel * 0.02f;

            // Main gearbox mesh: louder inside the cabin.
            gearPhase += GearMeshHz * rotor / sampleRate;
            if (gearPhase >= 1.0) gearPhase -= Math.Floor(gearPhase);
            float gear = Sine(gearPhase) * rotor * (0.012f + 0.03f * interior) * (0.6f + 0.4f * Clamp01(loadFactor));

            // Airflow: low-passed noise that brightens and grows with airspeed, plus turbulence.
            float windCutoff = 220f + 32f * Math.Max(0f, airspeed);
            float windCoefficient = Coefficient(windCutoff);
            windFilter1 += (white - windFilter1) * windCoefficient;
            windFilter2 += (windFilter1 - windFilter2) * windCoefficient;
            float windLevel = Clamp01(airspeed * airspeed / 2025f + Clamp01(gust) * 0.3f) * (0.9f - 0.4f * interior);
            float wind = windFilter2 * windLevel * 1.8f;

            // Skids scraping over the surface.
            scrapeHigh += (white - scrapeHigh) * Coefficient(3200f);
            scrapeLow += (scrapeHigh - scrapeLow) * Coefficient(900f);
            float scrapeSound = (scrapeHigh - scrapeLow) * Clamp01(scrape) * 0.9f;

            // Low rotor RPM horn: a pulsed tone with odd harmonics.
            float hornSound = 0f;
            if (horn > 0.001f)
            {
                hornPhase += HornHz / sampleRate;
                hornGatePhase += HornPulseHz / sampleRate;
                if (hornPhase >= 1.0) hornPhase -= Math.Floor(hornPhase);
                if (hornGatePhase >= 1.0) hornGatePhase -= Math.Floor(hornGatePhase);
                float pulse = hornGatePhase < 0.55 ? 1f : 0f;
                hornSound = (Sine(hornPhase) + Sine(hornPhase * 3.0) * 0.33f + Sine(hornPhase * 5.0) * 0.2f) * pulse * Clamp01(horn) * 0.09f;
            }
            return thump + swish + slapSound + turbine + gear + wind + scrapeSound + hornSound;
        }

        /// <summary>
        /// Gas generator speed after one step: it settles near the governed setting while the engine runs, runs down over
        /// several seconds after a failure, and a restored engine spools back up quickly.
        /// </summary>
        public static float StepEngineSpeed(float engineSpeed01, bool running, float torqueFraction, float deltaSeconds)
        {
            float dt = Math.Max(0f, deltaSeconds);
            float goal = running ? 0.94f + 0.06f * Clamp01(torqueFraction) : 0f;
            float timeConstant = running ? (goal > engineSpeed01 ? 0.8f : 0.5f) : 2.4f;
            return engineSpeed01 + (goal - engineSpeed01) * (1f - (float)Math.Exp(-dt / timeConstant));
        }

        private float Coefficient(float cutoffHz) => 1f - (float)Math.Exp(-2.0 * Math.PI * cutoffHz / sampleRate);

        private float Noise()
        {
            // xorshift32: fast, allocation-free and deterministic per seed.
            uint x = noiseState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            noiseState = x;
            return (x & 0xFFFFFF) / 8388607.5f - 1f;
        }

        private static float Sine(double phase)
        {
            double wrapped = phase - Math.Floor(phase);
            return SineTable[(int)(wrapped * SineTableSize) & (SineTableSize - 1)];
        }

        private static float[] BuildSineTable()
        {
            var table = new float[SineTableSize];
            for (int i = 0; i < SineTableSize; i++) table[i] = (float)Math.Sin(2.0 * Math.PI * i / SineTableSize);
            return table;
        }

        /// <summary>Smooth limiter that keeps the mix inside ±1 without hard clipping.</summary>
        private static float SoftClip(float x) => x / (1f + Math.Abs(x));

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}
