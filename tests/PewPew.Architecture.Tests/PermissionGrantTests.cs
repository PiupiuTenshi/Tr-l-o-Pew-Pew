using PewPew.Domain.Permissions;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class PermissionGrantTests
{
    [Fact]
    public void ActiveGrantAllowsOnlyAnExactScopeMatch()
    {
        var scope = NewScope();
        var grant = ActiveGrant(scope);

        Assert.True(grant.Allows(scope, DateTimeOffset.UtcNow));
        Assert.False(grant.Allows(NewScope(action: "write"), DateTimeOffset.UtcNow));
        Assert.False(grant.Allows(NewScope(isRemote: true), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MissingOrInactiveGrantDeniesByDefault()
    {
        var scope = NewScope();
        var grant = new PermissionGrant(EntityId.New(), scope, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.False(grant.Allows(scope, DateTimeOffset.UtcNow));
        grant.Submit();
        Assert.False(grant.Allows(scope, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ExpiredGrantDeniesAndBecomesTerminal()
    {
        var expiresAt = DateTimeOffset.UtcNow;
        var scope = NewScope();
        var grant = ActiveGrant(scope, expiresAt);

        Assert.False(grant.Allows(scope, expiresAt));
        grant.Expire(expiresAt);

        Assert.Equal(PermissionGrantStatus.Expired, grant.Status);
        Assert.False(grant.Allows(scope, expiresAt.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(grant.Reapprove);
    }

    [Fact]
    public void RevokedGrantDeniesAllFutureRequests()
    {
        var scope = NewScope();
        var grant = ActiveGrant(scope);

        grant.Revoke();

        Assert.Equal(PermissionGrantStatus.Revoked, grant.Status);
        Assert.False(grant.Allows(scope, DateTimeOffset.UtcNow));
        Assert.Throws<InvalidOperationException>(grant.Restore);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("read*")]
    [InlineData("")]
    public void WildcardOrEmptyScopeIsRejected(string action) =>
        Assert.Throws<ArgumentException>(() => NewScope(action: action));

    private static PermissionGrant ActiveGrant(PermissionScope scope, DateTimeOffset? expiresAtUtc = null)
    {
        var grant = new PermissionGrant(EntityId.New(), scope, expiresAtUtc ?? DateTimeOffset.UtcNow.AddMinutes(1));
        grant.Submit();
        grant.Approve();
        return grant;
    }

    private static PermissionScope NewScope(string action = "read", bool isRemote = false) =>
        PermissionScope.Create(EntityId.New(), EntityId.New(), "device-status", "device:123", action, isRemote);
}
