using DevDocSpace.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocSpace.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ApiSpec> ApiSpecs => Set<ApiSpec>();
    public DbSet<ServiceEnvironment> ServiceEnvironments => Set<ServiceEnvironment>();
    public DbSet<DocNamespace> DocNamespaces => Set<DocNamespace>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<ApiCallLog> ApiCallLogs => Set<ApiCallLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.FirebaseUid).IsUnique();
            e.HasIndex(u => u.Email);
            e.Property(u => u.Role).HasConversion<string>();
        });

        b.Entity<ApiSpec>(e =>
        {
            e.HasIndex(s => new { s.Service, s.Version }).IsUnique();
            e.Property(s => s.RequiredRole).HasConversion<string>();
            e.HasMany(s => s.Environments).WithOne(x => x.Spec).HasForeignKey(x => x.SpecId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ServiceEnvironment>(e =>
        {
            e.HasIndex(x => new { x.SpecId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasConversion<string>();
        });

        b.Entity<DocNamespace>(e =>
        {
            e.HasIndex(n => n.Slug).IsUnique();
            e.Property(n => n.RequiredRole).HasConversion<string>();
        });

        b.Entity<ApiKey>(e =>
        {
            e.HasIndex(k => k.Prefix);
            e.HasOne(k => k.User).WithMany(u => u.ApiKeys).HasForeignKey(k => k.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(k => k.IsActive);
        });

        b.Entity<ApiCallLog>(e =>
        {
            e.HasIndex(l => new { l.UserId, l.At });
            e.Property(l => l.Environment).HasConversion<string>();
        });
    }
}
