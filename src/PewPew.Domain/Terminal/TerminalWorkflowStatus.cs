namespace PewPew.Domain.Terminal;

/// <summary>
/// Domain lifecycle status of a <see cref="TerminalWorkflowDefinition"/>.
/// </summary>
public enum TerminalWorkflowStatus
{
    /// <summary>
    /// Initial draft state. Cannot be executed.
    /// </summary>
    Draft,

    /// <summary>
    /// Workflow submitted for policy and hash approval.
    /// </summary>
    Submitted,

    /// <summary>
    /// Approved by policy engine with verified SHA-256 hash.
    /// </summary>
    PolicyApproved,

    /// <summary>
    /// Active and authorized for execution by worker runner.
    /// </summary>
    Active,

    /// <summary>
    /// Archived/deprecated workflow. Execution denied.
    /// </summary>
    Archived,

    /// <summary>
    /// Rejected by policy or due to hash mismatch. Execution permanently blocked.
    /// </summary>
    PolicyRejected
}
