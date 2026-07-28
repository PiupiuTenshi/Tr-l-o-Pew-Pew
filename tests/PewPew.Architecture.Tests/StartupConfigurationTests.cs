using PewPew.SharedKernel.Configuration;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class StartupConfigurationTests
{
    [Fact]
    public void LoadDefaultsToPrivateModeAndLocalDataDirectory()
    {
        var configuration = StartupConfiguration.Load(new Dictionary<string, string?>());

        Assert.True(configuration.PrivateMode);
        Assert.True(Path.IsPathFullyQualified(configuration.LocalDataDirectory));
    }

    [Fact]
    public void LoadRejectsInvalidPrivateModeWithoutEchoingValue()
    {
        var exception = Assert.Throws<StartupConfigurationException>(() => StartupConfiguration.Load(
            new Dictionary<string, string?>
            {
                ["PEWPEW_PRIVATE_MODE"] = "not-a-boolean"
            }));

        Assert.DoesNotContain("not-a-boolean", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadRejectsPlaintextSecretWithoutEchoingValue()
    {
        var exception = Assert.Throws<StartupConfigurationException>(() => StartupConfiguration.Load(
            new Dictionary<string, string?>
            {
                ["PEWPEW_CLOUD_API_KEY"] = "example-secret-value"
            }));

        Assert.DoesNotContain("example-secret-value", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadRejectsEmptyLocalDataDirectory()
    {
        Assert.Throws<StartupConfigurationException>(() => StartupConfiguration.Load(
            new Dictionary<string, string?>
            {
                ["PEWPEW_LOCAL_DATA_DIRECTORY"] = " "
            }));
    }

    [Fact]
    public void LoadAcceptsExplicitSafeSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PewPew", "config-test");
        var configuration = StartupConfiguration.Load(
            new Dictionary<string, string?>
            {
                ["PEWPEW_PRIVATE_MODE"] = "false",
                ["PEWPEW_LOCAL_DATA_DIRECTORY"] = directory
            });

        Assert.False(configuration.PrivateMode);
        Assert.Equal(Path.GetFullPath(directory), configuration.LocalDataDirectory);
    }
}
