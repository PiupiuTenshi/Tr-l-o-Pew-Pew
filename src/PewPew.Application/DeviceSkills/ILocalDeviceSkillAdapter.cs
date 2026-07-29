using PewPew.Application.IntentRouting;

namespace PewPew.Application.DeviceSkills;

/// <summary>
/// A deliberately small, structured command set for local Level 0-1 device
/// skills. It is not a shell command and cannot carry a file path or argument.
/// </summary>
public sealed record LocalDeviceSkillCommand(
    LocalIntentName Intent,
    string Resource)
{
    public string Skill => Intent switch
    {
        LocalIntentName.OpenApplication => "open_application",
        LocalIntentName.MediaControl => "media_control",
        LocalIntentName.AdjustVolume => "adjust_volume",
        _ => throw new InvalidOperationException("Unsupported local device skill.")
    };

    public static bool TryCreate(LocalIntentProposal proposal, out LocalDeviceSkillCommand? command)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        command = proposal.Intent switch
        {
            LocalIntentName.OpenApplication when proposal.Arguments.TryGetValue("applicationId", out var applicationId)
                && applicationId is "calculator" or "notepad" => new(proposal.Intent, applicationId),
            LocalIntentName.MediaControl when proposal.Arguments.TryGetValue("command", out var mediaCommand)
                && mediaCommand is "play" or "pause" or "stop" or "next" or "previous" => new(proposal.Intent, mediaCommand),
            LocalIntentName.AdjustVolume when proposal.Arguments.TryGetValue("direction", out var direction)
                && direction is "up" or "down" => new(proposal.Intent, direction),
            _ => null
        };

        return command is not null;
    }
}

/// <summary>
/// Platform boundary for executing an already policy-authorized local device
/// command. Implementations must never accept unstructured input.
/// </summary>
public interface ILocalDeviceSkillAdapter
{
    Task<LocalDeviceSkillAdapterResult> ExecuteAsync(
        AuthorizedLocalDeviceSkillCommand command,
        CancellationToken cancellationToken);
}

/// <summary>
/// Capability token created only by the Application control path after policy
/// dispatch. Desktop/UI code cannot construct one to call an adapter directly.
/// </summary>
public sealed class AuthorizedLocalDeviceSkillCommand
{
    internal AuthorizedLocalDeviceSkillCommand(LocalDeviceSkillCommand command)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
    }

    public LocalDeviceSkillCommand Command { get; }
}

public sealed record LocalDeviceSkillAdapterResult(
    bool IsVerified,
    string Evidence,
    string? FailureReason = null)
{
    public static LocalDeviceSkillAdapterResult Verified(string evidence) =>
        new(true, RequireText(evidence, nameof(evidence)));

    public static LocalDeviceSkillAdapterResult Failed(string reason) =>
        new(false, "adapter_failure", RequireText(reason, nameof(reason)));

    private static string RequireText(string value, string parameterName) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("A non-empty result value is required.", parameterName);
}
