using Microsoft.EntityFrameworkCore;
using RigMatch.Api.Data.Entities;

namespace RigMatch.Api.Data;

public class RigMatchDbContext : DbContext
{
    public RigMatchDbContext(DbContextOptions<RigMatchDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<CvRecord> CvRecords => Set<CvRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>()
            .HasIndex(c => c.ExternalId)
            .IsUnique();

        modelBuilder.Entity<Company>()
            .Property(c => c.ExternalId)
            .HasMaxLength(200);

        modelBuilder.Entity<Company>()
            .Property(c => c.Name)
            .HasMaxLength(200);

        modelBuilder.Entity<CvRecord>()
            .Property(c => c.FileUrl)
            .HasMaxLength(500);

        modelBuilder.Entity<CvRecord>()
            .HasOne(c => c.Company)
            .WithMany(c => c.CvRecords)
            .HasForeignKey(c => c.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
