using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Foundation.Domain.Interfaces.Data;
using RevendaPro.Infrastructure.Screens;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// The one guarantee this architecture gave up when it chose Dapper.
    ///
    /// Entity Framework applies a global query filter, so a soft deleted row simply never
    /// comes back. Dapper has no such thing: the filter lives in each statement, and a
    /// forgotten WHERE hands back a deleted user or a revoked permission — silently, with
    /// no error anywhere.
    ///
    /// So every SELECT written by hand is inspected here. This test is the reason the
    /// decision is safe. See ADR-0003.
    /// </summary>
    public partial class SoftDeleteTests
    {
        private static readonly Assembly Infrastructure = typeof(ScreenCatalog).Assembly;

        /// <summary>
        /// Statements that read every row on purpose. Each one states why, so adding to this
        /// list is a decision someone has to write down.
        /// </summary>
        private static readonly Dictionary<string, string> ReadsDeletedOnPurpose = new()
        {
            ["FindUserByCodeIncludingDeletedQuery"] =
                "restoring a user is the one operation that has to see what every other "
                + "reading hides; only RestoreUserHandler calls it",
            ["ListAllScreensQuery"] =
                "the synchronizer has to see deactivated screens: one returning to the "
                + "catalog is reactivated, never inserted again",
            ["ListDeletedVehicleDocumentsQuery"] =
                "the administrative screen of deleted documents exists to show exactly "
                + "what every other reading hides; the vehicle it joins is still filtered",
            ["FindVehicleDocumentByCodeIncludingDeletedQuery"] =
                "restoring a document has to find the deleted row; only the restore and "
                + "the download of that screen call it"
        };

        public static TheoryData<string, string> SelectQueries()
        {
            var data = new TheoryData<string, string>();

            foreach (var (name, sql) in QueriesOfKind("SELECT"))
            {
                data.Add(name, sql);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(SelectQueries))]
        public void EverySelectFiltersSoftDeletedRows(string name, string sql)
        {
            if (ReadsDeletedOnPurpose.ContainsKey(name))
            {
                return;
            }

            IsActiveFilter().IsMatch(sql).Should().BeTrue(
                "{0} reads rows and must exclude the soft deleted ones. SQL:\n{1}", name, sql);
        }

        /// <summary>
        /// A JOIN brings a second table along, and each one carries its own soft delete. A
        /// query joining Role and Screen while filtering only User would still hand back a
        /// permission that was revoked on the role.
        /// </summary>
        [Fact]
        public void EveryJoinedTableCarriesItsOwnFilter()
        {
            var offenders = new List<string>();

            foreach (var (name, sql) in QueriesOfKind("SELECT"))
            {
                if (ReadsDeletedOnPurpose.ContainsKey(name))
                {
                    continue;
                }

                var joins = JoinClause().Matches(sql).Count;

                if (joins == 0)
                {
                    continue;
                }

                // One filter for the driving table plus one per joined table.
                var filters = IsActiveFilter().Matches(sql).Count;

                if (filters < joins + 1)
                {
                    offenders.Add($"{name}: {joins} join(s) but {filters} IsActive filter(s)");
                }
            }

            offenders.Should().BeEmpty();
        }

        [Fact]
        public void ThereAreQueriesToInspect()
        {
            // Guards the test itself: a rename that stops finding the query objects would
            // otherwise turn every assertion above into a silent pass.
            QueriesOfKind("SELECT").Should().HaveCountGreaterThan(5);
        }

        private static List<(string Name, string Sql)> QueriesOfKind(string keyword) =>
            [.. Infrastructure.GetTypes()
                .Where(t => typeof(ISqlQuery).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .Select(t => (t.Name, Sql: SqlOf(t)))
                .Where(x => x.Sql is not null
                            && x.Sql.TrimStart().StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                .Select(x => (x.Name, x.Sql!))];

        /// <summary>
        /// Builds the query object with placeholder arguments just to read its statement.
        /// The SQL is a constant, so the values never matter.
        /// </summary>
        private static string? SqlOf(Type type)
        {
            var constructor = type.GetConstructors().OrderBy(c => c.GetParameters().Length).First();

            var arguments = constructor.GetParameters()
                .Select(p => PlaceholderFor(p.ParameterType))
                .ToArray();

            try
            {
                var instance = (ISqlQuery)constructor.Invoke(arguments);
                return instance.GetSql();
            }
            catch (TargetInvocationException)
            {
                return null;
            }
        }

        private static object? PlaceholderFor(Type type)
        {
            if (type == typeof(string))
            {
                return "x";
            }

            if (Nullable.GetUnderlyingType(type) is not null || !type.IsValueType)
            {
                // A collection parameter arrives as an interface, which cannot be created.
                // An empty array of the right element type satisfies the constructor, and the
                // SQL is a constant anyway.
                if (type.IsGenericType && type.GetGenericArguments().Length == 1)
                {
                    var element = type.GetGenericArguments()[0];

                    if (type.IsAssignableFrom(element.MakeArrayType()))
                    {
                        return Array.CreateInstance(element, 0);
                    }
                }

                return null;
            }

            return Activator.CreateInstance(type);
        }

        [GeneratedRegex(@"IsActive\s*=\s*1", RegexOptions.IgnoreCase)]
        private static partial Regex IsActiveFilter();

        [GeneratedRegex(@"\bJOIN\b", RegexOptions.IgnoreCase)]
        private static partial Regex JoinClause();
    }
}
