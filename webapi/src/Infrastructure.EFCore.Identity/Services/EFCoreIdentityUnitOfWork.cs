using Domain.Identity.Interfaces;
using Infrastructure.EFCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Identity.Services;

/// <summary>
/// EF Core implementation of IIdentityUnitOfWork.
/// Coordinates persistence for Identity feature repositories using a shared DbContext.
/// </summary>
internal sealed class EFCoreIdentityUnitOfWork(EFCoreDbContext context) : IIdentityUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesWithExceptionHandlingAsync(cancellationToken);
    }
}
