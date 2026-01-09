using Domain.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Extensions;

/// <summary>
/// Extension methods for DbContext that provide standardized exception handling.
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Saves all changes made in this context to the database with standardized exception handling.
    /// Converts database-specific exceptions (like UNIQUE constraint violations) to domain exceptions.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>The number of state entries written to the database.</returns>
    /// <exception cref="DuplicateEntityException">Thrown when a UNIQUE constraint violation occurs.</exception>
    public static async Task<int> SaveChangesWithExceptionHandlingAsync(
        this DbContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            var entityInfo = ExtractEntityInfoFromException(ex);
            throw new DuplicateEntityException(entityInfo.EntityType, entityInfo.Identifier);
        }
    }

    /// <summary>
    /// Saves all changes made in this context to the database with standardized exception handling.
    /// Allows providing entity context for more descriptive error messages.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <param name="entityType">The type of entity being saved (e.g., "User", "Role").</param>
    /// <param name="entityIdentifier">An identifier for the entity (e.g., username, role code).</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>The number of state entries written to the database.</returns>
    /// <exception cref="DuplicateEntityException">Thrown when a UNIQUE constraint violation occurs.</exception>
    public static async Task<int> SaveChangesWithExceptionHandlingAsync(
        this DbContext context,
        string entityType,
        string entityIdentifier,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new DuplicateEntityException(entityType, entityIdentifier);
        }
    }

    /// <summary>
    /// Saves all changes made in this context to the database with custom exception mapping.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <param name="duplicateExceptionFactory">
    /// Factory function that creates a DuplicateEntityException from the DbUpdateException.
    /// Return null to use default exception handling.
    /// </param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>The number of state entries written to the database.</returns>
    /// <exception cref="DuplicateEntityException">Thrown when a UNIQUE constraint violation occurs.</exception>
    public static async Task<int> SaveChangesWithExceptionHandlingAsync(
        this DbContext context,
        Func<DbUpdateException, DuplicateEntityException?> duplicateExceptionFactory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            var customException = duplicateExceptionFactory(ex);
            if (customException is not null)
            {
                throw customException;
            }
            
            var entityInfo = ExtractEntityInfoFromException(ex);
            throw new DuplicateEntityException(entityInfo.EntityType, entityInfo.Identifier);
        }
    }

    /// <summary>
    /// Checks if the exception represents a UNIQUE constraint violation.
    /// Supports SQLite, SQL Server, PostgreSQL, and MySQL.
    /// </summary>
    public static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        
        // SQLite: "UNIQUE constraint failed: TableName.ColumnName"
        if (message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
            return true;
        
        // SQL Server: "Violation of UNIQUE KEY constraint" or "Cannot insert duplicate key"
        if (message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            return true;
        
        // PostgreSQL: "duplicate key value violates unique constraint"
        if (message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase))
            return true;
        
        // MySQL: "Duplicate entry ... for key"
        if (message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase))
            return true;
        
        return false;
    }

    /// <summary>
    /// Extracts the constraint name from a UNIQUE constraint violation message.
    /// </summary>
    public static string? ExtractConstraintName(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        
        // SQLite: "UNIQUE constraint failed: Roles.Code"
        if (message.Contains("UNIQUE constraint failed:", StringComparison.OrdinalIgnoreCase))
        {
            var startIndex = message.IndexOf("UNIQUE constraint failed:", StringComparison.OrdinalIgnoreCase) + 25;
            var endIndex = message.IndexOf('\'', startIndex);
            if (endIndex == -1) endIndex = message.Length;
            return message[startIndex..endIndex].Trim();
        }
        
        return null;
    }

    /// <summary>
    /// Checks if the UNIQUE constraint violation is for a specific column.
    /// </summary>
    public static bool IsConstraintViolationForColumn(DbUpdateException ex, string columnName)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains(columnName, StringComparison.OrdinalIgnoreCase);
    }

    private static (string EntityType, string Identifier) ExtractEntityInfoFromException(DbUpdateException ex)
    {
        var constraintInfo = ExtractConstraintName(ex);
        
        if (constraintInfo is not null)
        {
            // Try to extract table and column from "TableName.ColumnName" format
            var parts = constraintInfo.Split('.');
            if (parts.Length >= 2)
            {
                return (parts[0], parts[1]);
            }
            return ("Entity", constraintInfo);
        }
        
        return ("Entity", "unknown");
    }
}
