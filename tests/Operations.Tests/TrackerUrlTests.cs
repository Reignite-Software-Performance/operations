using Operations.Issues;
using Xunit;

namespace Operations.Tests;

public class TrackerUrlTests
{
    [Fact]
    public void ForFingerprint_IsDeterministic()
    {
        var first = TrackerUrl.ForFingerprint("abc123");
        var second = TrackerUrl.ForFingerprint("abc123");

        Assert.Equal(first, second);
        Assert.Equal("/tracker/abc123", first);
    }

    [Fact]
    public void ForFingerprint_UsesTrackerRoutePrefix()
    {
        var url = TrackerUrl.ForFingerprint("deadbeef");

        Assert.StartsWith("/tracker/", url);
        Assert.EndsWith("deadbeef", url);
    }

    [Fact]
    public void ForFingerprint_EscapesReservedCharacters()
    {
        var url = TrackerUrl.ForFingerprint("a/b c");

        Assert.Equal("/tracker/a%2Fb%20c", url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ForFingerprint_RejectsBlankFingerprint(string? fingerprint)
    {
        Assert.ThrowsAny<ArgumentException>(() => TrackerUrl.ForFingerprint(fingerprint!));
    }
}
