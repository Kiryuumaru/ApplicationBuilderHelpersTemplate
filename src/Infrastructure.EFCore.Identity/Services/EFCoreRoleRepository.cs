using Application.Authorization.Interfaces;
using Domain.Authorization.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Identity.Services;

public sealed class EFCoreRoleRepository(EFCoreDbContext dbContext) : IRoleRepository, IRoleLookup
{
    private readonly EFCoreDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Role? FindById(Guid id)
    {
        return _dbContext.Roles.Find(id);
    }

    public IReadOnlyCollection<Role> GetByIds(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        return _dbContext.Roles
            .Where(r => idList.Contains(r.Id))
            .ToList();
    }

    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Roles.FindAsync([id], cancellationToken);
    }

    public async Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        return await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Code == normalizedCode, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Role>> ListAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Roles.ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(Role role, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Roles.FindAsync([role.Id], cancellationToken);
        if (existing == null)
        {
            _dbContext.Roles.Add(role);
        }
        else
        {
            _dbContext.Entry(existing).CurrentValues.SetValues(role);
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.FindAsync([id], cancellationToken);
        if (role == null) return false;
        
        _dbContext.Roles.Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
