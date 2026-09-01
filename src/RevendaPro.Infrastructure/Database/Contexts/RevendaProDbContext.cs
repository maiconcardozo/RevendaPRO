using Microsoft.EntityFrameworkCore;
using RevendaPro.Domain.Entities;

namespace RevendaPro.Infrastructure.Database.Contexts
{
    /// <summary>
    /// Exists ONLY to generate migrations and to carry the mappings.
    ///
    /// No repository and no handler touches it: at runtime every read and write goes
    /// through Dapper. See ADR-0003.
    /// </summary>
    public class RevendaProDbContext(DbContextOptions<RevendaProDbContext> options) : DbContext(options)
    {
        public DbSet<Tenant> Tenants => Set<Tenant>();

        public DbSet<Screen> Screens => Set<Screen>();

        public DbSet<Role> Roles => Set<Role>();

        public DbSet<RoleScreen> RoleScreens => Set<RoleScreen>();

        public DbSet<User> Users => Set<User>();

        public DbSet<UserRole> UserRoles => Set<UserRole>();

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(RevendaProDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}
