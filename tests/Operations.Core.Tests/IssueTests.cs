using Operations.Core.Entities;
using Xunit;

namespace Operations.Core.Tests;

public class IssueTests
{
    [Fact]
    public void DeriveId_IsDeterministic_ForSameFingerprint()
    {
        var first = Issue.DeriveId("boom.NullReferenceException:line42");
        var second = Issue.DeriveId("boom.NullReferenceException:line42");

        Assert.Equal(first, second);
    }

    [Fact]
    public void DeriveId_DiffersAcrossFingerprints()
    {
        var first = Issue.DeriveId("fingerprint-a");
        var second = Issue.DeriveId("fingerprint-b");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DeriveId_ThrowsForEmptyFingerprint()
    {
        Assert.Throws<ArgumentException>(() => Issue.DeriveId(string.Empty));
    }

    [Fact]
    public void Create_InitializesNewIssueInNewState()
    {
        var occurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var issue = Issue.Create("fp-1", "NullReferenceException at Foo.Bar", occurredAt);

        Assert.Equal(Issue.DeriveId("fp-1"), issue.Id);
        Assert.Equal(IssueState.New, issue.State);
        Assert.Equal(1, issue.OccurrenceCount);
        Assert.Equal(occurredAt, issue.FirstSeenUtc);
        Assert.Equal(occurredAt, issue.LastSeenUtc);
        Assert.Null(issue.LinkedRemediationId);
    }
}
