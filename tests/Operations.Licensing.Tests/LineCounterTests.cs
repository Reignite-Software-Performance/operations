using Operations.Licensing;

namespace Operations.Licensing.Tests;

public class LineCounterTests
{
    [Fact]
    public void CountNonBlankLines_ExcludesBlankAndWhitespaceOnlyLines()
    {
        var lines = new[]
        {
            "namespace Foo;",
            "",
            "public class Bar",
            "   ",
            "{",
            "\t",
            "}",
        };

        var result = LineCounter.CountNonBlankLines(lines);

        Assert.Equal(4, result);
    }

    [Fact]
    public void CountNonBlankLines_EmptyCollection_ReturnsZero()
    {
        var result = LineCounter.CountNonBlankLines(Array.Empty<string>());

        Assert.Equal(0, result);
    }

    [Fact]
    public void CountLines_NullOrWhitespacePath_Throws()
    {
        Assert.Throws<ArgumentException>(() => LineCounter.CountLines(""));
        Assert.Throws<ArgumentException>(() => LineCounter.CountLines("   "));
    }

    [Fact]
    public void CountLines_NonExistentDirectory_ThrowsDirectoryNotFound()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(() => LineCounter.CountLines(missing));
    }

    [Fact]
    public void CountLines_OnlyCountsCsAndVbFiles_RecursivelyAndExcludesBlankLines()
    {
        var root = Path.Combine(Path.GetTempPath(), "linecounter-test-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "nested");
        Directory.CreateDirectory(nested);

        try
        {
            File.WriteAllLines(Path.Combine(root, "Program.cs"), new[]
            {
                "using System;",
                "",
                "Console.WriteLine(\"hi\");",
            });

            File.WriteAllLines(Path.Combine(nested, "Module.vb"), new[]
            {
                "Module Program",
                "    ' comment counts as a line",
                "End Module",
                "",
            });

            // Non-source files must be ignored entirely.
            File.WriteAllLines(Path.Combine(root, "notes.txt"), new[]
            {
                "this should not be counted",
                "neither should this",
            });

            var result = LineCounter.CountLines(root);

            Assert.Equal(5, result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
