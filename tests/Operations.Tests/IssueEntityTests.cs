using Operations.Models;
using Xunit;

namespace Operations.Tests;

public class IssueEntityTests
{
    private static readonly DateTime Onset = new(2026, 10, 1, 8, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateNew_InitialisesNewIssue()
    {
        var issue = Issue.CreateNew("sig", "fp", Onset);

        Assert.Equal("fp", issue.Fingerprint);
        Assert.Equal("sig", issue.Signature);
        Assert.Equal(IssueState.New, issue.State);
        Assert.Equal(1, issue.OccurrenceCount);
        Assert.Equal(Onset, issue.FirstSeenUtc);
        Assert.Equal(Onset, issue.LastSeenUtc);
        Assert.Equal(Onset, issue.CreatedUtc);
        Assert.Null(issue.LinkedRemediationId);
    }

    [Fact]
    public void CreateNew_UnspecifiedKind_IsStoredAsUtc()
    {
        var issue = Issue.CreateNew("sig", "fp", new DateTime(2026, 10, 1, 8, 30, 0, DateTimeKind.Unspecified));

        Assert.Equal(DateTimeKind.Utc, issue.FirstSeenUtc.Kind);
    }

    [Fact]
    public void RecordOccurrence_IncrementsAndAdvancesLastSeen_PreservingOnset()
    {
        var issue = Issue.CreateNew("sig", "fp", Onset);
        var later = Onset.AddHours(3);

        issue.RecordOccurrence(later);

        Assert.Equal(2, issue.OccurrenceCount);
        Assert.Equal(Onset, issue.FirstSeenUtc);
        Assert.Equal(later, issue.LastSeenUtc);
        Assert.Equal(later, issue.UpdatedUtc);
    }

    [Fact]
    public void DeriveId_IsDeterministic()
    {
        Assert.Equal(Issue.DeriveId("fp-stable"), Issue.DeriveId("fp-stable"));
    }

    [Fact]
    public void DeriveId_DiffersForDifferentFingerprints()
    {
        Assert.NotEqual(Issue.DeriveId("fp-a"), Issue.DeriveId("fp-b"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void DeriveId_BlankFingerprint_Throws(string fingerprint)
    {
        Assert.ThrowsAny<ArgumentException>(() => Issue.DeriveId(fingerprint));
    }

    [Fact]
    public void CreateNew_BlankSignature_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => Issue.CreateNew("  ", "fp", Onset));
    }
}
