using System.Buffers;
using System.Text.RegularExpressions;
using PewPew.Domain.Terminal;

namespace PewPew.Application.Terminal;

/// <summary>
/// Application validator for terminal workflow definition executables, arguments, and placeholder parameter bindings.
/// Prevents shell command injection, argument pollution, path traversal, and unsafe executable invocation.
/// </summary>
public sealed class TerminalWorkflowValidator
{
    private static readonly SearchValues<char> ShellOperators = SearchValues.Create(['|', ';', '&', '$', '`', '>', '<', '\n', '\r']);
    private static readonly Regex PathTraversalRegex = new(@"\.\.[\\/]", RegexOptions.Compiled);

    /// <summary>
    /// Validates an argument string to ensure no shell chaining or command injection characters exist.
    /// </summary>
    public static bool IsArgumentSafe(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return true;
        }

        return argument.AsSpan().IndexOfAny(ShellOperators) < 0;
    }

    /// <summary>
    /// Validates a parameter value intended for placeholder substitution.
    /// Rejects shell injection characters, control codes, and relative path traversal ("..").
    /// </summary>
    public static bool IsParameterValueSafe(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (value.AsSpan().IndexOfAny(ShellOperators) >= 0)
        {
            return false;
        }

        if (PathTraversalRegex.IsMatch(value))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Binds provided placeholder values into a workflow's arguments list.
    /// Throws <see cref="ArgumentException"/> or <see cref="InvalidOperationException"/> if values are unsafe or unapproved placeholders are supplied.
    /// </summary>
    public static IReadOnlyList<string> SanitizeAndBindArguments(
        TerminalWorkflowDefinition workflow,
        IDictionary<string, string>? placeholderValues)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var boundArgs = new List<string>();
        var providedMap = placeholderValues ?? new Dictionary<string, string>();

        // 1. Verify provided keys match declared allowed placeholders
        foreach (var key in providedMap.Keys)
        {
            if (!workflow.AllowedPlaceholders.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Placeholder '{key}' is not declared in workflow '{workflow.Name}' allowed placeholders.");
            }

            var val = providedMap[key];
            if (!IsParameterValueSafe(val))
            {
                throw new InvalidOperationException($"Parameter value '{val}' for placeholder '{key}' contains illegal shell operators or path traversal.");
            }
        }

        // 2. Perform placeholder substitution on fixed arguments
        foreach (var arg in workflow.FixedArguments)
        {
            var replacedArg = arg;
            foreach (var (placeholder, val) in providedMap)
            {
                var token = $"{{{placeholder}}}";
                if (replacedArg.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    replacedArg = replacedArg.Replace(token, val, StringComparison.OrdinalIgnoreCase);
                }
            }

            if (!IsArgumentSafe(replacedArg))
            {
                throw new InvalidOperationException($"Bound argument '{replacedArg}' contains illegal shell operators.");
            }

            boundArgs.Add(replacedArg);
        }

        return boundArgs.AsReadOnly();
    }
}
