namespace PewPew.Application.Actions;

/// <summary>
/// Status of a worker process action execution.
/// </summary>
public enum WorkerExecutionStatus
{
    Completed = 1,
    Failed = 2,
    Cancelled = 3,
    Unknown = 4
}

/// <summary>
/// Outcome details returned by a worker execution payload handler.
/// Contains post-action verification evidence or failure/cancellation reasons.
/// </summary>
public sealed record WorkerExecutionOutcome(
    WorkerExecutionStatus Status,
    string? VerificationEvidence = null,
    string? FailureReason = null)
{
    public static WorkerExecutionOutcome Success(string verificationEvidence) =>
        new(WorkerExecutionStatus.Completed, VerificationEvidence: verificationEvidence);

    public static WorkerExecutionOutcome Failed(string failureReason) =>
        new(WorkerExecutionStatus.Failed, FailureReason: failureReason);

    public static WorkerExecutionOutcome Cancelled(string? reason = null) =>
        new(WorkerExecutionStatus.Cancelled, FailureReason: reason ?? "user_cancelled");

    public static WorkerExecutionOutcome Unknown(string? reason = null) =>
        new(WorkerExecutionStatus.Unknown, FailureReason: reason ?? "unknown_post_condition");
}
