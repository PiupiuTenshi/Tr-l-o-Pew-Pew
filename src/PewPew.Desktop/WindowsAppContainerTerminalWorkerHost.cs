using PewPew.Application.Terminal;

namespace PewPew.Desktop;

/// <summary>
/// Windows composition-bound adapter for DEC-014. It deliberately refuses all
/// execution until a separately confirmed provisioner proves the AppContainer,
/// Job Object and authenticated IPC are available. It never falls back to the
/// unsandboxed local process runner.
/// </summary>
public sealed class WindowsAppContainerTerminalWorkerHost : IIsolatedTerminalWorkerHost
{
    private readonly IWindowsAppContainerTerminalWorkerProvisioner _provisioner;
    private readonly IIsolatedTerminalWorkerBroker _broker;

    public WindowsAppContainerTerminalWorkerHost(
        IWindowsAppContainerTerminalWorkerProvisioner provisioner,
        IIsolatedTerminalWorkerBroker? broker = null)
    {
        _provisioner = provisioner ?? throw new ArgumentNullException(nameof(provisioner));
        _broker = broker ?? new UnavailableIsolatedTerminalWorkerBroker();
    }

    public bool ProvidesNetworkIsolation => IsEnforced(GetReadiness());

    public IsolatedTerminalWorkerReadiness GetReadiness()
    {
        var provisioning = _provisioner.GetReadiness();
        var authenticatedIpc = provisioning.HasAuthenticatedLocalIpc && _broker.IsAuthenticated;
        return provisioning with
        {
            IsReady = provisioning.HasNetworkDeniedAppContainer &&
                      provisioning.HasRestrictedJobObject &&
                      authenticatedIpc,
            HasAuthenticatedLocalIpc = authenticatedIpc
        };
    }

    public Task<TerminalProcessRunResult> RunAsync(
        TerminalProcessLaunchRequest request,
        Action<int> onProcessStarted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onProcessStarted);

        var readiness = GetReadiness();
        if (!IsEnforced(readiness))
        {
            throw new TerminalProcessBoundaryViolationException("isolated_terminal_worker_unavailable");
        }

        if (request.Binding is null)
        {
            throw new TerminalProcessBoundaryViolationException("isolated_terminal_worker_binding_invalid");
        }

        return _broker.ExecuteAsync(new IsolatedTerminalWorkerInvocation(request.Binding, request), onProcessStarted, cancellationToken);
    }

    private static bool IsEnforced(IsolatedTerminalWorkerReadiness readiness) =>
        readiness.IsReady &&
        readiness.HasNetworkDeniedAppContainer &&
        readiness.HasRestrictedJobObject &&
        readiness.HasAuthenticatedLocalIpc;
}

internal sealed class UnavailableIsolatedTerminalWorkerBroker : IIsolatedTerminalWorkerBroker
{
    public bool IsAuthenticated => false;

    public Task<TerminalProcessRunResult> ExecuteAsync(
        IsolatedTerminalWorkerInvocation invocation,
        Action<int> onProcessStarted,
        CancellationToken cancellationToken) =>
        throw new TerminalProcessBoundaryViolationException("isolated_terminal_worker_broker_unavailable");
}

/// <summary>
/// Desktop-only provisioner boundary. Implementations may create or remove a
/// per-user AppContainer profile and minimal directory ACLs only after an
/// explicit confirmation at the system side-effect boundary.
/// </summary>
public interface IWindowsAppContainerTerminalWorkerProvisioner
{
    IsolatedTerminalWorkerReadiness GetReadiness();
}
