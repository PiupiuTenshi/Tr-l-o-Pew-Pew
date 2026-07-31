namespace PewPew.Application.UiAutomation;

/// <summary>
/// Allowlist & blocklist service for Windows UI Automation targets.
/// Enforces threat mitigations by strictly excluding administrative elevation dialogs (UAC consent.exe),
/// system registry tools, task manager, and interactive shells.
/// </summary>
public sealed class UiAutomationTargetAllowlist
{
    private static readonly HashSet<string> ProhibitedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "consent", "consent.exe",        // Windows UAC Elevation Dialog
        "taskmgr", "taskmgr.exe",        // Task Manager
        "regedit", "regedit.exe",        // Registry Editor
        "mmc", "mmc.exe",                // Management Console
        "cmd", "cmd.exe",                // Command Prompt
        "powershell", "powershell.exe",  // PowerShell
        "pwsh", "pwsh.exe",              // PowerShell Core
        "lsass", "lsass.exe",            // Local Security Authority
        "services", "services.exe",      // Services Manager
        "secpol", "secpol.msc"           // Local Security Policy
    };

    private readonly HashSet<string> _allowedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "notepad", "notepad.exe",
        "explorer", "explorer.exe",
        "msedge", "msedge.exe",
        "chrome", "chrome.exe",
        "calculator", "calculatorapp.exe",
        "pewpew", "pewpew.exe"
    };

    public UiAutomationTargetAllowlist(IEnumerable<string>? customAllowedProcesses = null)
    {
        if (customAllowedProcesses is not null)
        {
            foreach (var process in customAllowedProcesses)
            {
                if (!string.IsNullOrWhiteSpace(process) && !IsProhibitedProcess(process))
                {
                    _allowedProcessNames.Add(process.Trim());
                }
            }
        }
    }

    /// <summary>
    /// Checks if a process name is prohibited (e.g. UAC consent dialog, registry editor, task manager).
    /// </summary>
    public static bool IsProhibitedProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return true;
        }

        var normalized = processName.Trim().ToLowerInvariant();
        return ProhibitedProcessNames.Contains(normalized);
    }

    /// <summary>
    /// Checks if a target scope is allowed for UI automation interaction.
    /// </summary>
    public bool IsTargetAllowed(UiTargetScope target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (IsProhibitedProcess(target.ProcessName))
        {
            return false;
        }

        var normalized = target.ProcessName.Trim().ToLowerInvariant();
        return _allowedProcessNames.Contains(normalized);
    }

    /// <summary>
    /// Explicitly registers a non-administrative process name to the allowlist.
    /// Returns false if the process is prohibited by system security policy.
    /// </summary>
    public bool AllowProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName) || IsProhibitedProcess(processName))
        {
            return false;
        }

        _allowedProcessNames.Add(processName.Trim());
        return true;
    }
}
