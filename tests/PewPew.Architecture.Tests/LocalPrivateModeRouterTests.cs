using PewPew.Application.Routing;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class LocalPrivateModeRouterTests
{
    [Theory]
    [InlineData(LocalPrivateMode.Local)]
    [InlineData(LocalPrivateMode.Private)]
    public async Task LocalAndPrivateModeNeverCallCloud(LocalPrivateMode mode)
    {
        var cloud = new CloudSpy();
        var router = new LocalPrivateModeRouter(new LocalFake(), cloud);

        var result = await router.RouteAsync("hello", mode, TestContext.Current.CancellationToken);

        Assert.Equal("local-response", result.Response);
        Assert.Equal(mode, result.Mode);
        Assert.False(result.CloudWasCalled);
        Assert.False(cloud.WasCalled);
    }

    [Fact]
    public async Task CancellationPropagatesWithoutCloudFallback()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cloud = new CloudSpy();
        var router = new LocalPrivateModeRouter(new LocalFake(), cloud);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            router.RouteAsync("hello", LocalPrivateMode.Private, cancellation.Token));

        Assert.False(cloud.WasCalled);
    }

    private sealed class LocalFake : ILocalResponseRoute
    {
        public Task<string> RespondAsync(string input, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("local-response");
        }
    }

    private sealed class CloudSpy : ICloudResponseRoute
    {
        public bool WasCalled { get; private set; }

        public Task<string> RespondAsync(string input, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult("cloud-response");
        }
    }
}
