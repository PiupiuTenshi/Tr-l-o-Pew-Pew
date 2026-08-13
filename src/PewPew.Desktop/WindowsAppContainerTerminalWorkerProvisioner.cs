using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using PewPew.Application.Terminal;

namespace PewPew.Desktop;

/// <summary>
/// Performs the narrowly scoped per-user provisioning approved by DEC-014.
/// The AppContainer has no capabilities, so Windows grants it no network
/// capability. The only additional access granted is Modify on its dedicated
/// worker directory.
/// </summary>
public sealed class WindowsAppContainerTerminalWorkerProvisioner : IWindowsAppContainerTerminalWorkerProvisioner
{
    public const string ProfileName = "PewPew.TerminalWorker";

    public static string DefaultWorkerDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PewPew",
        "TerminalWorker");

    private readonly string _workerDirectory;

    public WindowsAppContainerTerminalWorkerProvisioner(string? workerDirectory = null)
    {
        _workerDirectory = Path.GetFullPath(workerDirectory ?? DefaultWorkerDirectory);
    }

    public IsolatedTerminalWorkerReadiness GetReadiness()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new(false, "appcontainer_not_supported", false, false, false);
        }

        using var profile = TryGetExistingProfileSid();
        if (profile is null)
        {
            return new(false, "appcontainer_profile_missing", false, false, false);
        }

        if (!Directory.Exists(_workerDirectory) || !HasWorkerDirectoryAccess(profile.Sid))
        {
            return new(false, "appcontainer_worker_directory_acl_missing", true, false, false);
        }

        // The current task has not yet installed the broker or Job Object host.
        // Therefore readiness stays false and the execution boundary remains closed.
        return new(false, "isolated_terminal_worker_broker_unavailable", true, false, false);
    }

    /// <summary>
    /// Creates the no-capability AppContainer profile and grants its SID Modify
    /// rights only within the approved worker directory. This does not make the
    /// terminal host ready because it does not create a broker or Job Object.
    /// </summary>
    public AppContainerTerminalWorkerProvisioningResult Provision()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("appcontainer_not_supported");
        }

        var result = CreateAppContainerProfile(
            ProfileName,
            "PewPew Terminal Worker",
            "PewPew restricted terminal worker",
            IntPtr.Zero,
            0,
            out var sid);
        var profileCreated = result == 0;
        if (result != 0 && unchecked((uint)result) != 0x800700B7)
        {
            throw new InvalidOperationException($"appcontainer_profile_create_failed_0x{result:X8}");
        }

        NativeAppContainerSidHandle? profile = profileCreated
            ? new NativeAppContainerSidHandle(sid)
            : TryGetExistingProfileSid();
        if (profile is null)
        {
            throw new InvalidOperationException("appcontainer_profile_create_verification_failed");
        }

        using (profile)
        {
            Directory.CreateDirectory(_workerDirectory);
            GrantWorkerDirectoryAccess(profile.Sid);
        }

        return new AppContainerTerminalWorkerProvisioningResult(
            ProfileName,
            _workerDirectory,
            profileCreated,
            GetReadiness());
    }

    private static NativeAppContainerSidHandle? TryGetExistingProfileSid()
    {
        var result = DeriveAppContainerSidFromAppContainerName(ProfileName, out var sid);
        if (result != 0 || sid == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var folderResult = GetAppContainerFolderPath(new SecurityIdentifier(sid).Value, out var folderPath);
            if (folderResult != 0 || folderPath == IntPtr.Zero)
            {
                FreeSid(sid);
                return null;
            }

            FreeCoTaskMem(folderPath);
            return new NativeAppContainerSidHandle(sid);
        }
        catch
        {
            FreeSid(sid);
            throw;
        }
    }

    private void GrantWorkerDirectoryAccess(SecurityIdentifier appContainerSid)
    {
        var directory = new DirectoryInfo(_workerDirectory);
        var security = directory.GetAccessControl();
        var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        var rule = new FileSystemAccessRule(
            appContainerSid,
            FileSystemRights.Modify | FileSystemRights.Synchronize,
            inheritance,
            PropagationFlags.None,
            AccessControlType.Allow);

        security.AddAccessRule(rule);
        directory.SetAccessControl(security);
    }

    private bool HasWorkerDirectoryAccess(SecurityIdentifier appContainerSid)
    {
        var accessRules = new DirectoryInfo(_workerDirectory)
            .GetAccessControl()
            .GetAccessRules(true, true, typeof(SecurityIdentifier));

        return accessRules
            .OfType<FileSystemAccessRule>()
            .Any(rule => rule.AccessControlType == AccessControlType.Allow &&
                         rule.IdentityReference.Equals(appContainerSid) &&
                         (rule.FileSystemRights & FileSystemRights.Modify) == FileSystemRights.Modify);
    }

    private sealed class NativeAppContainerSidHandle : IDisposable
    {
        private IntPtr _sid;

        public NativeAppContainerSidHandle(IntPtr sid)
        {
            _sid = sid;
            Sid = new SecurityIdentifier(sid);
        }

        public SecurityIdentifier Sid { get; }

        public void Dispose()
        {
            if (_sid != IntPtr.Zero)
            {
                FreeSid(_sid);
                _sid = IntPtr.Zero;
            }
        }
    }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int CreateAppContainerProfile(
        string appContainerName,
        string displayName,
        string description,
        IntPtr capabilities,
        uint capabilityCount,
        out IntPtr appContainerSid);

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int DeriveAppContainerSidFromAppContainerName(
        string appContainerName,
        out IntPtr appContainerSid);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern IntPtr FreeSid(IntPtr sid);

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetAppContainerFolderPath(string appContainerSid, out IntPtr path);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void CoTaskMemFree(IntPtr memory);

    private static void FreeCoTaskMem(IntPtr memory)
    {
        if (memory != IntPtr.Zero)
        {
            CoTaskMemFree(memory);
        }
    }
}

/// <summary>Metadata-only result of the explicitly confirmed provisioning step.</summary>
public sealed record AppContainerTerminalWorkerProvisioningResult(
    string ProfileName,
    string WorkerDirectory,
    bool ProfileCreated,
    IsolatedTerminalWorkerReadiness Readiness);
