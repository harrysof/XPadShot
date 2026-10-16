using System.IO;
using System.Media;
using System.Text;

namespace XPadShot.Services;

/// <summary>
/// Plays a short synthesized camera-shutter click on capture. The WAV is
/// generated in memory, so there is no asset file and no licensing question.
/// </summary>
public sealed class SoundService
{
    private readonly SoundPlayer? _player;
    private readonly MemoryStream? _stream;

    public SoundService()
    {
        try
        {
            _stream = BuildShutterWav();
            _player = new SoundPlayer(_stream);
            _player.Load();
        }
        catch
        {
            _player = null;
            _stream = null;
        }
    }

    public void PlayShutter()
    {
        if (_player is null || _stream is null)
            return;

        try
        {
            _player.Play();
        }
        catch
        {
            // sound is best-effort
        }
    }

    private static MemoryStream BuildShutterWav()
    {
        const int sampleRate = 22050;
        const double durationSeconds = 0.16;

        int length = (int)(sampleRate * durationSeconds);
        var data = new double[length];
        var rng = new Random(7);

        void MixClick(int startMs, double amplitude, double frequency, double decayMs, double noisePortion)
        {
            int start = startMs * sampleRate / 1000;

            for (int i = 0; start + i < length; i++)
            {
                double t = i / (double)sampleRate;
                double envelope = Math.Exp(-t / (decayMs / 1000.0));
                if (envelope < 0.0008)
                    break;

                double attack = Math.Min(1.0, t / 0.0006);
                double noise = (rng.NextDouble() * 2) - 1;
                double tone = (Math.Sin(2 * Math.PI * frequency * t) * 0.55)
                            + (Math.Sin(2 * Math.PI * frequency * 1.9 * t) * 0.25);

                data[start + i] += amplitude * attack * envelope * ((noisePortion * noise) + ((1 - noisePortion) * tone));
            }
        }

        MixClick(0, 0.55, 1650, 2.8, 0.55);   // shutter: first click
        MixClick(48, 0.50, 1050, 3.6, 0.45);  // shutter: second click

        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        int dataBytes = length * 2;
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataBytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);                       // fmt chunk size
        writer.Write((short)1);                 // PCM
        writer.Write((short)1);                 // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);           // byte rate
        writer.Write((short)2);                 // block align
        writer.Write((short)16);                // bits per sample
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);

        foreach (double sample in data)
        {
            short value = (short)(Math.Clamp(sample, -1.0, 1.0) * short.MaxValue * 0.9);
            writer.Write(value);
        }

        writer.Flush();
        stream.Position = 0;
        return stream;
    }
}
