using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Permissions;

public sealed record PermissionScope
{
    private PermissionScope(
        EntityId userId,
        EntityId deviceId,
        string skill,
        string resource,
        string action,
        bool isRemote)
    {
        UserId = userId;
        DeviceId = deviceId;
        Skill = skill;
        Resource = resource;
        Action = action;
        IsRemote = isRemote;
    }

    public EntityId UserId { get; }

    public EntityId DeviceId { get; }

    public string Skill { get; }

    public string Resource { get; }

    public string Action { get; }

    public bool IsRemote { get; }

    public static PermissionScope Create(
        EntityId userId,
        EntityId deviceId,
        string skill,
        string resource,
        string action,
        bool isRemote)
    {
        if (IsInvalid(skill) || IsInvalid(resource) || IsInvalid(action))
        {
            throw new ArgumentException("Permission scope values must be specific and cannot use wildcards.");
        }

        return new PermissionScope(userId, deviceId, skill, resource, action, isRemote);
    }

    private static bool IsInvalid(string value) => string.IsNullOrWhiteSpace(value) || value.Contains('*');
}
