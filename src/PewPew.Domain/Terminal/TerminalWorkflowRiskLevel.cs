namespace PewPew.Domain.Terminal;

/// <summary>
/// Specified risk level rating for a <see cref="TerminalWorkflowDefinition"/>.
/// </summary>
public enum TerminalWorkflowRiskLevel
{
    /// <summary>
    /// Read-only inspection commands (e.g. git status, dotnet --version).
    /// </summary>
    Low,

    /// <summary>
    /// Standard build and test commands (e.g. dotnet build, dotnet test).
    /// </summary>
    Medium,

    /// <summary>
    /// Package installation or project modification commands (e.g. npm install, dotnet publish).
    /// Requires user confirmation or trusted routine binding.
    /// </summary>
    High,

    /// <summary>
    /// Destructive or system-wide commands. Strictly prohibited in pre-approved workflows.
    /// </summary>
    Critical
}
