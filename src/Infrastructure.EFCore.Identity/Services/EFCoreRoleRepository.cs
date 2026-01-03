using Application.Authorization.Interfaces.Infrastructure;
using Domain.Authorization.Models;
using Microsoft.EntityFrameworkCore;
using RolesConstants = Domain.Authorization.Constants.Roles;

namespace Infrastructure.EFCore.Identity.Services;

/// <summary>
/// EF Core implementation of the internal IRoleRepository.
/// Checks static/system roles from domain constants first, then falls back to database.
/// </summary>
internal sealed class EFCoreRoleRepository(IDbContextFactory<EFCoreDbContext> contextFactory) : IRoleRepository
{
    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        // Check static roles first
        if (RolesConstants.TryGetById(id, out var staticRole))
        {
            return staticRole;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<Role>().FindAsync([id], cancellationToken);
    }

    public async Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        // Check static roles first
        if (RolesConstants.TryGetByCode(code, out var staticRole))
        {
            return staticRole;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedCode = code.Trim().ToUpperInvariant();
        return await context.Set<Role>()
            .FirstOrDefaultAsync(r => r.Code == normalizedCode, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Role>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var idList = ids.Distinct().ToArray();
        if (idList.Length == 0)
        {
            return [];
        }

        var result = new Dictionary<Guid, Role>();
        var remainingIds = new List<Guid>();

        // Check static roles first
        foreach (var id in idList)
        {
            if (RolesConstants.TryGetById(id, out var staticRole))
            {
                result[id] = staticRole;
            }
            else
            {
                remainingIds.Add(id);
            }
        }

        // Query database for non-static roles
        if (remainingIds.Count > 0)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var dbRoles = await context.Set<Role>()
                .Where(r => remainingIds.Contains(r.Id))
                .ToListAsync(cancellationToken);
            
            foreach (var role in dbRoles)
            {
                result[role.Id] = role;
            }
        }

        return [.. result.Values];
    }

    public async Task<IReadOnlyCollection<Role>> ListAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, Role>();

        // Add all static roles first
        foreach (var role in RolesConstants.AllRoles)
        {
            result[role.Id] = role;
        }

        // Add database roles (excluding duplicates of static roles)
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var dbRoles = await context.Set<Role>().ToListAsync(cancellationToken);
        foreach (var role in dbRoles)
        {
            if (!RolesConstants.IsStaticRole(role.Id))
            {
                result[role.Id] = role;
            }
        }

        return [.. result.Values];
    }

    public async Task SaveAsync(Role role, CancellationToken cancellationToken)
    {
        // Static roles cannot be saved - they are defined in code
        if (RolesConstants.IsStaticRole(role.Id))
        {
            throw new InvalidOperationException($"Cannot save static role '{role.Code}'. Static roles are defined in code.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        
        var existing = await context.Set<Role>().FindAsync([role.Id], cancellationToken);
        if (existing is null)
        {
            context.Set<Role>().Add(role);
        }
        else
        {
            context.Entry(existing).CurrentValues.SetValues(role);
        }
        
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        // Static roles cannot be deleted
        if (RolesConstants.IsStaticRole(id))
        {
            throw new InvalidOperationException($"Cannot delete static role. Static roles are defined in code.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        
        var role = await context.Set<Role>().FindAsync([id], cancellationToken);
        if (role is null)
        {
            return false;
        }

        context.Set<Role>().Remove(role);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
