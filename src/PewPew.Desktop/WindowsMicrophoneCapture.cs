using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace PewPew.Desktop;

public interface ILocalMicrophoneCapture : IDisposable
{
    bool IsRecording { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task<MemoryStream> StopAsync(CancellationToken cancellationToken);

    Task CancelAsync();
}

/// <summary>
/// Windows microphone capture for an explicit push-to-talk session. Captured WAV
/// data stays in memory, is returned once, and is disposed by the caller.
/// </summary>
public sealed class WindowsMicrophoneCapture : ILocalMicrophoneCapture
{
    private MediaCapture? _capture;
    private InMemoryRandomAccessStream? _recording;
    private bool _isRecording;

    public bool IsRecording => _isRecording;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_isRecording)
        {
            throw new InvalidOperationException("Microphone capture is already active.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var capture = new MediaCapture();
        try
        {
            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Audio
            });

            var recording = new InMemoryRandomAccessStream();
            var encoding = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Auto);
            encoding.Audio = AudioEncodingProperties.CreatePcm(sampleRate: 16_000, channelCount: 1, bitsPerSample: 16);
            await capture.StartRecordToStreamAsync(encoding, recording);
            _capture = capture;
            _recording = recording;
            _isRecording = true;
        }
        catch
        {
            capture.Dispose();
            throw;
        }
    }

    public async Task<MemoryStream> StopAsync(CancellationToken cancellationToken)
    {
        EnsureRecording();
        var capture = _capture!;
        var recording = _recording!;
        _isRecording = false;
        _capture = null;
        _recording = null;

        try
        {
            await capture.StopRecordAsync();
            recording.Seek(0);
            var audio = new MemoryStream();
            using var input = recording.AsStreamForRead();
            await input.CopyToAsync(audio, cancellationToken).ConfigureAwait(false);
            audio.Position = 0;
            return audio;
        }
        finally
        {
            recording.Dispose();
            capture.Dispose();
        }
    }

    public async Task CancelAsync()
    {
        if (!_isRecording)
        {
            return;
        }

        var capture = _capture;
        var recording = _recording;
        _isRecording = false;
        _capture = null;
        _recording = null;

        try
        {
            if (capture is not null)
            {
                await capture.StopRecordAsync();
            }
        }
        finally
        {
            recording?.Dispose();
            capture?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_isRecording)
        {
            _ = CancelAsync();
        }
    }

    private void EnsureRecording()
    {
        if (!_isRecording)
        {
            throw new InvalidOperationException("Microphone capture is not active.");
        }
    }
}
