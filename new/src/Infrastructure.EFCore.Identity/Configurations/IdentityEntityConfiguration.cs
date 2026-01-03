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
        ConfigureUserPasskey(modelBuilder);  // For SignInManager/Blazor cookie-based passkeys
        ConfigurePasskeyChallenge(modelBuilder);  // For REST API passkeys
        ConfigurePasskeyCredential(modelBuilder);  // For REST API passkeys
        ConfigureUserRoleAssignment(modelBuilder);
        ConfigureLoginSession(modelBuilder);
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

        entity.Property(u => u.UserName).HasMaxLength(256);
        entity.Property(u => u.NormalizedUserName).HasMaxLength(256);
        entity.Property(u => u.Email).HasMaxLength(256);
        entity.Property(u => u.NormalizedEmail).HasMaxLength(256);
        entity.Property(u => u.PasswordHash);
        entity.Property(u => u.SecurityStamp);
        entity.Property(u => u.PhoneNumber).HasMaxLength(20);
        entity.Property(u => u.AuthenticatorKey);
        entity.Property(u => u.RecoveryCodes);

        entity.HasIndex(u => u.NormalizedUserName).IsUnique();
        entity.HasIndex(u => u.NormalizedEmail);

        // Map login tracking fields for cleanup queries
        entity.Property(u => u.LastLoginAt);
        entity.Property(u => u.LastFailedLoginAt);
        
        // Ignore navigation properties - we'll store them in separate tables
        entity.Ignore(u => u.PermissionGrants);
        entity.Ignore(u => u.RoleIds);
        entity.Ignore(u => u.RoleAssignments);
        entity.Ignore(u => u.IdentityLinks);
        entity.Ignore(u => u.Status);
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
        
        // Ignore ScopeTemplates - roles are defined in code, not persisted
        entity.Ignore(r => r.ScopeTemplates);
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

    private static void ConfigureUserPasskey(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserPasskeyEntity>();

        entity.ToTable("UserPasskeys");

        entity.HasKey(up => new { up.UserId, up.CredentialId });

        entity.Property(up => up.UserId)
            .HasConversion(id => id.ToString(), str => Guid.Parse(str))
            .IsRequired();
        entity.Property(up => up.CredentialId).IsRequired();
        entity.Property(up => up.PublicKey);
        entity.Property(up => up.Name).HasMaxLength(256);
        entity.Property(up => up.SignCount);
        entity.Property(up => up.Transports);
        entity.Property(up => up.AttestationObject);
        entity.Property(up => up.ClientDataJson);

        entity.HasIndex(up => up.CredentialId);
    }

    private static void ConfigurePasskeyChallenge(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PasskeyChallengeEntity>();

        entity.ToTable("PasskeyChallenges");

        entity.HasKey(c => c.Id);

        entity.Property(c => c.Id)
            .HasConversion(id => id.ToString(), str => Guid.Parse(str))
            .IsRequired();
        entity.Property(c => c.Challenge).IsRequired();
        entity.Property(c => c.UserId)
            .HasConversion(id => id.HasValue ? id.Value.ToString() : null, str => str != null ? Guid.Parse(str) : null);
        entity.Property(c => c.Type).IsRequired();
        entity.Property(c => c.OptionsJson).IsRequired();
        entity.Property(c => c.CreatedAt).IsRequired();
        entity.Property(c => c.ExpiresAt).IsRequired();

        entity.HasIndex(c => c.ExpiresAt);  // For cleanup queries
    }

    private static void ConfigurePasskeyCredential(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PasskeyCredentialEntity>();

        entity.ToTable("PasskeyCredentials");

        entity.HasKey(c => c.Id);

        entity.Property(c => c.Id)
            .HasConversion(id => id.ToString(), str => Guid.Parse(str))
            .IsRequired();
        entity.Property(c => c.UserId)
            .HasConversion(id => id.ToString(), str => Guid.Parse(str))
            .IsRequired();
        entity.Property(c => c.Name).IsRequired().HasMaxLength(256);
        entity.Property(c => c.CredentialId).IsRequired();
        entity.Property(c => c.PublicKey).IsRequired();
        entity.Property(c => c.SignCount).IsRequired();
        entity.Property(c => c.AaGuid)
            .HasConversion(id => id.ToString(), str => Guid.Parse(str))
            .IsRequired();
        entity.Property(c => c.CredentialType).IsRequired().HasMaxLength(50);
        entity.Property(c => c.RegisteredAt).IsRequired();
        entity.Property(c => c.UserHandle).IsRequired();
        entity.Property(c => c.AttestationFormat).IsRequired().HasMaxLength(50);

        entity.HasIndex(c => c.UserId);
        entity.HasIndex(c => c.CredentialId);
        entity.HasIndex(c => c.UserHandle);
    }

    private static void ConfigureUserRoleAssignment(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserRoleAssignmentEntity>();

        entity.ToTable("UserRoleAssignments");

        entity.HasKey(ura => new { ura.UserId, ura.RoleId });

        entity.Property(ura => ura.UserId)
            .HasConversion(id => id.ToString(), str => Guid.Parse(str))
            .IsRequired();
        entity.Property(ura => ura.RoleId)
            .HasConversion(id => id.ToString(), str => Guid.Parse(str))
            .IsRequired();
        entity.Property(ura => ura.ParameterValuesJson).HasMaxLength(4000);

        entity.HasIndex(ura => ura.UserId);
        entity.HasIndex(ura => ura.RoleId);
    }

    private static void ConfigureLoginSession(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<LoginSessionEntity>();

        entity.ToTable("LoginSessions");

        entity.HasKey(s => s.Id);

        entity.Property(s => s.Id)
            .HasConversion(id => id.ToString(), str => Guid.Parse(str))
            .IsRequired();
        entity.Property(s => s.UserId)
            .HasConversion(id => id.ToString(), str => Guid.Parse(str))
            .IsRequired();
        entity.Property(s => s.RefreshTokenHash).IsRequired().HasMaxLength(256);
        entity.Property(s => s.DeviceName).HasMaxLength(256);
        entity.Property(s => s.UserAgent).HasMaxLength(512);
        entity.Property(s => s.IpAddress).HasMaxLength(64);
        entity.Property(s => s.CreatedAt).IsRequired();
        entity.Property(s => s.LastUsedAt).IsRequired();
        entity.Property(s => s.ExpiresAt).IsRequired();
        entity.Property(s => s.IsRevoked).IsRequired();

        entity.HasIndex(s => s.UserId);
        entity.HasIndex(s => s.ExpiresAt);  // For cleanup queries
        entity.HasIndex(s => new { s.UserId, s.IsRevoked });  // For active sessions query
    }
}
