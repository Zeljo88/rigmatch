using Microsoft.EntityFrameworkCore;
using RigMatch.Api.Data.Entities;

namespace RigMatch.Api.Data;

public class RigMatchDbContext : DbContext
{
    public RigMatchDbContext(DbContextOptions<RigMatchDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<EmployerUser> EmployerUsers => Set<EmployerUser>();

    public DbSet<CvRecord> CvRecords => Set<CvRecord>();

    public DbSet<CompanyProject> CompanyProjects => Set<CompanyProject>();

    public DbSet<StandardRole> StandardRoles => Set<StandardRole>();

    public DbSet<RoleAlias> RoleAliases => Set<RoleAlias>();

    public DbSet<SuggestedRoleAlias> SuggestedRoleAliases => Set<SuggestedRoleAlias>();

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

        modelBuilder.Entity<EmployerUser>()
            .HasIndex(user => user.EmailNormalized)
            .IsUnique();

        modelBuilder.Entity<EmployerUser>()
            .Property(user => user.FullName)
            .HasMaxLength(200);

        modelBuilder.Entity<EmployerUser>()
            .Property(user => user.Email)
            .HasMaxLength(200);

        modelBuilder.Entity<EmployerUser>()
            .Property(user => user.EmailNormalized)
            .HasMaxLength(200);

        modelBuilder.Entity<EmployerUser>()
            .HasOne(user => user.Company)
            .WithMany(company => company.Users)
            .HasForeignKey(user => user.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CvRecord>()
            .Property(c => c.FileUrl)
            .HasMaxLength(500);

        modelBuilder.Entity<CvRecord>()
            .Property(c => c.FileHash)
            .HasMaxLength(128);

        modelBuilder.Entity<CvRecord>()
            .HasIndex(c => c.FileHash);

        modelBuilder.Entity<CvRecord>()
            .HasOne(c => c.Company)
            .WithMany(c => c.CvRecords)
            .HasForeignKey(c => c.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CompanyProject>()
            .Property(project => project.Title)
            .HasMaxLength(200);

        modelBuilder.Entity<CompanyProject>()
            .Property(project => project.ClientName)
            .HasMaxLength(200);

        modelBuilder.Entity<CompanyProject>()
            .Property(project => project.PrimaryRole)
            .HasMaxLength(150);

        modelBuilder.Entity<CompanyProject>()
            .Property(project => project.Location)
            .HasMaxLength(200);

        modelBuilder.Entity<CompanyProject>()
            .Property(project => project.PreferredEducation)
            .HasMaxLength(200);

        modelBuilder.Entity<CompanyProject>()
            .Property(project => project.Status)
            .HasMaxLength(50);

        modelBuilder.Entity<CompanyProject>()
            .HasIndex(project => project.CompanyId);

        modelBuilder.Entity<CompanyProject>()
            .HasOne(project => project.Company)
            .WithMany(company => company.Projects)
            .HasForeignKey(project => project.CompanyId)
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
            .Property(a => a.RequiresReview)
            .HasDefaultValue(false);

        modelBuilder.Entity<RoleAlias>()
            .HasOne(a => a.StandardRole)
            .WithMany(r => r.Aliases)
            .HasForeignKey(a => a.StandardRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SuggestedRoleAlias>()
            .HasIndex(a => new { a.CompanyId, a.StandardRoleId, a.RawAliasNormalized })
            .IsUnique();

        modelBuilder.Entity<SuggestedRoleAlias>()
            .Property(a => a.RawAlias)
            .HasMaxLength(150);

        modelBuilder.Entity<SuggestedRoleAlias>()
            .Property(a => a.RawAliasNormalized)
            .HasMaxLength(150);

        modelBuilder.Entity<SuggestedRoleAlias>()
            .HasOne(a => a.Company)
            .WithMany(c => c.SuggestedRoleAliases)
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SuggestedRoleAlias>()
            .HasOne(a => a.StandardRole)
            .WithMany(r => r.SuggestedAliases)
            .HasForeignKey(a => a.StandardRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SuggestedRoleAlias>()
            .HasOne(a => a.LastCvRecord)
            .WithMany(r => r.SuggestedRoleAliases)
            .HasForeignKey(a => a.LastCvRecordId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
