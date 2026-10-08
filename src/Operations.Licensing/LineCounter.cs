namespace Operations.Licensing;

/// <summary>
/// Counts non-blank lines of code across a directory tree, mirroring the
/// licensing-tier line-count rule described in the repo README: only C#
/// (.cs) and VB.NET (.vb) source files count, and blank/whitespace-only
/// lines are excluded.
/// </summary>
public static class LineCounter
{
    private static readonly string[] IncludedExtensions = { ".cs", ".vb" };

    /// <summary>
    /// Recursively counts non-blank lines in all .cs and .vb files under
    /// <paramref name="rootPath"/>.
    /// </summary>
    /// <param name="rootPath">Directory to scan.</param>
    /// <exception cref="ArgumentException">When <paramref name="rootPath"/> is null or whitespace.</exception>
    /// <exception cref="DirectoryNotFoundException">When <paramref name="rootPath"/> does not exist.</exception>
    public static int CountLines(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Path must not be null or whitespace.", nameof(rootPath));
        }

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {rootPath}");
        }

        var files = IncludedExtensions
            .SelectMany(ext => Directory.EnumerateFiles(rootPath, $"*{ext}", SearchOption.AllDirectories));

        var total = 0;
        foreach (var file in files)
        {
            total += CountNonBlankLines(file);
        }

        return total;
    }

    /// <summary>
    /// Counts non-blank lines within a single source file's contents.
    /// </summary>
    public static int CountNonBlankLines(IEnumerable<string> lines) =>
        lines.Count(line => !string.IsNullOrWhiteSpace(line));

    private static int CountNonBlankLines(string filePath) =>
        CountNonBlankLines(File.ReadLines(filePath));
}
