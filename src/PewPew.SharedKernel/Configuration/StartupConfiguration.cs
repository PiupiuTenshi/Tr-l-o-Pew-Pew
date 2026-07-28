namespace PewPew.SharedKernel.Configuration;

public sealed record StartupConfiguration(bool PrivateMode, string LocalDataDirectory)
{
    private const string PrivateModeKey = "PEWPEW_PRIVATE_MODE";
    private const string LocalDataDirectoryKey = "PEWPEW_LOCAL_DATA_DIRECTORY";

    private static readonly string[] SensitiveKeySuffixes =
    [
        "_API_KEY",
        "_ACCESS_TOKEN",
        "_REFRESH_TOKEN",
        "_CLIENT_SECRET",
        "_PASSWORD",
        "_SECRET"
    ];

    public static StartupConfiguration LoadFromEnvironment()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key)
            {
                values[key] = entry.Value?.ToString();
            }
        }

        return Load(values);
    }

    public static StartupConfiguration Load(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        RejectPlaintextSecrets(values);

        var privateMode = ParsePrivateMode(values);
        var localDataDirectory = ResolveLocalDataDirectory(values);
        return new StartupConfiguration(privateMode, localDataDirectory);
    }

    private static bool ParsePrivateMode(IReadOnlyDictionary<string, string?> values)
    {
        if (!values.TryGetValue(PrivateModeKey, out var configuredValue))
        {
            return true;
        }

        if (bool.TryParse(configuredValue, out var privateMode))
        {
            return privateMode;
        }

        throw new StartupConfigurationException($"{PrivateModeKey} must be either true or false.");
    }

    private static string ResolveLocalDataDirectory(IReadOnlyDictionary<string, string?> values)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PewPew");
        if (values.TryGetValue(LocalDataDirectoryKey, out var configuredDirectory))
        {
            if (string.IsNullOrWhiteSpace(configuredDirectory))
            {
                throw new StartupConfigurationException($"{LocalDataDirectoryKey} must not be empty.");
            }

            directory = configuredDirectory;
        }

        try
        {
            return Path.GetFullPath(directory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new StartupConfigurationException($"{LocalDataDirectoryKey} must be a valid local path.", exception);
        }
    }

    private static void RejectPlaintextSecrets(IReadOnlyDictionary<string, string?> values)
    {
        foreach (var pair in values)
        {
            if (pair.Key.StartsWith("PEWPEW_", StringComparison.Ordinal) &&
                SensitiveKeySuffixes.Any(suffix => pair.Key.EndsWith(suffix, StringComparison.Ordinal)))
            {
                throw new StartupConfigurationException(
                    "Plaintext secrets are not accepted in environment configuration. Use a secret adapter.");
            }
        }
    }
}

public sealed class StartupConfigurationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
