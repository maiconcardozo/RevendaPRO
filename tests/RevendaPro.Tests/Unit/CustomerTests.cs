using FluentAssertions;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Tests.Unit
{
    /// <summary>
    /// O cliente (M21): quem ofereceu, quem comprou, quem volta.
    ///
    /// O que se prova aqui é o contorno da entidade: nome e telefone bastam para nascer, o
    /// documento é conferido de verdade porque vai na linha de assinatura, e o aproveitamento
    /// completa só o que está em branco. A proposta e a venda apontam para um cliente, e
    /// jamais para o zero.
    /// </summary>
    public class CustomerTests
    {
        [Fact]
        public void NameAndPhone_AreEnoughToBeBorn_AndThePhoneKeepsOnlyDigits()
        {
            var customer = Customer.Create(1, "  Marcos Silva ", "(51) 99999-0001");

            customer.Name.Should().Be("Marcos Silva");
            customer.Phone.Should().Be("51999990001");
            customer.Document.Should().BeNull();
        }

        [Fact]
        public void ANamelessCustomer_IsRefused()
        {
            var act = () => Customer.Create(1, "   ", null);

            act.Should().Throw<BusinessRuleException>().WithMessage("*nome do cliente*");
        }

        [Theory]
        [InlineData("390.533.447-05", "39053344705")]
        [InlineData("12.345.678/0001-95", "12345678000195")]
        [InlineData("", null)]
        public void TheDocument_IsKeptAsDigits_OrLeftEmpty(string typed, string? stored)
        {
            var customer = Customer.Create(1, "Marcos", null);

            customer.SetContact(typed, "51 3333-4444", "Marcos@Mail.com", "  Rua A, 10 ", null);

            customer.Document.Should().Be(stored);
            customer.Phone.Should().Be("5133334444");
            customer.Email.Should().Be("marcos@mail.com");
            customer.Address.Should().Be("Rua A, 10");
        }

        [Fact]
        public void AnInvalidDocument_IsRefused_BecauseItGoesOnTheSignatureLine()
        {
            var customer = Customer.Create(1, "Marcos", null);

            var act = () => customer.SetContact("111.111.111-11", null, null, null, null);

            act.Should().Throw<BusinessRuleException>().WithMessage("*CPF ou CNPJ válido*");
        }

        [Fact]
        public void FillingTheBlanks_NeverOverwrites_WhatIsAlreadyThere()
        {
            var customer = Customer.Create(1, "Marcos", "51999990001");
            customer.SetContact("39053344705", "51999990001", null, null, null);

            customer.FillBlanks("12345678909", "51888880000");

            customer.Document.Should().Be("39053344705");
            customer.Phone.Should().Be("51999990001");
        }

        [Fact]
        public void FillingTheBlanks_TakesADocumentOnlyWhenItIsValid_AndAPhoneWhenThereWasNone()
        {
            var customer = Customer.Create(1, "Marcos", null);

            customer.FillBlanks("11111111111", "(51) 98888-0000");
            customer.Document.Should().BeNull("um CPF inválido vindo de uma venda antiga fica de fora");
            customer.Phone.Should().Be("51988880000");

            customer.FillBlanks("39053344705", null);
            customer.Document.Should().Be("39053344705");
        }

        [Fact]
        public void AProposalAndASale_PointAtACustomer_AndNeverAtZero()
        {
            var proposal = Proposal.Create(
                1, "Marcos", "51999990001", 50_000m, new DateOnly(2026, 9, 1),
                PaymentMethod.Cash, SaleChannel.Direct, null, null, null);

            var sale = Sale.Create(
                1, null, new DateOnly(2026, 9, 2), 50_000m, PaymentMethod.Cash, SaleChannel.Direct,
                null, null, null, 0m, null, "Marcos", null, null, null, null);

            proposal.IdCustomer.Should().BeNull("a proposta antiga nasce sem cliente e o aproveitamento a completa");

            proposal.AssignCustomer(7);
            sale.AssignCustomer(7);

            proposal.IdCustomer.Should().Be(7);
            sale.IdCustomer.Should().Be(7);

            var act = () => proposal.AssignCustomer(0);
            act.Should().Throw<BusinessRuleException>();
        }
    }
}
