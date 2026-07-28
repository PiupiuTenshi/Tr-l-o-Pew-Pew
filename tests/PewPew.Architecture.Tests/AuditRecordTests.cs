using PewPew.Domain.Audit;
using PewPew.SharedKernel.Primitives;
using Xunit;
namespace PewPew.Architecture.Tests;

public sealed class AuditRecordTests
{
    [Fact] public void SealedRecordIsImmutableAndCanArchive() { var r = NewRecord(); r.Seal(); r.Archive(); Assert.Equal(AuditRecordStatus.Archived, r.Status); Assert.Throws<InvalidOperationException>(r.Seal); }
    [Theory][InlineData("token")][InlineData("password")][InlineData("otp")][InlineData("private_key")] public void SensitivePayloadFieldsAreNotPartOfAuditContract(string forbidden) { Assert.DoesNotContain(forbidden, typeof(AuditRecord).ToString(), StringComparison.OrdinalIgnoreCase); }
    [Fact] public void RequiredAuditMetadataCannotBeEmpty() => Assert.Throws<ArgumentException>(() => new AuditRecord(EntityId.New(), "", EntityId.New(), EntityId.New(), EntityId.New(), "action", "allow"));
    private static AuditRecord NewRecord() => new(EntityId.New(), "corr", EntityId.New(), EntityId.New(), EntityId.New(), "permission_granted", "allow");
}
