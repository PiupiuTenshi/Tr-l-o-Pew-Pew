using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Interactions;

public enum InteractionStatus { Created, CapturingInput, Understanding, WaitingClarification, Cancelled, Expired }

public sealed class InteractionSession
{
    public InteractionSession(EntityId id, DateTimeOffset expiresAtUtc) { Id = id; ExpiresAtUtc = expiresAtUtc; }
    public EntityId Id { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public InteractionStatus Status { get; private set; } = InteractionStatus.Created;
    public void BeginInput() => Move(InteractionStatus.Created, InteractionStatus.CapturingInput);
    public void CaptureInput() => Move(InteractionStatus.CapturingInput, InteractionStatus.Understanding);
    public void RequireClarification() => Move(InteractionStatus.Understanding, InteractionStatus.WaitingClarification);
    public void AnswerClarification() => Move(InteractionStatus.WaitingClarification, InteractionStatus.Understanding);
    public void Cancel() { EnsureOpen(); Status = InteractionStatus.Cancelled; }
    public void Expire(DateTimeOffset now) { if (now < ExpiresAtUtc) { throw new InvalidOperationException("Interaction has not expired."); } EnsureOpen(); Status = InteractionStatus.Expired; }
    private void Move(InteractionStatus expected, InteractionStatus next) { if (Status != expected) { throw new InvalidOperationException("Interaction transition denied."); } Status = next; }
    private void EnsureOpen() { if (Status is InteractionStatus.Cancelled or InteractionStatus.Expired) { throw new InvalidOperationException("Interaction is terminal."); } }
}
