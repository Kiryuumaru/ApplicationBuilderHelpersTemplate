using Infrastructure.Sqlite;

namespace Infrastructure.Sqlite.Identity;

public sealed class IdentityTableInitializer(SqliteConnectionFactory connectionFactory) : DatabaseBootstrap(connectionFactory)
{
    public override async Task SetupAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                UserName TEXT,
                NormalizedUserName TEXT,
                Email TEXT,
                NormalizedEmail TEXT,
                EmailConfirmed INTEGER NOT NULL,
                PasswordHash TEXT,
                SecurityStamp TEXT,
                ConcurrencyStamp TEXT,
                PhoneNumber TEXT,
                PhoneNumberConfirmed INTEGER NOT NULL,
                TwoFactorEnabled INTEGER NOT NULL,
                LockoutEnd TEXT,
                LockoutEnabled INTEGER NOT NULL,
                AccessFailedCount INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Roles (
                Id TEXT PRIMARY KEY,
                Code TEXT,
                Name TEXT,
                NormalizedName TEXT,
                Description TEXT,
                IsSystemRole INTEGER NOT NULL,
                ConcurrencyStamp TEXT
            );

            CREATE TABLE IF NOT EXISTS UserRoles (
                UserId TEXT NOT NULL,
                RoleId TEXT NOT NULL,
                ParameterValues TEXT,
                PRIMARY KEY (UserId, RoleId),
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS UserPermissions (
                UserId TEXT NOT NULL,
                PermissionIdentifier TEXT NOT NULL,
                PRIMARY KEY (UserId, PermissionIdentifier),
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS UserClaims (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                ClaimType TEXT,
                ClaimValue TEXT,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS RoleClaims (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RoleId TEXT NOT NULL,
                ClaimType TEXT,
                ClaimValue TEXT,
                FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS UserLogins (
                LoginProvider TEXT NOT NULL,
                ProviderKey TEXT NOT NULL,
                ProviderDisplayName TEXT,
                UserId TEXT NOT NULL,
                PRIMARY KEY (LoginProvider, ProviderKey),
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS UserTokens (
                UserId TEXT NOT NULL,
                LoginProvider TEXT NOT NULL,
                Name TEXT NOT NULL,
                Value TEXT,
                PRIMARY KEY (UserId, LoginProvider, Name),
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS RolePermissions (
                RoleId TEXT NOT NULL,
                IdentifierTemplate TEXT NOT NULL,
                Description TEXT,
                RequiredParameters TEXT,
                FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE
            );
        ";

        await ExecuteSqlAsync(sql, cancellationToken);
    }
}
