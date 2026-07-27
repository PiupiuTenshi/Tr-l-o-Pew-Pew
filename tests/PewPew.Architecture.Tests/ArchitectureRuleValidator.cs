using System.Xml.Linq;

namespace PewPew.Architecture.Tests;

internal static class ArchitectureRuleValidator
{
    private static readonly HashSet<string> ApplicationForbiddenReferences =
    [
        "PewPew.Api",
        "PewPew.Infrastructure",
        "PewPew.Persistence",
        "PewPew.Desktop",
        "PewPew.Web",
        "PewPew.Mobile",
        "PewPew.BrowserExtension",
        "PewPew.HomeAssistant"
    ];

    private static readonly HashSet<string> PresentationProjects =
    [
        "PewPew",
        "PewPew.Desktop",
        "PewPew.Web",
        "PewPew.Mobile",
        "PewPew.BrowserExtension"
    ];

    public static IReadOnlyList<ArchitectureViolation> Validate(IEnumerable<ProjectNode> projects)
    {
        var projectList = projects.ToList();
        var violations = new List<ArchitectureViolation>();

        foreach (var project in projectList)
        {
            foreach (var reference in project.References)
            {
                if (project.Name == "PewPew.Domain" && reference != "PewPew.SharedKernel")
                {
                    violations.Add(new("DR-001", project.Name, reference));
                }

                if (project.Name == "PewPew.Application" && ApplicationForbiddenReferences.Contains(reference))
                {
                    violations.Add(new("DR-002", project.Name, reference));
                }

                if (PresentationProjects.Contains(project.Name) && reference == "PewPew.Persistence")
                {
                    violations.Add(new("DR-005", project.Name, reference));
                }
            }
        }

        foreach (var cycle in FindCycles(projectList))
        {
            violations.Add(new("DR-011", cycle.From, cycle.To));
        }

        return violations;
    }

    public static IReadOnlyList<ProjectNode> LoadProjects(IEnumerable<string> projectFiles)
    {
        return projectFiles.Select(LoadProject).ToList();
    }

    private static ProjectNode LoadProject(string projectFile)
    {
        var fullPath = Path.GetFullPath(projectFile);
        var document = XDocument.Load(fullPath);
        var references = document
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!))
            .ToList();

        return new(Path.GetFileNameWithoutExtension(fullPath), references);
    }

    private static IEnumerable<(string From, string To)> FindCycles(IEnumerable<ProjectNode> projects)
    {
        var graph = projects.ToDictionary(project => project.Name, project => project.References);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);

        foreach (var project in graph.Keys)
        {
            foreach (var cycle in Visit(project))
            {
                yield return cycle;
            }
        }

        IEnumerable<(string From, string To)> Visit(string project)
        {
            if (!visited.Add(project))
            {
                yield break;
            }

            active.Add(project);
            foreach (var reference in graph[project].Where(graph.ContainsKey))
            {
                if (active.Contains(reference))
                {
                    yield return (project, reference);
                    continue;
                }

                foreach (var cycle in Visit(reference))
                {
                    yield return cycle;
                }
            }

            active.Remove(project);
        }
    }
}

internal sealed record ProjectNode(string Name, IReadOnlyList<string> References);

internal sealed record ArchitectureViolation(string RuleId, string Project, string Reference);
