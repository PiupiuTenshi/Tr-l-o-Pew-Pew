namespace PewPew.Desktop;

public enum AudioSessionStatus
{
    Idle,
    Listening,
    Stopped,
    Cancelled
}

/// <summary>
/// Owns the explicit, in-memory boundary for one push-to-talk session.
/// It intentionally does not access a microphone or a speech provider.
/// </summary>
public sealed class AudioSessionState
{
    private byte[] _temporaryBuffer = [];

    public AudioSessionStatus Status { get; private set; } = AudioSessionStatus.Idle;

    public bool IsListening => Status == AudioSessionStatus.Listening;

    public int TemporaryBufferLength => _temporaryBuffer.Length;

    public string ConsentLabel => Status switch
    {
        AudioSessionStatus.Listening => "Listening — explicit push-to-talk consent is active.",
        AudioSessionStatus.Stopped => "Stopped — temporary audio buffer cleared.",
        AudioSessionStatus.Cancelled => "Cancelled — temporary audio buffer cleared.",
        _ => "Not listening — microphone capture is not enabled."
    };

    public void Start()
    {
        if (IsListening)
        {
            throw new InvalidOperationException("An audio session is already listening.");
        }

        ClearTemporaryBuffer();
        Status = AudioSessionStatus.Listening;
    }

    public void AppendTemporaryAudio(ReadOnlySpan<byte> audio)
    {
        if (!IsListening)
        {
            throw new InvalidOperationException("Temporary audio can only be buffered while listening.");
        }

        if (audio.IsEmpty)
        {
            return;
        }

        var updated = new byte[_temporaryBuffer.Length + audio.Length];
        _temporaryBuffer.CopyTo(updated, 0);
        audio.CopyTo(updated.AsSpan(_temporaryBuffer.Length));
        ClearTemporaryBuffer();
        _temporaryBuffer = updated;
    }

    public void Stop()
    {
        EnsureListening();
        ClearTemporaryBuffer();
        Status = AudioSessionStatus.Stopped;
    }

    public void Cancel()
    {
        EnsureListening();
        ClearTemporaryBuffer();
        Status = AudioSessionStatus.Cancelled;
    }

    private void EnsureListening()
    {
        if (!IsListening)
        {
            throw new InvalidOperationException("No listening audio session is active.");
        }
    }

    private void ClearTemporaryBuffer()
    {
        Array.Clear(_temporaryBuffer);
        _temporaryBuffer = [];
    }
}
