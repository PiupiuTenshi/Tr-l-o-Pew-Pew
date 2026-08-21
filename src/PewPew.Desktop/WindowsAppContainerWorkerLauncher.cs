using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using PewPew.Application.Terminal;

namespace PewPew.Desktop;

/// <summary>
/// Starts the dedicated worker suspended in the no-capability AppContainer,
/// assigns it to the restricted Job Object, then resumes it. There is no
/// non-AppContainer launch path in this class.
/// </summary>
public sealed class WindowsAppContainerWorkerLauncher
{
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint CreateSuspended = 0x00000004;
    private const nuint ProcThreadAttributeSecurityCapabilities = 0x00020009;

    public static LaunchedAppContainerWorker Launch(string workerExecutablePath, string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        return LaunchCore(workerExecutablePath, Path.GetDirectoryName(Path.GetFullPath(workerExecutablePath)), [pipeName]);
    }

    /// <summary>
    /// Directly launches one prevalidated workload in the no-capability
    /// AppContainer. Desktop retains the only process handle and Job Object;
    /// no workload IPC endpoint is created.
    /// </summary>
    public static LaunchedAppContainerWorker LaunchDirect(
        string executablePath,
        string workingDirectory,
        IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);
        return LaunchCore(executablePath, workingDirectory, arguments);
    }

    private static LaunchedAppContainerWorker LaunchCore(
        string executablePath,
        string? workingDirectory,
        IReadOnlyList<string> arguments)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("appcontainer_not_supported");
        }

        var fullPath = Path.GetFullPath(executablePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("isolated_terminal_worker_executable_missing", fullPath);
        }

        var sidResult = DeriveAppContainerSidFromAppContainerName(WindowsAppContainerTerminalWorkerProvisioner.ProfileName, out var appContainerSid);
        if (sidResult != 0)
        {
            throw new InvalidOperationException("appcontainer_profile_missing");
        }

        IntPtr attributes = IntPtr.Zero;
        IntPtr securityCapabilities = IntPtr.Zero;
        IntPtr attributeList = IntPtr.Zero;
        SafeFileHandle? processHandle = null;
        SafeFileHandle? threadHandle = null;
        WindowsRestrictedJobObject? job = null;
        try
        {
            var requiredSize = IntPtr.Zero;
            _ = InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref requiredSize);
            attributeList = Marshal.AllocHGlobal(requiredSize);
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref requiredSize))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "appcontainer_attribute_list_create_failed");
            }

            securityCapabilities = Marshal.AllocHGlobal(Marshal.SizeOf<SecurityCapabilities>());
            Marshal.StructureToPtr(new SecurityCapabilities
            {
                AppContainerSid = appContainerSid,
                Capabilities = IntPtr.Zero,
                CapabilityCount = 0,
                Reserved = 0
            }, securityCapabilities, false);
            if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    ProcThreadAttributeSecurityCapabilities,
                    securityCapabilities,
                    (nuint)Marshal.SizeOf<SecurityCapabilities>(),
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "appcontainer_security_capabilities_configure_failed");
            }

            var startup = new StartupInfoEx
            {
                StartupInfo = new StartupInfo { Cb = Marshal.SizeOf<StartupInfoEx>() },
                AttributeList = attributeList
            };
            var commandLine = (BuildCommandLine(fullPath, arguments) + "\0").ToCharArray();
            if (!CreateProcess(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    ExtendedStartupInfoPresent | CreateSuspended,
                    IntPtr.Zero,
                    Path.GetFullPath(workingDirectory ?? throw new InvalidOperationException("appcontainer_working_directory_missing")),
                    ref startup,
                    out var processInformation))
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new TerminalProcessBoundaryViolationException($"appcontainer_worker_launch_failed_{errorCode}");
            }

            processHandle = new SafeFileHandle(processInformation.Process, ownsHandle: true);
            threadHandle = new SafeFileHandle(processInformation.Thread, ownsHandle: true);
            job = WindowsRestrictedJobObject.Create();
            job.AssignProcess(processHandle);
            if (ResumeThread(threadHandle) == uint.MaxValue)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "appcontainer_worker_resume_failed");
            }

            var launched = new LaunchedAppContainerWorker(processInformation.ProcessId, processHandle, job);
            processHandle = null;
            job = null;
            return launched;
        }
        finally
        {
            threadHandle?.Dispose();
            processHandle?.Dispose();
            job?.Dispose();
            if (attributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }
            if (securityCapabilities != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(securityCapabilities);
            }
            if (appContainerSid != IntPtr.Zero)
            {
                FreeSid(appContainerSid);
            }
        }
    }

    private static string BuildCommandLine(string executablePath, IReadOnlyList<string> arguments) =>
        string.Join(" ", new[] { QuoteArgument(executablePath) }.Concat(arguments.Select(QuoteArgument)));

    // Mirrors CommandLineToArgvW quoting semantics. This is process argument
    // construction, never a shell command line.
    private static string QuoteArgument(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '"']) < 0)
        {
            return argument;
        }

        var value = new System.Text.StringBuilder("\"");
        var slashCount = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                slashCount++;
                continue;
            }

            if (character == '"')
            {
                value.Append('\\', slashCount * 2 + 1);
                value.Append(character);
                slashCount = 0;
                continue;
            }

            value.Append('\\', slashCount);
            slashCount = 0;
            value.Append(character);
        }

        value.Append('\\', slashCount * 2);
        value.Append('"');
        return value.ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityCapabilities
    {
        public IntPtr AppContainerSid;
        public IntPtr Capabilities;
        public uint CapabilityCount;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Cb;
        public IntPtr Reserved;
        public string? Desktop;
        public string? Title;
        public uint X;
        public uint Y;
        public uint XSize;
        public uint YSize;
        public uint XCountChars;
        public uint YCountChars;
        public uint FillAttribute;
        public uint Flags;
        public ushort ShowWindow;
        public ushort Reserved2;
        public IntPtr Reserved2Pointer;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public uint ProcessId;
        public uint ThreadId;
    }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int DeriveAppContainerSidFromAppContainerName(string appContainerName, out IntPtr appContainerSid);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern IntPtr FreeSid(IntPtr sid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, nuint attribute, IntPtr value, nuint size, IntPtr previousValue, IntPtr returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(
        string? applicationName,
        char[] commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(SafeFileHandle thread);
}

public sealed class LaunchedAppContainerWorker : IDisposable
{
    private readonly SafeFileHandle _processHandle;
    private readonly WindowsRestrictedJobObject _job;

    internal LaunchedAppContainerWorker(uint processId, SafeFileHandle processHandle, WindowsRestrictedJobObject job)
    {
        ProcessId = checked((int)processId);
        _processHandle = processHandle;
        _job = job;
    }

    public int ProcessId { get; }

    public void TerminateForSecurityStop() => _job.TerminateForSecurityStop();

    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(TerminateForSecurityStop);
        var wait = await Task.Run(
            () => WaitForSingleObject(_processHandle, uint.MaxValue),
            CancellationToken.None).ConfigureAwait(false);
        if (wait != 0)
        {
            throw new TerminalProcessBoundaryViolationException("appcontainer_workload_wait_failed");
        }

        if (!GetExitCodeProcess(_processHandle, out var exitCode))
        {
            throw new TerminalProcessBoundaryViolationException("appcontainer_workload_exit_code_unavailable");
        }
        cancellationToken.ThrowIfCancellationRequested();
        return unchecked((int)exitCode);
    }

    public void Dispose()
    {
        _job.Dispose();
        _processHandle.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeFileHandle handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(SafeFileHandle process, out uint exitCode);
}
