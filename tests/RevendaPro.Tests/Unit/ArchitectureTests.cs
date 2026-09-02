using Foundation.Domain.Abstractions;
using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using RevendaPro.Api.Controllers;
using RevendaPro.Application.Behaviors;
using RevendaPro.Domain.Entities;
using RevendaPro.Infrastructure.Screens;
using RevendaPro.Shared.Settings;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// Guards the layer rules of ADR-0003. These break loudly when someone adds a reference
    /// that would quietly turn the architecture into a ball of mud.
    /// </summary>
    public class ArchitectureTests
    {
        private static readonly Assembly Domain = typeof(User).Assembly;
        private static readonly Assembly Application = typeof(ValidationBehavior<,>).Assembly;
        private static readonly Assembly Infrastructure = typeof(ScreenCatalog).Assembly;
        private static readonly Assembly Api = typeof(AuthController).Assembly;
        private static readonly Assembly Shared = typeof(JwtSettings).Assembly;

        private const string DomainNamespace = "RevendaPro.Domain";
        private const string ApplicationNamespace = "RevendaPro.Application";
        private const string InfrastructureNamespace = "RevendaPro.Infrastructure";
        private const string ApiNamespace = "RevendaPro.Api";

        [Fact]
        public void Domain_DependsOnNothingButShared()
        {
            var result = Types.InAssembly(Domain)
                .ShouldNot()
                .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
                .GetResult();

            Failing(result).Should().BeEmpty(
                "the domain must stay free of the layers built on top of it");
        }

        [Fact]
        public void Application_NeverReachesInfrastructureOrApi()
        {
            var result = Types.InAssembly(Application)
                .ShouldNot()
                .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
                .GetResult();

            Failing(result).Should().BeEmpty(
                "the application talks to the domain contracts, never to a concrete adapter");
        }

        [Fact]
        public void Infrastructure_NeverReachesApplicationOrApi()
        {
            var result = Types.InAssembly(Infrastructure)
                .ShouldNot()
                .HaveDependencyOnAny(ApplicationNamespace, ApiNamespace)
                .GetResult();

            Failing(result).Should().BeEmpty();
        }

        [Fact]
        public void Shared_DependsOnNoOtherLayer()
        {
            var result = Types.InAssembly(Shared)
                .ShouldNot()
                .HaveDependencyOnAny(
                    DomainNamespace, ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
                .GetResult();

            Failing(result).Should().BeEmpty("shared is the bottom of the stack");
        }

        /// <summary>
        /// Entity Framework only generates migrations and maps tables. Anything outside the
        /// Infrastructure database folder touching it means the Dapper decision leaked.
        /// </summary>
        [Fact]
        public void EntityFrameworkStaysOutOfDomainApplicationAndApi()
        {
            foreach (var assembly in new[] { Domain, Application, Api })
            {
                var result = Types.InAssembly(assembly)
                    .ShouldNot()
                    .HaveDependencyOn("Microsoft.EntityFrameworkCore")
                    .GetResult();

                Failing(result).Should().BeEmpty(
                    $"{assembly.GetName().Name} must stay free of EF Core (ADR-0003)");
            }
        }

        /// <summary>Dapper is an Infrastructure detail; it never surfaces above it.</summary>
        [Fact]
        public void DapperStaysInsideInfrastructure()
        {
            foreach (var assembly in new[] { Domain, Application, Api })
            {
                var result = Types.InAssembly(assembly)
                    .ShouldNot()
                    .HaveDependencyOn("Dapper")
                    .GetResult();

                Failing(result).Should().BeEmpty(
                    $"{assembly.GetName().Name} must stay free of Dapper");
            }
        }

        /// <summary>
        /// The storage provider is configuration, never a dependency. If the AWS SDK reaches
        /// any layer above Infrastructure, the promise that moving from MinIO to Cloudflare R2
        /// to AWS S3 costs nothing but settings has already been broken. See ADR-0004.
        /// </summary>
        [Fact]
        public void TheStorageSdkStaysInsideInfrastructure()
        {
            foreach (var assembly in new[] { Domain, Application, Api, Shared })
            {
                var result = Types.InAssembly(assembly)
                    .ShouldNot()
                    .HaveDependencyOn("Amazon")
                    .GetResult();

                Failing(result).Should().BeEmpty(
                    $"{assembly.GetName().Name} must stay free of the AWS SDK (ADR-0004)");
            }
        }

        /// <summary>
        /// No type carries the name of a storage provider. MinIO, Cloudflare R2 and AWS S3
        /// speak the same API: a class named after one of them is the shape the leak takes
        /// before the dependency itself appears.
        /// </summary>
        [Fact]
        public void NoTypeIsNamedAfterAStorageProvider()
        {
            string[] providers = ["Cloudflare", "MinIo", "MinIO", "R2Storage", "AzureBlob"];

            var offenders = new[] { Domain, Application, Api, Shared, Infrastructure }
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => providers.Any(provider =>
                    type.Name.Contains(provider, StringComparison.OrdinalIgnoreCase)))
                .Select(type => type.FullName)
                .ToList();

            offenders.Should().BeEmpty(
                "the provider is a configuration value, and never part of a type name");
        }

        [Fact]
        public void EveryPersistedEntityInheritsFoundationEntity()
        {
            var entities = Types.InAssembly(Domain)
                .That()
                .ResideInNamespace($"{DomainNamespace}.Entities")
                .And().ArePublic()
                .And().AreClasses()
                .And().AreNotAbstract()
                .GetTypes();

            entities.Should().NotBeEmpty();

            entities.Should().OnlyContain(
                t => typeof(Entity).IsAssignableFrom(t),
                "Id, Code UUID v7, audit and soft delete come from Foundation's Entity");
        }

        [Fact]
        public void ForeignKeysAreNamedIdFirst()
        {
            // The primary key is Id; a foreign key is Id followed by the entity it points at:
            // IdTenant, IdUser, IdRole. Never UserId. Because there is no HasColumnName, the
            // property name IS the column name, so this one rule covers C# and the database.
            var offenders = Types.InAssembly(Domain)
                .That()
                .ResideInNamespace($"{DomainNamespace}.Entities")
                .GetTypes()
                .SelectMany(t => t.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(p => p.Name.EndsWith("Id", StringComparison.Ordinal))
                .Where(p => p.Name.Length > "Id".Length)
                .Select(p => $"{p.DeclaringType?.Name}.{p.Name}")
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            offenders.Should().BeEmpty(
                "a foreign key is written Id first, as in IdUser, and never UserId");
        }

        [Fact]
        public void HandlersLiveInTheApplicationLayer()
        {
            var handlersInApi = Types.InAssembly(Api)
                .That().HaveNameEndingWith("Handler")
                .GetTypes()
                .Where(t => t.IsPublic)
                .Select(t => t.Name)
                .ToList();

            handlersInApi.Should().BeEmpty("controllers stay thin and delegate to MediatR");
        }

        private static IEnumerable<string> Failing(TestResult result) =>
            result.FailingTypeNames ?? [];
    }
}
