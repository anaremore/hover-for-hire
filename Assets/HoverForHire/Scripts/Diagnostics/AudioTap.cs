using System;
using System.IO;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>
    /// Diagnostics only: records the audio listener's final mix so automated runs can analyze the sound the player
    /// actually hears. Add it to the GameObject with the AudioListener; nothing is recorded unless Begin is called.
    /// </summary>
    public sealed class AudioTap : MonoBehaviour
    {
        private float[] buffer;
        private int written, channels = 2;
        private volatile bool recording;

        public int SampleRate { get; private set; }
        public int RecordedSamples => written;

        public void Begin(float seconds)
        {
            SampleRate = AudioSettings.outputSampleRate;
            buffer = new float[Mathf.CeilToInt(seconds * SampleRate) * 2];
            written = 0;
            recording = true;
        }

        public void End() => recording = false;

        private void OnAudioFilterRead(float[] data, int dataChannels)
        {
            if (!recording || buffer == null) return;
            channels = dataChannels;
            int count = Math.Min(data.Length, buffer.Length - written);
            Array.Copy(data, 0, buffer, written, count);
            written += count;
            if (written >= buffer.Length) recording = false;
        }

        /// <summary>Write the recording as 16-bit PCM WAV.</summary>
        public void Save(string path)
        {
            recording = false;
            int samples = Math.Max(0, written - written % Math.Max(1, channels));
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                int bytes = samples * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + bytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(SampleRate);
                writer.Write(SampleRate * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(bytes);
                for (int i = 0; i < samples; i++) writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(buffer[i] * 32767f), -32768, 32767));
            }
        }
    }
}
