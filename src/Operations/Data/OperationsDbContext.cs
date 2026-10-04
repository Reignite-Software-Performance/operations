using Microsoft.EntityFrameworkCore;
using Operations.Models;

namespace Operations.Data;

public sealed class OperationsDbContext : DbContext
{
    public OperationsDbContext(DbContextOptions<OperationsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Issue> Issues => Set<Issue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var issue = modelBuilder.Entity<Issue>();

        issue.HasKey(i => i.Id);
        // The fingerprint is the deduplication key: enforce it at the database level so a
        // concurrent ingestion race cannot create a second row for the same signature.
        issue.HasIndex(i => i.Fingerprint).IsUnique();
        issue.Property(i => i.Fingerprint).IsRequired().HasMaxLength(512);
        issue.Property(i => i.Signature).IsRequired();
        issue.Property(i => i.State).HasConversion<string>().HasMaxLength(32);
        issue.Property(i => i.LinkedRemediationId).HasMaxLength(512);

        base.OnModelCreating(modelBuilder);
    }
}
