using PewPew.Application.UiAutomation;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace PewPew.Desktop.Accessibility;

/// <summary>
/// Windows UI Accessibility Automation adapter using non-admin APIs and allowlist bounds.
/// Prohibits interaction with UAC elevation prompts (consent.exe), task manager, registry editor,
/// and un-allowlisted application windows.
/// </summary>
public sealed class WindowsAccessibilityAdapter : IWindowsAccessibilityAdapter, IVerifiedWindowsUiAutomationChannel
{
    private readonly UiAutomationTargetAllowlist _allowlist;

    public WindowsAccessibilityAdapter(UiAutomationTargetAllowlist? allowlist = null)
    {
        _allowlist = allowlist ?? new UiAutomationTargetAllowlist();
    }

    public Task<UiAutomationResult> ExecuteUiActionAsync(
        UiTargetScope target,
        UiActionKind actionKind,
        string? valuePayload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(UiAutomationResult.Failed("uia_legacy_control_path_disabled"));
    }

    public Task<WindowsUiAutomationReadback?> ExecuteAndReadbackAsync(
        UiTargetScope target,
        UiActionKind action,
        string? valuePayload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        if (UiAutomationTargetAllowlist.IsProhibitedProcess(target.ProcessName) || !_allowlist.IsTargetAllowed(target))
        {
            return Task.FromResult<WindowsUiAutomationReadback?>(null);
        }

        var element = ResolveExactlyOne(target);
        Execute(element, action, valuePayload);
        cancellationToken.ThrowIfCancellationRequested();

        // Re-resolve rather than reusing the action element: this is the independent readback gate.
        var readback = ResolveExactlyOne(target);
        return Task.FromResult<WindowsUiAutomationReadback?>(CreateReadback(target, readback, action));
    }

    private static AutomationElement ResolveExactlyOne(UiTargetScope target)
    {
        var processName = Path.GetFileNameWithoutExtension(target.ProcessName.Trim());
        var processIds = target.ProcessId is { } boundProcessId
            ? Process.GetProcessesByName(processName).Where(process => process.Id == boundProcessId).Select(process => process.Id).ToHashSet()
            : Process.GetProcessesByName(processName).Select(process => process.Id).ToHashSet();
        if (processIds.Count == 0)
        {
            throw new InvalidOperationException("uia_target_not_found");
        }

        var windows = new List<AutomationElement>();
        foreach (var processId in processIds)
        {
            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ProcessIdProperty, processId),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
            windows.AddRange(AutomationElement.RootElement
                .FindAll(TreeScope.Children, condition)
                .Cast<AutomationElement>()
                .Where(window => window.Current.NativeWindowHandle != 0));
        }

        var matchingWindows = windows.Where(window => TitleMatches(window.Current.Name, target.WindowTitlePattern)).ToArray();
        if (matchingWindows.Length != 1)
        {
            throw new InvalidOperationException(matchingWindows.Length == 0 ? "uia_window_not_found" : "uia_window_ambiguous");
        }

        var conditionForElement = BuildElementCondition(target);
        var matches = conditionForElement is null
            ? [matchingWindows[0]]
            : matchingWindows[0].FindAll(TreeScope.Descendants, conditionForElement).Cast<AutomationElement>().ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(matches.Length == 0 ? "uia_target_not_found" : "uia_target_ambiguous");
        }

        return matches[0];
    }

    private static Condition? BuildElementCondition(UiTargetScope target)
    {
        var conditions = new List<Condition>();
        if (!string.IsNullOrWhiteSpace(target.AutomationId))
        {
            conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, target.AutomationId));
        }
        if (!string.IsNullOrWhiteSpace(target.ElementName))
        {
            conditions.Add(new PropertyCondition(AutomationElement.NameProperty, target.ElementName));
        }
        if (!string.IsNullOrWhiteSpace(target.ControlTypeName))
        {
            var controlType = typeof(ControlType).GetField(target.ControlTypeName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null) as ControlType;
            if (controlType is null)
            {
                throw new InvalidOperationException("uia_control_type_unsupported");
            }
            conditions.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, controlType));
        }
        return conditions.Count switch
        {
            0 => null,
            1 => conditions[0],
            _ => new AndCondition(conditions.ToArray())
        };
    }

    private static void Execute(AutomationElement element, UiActionKind action, string? valuePayload)
    {
        switch (action)
        {
            case UiActionKind.Click when element.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke):
                ((InvokePattern)invoke).Invoke();
                return;
            case UiActionKind.SetText when element.TryGetCurrentPattern(ValuePattern.Pattern, out var value):
                ((ValuePattern)value).SetValue(valuePayload ?? string.Empty);
                return;
            case UiActionKind.Focus:
                element.SetFocus();
                return;
            case UiActionKind.ReadText:
                return;
            default:
                throw new InvalidOperationException("uia_action_pattern_unsupported");
        }
    }

    private static WindowsUiAutomationReadback CreateReadback(UiTargetScope target, AutomationElement element, UiActionKind action)
    {
        var valueHash = element.TryGetCurrentPattern(ValuePattern.Pattern, out var value)
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(((ValuePattern)value).Current.Value ?? string.Empty)))
            : null;
        var state = action switch
        {
            UiActionKind.Focus when HasFocusWithinTarget(element) => "focused",
            UiActionKind.SetText when valueHash is not null => "value_set",
            UiActionKind.ReadText => "read",
            UiActionKind.Click when element.Current.IsEnabled => "invoked",
            _ => "readback_mismatch"
        };
        return new(target.TargetId, state, valueHash);
    }

    private static bool HasFocusWithinTarget(AutomationElement target)
    {
        var focused = AutomationElement.FocusedElement;
        for (var current = focused; current is not null; current = TreeWalker.ControlViewWalker.GetParent(current))
        {
            if (current.Equals(target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TitleMatches(string actual, string? pattern) =>
        string.IsNullOrWhiteSpace(pattern) || Regex.IsMatch(actual ?? string.Empty, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.CultureInvariant);
}
