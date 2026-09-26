using UnityEngine;

namespace HoverForHire
{
    /// <summary>Runs the synthesizer on the audio thread for the AudioSource on the same GameObject.</summary>
    public sealed class RotorAudioFilter : MonoBehaviour
    {
        public RotorSoundSynth Synth;

        private void OnAudioFilterRead(float[] data, int channels) => Synth?.Render(data, channels);
    }
}
