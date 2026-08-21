using PewPew.Domain.Terminal;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Application.Terminal;

/// <summary>
/// Application service managing the creation, policy approval, hash verification,
/// and pre-execution argument binding of structured terminal workflows.
/// </summary>
public sealed class TerminalWorkflowService
{
    /// <summary>
    /// Creates a new draft <see cref="TerminalWorkflowDefinition"/>.
    /// Auto-computes the expected SHA-256 hash if expectedHash is omitted.
    /// </summary>
    public static TerminalWorkflowDefinition CreateDraft(
        string name,
        string version,
        string executablePath,
        string workingDirectoryRoot,
        IEnumerable<string> fixedArguments,
        IEnumerable<string> allowedPlaceholders,
        TerminalWorkflowRiskLevel riskLevel,
        string? expectedHash = null,
        string? expectedExecutableSha256Hash = null,
        DateTimeOffset? nowUtc = null)
    {
        var fixedList = fixedArguments?.ToList() ?? new List<string>();
        var placeholderList = allowedPlaceholders?.ToList() ?? new List<string>();

        var computedHash = expectedHash;
        if (string.IsNullOrWhiteSpace(computedHash))
        {
            computedHash = TerminalWorkflowDefinition.CalculateSha256Hash(
                executablePath,
                fixedList,
                placeholderList,
                version);
        }

        return new TerminalWorkflowDefinition(
            EntityId.New(),
            name,
            version,
            executablePath,
            workingDirectoryRoot,
            fixedList,
            placeholderList,
            riskLevel,
            computedHash,
            expectedExecutableSha256Hash,
            nowUtc);
    }

    /// <summary>
    /// Validates, checks hash verification, and prepares a workflow for execution.
    /// Returns the verified executable path and bound argument list.
    /// </summary>
    public static PreparedTerminalWorkflowExecution PrepareExecution(
        TerminalWorkflowDefinition workflow,
        IDictionary<string, string>? parameterValues,
        string providedSha256Hash,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        if (workflow.Status != TerminalWorkflowStatus.Active && workflow.Status != TerminalWorkflowStatus.PolicyApproved)
        {
            throw new InvalidOperationException($"Workflow '{workflow.Name}' ({workflow.Id}) cannot be executed from status '{workflow.Status}'. Must be Active or PolicyApproved.");
        }

        var isHashValid = workflow.VerifyHash(providedSha256Hash, nowUtc);
        if (!isHashValid)
        {
            throw new InvalidOperationException($"Workflow '{workflow.Name}' hash verification failed against expected hash '{workflow.ExpectedSha256Hash}'.");
        }

        var boundArguments = TerminalWorkflowValidator.SanitizeAndBindArguments(workflow, parameterValues);

        return new PreparedTerminalWorkflowExecution(
            workflow.ExecutablePath,
            workflow.WorkingDirectoryRoot,
            boundArguments,
            workflow.ExpectedExecutableSha256Hash);
    }
}

public sealed record PreparedTerminalWorkflowExecution(
    string ExecutablePath,
    string WorkingDirectoryRoot,
    IReadOnlyList<string> BoundArguments,
    string? ExpectedExecutableSha256Hash);
