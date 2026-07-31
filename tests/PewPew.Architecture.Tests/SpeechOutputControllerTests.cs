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

    [Fact]
    public void SelectingVietnameseVoiceUpdatesTheSelectedVoice()
    {
        var speech = new FakeSpeechOutput(SpeechOutputStatus.Completed);
        var controller = new SpeechOutputController(speech);

        var result = controller.SelectVoice("voice:microsoft-an");

        Assert.Equal(SpeechOutputStatus.Completed, result.Status);
        Assert.Equal("voice:microsoft-an", controller.SelectedVoiceId);
        Assert.Equal("Microsoft An - Vietnamese (Vietnam)", controller.SelectedVoice?.DisplayName);
    }

    [Fact]
    public void UnknownVoiceKeepsTheExistingVoiceAndExposesFailure()
    {
        var speech = new FakeSpeechOutput(SpeechOutputStatus.Completed);
        var controller = new SpeechOutputController(speech);

        var result = controller.SelectVoice("voice:not-installed");

        Assert.Equal(SpeechOutputStatus.Failed, result.Status);
        Assert.Equal("voice:microsoft-an", controller.SelectedVoiceId);
        Assert.Equal(SpeechOutputState.Failed, controller.State);
    }

    [Fact]
    public void InstalledMicrosoftAnIsExposedAlongsideAutomaticLanguageSelection()
    {
        using var speech = new WindowsSpeechOutput();
        var microsoftAn = speech.AvailableVoices.FirstOrDefault(voice =>
            voice.DisplayName.Contains("Microsoft An", StringComparison.OrdinalIgnoreCase));

        if (microsoftAn is null)
        {
            return;
        }

        Assert.Equal(LocalSpeechVoice.AutomaticId, speech.SelectedVoiceId);
        Assert.Equal(SpeechOutputStatus.Completed, speech.SelectVoice(microsoftAn.Id).Status);
    }

    [Fact]
    public void AutomaticSelectionUsesVietnameseForVietnameseTextAndEnglishForEnglishText()
    {
        LocalSpeechVoice[] voices =
        [
            new(LocalSpeechVoice.AutomaticId, "Automatic", string.Empty),
            new("voice:an", "Microsoft An - Vietnamese (Vietnam)", "vi-VN"),
            new("voice:david", "Microsoft David - English (United States)", "en-US")
        ];

        Assert.Equal("voice:an", LocalSpeechVoiceSelector.ResolveVoiceId("Xin chào Pew Pew", voices));
        Assert.Equal("voice:david", LocalSpeechVoiceSelector.ResolveVoiceId("Hello Pew Pew", voices));
    }

    private sealed class FakeSpeechOutput(SpeechOutputStatus result) : ILocalSpeechOutput
    {
        private readonly IReadOnlyList<LocalSpeechVoice> _voices =
        [
            new("voice:microsoft-an", "Microsoft An - Vietnamese (Vietnam)", "vi-VN"),
            new("voice:english", "English test voice", "en-US")
        ];

        public int SpeakCalls { get; private set; }

        public IReadOnlyList<LocalSpeechVoice> AvailableVoices => _voices;

        public string SelectedVoiceId { get; private set; } = "voice:microsoft-an";

        public SpeechOutputResult SelectVoice(string voiceId)
        {
            if (_voices.All(voice => voice.Id != voiceId))
            {
                return new SpeechOutputResult(SpeechOutputStatus.Failed);
            }

            SelectedVoiceId = voiceId;
            return new SpeechOutputResult(SpeechOutputStatus.Completed);
        }

        public Task<SpeechOutputResult> SpeakAsync(string text, CancellationToken cancellationToken)
        {
            SpeakCalls++;
            return Task.FromResult(new SpeechOutputResult(result));
        }

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class BlockingSpeechOutput : ILocalSpeechOutput
    {
        public IReadOnlyList<LocalSpeechVoice> AvailableVoices => Array.Empty<LocalSpeechVoice>();

        public string SelectedVoiceId => string.Empty;

        public SpeechOutputResult SelectVoice(string voiceId) => new(SpeechOutputStatus.Unavailable);

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
