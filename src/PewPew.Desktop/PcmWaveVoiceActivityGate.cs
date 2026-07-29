using System.Buffers.Binary;
using PewPew.Application.Voice;

namespace PewPew.Desktop;

/// <summary>
/// Bounded, in-memory PCM WAV energy gate. It is deliberately a gate only:
/// no samples are retained after the call and no network/provider is involved.
/// </summary>
public sealed class PcmWaveVoiceActivityGate : ILocalVoiceActivityGate
{
    private const int MaximumBytes = 512 * 1024;
    private const short SpeechAmplitude = 800;
    private const int RequiredSpeechSamples = 256;

    public async Task<bool> HasSpeechAsync(Stream waveAudio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(waveAudio);
        if (waveAudio.CanSeek)
        {
            waveAudio.Position = 0;
        }

        var buffer = new byte[MaximumBytes];
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await waveAudio.ReadAsync(buffer.AsMemory(read, buffer.Length - read), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        try
        {
            if (read <= 44 || !IsPcmWave(buffer.AsSpan(0, read)))
            {
                return false;
            }

            var speechSamples = 0;
            for (var offset = 44; offset + sizeof(short) <= read; offset += sizeof(short))
            {
                var sample = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(offset, sizeof(short)));
                if (Math.Abs((int)sample) < SpeechAmplitude)
                {
                    continue;
                }

                if (++speechSamples >= RequiredSpeechSamples)
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            Array.Clear(buffer);
            if (waveAudio.CanSeek)
            {
                waveAudio.Position = 0;
            }
        }
    }

    private static bool IsPcmWave(ReadOnlySpan<byte> wave) =>
        wave[..4].SequenceEqual("RIFF"u8)
        && wave.Slice(8, 4).SequenceEqual("WAVE"u8)
        && wave.Slice(12, 4).SequenceEqual("fmt "u8)
        && BinaryPrimitives.ReadInt16LittleEndian(wave.Slice(20, 2)) == 1;
}
