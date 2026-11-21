using System.IO;
using System.Text;
using UnityEngine;

namespace Poker.EditorTools.Audio
{
    /// <summary>Saves samples (-1..1) as a 16-bit PCM mono WAV file.</summary>
    public static class WavWriter
    {
        public static void Write(string path, float[] samples, int sampleRate)
        {
            const short bitsPerSample = 16;
            const short channels = 1;
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            int dataSize = samples.Length * 2;

            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var w = new BinaryWriter(stream);
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataSize);
            w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);                          // PCM header size
            w.Write((short)1);                    // PCM
            w.Write(channels);
            w.Write(sampleRate);
            w.Write(byteRate);
            w.Write((short)(channels * bitsPerSample / 8));
            w.Write(bitsPerSample);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(dataSize);
            foreach (float s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }
    }
}
