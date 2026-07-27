using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class ProjectReferenceArchitectureTests
{
    [Fact]
    public void SolutionProjectGraphObeysCoreDependencyRules()
    {
        var violations = ArchitectureRuleValidator.Validate(
            ArchitectureRuleValidator.LoadProjects(GetSolutionProjectFiles()));

        Assert.Empty(violations);
    }

    [Fact]
    public void NegativeFixtureIsRejectedWhenApplicationReferencesInfrastructure()
    {
        var fixtureDirectory = Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "PewPew.Architecture.Tests",
            "Fixtures",
            "NegativeApplicationToInfrastructure");
        var violations = ArchitectureRuleValidator.Validate(
            ArchitectureRuleValidator.LoadProjects(Directory.GetFiles(fixtureDirectory, "*.csproj")));

        Assert.Contains(violations, violation =>
            violation.RuleId == "DR-002" &&
            violation.Project == "PewPew.Application" &&
            violation.Reference == "PewPew.Infrastructure");
    }

    private static IEnumerable<string> GetSolutionProjectFiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        return Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories))
            .Where(projectFile => !projectFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PewPew.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root containing PewPew.sln was not found.");
    }
}
