using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Foundation.Domain.Interfaces.Data;
using RevendaPro.Global.Infrastructure.Database.Contexts;

namespace RevendaPro.Global.Infrastructure.Database
{
    /// <summary>
    /// Applies the pending migrations without going through <c>Database.MigrateAsync</c>.
    ///
    /// Why not the built-in call: since EF Core 9 the migrator takes an exclusive lock via
    /// <c>GET_LOCK</c> before applying anything, and Oracle's MySQL provider reads the
    /// result as a non-nullable <c>long</c>. MariaDB can answer NULL there, and the startup
    /// dies with "Unable to cast object of type 'System.DBNull' to type 'System.Int64'".
    ///
    /// The workaround fits this architecture rather than fighting it: EF Core is only here
    /// to generate the SQL, so the SQL is generated and executed through the same Dapper
    /// connection everything else uses. History is kept in <c>__EFMigrationsHistory</c>, the
    /// same table EF reads, so <c>dotnet ef</c> keeps working normally.
    ///
    /// One instance runs at a time thanks to the advisory lock taken below, so two API
    /// replicas starting together do not apply the same migration twice.
    /// </summary>
    public class SchemaMigrator(
        RevendaProDbContext context,
        ISqlConnectionFactory connectionFactory,
        ILogger<SchemaMigrator> logger)
    {
        private const string HistoryTable = "__EFMigrationsHistory";
        private const string LockName = "revendapro_schema_migration";

        /// <summary>Applies every migration that is not recorded yet.</summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>How many migrations were applied.</returns>
        public async Task<int> ApplyAsync(CancellationToken cancellationToken = default)
        {
            using var connection = connectionFactory.CreateConnection();
            connection.Open();

            await AcquireLockAsync(connection, cancellationToken).ConfigureAwait(false);

            try
            {
                await EnsureHistoryTableAsync(connection, cancellationToken).ConfigureAwait(false);

                var applied = (await connection.QueryAsync<string>(
                        new CommandDefinition(
                            $"SELECT MigrationId FROM `{HistoryTable}`",
                            cancellationToken: cancellationToken))
                    .ConfigureAwait(false))
                    .ToHashSet(StringComparer.Ordinal);

                var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
                var assembly = context.GetInfrastructure().GetRequiredService<IMigrationsAssembly>();

                var pending = assembly.Migrations.Keys
                    .Where(id => !applied.Contains(id))
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList();

                if (pending.Count == 0)
                {
                    return 0;
                }


                foreach (var migrationId in pending)
                {
                    logger.LogInformation("Applying migration {MigrationId}.", migrationId);

                    var previous = applied.Count == 0 ? Migration.InitialDatabase : null;
                    var script = migrator.GenerateScript(previous, migrationId);

                    await ExecuteScriptAsync(connection, script, cancellationToken)
                        .ConfigureAwait(false);

                    applied.Add(migrationId);
                }

                logger.LogInformation("{Count} migration(s) applied.", pending.Count);

                return pending.Count;
            }
            finally
            {
                await ReleaseLockAsync(connection).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// The generated script carries the INSERT into the history table, so recording and
        /// schema change land together. It is split on the delimiter EF emits between
        /// statements, because a driver command runs one statement at a time.
        /// </summary>
        private static async Task ExecuteScriptAsync(
            IDbConnection connection,
            string script,
            CancellationToken cancellationToken)
        {
            var statements = script
                .Split(["\r\n", "\n"], StringSplitOptions.None)
                .Aggregate(
                    new List<string> { string.Empty },
                    (batches, line) =>
                    {
                        if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
                        {
                            batches.Add(string.Empty);
                        }
                        else
                        {
                            batches[^1] += line + "\n";
                        }

                        return batches;
                    })
                .SelectMany(batch => batch.Split(';'))
                .Select(statement => statement.Trim())
                .Where(statement => statement.Length > 0 && !statement.StartsWith("--", StringComparison.Ordinal));

            foreach (var statement in statements)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(statement, cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
            }
        }

        private static Task EnsureHistoryTableAsync(
            IDbConnection connection,
            CancellationToken cancellationToken) =>
            connection.ExecuteAsync(new CommandDefinition($"""
                CREATE TABLE IF NOT EXISTS `{HistoryTable}` (
                    MigrationId varchar(150) NOT NULL,
                    ProductVersion varchar(32) NOT NULL,
                    PRIMARY KEY (MigrationId)
                ) CHARACTER SET=utf8mb4
                """, cancellationToken: cancellationToken));

        /// <summary>
        /// Advisory lock so two replicas starting together do not apply the same migration.
        /// Read as a nullable long precisely because MariaDB can answer NULL - the bug that
        /// made the provider blow up.
        /// </summary>
        private async Task AcquireLockAsync(IDbConnection connection, CancellationToken cancellationToken)
        {
            var acquired = await connection.ExecuteScalarAsync<long?>(
                new CommandDefinition(
                    "SELECT GET_LOCK(@Name, 60)",
                    new { Name = LockName },
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (acquired != 1)
            {
                logger.LogWarning(
                    "Could not take the migration lock; proceeding without it. " +
                    "Safe with a single replica.");
            }
        }

        private static Task ReleaseLockAsync(IDbConnection connection) =>
            connection.ExecuteAsync("SELECT RELEASE_LOCK(@Name)", new { Name = LockName });
    }
}
