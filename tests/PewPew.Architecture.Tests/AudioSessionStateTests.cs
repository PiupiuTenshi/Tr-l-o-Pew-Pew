using PewPew.Desktop;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class AudioSessionStateTests
{
    [Fact]
    public void StartCreatesAnExplicitListeningSession()
    {
        var session = new AudioSessionState();

        session.Start();

        Assert.Equal(AudioSessionStatus.Listening, session.Status);
        Assert.True(session.IsListening);
        Assert.Equal("Listening — explicit push-to-talk consent is active.", session.ConsentLabel);
    }

    [Fact]
    public void StopClearsTheTemporaryAudioBuffer()
    {
        var session = ListeningSession();
        session.AppendTemporaryAudio([1, 2, 3]);

        session.Stop();

        Assert.Equal(AudioSessionStatus.Stopped, session.Status);
        Assert.False(session.IsListening);
        Assert.Equal(0, session.TemporaryBufferLength);
        Assert.Equal("Stopped — temporary audio buffer cleared.", session.ConsentLabel);
    }

    [Fact]
    public void CancelClearsTheTemporaryAudioBuffer()
    {
        var session = ListeningSession();
        session.AppendTemporaryAudio([1, 2, 3]);

        session.Cancel();

        Assert.Equal(AudioSessionStatus.Cancelled, session.Status);
        Assert.False(session.IsListening);
        Assert.Equal(0, session.TemporaryBufferLength);
        Assert.Equal("Cancelled — temporary audio buffer cleared.", session.ConsentLabel);
    }

    [Fact]
    public void BufferingIsDeniedOutsideAnExplicitListeningSession()
    {
        var session = new AudioSessionState();

        Assert.Throws<InvalidOperationException>(() => session.AppendTemporaryAudio([1]));
        Assert.Equal(0, session.TemporaryBufferLength);
    }

    [Fact]
    public void ANewExplicitStartDoesNotReusePriorTemporaryAudio()
    {
        var session = ListeningSession();
        session.AppendTemporaryAudio([1, 2, 3]);
        session.Stop();

        session.Start();

        Assert.Equal(AudioSessionStatus.Listening, session.Status);
        Assert.Equal(0, session.TemporaryBufferLength);
    }

    private static AudioSessionState ListeningSession()
    {
        var session = new AudioSessionState();
        session.Start();
        return session;
    }
}
