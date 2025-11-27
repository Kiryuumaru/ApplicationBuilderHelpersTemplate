using Domain.Authorization.Models;
using Domain.Identity.Models;
using Infrastructure.EFCore.Identity.Models;
using Infrastructure.EFCore.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Identity.Configurations;

/// <summary>
/// EF Core entity configuration for User and Role entities.
/// </summary>
public class IdentityEntityConfiguration : IEFCoreEntityConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ConfigureUser(modelBuilder);
        ConfigureRole(modelBuilder);
        ConfigureUserLogin(modelBuilder);
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

    private static void ConfigureUserLogin(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserLoginEntity>();

        entity.ToTable("UserLogins");

        entity.HasKey(ul => new { ul.LoginProvider, ul.ProviderKey });

        entity.Property(ul => ul.LoginProvider).IsRequired().HasMaxLength(128);
        entity.Property(ul => ul.ProviderKey).IsRequired().HasMaxLength(128);
        entity.Property(ul => ul.UserId)
            .HasConversion(id => id.ToString(), str => Guid.Parse(str))
            .IsRequired();
        entity.Property(ul => ul.ProviderDisplayName).HasMaxLength(256);
        entity.Property(ul => ul.Email).HasMaxLength(256);

        entity.HasIndex(ul => ul.UserId);
    }
}
