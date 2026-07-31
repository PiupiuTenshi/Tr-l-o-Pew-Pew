using System.Security.Cryptography;
using System.Text;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Actions;

public sealed record StructuredActionPlan(EntityId Id, int Version, string Intent, string Target, string Payload)
{
    public string Hash { get; } = ComputeHash(Version, Intent, Target, Payload);

    public static StructuredActionPlan Create(EntityId id, int version, string intent, string target, string payload)
    {
        if (version < 1 || string.IsNullOrWhiteSpace(intent) || string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("Plan version, intent and target are required.");
        }

        return new StructuredActionPlan(id, version, intent, target, payload ?? string.Empty);
    }

    private static string ComputeHash(int version, string intent, string target, string payload)
    {
        var content = string.Join("\n", version, intent, target, payload);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
}
