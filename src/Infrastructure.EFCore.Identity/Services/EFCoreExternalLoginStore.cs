using Application.Identity.Interfaces;
using Application.Identity.Models;
using Domain.Identity.Enums;
using Infrastructure.EFCore;
using Infrastructure.EFCore.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Identity.Services;

/// <summary>
/// EF Core implementation of external login storage.
/// </summary>
public sealed class EFCoreExternalLoginStore(IDbContextFactory<EFCoreDbContext> contextFactory) : IExternalLoginStore
{
    public async Task<Guid?> FindUserByLoginAsync(
        ExternalLoginProvider provider,
        string providerKey,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var providerName = provider.ToString();
        var login = await context.Set<UserLoginEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.LoginProvider == providerName && l.ProviderKey == providerKey,
                cancellationToken);

        return login?.UserId;
    }

    public async Task AddLoginAsync(
        Guid userId,
        ExternalLoginProvider provider,
        string providerKey,
        string? displayName,
        string? email,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var providerName = provider.ToString();

        // Check if already exists
        var existing = await context.Set<UserLoginEntity>()
            .FirstOrDefaultAsync(
                l => l.UserId == userId && l.LoginProvider == providerName,
                cancellationToken);

        if (existing is not null)
        {
            // Update existing
            existing.ProviderKey = providerKey;
            existing.ProviderDisplayName = displayName;
            existing.Email = email;
        }
        else
        {
            // Add new
            context.Set<UserLoginEntity>().Add(new UserLoginEntity
            {
                UserId = userId,
                LoginProvider = providerName,
                ProviderKey = providerKey,
                ProviderDisplayName = displayName,
                Email = email,
                LinkedAt = DateTimeOffset.UtcNow
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveLoginAsync(
        Guid userId,
        ExternalLoginProvider provider,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var providerName = provider.ToString();
        var login = await context.Set<UserLoginEntity>()
            .FirstOrDefaultAsync(
                l => l.UserId == userId && l.LoginProvider == providerName,
                cancellationToken);

        if (login is null)
        {
            return false;
        }

        context.Set<UserLoginEntity>().Remove(login);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyCollection<ExternalLoginInfo>> GetLoginsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var logins = await context.Set<UserLoginEntity>()
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .ToListAsync(cancellationToken);

        return logins.Select(l => new ExternalLoginInfo
        {
            Provider = l.LoginProvider,
            DisplayName = l.ProviderDisplayName,
            Email = l.Email,
            LinkedAt = l.LinkedAt
        }).ToList();
    }

    public async Task<bool> HasAnyLoginAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Set<UserLoginEntity>()
            .AsNoTracking()
            .AnyAsync(l => l.UserId == userId, cancellationToken);
    }
}
