using PewPew.Domain.Actions;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class StructuredActionPlanTests
{
    [Fact]
    public void ValidPlanHasStableHashAndChangedPayloadChangesHash()
    {
        var id = EntityId.New();
        var first = StructuredActionPlan.Create(id, 1, "read", "device", "a");
        var same = StructuredActionPlan.Create(id, 1, "read", "device", "a");
        var changed = StructuredActionPlan.Create(id, 1, "read", "device", "b");
        Assert.Equal(first.Hash, same.Hash);
        Assert.NotEqual(first.Hash, changed.Hash);
    }

    [Fact]
    public void InvalidSchemaIsRejected()
    {
        Assert.Throws<ArgumentException>(() => StructuredActionPlan.Create(EntityId.New(), 0, "", "", ""));
    }
}
