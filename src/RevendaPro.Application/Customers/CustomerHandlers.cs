using FluentValidation;
using MediatR;
using RevendaPro.Application.Customers.Commands;
using RevendaPro.Application.Customers.DTOs;
using RevendaPro.Application.Customers.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;
using RevendaPro.Shared.Helpers;

namespace RevendaPro.Application.Customers.Validators
{
    /// <summary>Formato do cadastro de cliente. A regra de negócio fica na entidade.</summary>
    public class SaveCustomerValidator : AbstractValidator<SaveCustomerCommand>
    {
        /// <summary>Builds the rules.</summary>
        public SaveCustomerValidator()
        {
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage("Informe o nome do cliente.")
                .MaximumLength(120).WithMessage("O nome pode ter no máximo 120 caracteres.");

            RuleFor(c => c.Email)
                .EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.Email))
                .WithMessage("Informe um e-mail válido.")
                .MaximumLength(160).WithMessage("O e-mail pode ter no máximo 160 caracteres.");

            RuleFor(c => c.Address)
                .MaximumLength(240).WithMessage("O endereço pode ter no máximo 240 caracteres.");

            RuleFor(c => c.Notes)
                .MaximumLength(500).WithMessage("A anotação pode ter no máximo 500 caracteres.");
        }
    }
}

