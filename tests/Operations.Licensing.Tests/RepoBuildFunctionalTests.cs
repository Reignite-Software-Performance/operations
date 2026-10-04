using System.Xml.Linq;

namespace Operations.Licensing.Tests;

/// <summary>
/// End-to-end regression coverage for build-failed:6f607a64da64 — CI ran
/// `dotnet build --no-restore` from the repo root and failed with MSB1003
/// because MSBuild's project/solution auto-discovery found nothing in the
/// current working directory. These tests walk the actual repo tree and
/// assert the exact condition MSB1003 checks for (a discoverable solution
/// file that resolves to real, well-formed project files) so a regression
/// where the solution is removed, renamed, or left pointing at missing
/// projects fails here too.
/// </summary>
public class RepoBuildFunctionalTests
{
    private static string RepoRoot
    {
        get
        {
            // Walk up from the test assembly's output directory until we find the
            // ancestor that contains the repo's solution file. A fixed level-count
            // is fragile: Release vs Debug config, RID-specific publish output
            // (bin/<Config>/<tfm>/<rid>/), or a containerized test runner can all
            // change how many directories sit between the test binaries and the
            // repo root.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (dir.GetFiles("*.sln", SearchOption.TopDirectoryOnly).Length > 0)
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException(
                $"Could not resolve repo root (no ancestor of '{AppContext.BaseDirectory}' contains a .sln file).");
        }
    }

    [Fact]
    public void RepoRoot_ContainsExactlyOneSolutionFile()
    {
        // This is precisely what `dotnet build` (no target argument) auto-discovers.
        // Before the fix, this directory contained zero .sln files, which is the
        // root cause of MSB1003.
        var solutionFiles = Directory.GetFiles(RepoRoot, "*.sln", SearchOption.TopDirectoryOnly);

        Assert.Single(solutionFiles);
    }

    [Fact]
    public void SolutionFile_ReferencesProjects_ThatExistOnDisk()
    {
        var solutionFile = Directory.GetFiles(RepoRoot, "*.sln", SearchOption.TopDirectoryOnly).Single();
        var solutionDir = Path.GetDirectoryName(solutionFile)!;

        var referencedProjectPaths = GetReferencedProjectRelativePaths(solutionFile);

        Assert.NotEmpty(referencedProjectPaths);

        foreach (var relativePath in referencedProjectPaths)
        {
            var fullPath = Path.GetFullPath(Path.Combine(solutionDir, relativePath));
            Assert.True(File.Exists(fullPath), $"Solution references missing project file: {fullPath}");
        }
    }

    [Fact]
    public void ReferencedProjectFiles_AreWellFormedSdkStyleProjects()
    {
        var solutionFile = Directory.GetFiles(RepoRoot, "*.sln", SearchOption.TopDirectoryOnly).Single();
        var solutionDir = Path.GetDirectoryName(solutionFile)!;

        var projectFiles = GetReferencedProjectRelativePaths(solutionFile)
            .Select(path => Path.GetFullPath(Path.Combine(solutionDir, path)))
            .ToList();

        Assert.NotEmpty(projectFiles);

        foreach (var projectFile in projectFiles)
        {
            // Parse from already-read text rather than XDocument.Load(path): the
            // latter resolves the path as a URI through XmlUrlResolver, which can
            // be blocked in locked-down/sandboxed test runners.
            var doc = XDocument.Parse(File.ReadAllText(projectFile));
            var sdkAttribute = doc.Root?.Attribute("Sdk")?.Value;

            Assert.Equal("Project", doc.Root?.Name.LocalName);
            Assert.False(string.IsNullOrWhiteSpace(sdkAttribute), $"{projectFile} is not an SDK-style project.");
        }
    }

    private static readonly string[] ProjectFileExtensions = { ".csproj", ".vbproj", ".fsproj" };

    private static List<string> GetReferencedProjectRelativePaths(string solutionFile) =>
        File.ReadLines(solutionFile)
            .Where(line => line.TrimStart().StartsWith("Project(", StringComparison.Ordinal))
            .Select(ExtractProjectRelativePath)
            .Where(path => path is not null && ProjectFileExtensions.Contains(Path.GetExtension(path)))
            .Select(path => path!)
            .ToList();

    private static string? ExtractProjectRelativePath(string projectLine)
    {
        // Lines look like:
        // Project("{GUID}") = "Name", "relative\path\Project.csproj", "{GUID}"
        // Splitting on '"' yields: [0]=Project( [1]=GUID [2]=") = " [3]=Name [4]=", " [5]=path [6]=", " [7]=GUID
        // Solution folders use the same shape but their "path" has no file extension.
        var parts = projectLine.Split('"');
        return parts.Length >= 6 ? parts[5] : null;
    }
}
