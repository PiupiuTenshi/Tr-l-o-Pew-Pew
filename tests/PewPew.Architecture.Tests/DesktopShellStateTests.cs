using PewPew.Desktop;
using PewPew.SharedKernel.Configuration;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class DesktopShellStateTests
{
    [Fact]
    public void PrivateModeIsVisibleWhenItIsConfigured()
    {
        var state = new DesktopShellState(new StartupConfiguration(true, "C:\\PewPew"));

        Assert.Equal("Private Mode", state.ModeLabel);
        Assert.Equal(DesktopShellStatus.Ready, state.Status);
        Assert.Equal("Ready for text input", state.StatusLabel);
    }

    [Fact]
    public void SubmitTextRecordsInputWithoutDispatchingAnAction()
    {
        var state = new DesktopShellState(new StartupConfiguration(false, "C:\\PewPew"));

        state.SubmitText("Open the desktop shell");

        Assert.Equal("Local Mode", state.ModeLabel);
        Assert.Equal(DesktopShellStatus.Understanding, state.Status);
        Assert.Equal("Understanding text input", state.StatusLabel);
        Assert.Equal("Text received. Local action routing is not enabled yet.", state.ResponseLabel);
    }

    [Fact]
    public void BlankTextDoesNotCreateAnAction()
    {
        var state = new DesktopShellState(new StartupConfiguration(true, "C:\\PewPew"));

        state.SubmitText(" ");

        Assert.Equal(DesktopShellStatus.InputRequired, state.Status);
        Assert.Equal("Enter text before submitting", state.StatusLabel);
        Assert.Equal("No action was requested.", state.ResponseLabel);
    }
}
