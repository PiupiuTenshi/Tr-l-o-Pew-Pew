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

    public WindowsAppContainerTerminalWorkerHost(IWindowsAppContainerTerminalWorkerProvisioner provisioner)
    {
        _provisioner = provisioner ?? throw new ArgumentNullException(nameof(provisioner));
    }

    public bool ProvidesNetworkIsolation => IsEnforced(GetReadiness());

    public IsolatedTerminalWorkerReadiness GetReadiness() => _provisioner.GetReadiness();

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

        // The provisioned broker is introduced only after profile/ACL setup has
        // explicit user confirmation and an integration task. No ProcessStartInfo
        // fallback is permitted at this boundary.
        throw new TerminalProcessBoundaryViolationException("isolated_terminal_worker_broker_unavailable");
    }

    private static bool IsEnforced(IsolatedTerminalWorkerReadiness readiness) =>
        readiness.IsReady &&
        readiness.HasNetworkDeniedAppContainer &&
        readiness.HasRestrictedJobObject &&
        readiness.HasAuthenticatedLocalIpc;
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
