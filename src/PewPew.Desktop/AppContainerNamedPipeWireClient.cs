using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using PewPew.Application.Terminal;

namespace PewPew.Desktop;

/// <summary>
/// Creates a one-use named pipe whose ACL admits only the current user and the
/// approved AppContainer SID. It launches a suspended AppContainer worker,
/// assigns it to a kill-on-close Job Object, then relays one bound invocation.
/// </summary>
public sealed class AppContainerNamedPipeWireClient : IIsolatedTerminalWorkerWireClient
{
    private readonly string _workerExecutablePath;

    public AppContainerNamedPipeWireClient(string workerExecutablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerExecutablePath);
        _workerExecutablePath = Path.GetFullPath(workerExecutablePath);
    }

    public bool IsAuthenticated => OperatingSystem.IsWindows() && File.Exists(_workerExecutablePath);

    public bool ProvidesRestrictedJobObject => IsAuthenticated;

    public async Task<TerminalProcessRunResult> SendAsync(
        IsolatedTerminalWorkerInvocation invocation,
        Action<int> onProcessStarted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(onProcessStarted);
        if (!IsAuthenticated)
        {
            throw new TerminalProcessBoundaryViolationException("isolated_terminal_worker_executable_missing");
        }

        var pipeName = $"pewpew-terminal-{Guid.NewGuid():N}";
        await using var pipe = CreatePipe(pipeName);
        using var launched = WindowsAppContainerWorkerLauncher.Launch(_workerExecutablePath, pipeName);
        onProcessStarted(launched.ProcessId);
        using var cancellationRegistration = cancellationToken.Register(launched.TerminateForSecurityStop);
        try
        {
            await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            await NativeMessagingFrameCodec.WriteAsync(
                pipe,
                IsolatedTerminalWorkerWireInvocation.From(invocation),
                cancellationToken).ConfigureAwait(false);
            var response = await NativeMessagingFrameCodec.ReadAsync<IsolatedTerminalWorkerWireResponse>(pipe, cancellationToken).ConfigureAwait(false);
            if (response is null)
            {
                throw new TerminalProcessBoundaryViolationException("isolated_terminal_worker_response_missing");
            }
            if (!string.Equals(response.Status, "completed", StringComparison.Ordinal) || response.Result is null)
            {
                throw new TerminalProcessBoundaryViolationException(
                    string.IsNullOrWhiteSpace(response.ReasonCode) ? "isolated_terminal_worker_response_invalid" : response.ReasonCode);
            }

            return response.Result;
        }
        catch (OperationCanceledException)
        {
            launched.TerminateForSecurityStop();
            throw;
        }
        catch
        {
            launched.TerminateForSecurityStop();
            throw;
        }
    }

    private static NamedPipeServerStream CreatePipe(string pipeName)
    {
        var currentUser = WindowsIdentity.GetCurrent().User
            ?? throw new TerminalProcessBoundaryViolationException("current_user_sid_unavailable");
        var appContainer = AppContainerSid.Derive();
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(appContainer, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            64 * 1024,
            64 * 1024,
            security,
            HandleInheritability.None,
            (PipeAccessRights)0);
    }
}

internal sealed record IsolatedTerminalWorkerWireResponse(string Status, string ReasonCode, TerminalProcessRunResult? Result);

internal static class AppContainerSid
{
    public static SecurityIdentifier Derive()
    {
        var result = DeriveAppContainerSidFromAppContainerName(
            WindowsAppContainerTerminalWorkerProvisioner.ProfileName,
            out var nativeSid);
        if (result != 0 || nativeSid == IntPtr.Zero)
        {
            throw new TerminalProcessBoundaryViolationException("appcontainer_profile_missing");
        }

        try
        {
            return new SecurityIdentifier(nativeSid);
        }
        finally
        {
            FreeSid(nativeSid);
        }
    }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int DeriveAppContainerSidFromAppContainerName(string appContainerName, out IntPtr appContainerSid);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern IntPtr FreeSid(IntPtr sid);
}
