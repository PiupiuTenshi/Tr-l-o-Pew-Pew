using PewPew.Application.Speech;
using PewPew.Desktop;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class SpeechOutputControllerTests
{
    [Fact]
    public async Task CompletedSpeechReturnsToTextFallbackReadyState()
    {
        var controller = new SpeechOutputController(new FakeSpeechOutput(SpeechOutputStatus.Completed));

        await controller.SpeakAsync("The response is still shown as text.");

        Assert.Equal(SpeechOutputState.Ready, controller.State);
        Assert.Equal("Speech is ready. Text remains available.", controller.StatusLabel);
    }

    [Fact]
    public async Task UnavailableSpeechKeepsTextFallbackVisible()
    {
        var controller = new SpeechOutputController(new FakeSpeechOutput(SpeechOutputStatus.Unavailable));

        await controller.SpeakAsync("Local voice is unavailable.");

        Assert.Equal(SpeechOutputState.Unavailable, controller.State);
        Assert.Equal("Local speech is unavailable. Text remains available.", controller.StatusLabel);
    }

    [Fact]
    public async Task CancellingSpeechPropagatesToTheAdapter()
    {
        var speech = new BlockingSpeechOutput();
        var controller = new SpeechOutputController(speech);
        var speaking = controller.SpeakAsync("Please stop.");
        await speech.Started.Task;

        await controller.CancelAsync();
        await speaking;

        Assert.Equal(1, speech.CancelCalls);
        Assert.Equal(SpeechOutputState.Cancelled, controller.State);
        Assert.Equal("Speech cancelled. Text remains available.", controller.StatusLabel);
    }

    [Fact]
    public async Task EmptySpeechDoesNotCallTheAdapter()
    {
        var speech = new FakeSpeechOutput(SpeechOutputStatus.Completed);
        var controller = new SpeechOutputController(speech);

        await controller.SpeakAsync(" ");

        Assert.Equal(0, speech.SpeakCalls);
        Assert.Equal(SpeechOutputState.Ready, controller.State);
    }

    [Fact]
    public async Task TimedOutSpeechUsesTheVisibleCancellationFallback()
    {
        var controller = new SpeechOutputController(new BlockingSpeechOutput(), TimeSpan.FromMilliseconds(25));

        await controller.SpeakAsync("This output must not run indefinitely.");

        Assert.Equal(SpeechOutputState.Cancelled, controller.State);
        Assert.Equal("Speech cancelled. Text remains available.", controller.StatusLabel);
    }

    private sealed class FakeSpeechOutput(SpeechOutputStatus result) : ILocalSpeechOutput
    {
        public int SpeakCalls { get; private set; }

        public Task<SpeechOutputResult> SpeakAsync(string text, CancellationToken cancellationToken)
        {
            SpeakCalls++;
            return Task.FromResult(new SpeechOutputResult(result));
        }

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class BlockingSpeechOutput : ILocalSpeechOutput
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CancelCalls { get; private set; }

        public async Task<SpeechOutputResult> SpeakAsync(string text, CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new SpeechOutputResult(SpeechOutputStatus.Completed);
        }

        public Task CancelAsync(CancellationToken cancellationToken)
        {
            CancelCalls++;
            return Task.CompletedTask;
        }
    }
}
