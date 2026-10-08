using Dapper;
using GeekGuard.Core.Data.Migrations;
using GeekGuard.Core.Users;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GeekGuard.Core.Data;

public static class DatabaseServiceCollectionExtensions
{
    /// <summary>
    /// Registers PostgreSQL access, the migration runner, the core migrations and the core repositories.
    /// Call <see cref="MigrationRunner.RunAsync"/> once at startup before handling updates.
    /// </summary>
    public static IServiceCollection AddGeekGuardDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        // Lets Dapper map snake_case columns (first_name) to PascalCase properties (FirstName).
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        services.AddOptions<DatabaseOptions>()
            .Configure(o => o.ConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? "")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString),
                $"ConnectionStrings:{DatabaseOptions.ConnectionStringName} is missing. Set it with User Secrets (see README).")
            .ValidateOnStart();

        services.AddSingleton(sp => NpgsqlDataSource.Create(sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddSingleton<MigrationRunner>();

        // Core tables come first; plugins add their own sources after this.
        services.AddSingleton<IMigrationSource>(new EmbeddedMigrationSource("core", typeof(DatabaseOptions).Assembly));

        services.AddSingleton<UserRepository>();
        return services;
    }
}
