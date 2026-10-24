using System.Xml.Linq;

namespace ShipFlow.Domain.Tests;

/// <summary>
/// Architecture rules checked against the project files themselves. Assembly metadata is not used because the
/// compiler drops unused references, which would let these rules pass vacuously.
/// </summary>
public class DomainArchitectureTests
{
    [Fact]
    public void Domain_HasNoProjectReferences() =>
        Assert.Empty(ProjectFiles.ProjectReferences("src/ShipFlow.Domain/ShipFlow.Domain.csproj"));

    [Fact]
    public void Domain_HasNoPackageOrFrameworkReferences()
    {
        var doc = ProjectFiles.Load("src/ShipFlow.Domain/ShipFlow.Domain.csproj");

        Assert.Empty(doc.Descendants("PackageReference"));
        Assert.Empty(doc.Descendants("FrameworkReference"));
    }

    [Fact]
    public void Application_ReferencesOnlyDomain() =>
        Assert.Equal(
            new[] { "ShipFlow.Domain" },
            ProjectFiles.ProjectReferences("src/ShipFlow.Application/ShipFlow.Application.csproj"));

    [Fact]
    public void Infrastructure_ReferencesOnlyApplicationAndDomain() =>
        Assert.Equal(
            new[] { "ShipFlow.Application", "ShipFlow.Domain" },
            ProjectFiles.ProjectReferences("src/ShipFlow.Infrastructure/ShipFlow.Infrastructure.csproj"));

    [Fact]
    public void Api_ReferencesOnlyApplicationAndInfrastructure() =>
        Assert.Equal(
            new[] { "ShipFlow.Application", "ShipFlow.Infrastructure" },
            ProjectFiles.ProjectReferences("src/ShipFlow.API/ShipFlow.API.csproj"));

    [Fact]
    public void NoSourceProject_ReferencesATestProject()
    {
        foreach (var project in new[] { "Domain", "Application", "Infrastructure", "API" })
        {
            var refs = ProjectFiles.ProjectReferences($"src/ShipFlow.{project}/ShipFlow.{project}.csproj");
            Assert.DoesNotContain(refs, r => r.EndsWith("Tests", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void DomainSource_DoesNotUseFrameworkInfrastructureNamespaces()
    {
        var forbidden = new[]
        {
            "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Microsoft.Extensions", "System.Data", "Newtonsoft"
        };
        var domainDir = Path.Combine(ProjectFiles.RepoRoot, "src", "ShipFlow.Domain");

        var offenders = Directory.EnumerateFiles(domainDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(p => File.ReadLines(p)
                .Where(l => l.TrimStart().StartsWith("using ", StringComparison.Ordinal) && forbidden.Any(l.Contains))
                .Select(l => $"{Path.GetFileName(p)}: {l.Trim()}"))
            .ToList();

        Assert.Empty(offenders);
    }
}

internal static class ProjectFiles
{
    public static string RepoRoot { get; } = FindRoot();

    public static XDocument Load(string relativePath) =>
        XDocument.Load(Path.Combine(RepoRoot, relativePath));

    /// <summary>Referenced project names (file name without extension), sorted ordinally.</summary>
    public static string[] ProjectReferences(string relativePath) =>
        Load(relativePath)
            .Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

    private static string FindRoot()
    {
        // Normally the test output sits under the repo; fall back to the working directory so the tests
        // also work when the binaries are run from somewhere else.
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ShipFlow.sln")))
            {
                dir = dir.Parent;
            }

            if (dir is not null)
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate ShipFlow.sln above the test output or working directory.");
    }
}
