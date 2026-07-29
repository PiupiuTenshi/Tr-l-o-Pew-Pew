using PewPew.SharedKernel.Configuration;
using System.Text.Json;

namespace PewPew.Desktop;

/// <summary>
/// Local model configuration. Model artifacts remain outside Git and can be
/// changed without recompiling the desktop application.
/// </summary>
public sealed record LocalSpeechModelOptions(string ModelPath, string? ExpectedSha256)
{
    private static readonly JsonSerializerOptions LocalConfigurationJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public const string ModelPathEnvironmentKey = "PEWPEW_LOCAL_SPEECH_MODEL_PATH";
    public const string ModelSha256EnvironmentKey = "PEWPEW_LOCAL_SPEECH_MODEL_SHA256";
    public const string LocalConfigurationFileName = "appsettings.desktop.local.json";

    public static LocalSpeechModelOptions LoadFromEnvironment(StartupConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var localConfiguration = LoadLocalConfiguration();
        var configuredPath = Environment.GetEnvironmentVariable(ModelPathEnvironmentKey) ?? localConfiguration?.ModelPath;
        var modelPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(configuration.LocalDataDirectory, "models", "ggml-small.bin")
            : configuredPath;
        var configuredHash = Environment.GetEnvironmentVariable(ModelSha256EnvironmentKey) ?? localConfiguration?.ExpectedSha256;

        try
        {
            return new LocalSpeechModelOptions(
                Path.GetFullPath(modelPath),
                string.IsNullOrWhiteSpace(configuredHash) ? null : NormalizeSha256(configuredHash));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new StartupConfigurationException($"{ModelPathEnvironmentKey} must be a valid local path.", exception);
        }
    }

    private static string NormalizeSha256(string value)
    {
        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal).Trim();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new StartupConfigurationException($"{ModelSha256EnvironmentKey} must be a SHA-256 hexadecimal value.");
        }

        return normalized.ToUpperInvariant();
    }

    private static LocalSpeechModelLocalConfiguration? LoadLocalConfiguration()
    {
        foreach (var candidate in GetLocalConfigurationCandidates())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                using var stream = File.OpenRead(candidate);
                return JsonSerializer.Deserialize<LocalSpeechModelLocalConfiguration>(stream, LocalConfigurationJsonOptions);
            }
            catch (JsonException exception)
            {
                throw new StartupConfigurationException($"{LocalConfigurationFileName} contains invalid JSON.", exception);
            }
        }

        return null;
    }

    private static IEnumerable<string> GetLocalConfigurationCandidates()
    {
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(root);
            while (directory is not null)
            {
                var besideExecutable = Path.Combine(directory.FullName, LocalConfigurationFileName);
                if (yielded.Add(besideExecutable))
                {
                    yield return besideExecutable;
                }

                var repositoryLocalConfiguration = Path.Combine(
                    directory.FullName,
                    "src",
                    "PewPew.Desktop",
                    LocalConfigurationFileName);
                if (yielded.Add(repositoryLocalConfiguration))
                {
                    yield return repositoryLocalConfiguration;
                }

                directory = directory.Parent;
            }
        }
    }

    private sealed record LocalSpeechModelLocalConfiguration(string? ModelPath, string? ExpectedSha256);
}
