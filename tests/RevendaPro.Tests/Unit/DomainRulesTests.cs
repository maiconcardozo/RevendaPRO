using FluentAssertions;
using RevendaPro.Domain.Entities;
using RevendaPro.Shared.Helpers;

namespace RevendaPro.Tests.Unit
{
    /// <summary>The business rules the entities enforce on their own.</summary>
    public class DomainRulesTests
    {
        [Fact]
        public void Entity_GetsATimeOrderedCode()
        {
            var user = SampleUser();

            // Version 7 is the 13th hex digit of the string form. v4 would answer "4", and
            // that is exactly what Foundation's Entity used to produce before 3.2.0-rc.3: a
            // code that fragments the index it sits on.
            user.Code.ToString()[14].Should().Be('7');
        }

        [Fact]
        public void Codes_KeepGrowingSoTheIndexStaysOrdered()
        {
            // Only the leading timestamp is ordered; the tail is random. Two codes minted in
            // the same millisecond can come out in either order, so the wait is what makes
            // this assert the property that matters - a code minted later sorts later.
            var first = SampleUser().Code;
            Thread.Sleep(5);
            var second = SampleUser().Code;

            string.CompareOrdinal(second.ToString(), first.ToString()).Should().BePositive();
        }

        [Fact]
        public void NewEntity_StartsActiveAndAudited()
        {
            var user = SampleUser();

            user.IsActive.Should().BeTrue();
            user.IsDeleted.Should().BeFalse();
            user.CreatedBy.Should().NotBeEmpty();
            user.DtCreated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Delete_IsAlwaysLogicalAndRecordsWho()
        {
            var user = SampleUser();

            user.SoftDelete("tester");

            user.IsDeleted.Should().BeTrue();
            user.IsActive.Should().BeFalse();
            user.DeletedBy.Should().Be("tester");
            user.DtDeleted.Should().NotBeNull();
        }

        [Fact]
        public void Restore_ClearsTheDeletionTrail()
        {
            var user = SampleUser();
            user.SoftDelete("tester");

            user.Activate("tester");

            user.IsActive.Should().BeTrue();
            user.DtDeleted.Should().BeNull();
            user.DeletedBy.Should().BeNull();
        }

        [Fact]
        public void Delete_OnAnAlreadyDeletedEntity_KeepsTheFirstRecord()
        {
            var user = SampleUser();
            user.SoftDelete("first");

            user.SoftDelete("second");

            user.DeletedBy.Should().Be("first", "the original deletion is the one that happened");
        }

        [Fact]
        public void User_NormalizesTheEmail()
        {
            var user = User.Create(1, "Ana", "  Ana@Empresa.COM  ", "hash");

            user.Email.Should().Be("ana@empresa.com");
        }

        [Fact]
        public void User_StoresDocumentAndPhoneAsDigitsOnly()
        {
            var user = SampleUser();

            user.Update("Ana", "ana@x.com", "529.982.247-25", "(11) 99999-8888", "tester");

            user.Document.Should().Be("52998224725", "the mask belongs to the screen");
            user.Phone.Should().Be("11999998888");
        }

        [Fact]
        public void User_RejectsAnEmptyPasswordHash()
        {
            var create = () => User.Create(1, "Ana", "ana@x.com", "   ");

            create.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void SystemRole_RefusesDeletion()
        {
            var role = Role.Create(1, "Administrador", null, isSystem: true);

            role.CanBeDeleted.Should().BeFalse();
        }

        [Fact]
        public void CustomRole_AllowsDeletion()
        {
            var role = Role.Create(1, "Conferente", null);

            role.CanBeDeleted.Should().BeTrue();
        }

        [Fact]
        public void Screen_Sync_ReportsNoChangeWhenNothingMoved()
        {
            var screen = Screen.Create("users", "Usuários", "/users", "Users", "Administração", 10, true);

            var changed = screen.Sync("Usuários", "/users", "Users", "Administração", 10, true, null);

            changed.Should().BeFalse("a deploy without catalog changes must write nothing");
        }

        [Fact]
        public void Screen_Sync_ReactivatesAScreenThatReturnedToTheCatalog()
        {
            var screen = Screen.Create("users", "Usuários", "/users", "Users", "Administração", 10, true);
            screen.SoftDelete("tester");

            var changed = screen.Sync("Usuários", "/users", "Users", "Administração", 10, true, null);

            changed.Should().BeTrue();
            screen.IsActive.Should().BeTrue("its permission links were preserved and come back");
        }

        [Fact]
        public void RefreshToken_ExpiredOrRevoked_StopsBeingValid()
        {
            var now = DateTime.UtcNow;

            var expired = RefreshToken.Create(1, "hash", now.AddMinutes(-1));
            expired.IsValid(now).Should().BeFalse();

            var revoked = RefreshToken.Create(1, "hash", now.AddDays(1));
            revoked.Revoke();
            revoked.IsValid(now).Should().BeFalse();

            RefreshToken.Create(1, "hash", now.AddDays(1)).IsValid(now).Should().BeTrue();
        }

        [Theory]
        [InlineData("529.982.247-25", true)]
        [InlineData("52998224725", true)]
        [InlineData("111.111.111-11", false, "repeated digits pass any mask and are invalid")]
        [InlineData("529.982.247-26", false, "wrong check digit")]
        [InlineData("", true, "the field is optional")]
        public void Cpf_IsValidatedByItsCheckDigits(string value, bool expected, string? because = null)
        {
            BrazilianDocuments.IsValidCpfOrCnpj(value).Should().Be(expected, because ?? string.Empty);
        }

        [Theory]
        [InlineData("11.222.333/0001-81", true)]
        [InlineData("11.222.333/0001-82", false)]
        [InlineData("11111111111111", false)]
        public void Cnpj_IsValidatedByItsCheckDigits(string value, bool expected)
        {
            BrazilianDocuments.IsValidCpfOrCnpj(value).Should().Be(expected);
        }

        [Theory]
        [InlineData("(11) 99999-8888", true)]
        [InlineData("(11) 3333-4444", true, "a landline has ten digits")]
        [InlineData("(01) 99999-8888", false, "area codes start at 11")]
        [InlineData("(11) 89999-8888", false, "a mobile carries a 9 in front")]
        [InlineData("", true, "the field is optional")]
        public void Phone_IsValidated(string value, bool expected, string? because = null)
        {
            BrazilianDocuments.IsValidPhone(value).Should().Be(expected, because ?? string.Empty);
        }

        private static User SampleUser() => User.Create(1, "Ana", "ana@x.com", "hash");
    }
}
