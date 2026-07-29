namespace PewPew.Application.Routing;

public enum LocalPrivateMode
{
    Local,
    Private
}

public interface ILocalResponseRoute
{
    Task<string> RespondAsync(string input, CancellationToken cancellationToken);
}

public interface ICloudResponseRoute
{
    Task<string> RespondAsync(string input, CancellationToken cancellationToken);
}

public sealed record LocalPrivateRouteResult(string Response, LocalPrivateMode Mode, bool CloudWasCalled);

/// <summary>
/// P02's fail-closed routing boundary. No cloud provider is enabled in either
/// Local or Private mode; the injected cloud route exists only to make a
/// network-spy assertion possible in tests.
/// </summary>
public sealed class LocalPrivateModeRouter(ILocalResponseRoute localRoute, ICloudResponseRoute cloudRoute)
{
    public async Task<LocalPrivateRouteResult> RouteAsync(
        string input,
        LocalPrivateMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentNullException.ThrowIfNull(localRoute);
        ArgumentNullException.ThrowIfNull(cloudRoute);

        var response = await localRoute.RespondAsync(input, cancellationToken).ConfigureAwait(false);
        return new LocalPrivateRouteResult(response, mode, CloudWasCalled: false);
    }
}
