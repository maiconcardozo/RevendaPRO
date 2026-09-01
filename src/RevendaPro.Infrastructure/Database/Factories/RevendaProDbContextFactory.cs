using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RevendaPro.Infrastructure.Database.Contexts;

namespace RevendaPro.Infrastructure.Database.Factories
{
    /// <summary>
    /// Used only by "dotnet ef" to generate migrations without starting the API.
    /// The connection string here does not need to point at a live database.
    /// </summary>
    public class RevendaProDbContextFactory : IDesignTimeDbContextFactory<RevendaProDbContext>
    {
        /// <inheritdoc/>
        public RevendaProDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("REVENDAPRO_CONNECTION")
                ?? "server=127.0.0.1;port=3308;database=revendapro;user=root;password=design";

            var options = new DbContextOptionsBuilder<RevendaProDbContext>()
                .UseMySQL(connectionString)
                .Options;

            return new RevendaProDbContext(options);
        }
    }
}