namespace RevendaPro.Application.Customers.Handlers
{
    /// <summary>Os clientes da revenda, com o resumo do que cada um já fez.</summary>
    public class ListCustomersHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ListCustomersQuery, IReadOnlyList<CustomerDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<CustomerDto>> Handle(
            ListCustomersQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var customers = await unitOfWork.CustomerRepository
                .ListByTenantAsync(currentUser.IdTenant, request.Search, cancellationToken)
                .ConfigureAwait(false);

            if (customers.Count == 0)
            {
                return [];
            }

            var summaries = await unitOfWork.CustomerRepository
                .SummarizeByTenantAsync(currentUser.IdTenant, cancellationToken)
                .ConfigureAwait(false);

            var byCustomer = summaries.ToDictionary(summary => summary.IdCustomer);

            return [.. customers.Select(customer =>
                CustomerMapper.ToDto(customer, byCustomer.GetValueOrDefault(customer.Id)))];
        }
    }

    /// <summary>A ficha de um cliente: os dados e o histórico.</summary>
    public class GetCustomerHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<GetCustomerQuery, CustomerDetailDto>
    {
        /// <inheritdoc/>
        public async Task<CustomerDetailDto> Handle(GetCustomerQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;

            var customer = await unitOfWork.CustomerRepository
                .GetByCodeAsync(idTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Cliente inexistente.");

            var proposals = await unitOfWork.CustomerRepository
                .ListProposalsAsync(idTenant, customer.Id, cancellationToken)
                .ConfigureAwait(false);

            var sales = await unitOfWork.CustomerRepository
                .ListSalesAsync(idTenant, customer.Id, cancellationToken)
                .ConfigureAwait(false);

            // O resumo sai das próprias linhas: a ficha já as tem, e outra ida ao banco só para
            // contar o que está na mão seria consulta sem retorno.
            var summary = new CustomerSummary(
                customer.Id,
                proposals.Count,
                sales.Count,
                sales.Sum(sale => sale.Amount),
                proposals.Select(p => p.Date).Concat(sales.Select(s => s.Date)).DefaultIfEmpty().Max() is var last
                    && last != default ? last : null);

            return new CustomerDetailDto(
                CustomerMapper.ToDto(customer, summary),
                [.. proposals.Select(CustomerMapper.ToDto)],
                [.. sales.Select(CustomerMapper.ToDto)]);
        }
    }

    /// <summary>Cadastra ou edita um cliente.</summary>
    public class SaveCustomerHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<SaveCustomerCommand, CustomerDto>
    {
        /// <inheritdoc/>
        public async Task<CustomerDto> Handle(SaveCustomerCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;
            var actor = currentUser.Code.ToString();

            Customer customer;
            var isNew = request.Code is null;

            if (isNew)
            {
                customer = Customer.Create(idTenant, request.Name, request.Phone, actor);
            }
            else
            {
                customer = await unitOfWork.CustomerRepository
                    .GetByCodeAsync(idTenant, request.Code!.Value, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new NotFoundException("Cliente inexistente.");

                customer.Rename(request.Name);
            }

            // A entidade confere o documento e limpa os dígitos; o que fica para cá é o que
            // depende de olhar os outros clientes.
            customer.SetContact(
                request.Document, request.Phone, request.Email, request.Address, request.Notes, actor);

            await CustomerRules.RefuseDuplicatesAsync(
                unitOfWork, idTenant, customer, isNew ? null : customer.Id, request.ConfirmSamePhone, cancellationToken)
                .ConfigureAwait(false);

            if (isNew)
            {
                unitOfWork.CustomerRepository.Add(customer);
            }
            else
            {
                unitOfWork.CustomerRepository.Update(customer);
            }

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                idTenant, currentUser.Id, nameof(Customer), customer.Code,
                isNew ? AuditAction.Create : AuditAction.Update, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            var summary = isNew
                ? null
                : (await unitOfWork.CustomerRepository
                    .SummarizeByTenantAsync(idTenant, cancellationToken)
                    .ConfigureAwait(false))
                    .FirstOrDefault(row => row.IdCustomer == customer.Id);

            return CustomerMapper.ToDto(customer, summary);
        }
    }

    /// <summary>Exclui um cliente, logicamente. Só sem história.</summary>
    public class DeleteCustomerHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<DeleteCustomerCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;

            var customer = await unitOfWork.CustomerRepository
                .GetByCodeAsync(idTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Cliente inexistente.");

            var summary = (await unitOfWork.CustomerRepository
                .SummarizeByTenantAsync(idTenant, cancellationToken)
                .ConfigureAwait(false))
                .FirstOrDefault(row => row.IdCustomer == customer.Id);

            if (summary is not null && (summary.ProposalCount > 0 || summary.SaleCount > 0))
            {
                // Recusa com o número: excluir quem tem história apagaria a resposta para a
                // pergunta que o cadastro existe para responder — quem é essa pessoa para a loja.
                throw new BusinessRuleException(
                    $"{customer.Name} tem {Count(summary.ProposalCount, "proposta", "propostas")} e "
                    + $"{Count(summary.SaleCount, "compra", "compras")}. "
                    + "O cliente fica no cadastro enquanto houver história apontando para ele.");
            }

            var actor = currentUser.Code.ToString();

            unitOfWork.CustomerRepository.Remove(customer, actor);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                idTenant, currentUser.Id, nameof(Customer), customer.Code,
                AuditAction.Delete, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        private static string Count(int value, string one, string many) =>
            $"{value} {(value == 1 ? one : many)}";
    }

    /// <summary>
    /// As regras que olham os outros clientes: documento igual é recusa, telefone igual é aviso.
    /// </summary>
    public static class CustomerRules
    {
        /// <summary>Recusa o documento que já é de outro cliente, e o telefone repetido sem confirmação.</summary>
        /// <param name="unitOfWork">Acesso aos dados.</param>
        /// <param name="idTenant">Revenda dona do cadastro.</param>
        /// <param name="customer">O cliente como vai ficar.</param>
        /// <param name="ignoreId">O próprio cliente, ao editar.</param>
        /// <param name="confirmSamePhone">Se a pessoa já disse que o telefone repetido é de outra pessoa.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RefuseDuplicatesAsync(
            IUnitOfWork unitOfWork,
            int idTenant,
            Customer customer,
            int? ignoreId,
            bool confirmSamePhone,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(customer);

            if (customer.Document is not null)
            {
                var sameDocument = await unitOfWork.CustomerRepository
                    .FindByDocumentAsync(idTenant, customer.Document, ignoreId, cancellationToken)
                    .ConfigureAwait(false);

                if (sameDocument is not null)
                {
                    throw new BusinessRuleException(
                        $"Este {(customer.Document.Length == 11 ? "CPF" : "CNPJ")} já é de {sameDocument.Name}. "
                        + "Dois documentos iguais são a mesma pessoa: use o cadastro dela.");
                }
            }

            if (customer.Phone is not null && !confirmSamePhone)
            {
                var samePhone = (await unitOfWork.CustomerRepository
                    .FindByPhoneAsync(idTenant, customer.Phone, cancellationToken)
                    .ConfigureAwait(false))
                    .FirstOrDefault(other => other.Id != ignoreId);

                if (samePhone is not null)
                {
                    throw new BusinessRuleException(
                        $"Este telefone já é de {samePhone.Name}. Use o cadastro dele, ou confirme que é outra pessoa.");
                }
            }
        }
    }

    /// <summary>
    /// O cliente de uma proposta ou de uma venda (M21): o escolhido pelo código, ou o achado
    /// pelo documento ou pelo telefone, ou um novo, com o que a pessoa digitou.
    ///
    /// É o que permite cadastrar sem sair da proposta: nome e telefone bastam para nascer, e
    /// o resto vem na ficha ou na hora da venda.
    /// </summary>
    public static class CustomerResolver
    {
        /// <summary>Acha ou cria o cliente, e o devolve com o Id que o banco deu.</summary>
        /// <param name="unitOfWork">Acesso aos dados.</param>
        /// <param name="idTenant">Revenda dona do cadastro.</param>
        /// <param name="code">O cliente escolhido no seletor, quando a pessoa escolheu um.</param>
        /// <param name="name">O nome digitado.</param>
        /// <param name="document">O documento digitado, quando há.</param>
        /// <param name="phone">O telefone digitado, quando há.</param>
        /// <param name="actor">Quem está cadastrando.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O cliente.</returns>
        public static async Task<Customer> ResolveAsync(
            IUnitOfWork unitOfWork,
            int idTenant,
            Guid? code,
            string name,
            string? document,
            string? phone,
            string actor,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(unitOfWork);

            if (code is { } chosen)
            {
                var customer = await unitOfWork.CustomerRepository
                    .GetByCodeAsync(idTenant, chosen, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new BusinessRuleException("Escolha um cliente desta revenda.");

                // A venda pode trazer o CPF que a proposta deixou em branco.
                customer.FillBlanks(document, phone);
                unitOfWork.CustomerRepository.Update(customer);

                return customer;
            }

            var digits = DigitsOf(document);

            if (digits is not null)
            {
                var byDocument = await unitOfWork.CustomerRepository
                    .FindByDocumentAsync(idTenant, digits, ignoreId: null, cancellationToken)
                    .ConfigureAwait(false);

                if (byDocument is not null)
                {
                    byDocument.FillBlanks(document: null, phone);
                    unitOfWork.CustomerRepository.Update(byDocument);

                    return byDocument;
                }
            }

            var phoneDigits = DigitsOf(phone);

            if (phoneDigits is not null)
            {
                var byPhone = await unitOfWork.CustomerRepository
                    .FindByPhoneAsync(idTenant, phoneDigits, cancellationToken)
                    .ConfigureAwait(false);

                if (byPhone.Count > 0)
                {
                    byPhone[0].FillBlanks(digits, phone: null);
                    unitOfWork.CustomerRepository.Update(byPhone[0]);

                    return byPhone[0];
                }
            }

            var created = Customer.Create(idTenant, name, phone, actor);

            if (digits is not null && BrazilianDocuments.IsValidCpfOrCnpj(digits))
            {
                created.SetContact(digits, phone, email: null, address: null, notes: null, actor);
            }

            unitOfWork.CustomerRepository.Add(created);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            // O Id nasce no banco: a leitura de volta é o que o traz, antes de a proposta apontar.
            return await unitOfWork.CustomerRepository
                .GetByCodeAsync(idTenant, created.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new BusinessRuleException("Falha ao cadastrar o cliente.");
        }

        private static string? DigitsOf(string? value)
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

            return digits.Length == 0 ? null : digits;
        }
    }

    /// <summary>Do domínio para o que a tela lê.</summary>
    public static class CustomerMapper
    {
        /// <summary>O cliente com o resumo, ou com zeros quando ele só foi cadastrado.</summary>
        /// <param name="customer">O cliente.</param>
        /// <param name="summary">O resumo, quando há história.</param>
        /// <returns>O DTO.</returns>
        public static CustomerDto ToDto(Customer customer, CustomerSummary? summary) =>
            new(customer.Code,
                customer.Name,
                customer.Document,
                customer.Phone,
                customer.Email,
                customer.Address,
                customer.Notes,
                summary?.ProposalCount ?? 0,
                summary?.SaleCount ?? 0,
                summary?.BoughtTotal ?? 0m,
                summary?.LastDate);

        /// <summary>Uma linha de proposta da ficha.</summary>
        /// <param name="line">A linha.</param>
        /// <returns>O DTO.</returns>
        public static CustomerProposalDto ToDto(CustomerProposalLine line) =>
            new(line.Code,
                line.Date,
                line.Amount,
                (ProposalStatus)line.Status,
                (PaymentMethod)line.PaymentMethod,
                line.VehicleCode,
                line.Plate,
                VehicleName(line.Brand, line.Model, line.Version),
                line.ModelYear);

        /// <summary>Uma linha de compra da ficha.</summary>
        /// <param name="line">A linha.</param>
        /// <returns>O DTO.</returns>
        public static CustomerSaleDto ToDto(CustomerSaleLine line) =>
            new(line.Code,
                line.Date,
                line.Amount,
                (PaymentMethod)line.PaymentMethod,
                line.HadTradeIn,
                line.VehicleCode,
                line.Plate,
                VehicleName(line.Brand, line.Model, line.Version),
                line.ModelYear);

        private static string VehicleName(string brand, string model, string? version) =>
            string.IsNullOrWhiteSpace(version) ? $"{brand} {model}" : $"{brand} {model} {version}";
    }
}
