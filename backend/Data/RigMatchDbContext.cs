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

    public DbSet<StandardRole> StandardRoles => Set<StandardRole>();

    public DbSet<RoleAlias> RoleAliases => Set<RoleAlias>();

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

        modelBuilder.Entity<StandardRole>()
            .HasIndex(r => r.Name)
            .IsUnique();

        modelBuilder.Entity<StandardRole>()
            .Property(r => r.Name)
            .HasMaxLength(150);

        modelBuilder.Entity<RoleAlias>()
            .HasIndex(a => a.AliasNormalized)
            .IsUnique();

        modelBuilder.Entity<RoleAlias>()
            .Property(a => a.Alias)
            .HasMaxLength(150);

        modelBuilder.Entity<RoleAlias>()
            .Property(a => a.AliasNormalized)
            .HasMaxLength(150);

        modelBuilder.Entity<RoleAlias>()
            .HasOne(a => a.StandardRole)
            .WithMany(r => r.Aliases)
            .HasForeignKey(a => a.StandardRoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
