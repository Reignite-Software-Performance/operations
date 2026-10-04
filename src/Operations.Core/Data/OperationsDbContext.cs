using Microsoft.EntityFrameworkCore;
using Operations.Core.Entities;

namespace Operations.Core.Data;

/// <summary>
/// EF Core data-access context for operations entities.
/// </summary>
public class OperationsDbContext : DbContext
{
    public OperationsDbContext(DbContextOptions<OperationsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Issue> Issues => Set<Issue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Issue>(builder =>
        {
            builder.HasKey(issue => issue.Id);
            builder.HasIndex(issue => issue.Fingerprint).IsUnique();
            builder.Property(issue => issue.Fingerprint).IsRequired();
            builder.Property(issue => issue.Signature).IsRequired();
            builder.Property(issue => issue.State).HasConversion<string>();
        });
    }
}
