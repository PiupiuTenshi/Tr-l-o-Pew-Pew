using PewPew.Application.Terminal;

namespace PewPew.Desktop;

/// <summary>
/// Windows composition-bound adapter for DEC-014. It deliberately refuses all
/// execution until a separately confirmed provisioner proves the AppContainer,
/// Job Object and Desktop-owned authenticated control boundary are available. It never falls back to the
/// unsandboxed local process runner.
/// </summary>
public sealed class WindowsAppContainerTerminalWorkerHost : IIsolatedTerminalWorkerHost
{
    private readonly IWindowsAppContainerTerminalWorkerProvisioner _provisioner;
    private readonly IAppContainerTerminalWorkloadLauncher _launcher;

    public WindowsAppContainerTerminalWorkerHost(
        IWindowsAppContainerTerminalWorkerProvisioner provisioner,
        IAppContainerTerminalWorkloadLauncher? launcher = null)
    {
        _provisioner = provisioner ?? throw new ArgumentNullException(nameof(provisioner));
        _launcher = launcher ?? new UnavailableAppContainerTerminalWorkloadLauncher();
    }

    public bool ProvidesNetworkIsolation => IsEnforced(GetReadiness());

    public IsolatedTerminalWorkerReadiness GetReadiness()
    {
        var provisioning = _provisioner.GetReadiness();
        var authenticatedControl = _launcher.HasAuthenticatedLocalControl;
        var restrictedJobObject = _launcher.ProvidesRestrictedJobObject;
        return provisioning with
        {
            IsReady = provisioning.HasNetworkDeniedAppContainer &&
                      restrictedJobObject &&
                      authenticatedControl,
            HasRestrictedJobObject = restrictedJobObject,
            HasAuthenticatedLocalControl = authenticatedControl
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

        return _launcher.LaunchAsync(request, onProcessStarted, cancellationToken);
    }

    private static bool IsEnforced(IsolatedTerminalWorkerReadiness readiness) =>
        readiness.IsReady &&
        readiness.HasNetworkDeniedAppContainer &&
        readiness.HasRestrictedJobObject &&
        readiness.HasAuthenticatedLocalControl;
}

internal sealed class UnavailableAppContainerTerminalWorkloadLauncher : IAppContainerTerminalWorkloadLauncher
{
    public bool HasAuthenticatedLocalControl => false;

    public bool ProvidesRestrictedJobObject => false;

    public Task<TerminalProcessRunResult> LaunchAsync(
        TerminalProcessLaunchRequest request,
        Action<int> onProcessStarted,
        CancellationToken cancellationToken) =>
        throw new TerminalProcessBoundaryViolationException("isolated_terminal_workload_launcher_unavailable");
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
