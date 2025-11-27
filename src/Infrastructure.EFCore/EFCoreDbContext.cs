using Domain.Authorization.Models;
using Domain.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore;

/// <summary>
/// Base DbContext for EF Core infrastructure. Provider-specific contexts should inherit from this.
/// </summary>
public abstract class EFCoreDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<LocalStoreEntry> LocalStore => Set<LocalStoreEntry>();

    protected EFCoreDbContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        ConfigureUser(modelBuilder);

        // Configure Role entity
        ConfigureRole(modelBuilder);

        // Configure LocalStore table
        ConfigureLocalStore(modelBuilder);
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<User>();

        entity.ToTable("Users");

        entity.HasKey(u => u.Id);

        entity.Property(u => u.Id)
            .HasConversion(id => id.ToString(), str => Guid.Parse(str))
            .IsRequired();

        entity.Property(u => u.RevId)
            .HasConversion(id => id.ToString(), str => Guid.Parse(str));

        entity.Property(u => u.UserName).IsRequired().HasMaxLength(256);
        entity.Property(u => u.NormalizedUserName).IsRequired().HasMaxLength(256);
        entity.Property(u => u.Email).HasMaxLength(256);
        entity.Property(u => u.NormalizedEmail).HasMaxLength(256);
        entity.Property(u => u.PasswordHash);
        entity.Property(u => u.SecurityStamp);
        entity.Property(u => u.PhoneNumber).HasMaxLength(20);
        entity.Property(u => u.AuthenticatorKey);
        entity.Property(u => u.RecoveryCodes);

        entity.HasIndex(u => u.NormalizedUserName).IsUnique();
        entity.HasIndex(u => u.NormalizedEmail);

        // Ignore navigation properties - we'll store them in separate tables
        entity.Ignore(u => u.PermissionGrants);
        entity.Ignore(u => u.RoleIds);
        entity.Ignore(u => u.RoleAssignments);
        entity.Ignore(u => u.IdentityLinks);
        entity.Ignore(u => u.Status);
        entity.Ignore(u => u.LastLoginAt);
        entity.Ignore(u => u.LastFailedLoginAt);
    }

    private static void ConfigureRole(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Role>();

        entity.ToTable("Roles");

        entity.HasKey(r => r.Id);

        entity.Property(r => r.Id)
            .HasConversion(id => id.ToString(), str => Guid.Parse(str))
            .IsRequired();

        entity.Property(r => r.RevId)
            .HasConversion(id => id.ToString(), str => Guid.Parse(str));

        entity.Property(r => r.Code).IsRequired().HasMaxLength(100);
        entity.Property(r => r.Name).IsRequired().HasMaxLength(256);
        entity.Property(r => r.NormalizedName).IsRequired().HasMaxLength(256);
        entity.Property(r => r.Description).HasMaxLength(1000);

        entity.HasIndex(r => r.Code).IsUnique();
        entity.HasIndex(r => r.NormalizedName).IsUnique();

        // Ignore PermissionGrants - stored in separate table
        entity.Ignore(r => r.PermissionGrants);
    }

    private static void ConfigureLocalStore(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocalStoreEntry>(entity =>
        {
            entity.ToTable("LocalStore");
            entity.HasKey(e => new { e.Group, e.Id });
            entity.Property(e => e.Group).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Id).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Data);
        });
    }
}

/// <summary>
/// Entity for local key-value storage.
/// </summary>
public class LocalStoreEntry
{
    public required string Group { get; set; }
    public required string Id { get; set; }
    public string? Data { get; set; }
}
