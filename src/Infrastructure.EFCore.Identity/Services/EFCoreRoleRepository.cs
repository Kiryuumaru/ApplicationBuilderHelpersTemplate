using Application.Authorization.Interfaces;
using Domain.Authorization.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Identity.Services;

public sealed class EFCoreRoleRepository(EFCoreDbContext dbContext) : IRoleRepository, IRoleLookup
{
    private readonly EFCoreDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Role? FindById(Guid id)
    {
        return _dbContext.Set<Role>().Find(id);
    }

    public IReadOnlyCollection<Role> GetByIds(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        return _dbContext.Set<Role>()
            .Where(r => idList.Contains(r.Id))
            .ToList();
    }

    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Set<Role>().FindAsync([id], cancellationToken);
    }

    public async Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        return await _dbContext.Set<Role>()
            .FirstOrDefaultAsync(r => r.Code == normalizedCode, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Role>> ListAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Set<Role>().ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(Role role, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Set<Role>().FindAsync([role.Id], cancellationToken);
        if (existing == null)
        {
            _dbContext.Set<Role>().Add(role);
        }
        else
        {
            _dbContext.Entry(existing).CurrentValues.SetValues(role);
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Set<Role>().FindAsync([id], cancellationToken);
        if (role == null) return false;
        
        _dbContext.Set<Role>().Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
