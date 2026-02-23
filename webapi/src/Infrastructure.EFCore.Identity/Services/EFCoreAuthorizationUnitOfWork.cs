using Domain.Authorization.Interfaces;
using Infrastructure.EFCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Identity.Services;

/// <summary>
/// EF Core implementation of IAuthorizationUnitOfWork.
/// Coordinates persistence for Authorization feature repositories using a shared DbContext.
/// </summary>
internal sealed class EFCoreAuthorizationUnitOfWork(EFCoreDbContext context) : IAuthorizationUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesWithExceptionHandlingAsync(cancellationToken);
    }
}
