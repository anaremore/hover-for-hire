using System;
using NUnit.Framework;

namespace HoverForHire.Tests
{
    /// <summary>The rotor sound synthesizer: pitch follows rotor speed, slap and airflow shape the spectrum, output stays bounded.</summary>
    public sealed class RotorSoundTests
    {
        private const float Rate = 48000f;
        private const double BladePassHz = RotorSoundSynth.BladeCount * RotorSoundSynth.GovernedRotorHz;

        private static RotorSoundParameters Hover(float rotor = 1f) => new RotorSoundParameters
            { RotorSpeed01 = rotor, Load = 1f, EngineSpeed01 = 1f, Volume = 1f };

        /// <summary>Render in 1024-frame buffers, as the audio thread does, after one buffer to settle the parameter ramp.</summary>
        private static float[] Render(RotorSoundParameters parameters, float seconds = 1f, uint seed = 7)
        {
            var synth = new RotorSoundSynth(Rate, seed);
            synth.SetTarget(parameters);
            synth.Render(new float[1024], 1);
            var output = new float[(int)(Rate * seconds)];
            var buffer = new float[1024];
            for (int offset = 0; offset < output.Length; offset += buffer.Length)
            {
                Array.Clear(buffer, 0, buffer.Length);
                synth.Render(buffer, 1);
                Array.Copy(buffer, 0, output, offset, Math.Min(buffer.Length, output.Length - offset));
            }
            return output;
        }

        /// <summary>Goertzel power at one frequency.</summary>
        private static double Power(float[] x, double frequency)
        {
            double w = 2 * Math.PI * frequency / Rate, c = 2 * Math.Cos(w), s1 = 0, s2 = 0;
            foreach (float v in x)
            {
                double s0 = v + c * s1 - s2;
                s2 = s1;
                s1 = s0;
            }
            return s1 * s1 + s2 * s2 - c * s1 * s2;
        }

        private static double BandPower(float[] x, double low, double high)
        {
            double total = 0;
            for (double f = low; f <= high; f += 125) total += Power(x, f);
            return total;
        }

        private static double Rms(float[] x)
        {
            double sum = 0;
            foreach (float v in x) sum += v * v;
            return Math.Sqrt(sum / x.Length);
        }

        [Test]
        public void BladePassToneFollowsRotorSpeed()
        {
            float[] governed = Render(Hover());
            Assert.That(Power(governed, BladePassHz), Is.GreaterThan(100 * Power(governed, BladePassHz * 1.37)),
                "Four blades at 395 rpm: a strong 26 Hz blade-pass line.");
            float[] drooped = Render(Hover(0.9f));
            Assert.That(Power(drooped, BladePassHz * 0.9), Is.GreaterThan(10 * Power(drooped, BladePassHz)),
                "At 90% rotor speed the blade-pass line moves down with it.");
        }

        [Test]
        public void BladeSlapAddsImpulsiveHighFrequencyEnergy()
        {
            RotorSoundParameters slapping = Hover();
            slapping.Slap = 1f;
            Assert.That(BandPower(Render(slapping), 1000, 4000), Is.GreaterThan(1.8 * BandPower(Render(Hover()), 1000, 4000)),
                "A slapping rotor cracks: markedly more energy from 1 to 4 kHz.");
        }

        [Test]
        public void AirflowNoiseGrowsWithAirspeed()
        {
            var still = new RotorSoundParameters { Volume = 1f };
            var fast = new RotorSoundParameters { Volume = 1f, Airspeed = 40f };
            Assert.That(Rms(Render(fast)), Is.GreaterThan(0.05));
            Assert.That(Rms(Render(still)), Is.LessThan(1e-4), "No rotor, no engine, no airflow: silence.");
        }

        [Test]
        public void TurbineRunsDownAfterAnEngineFailureAndSpoolsBackUp()
        {
            float speed = 0.94f;
            for (int i = 0; i < 300; i++) speed = RotorSoundSynth.StepEngineSpeed(speed, false, 0f, 0.02f);
            Assert.That(speed, Is.LessThan(0.15f), "Six seconds after a failure the gas generator has nearly stopped.");
            for (int i = 0; i < 150; i++) speed = RotorSoundSynth.StepEngineSpeed(speed, true, 0.5f, 0.02f);
            Assert.That(speed, Is.GreaterThan(0.9f));
            var dead = Hover();
            dead.EngineSpeed01 = 0f;
            Assert.That(Power(Render(dead), RotorSoundSynth.TurbineWhineHz), Is.LessThan(0.01 * Power(Render(Hover()), RotorSoundSynth.TurbineWhineHz)),
                "No turbine whine with the engine stopped.");
        }

        [Test]
        public void OutputIsBoundedFiniteDeterministicAndSilentAtZeroVolume()
        {
            var extreme = new RotorSoundParameters
            {
                RotorSpeed01 = 1.4f, Load = 5f, Slap = 1f, EngineSpeed01 = 1f, Airspeed = 200f, Gust = 1f, Scrape = 1f, Horn = 1f, Volume = 1f, Interior = 1f
            };
            foreach (float v in Render(extreme))
            {
                Assert.That(float.IsNaN(v) || float.IsInfinity(v), Is.False);
                Assert.That(Math.Abs(v), Is.LessThan(1f));
            }
            extreme.Volume = 0f;
            Assert.That(Rms(Render(extreme)), Is.Zero);
            Assert.That(Render(Hover(), 0.2f), Is.EqualTo(Render(Hover(), 0.2f)), "Same seed, same sound.");
            var stereo = new float[2048];
            var synth = new RotorSoundSynth(Rate);
            synth.SetTarget(Hover());
            synth.Render(stereo, 2);
            for (int i = 0; i < stereo.Length; i += 2) Assert.That(stereo[i], Is.EqualTo(stereo[i + 1]), "Every channel carries the same mono signal.");
        }

        [Test]
        public void BladeSlapComesFromDescentAtSpeedVortexRingAndHighLoad()
        {
            Assert.That(FlightAudio.BladeSlap(2.5f, 20f, 1f, 0f), Is.GreaterThan(0.5f), "Descending approach: blade-vortex interaction.");
            Assert.That(FlightAudio.BladeSlap(0f, 20f, 1f, 0f), Is.Zero, "Level cruise is quiet.");
            Assert.That(FlightAudio.BladeSlap(2.5f, 0f, 1f, 0f), Is.Zero, "Vertical descent at zero airspeed has no interaction.");
            Assert.That(FlightAudio.BladeSlap(0f, 0f, 1f, 0.8f), Is.GreaterThan(0.6f), "The vortex ring buffets.");
            Assert.That(FlightAudio.BladeSlap(0f, 30f, 1.4f, 0f), Is.GreaterThan(0.5f), "Pulling high load slaps.");
        }
    }
}
