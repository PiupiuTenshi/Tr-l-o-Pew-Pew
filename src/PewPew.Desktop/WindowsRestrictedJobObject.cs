using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PewPew.Desktop;

/// <summary>
/// Owns an isolated worker process tree. Windows terminates all associated
/// processes when the final handle closes; callers may also terminate it
/// explicitly for cancellation, revoke or quarantine.
/// </summary>
public sealed class WindowsRestrictedJobObject : IDisposable
{
    private const uint JobObjectExtendedLimitInformationClass = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private SafeFileHandle? _handle;

    private WindowsRestrictedJobObject(SafeFileHandle handle)
    {
        _handle = handle;
    }

    public bool IsActive => _handle is { IsInvalid: false, IsClosed: false };

    public static WindowsRestrictedJobObject Create()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("job_object_not_supported");
        }

        var handle = CreateJobObject(IntPtr.Zero, null);
        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "job_object_create_failed");
        }

        var limits = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                LimitFlags = JobObjectLimitKillOnJobClose
            }
        };
        if (!SetInformationJobObject(
                handle,
                JobObjectExtendedLimitInformationClass,
                ref limits,
                (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
        {
            handle.Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error(), "job_object_limit_configure_failed");
        }

        return new WindowsRestrictedJobObject(handle);
    }

    public void AssignProcess(SafeHandle processHandle)
    {
        ArgumentNullException.ThrowIfNull(processHandle);
        var handle = GetHandle();
        if (processHandle.IsInvalid || processHandle.IsClosed || !AssignProcessToJobObject(handle, processHandle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "job_object_assign_process_failed");
        }
    }

    public void TerminateForSecurityStop()
    {
        var handle = GetHandle();
        if (!TerminateJobObject(handle, 1))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "job_object_terminate_failed");
        }
    }

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, null);
        handle?.Dispose();
    }

    private SafeFileHandle GetHandle() =>
        _handle is { IsInvalid: false, IsClosed: false } handle
            ? handle
            : throw new ObjectDisposedException(nameof(WindowsRestrictedJobObject), "job_object_unavailable");

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        SafeFileHandle job,
        uint jobObjectInformationClass,
        ref JobObjectExtendedLimitInformation jobObjectInformation,
        uint jobObjectInformationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, SafeHandle process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);
}
