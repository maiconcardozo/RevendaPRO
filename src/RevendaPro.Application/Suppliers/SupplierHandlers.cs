using System.Globalization;
using FluentValidation;
using MediatR;
using RevendaPro.Application.Suppliers.Commands;
using RevendaPro.Application.Suppliers.DTOs;
using RevendaPro.Application.Suppliers.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Repositories;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Application.Suppliers.Validators
{
    /// <summary>Formato do cadastro de fornecedor. A regra de negócio fica na entidade.</summary>
    public class SaveSupplierValidator : AbstractValidator<SaveSupplierCommand>
    {
        /// <summary>Builds the rules.</summary>
        public SaveSupplierValidator()
        {
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage("Informe o nome do fornecedor.")
                .MaximumLength(120).WithMessage("O nome pode ter no máximo 120 caracteres.");

            RuleFor(c => c.SegmentCode)
                .NotEmpty().WithMessage("Escolha o ramo do fornecedor.");

            RuleFor(c => c.ContactName)
                .MaximumLength(120).WithMessage("O contato pode ter no máximo 120 caracteres.");

            RuleFor(c => c.Notes)
                .MaximumLength(500).WithMessage("A anotação pode ter no máximo 500 caracteres.");
        }
    }

    /// <summary>Formato do cadastro de ramo.</summary>
    public class SaveSupplierSegmentValidator : AbstractValidator<SaveSupplierSegmentCommand>
    {
        /// <summary>Builds the rules.</summary>
        public SaveSupplierSegmentValidator()
        {
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage("Informe o nome do ramo.")
                .MaximumLength(80).WithMessage("O nome pode ter no máximo 80 caracteres.");
        }
    }
}

namespace RevendaPro.Application.Suppliers.Handlers
{
    /// <summary>Os fornecedores da revenda, com o ramo e quantos gastos apontam para cada um.</summary>
    public class ListSuppliersHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ListSuppliersQuery, IReadOnlyList<SupplierDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<SupplierDto>> Handle(
            ListSuppliersQuery request,
            CancellationToken cancellationToken)
        {
            var suppliers = await unitOfWork.SupplierRepository
                .ListByTenantAsync(currentUser.IdTenant, cancellationToken)
                .ConfigureAwait(false);

            var segments = await SupplierMapper
                .SegmentsByIdAsync(unitOfWork, currentUser.IdTenant, cancellationToken)
                .ConfigureAwait(false);

            var listed = new List<SupplierDto>(suppliers.Count);

            foreach (var supplier in suppliers)
            {
                var expenses = await unitOfWork.SupplierRepository
                    .CountExpensesAsync(supplier.Id, cancellationToken)
                    .ConfigureAwait(false);

                listed.Add(SupplierMapper.ToDto(supplier, segments, expenses));
            }

            return listed;
        }
    }

    /// <summary>Cadastra ou edita um fornecedor.</summary>
    public class SaveSupplierHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<SaveSupplierCommand, SupplierDto>
    {
        /// <inheritdoc/>
        public async Task<SupplierDto> Handle(SaveSupplierCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;
            var actor = currentUser.Code.ToString();

            // O ramo vem pelo código público, e tem de ser desta revenda: um código de outra
            // empresa é recusado como inexistente, e jamais vira ligação cruzada.
            var segment = await unitOfWork.SupplierSegmentRepository
                .GetByCodeAsync(idTenant, request.SegmentCode, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new BusinessRuleException("Escolha um ramo desta revenda.");

            Supplier supplier;
            var isNew = request.Code is null;

            if (isNew)
            {
                supplier = Supplier.Create(idTenant, request.Name, segment.Id, actor);
            }
            else
            {
                supplier = await unitOfWork.SupplierRepository
                    .GetByCodeAsync(idTenant, request.Code!.Value, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new NotFoundException("Fornecedor inexistente.");

                supplier.Rename(request.Name);
                supplier.SetSegment(segment.Id);
                supplier.UpdateAuditInfo(actor);
            }

            if (await unitOfWork.SupplierRepository
                    .NameExistsAsync(idTenant, request.Name, isNew ? null : supplier.Id, cancellationToken)
                    .ConfigureAwait(false))
            {
                throw new BusinessRuleException(
                    $"A revenda já tem um fornecedor chamado {request.Name.Trim()}.");
            }

            supplier.SetContact(request.ContactName, request.ContactPhone, request.Document, request.Notes);

            if (isNew)
            {
                unitOfWork.SupplierRepository.Add(supplier);
            }
            else
            {
                unitOfWork.SupplierRepository.Update(supplier);
            }

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                idTenant, currentUser.Id, nameof(Supplier), supplier.Code,
                isNew ? AuditAction.Create : AuditAction.Update, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            var expenses = isNew
                ? 0
                : await unitOfWork.SupplierRepository
                    .CountExpensesAsync(supplier.Id, cancellationToken)
                    .ConfigureAwait(false);

            return SupplierMapper.ToDto(supplier, segment, expenses);
        }
    }

    /// <summary>Exclui um fornecedor, logicamente.</summary>
    public class DeleteSupplierHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<DeleteSupplierCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var supplier = await unitOfWork.SupplierRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Fornecedor inexistente.");

            var expenses = await unitOfWork.SupplierRepository
                .CountExpensesAsync(supplier.Id, cancellationToken)
                .ConfigureAwait(false);

            // A despesa da loja também aponta para o fornecedor desde o M22: contar só o gasto
            // de carro deixaria apagar a imobiliária com doze aluguéis no nome dela.
            expenses += await unitOfWork.StoreExpenseRepository
                .CountBySupplierAsync(supplier.Id, cancellationToken)
                .ConfigureAwait(false);

            if (expenses > 0)
            {
                // Recusa com o número: excluir quem tem histórico apagaria a resposta para a
                // pergunta que o cadastro existe para responder — quanto já foi para ele.
                throw new BusinessRuleException(
                    expenses == 1
                        ? "Este fornecedor está em 1 gasto. Ele fica no cadastro enquanto houver gasto apontando para ele."
                        : $"Este fornecedor está em {expenses} gastos. Ele fica no cadastro enquanto houver gasto apontando para ele.");
            }

            var actor = currentUser.Code.ToString();

            unitOfWork.SupplierRepository.Remove(supplier, actor);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(Supplier), supplier.Code,
                AuditAction.Delete, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Os ramos da revenda, com quantos fornecedores estão em cada um.</summary>
    public class ListSupplierSegmentsHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ListSupplierSegmentsQuery, IReadOnlyList<SupplierSegmentDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<SupplierSegmentDto>> Handle(
            ListSupplierSegmentsQuery request,
            CancellationToken cancellationToken)
        {
            var segments = await unitOfWork.SupplierSegmentRepository
                .ListByTenantAsync(currentUser.IdTenant, cancellationToken)
                .ConfigureAwait(false);

            var listed = new List<SupplierSegmentDto>(segments.Count);

            foreach (var segment in segments)
            {
                var suppliers = await unitOfWork.SupplierSegmentRepository
                    .CountSuppliersAsync(segment.Id, cancellationToken)
                    .ConfigureAwait(false);

                listed.Add(SupplierMapper.ToDto(segment, suppliers));
            }

            return listed;
        }
    }

    /// <summary>Cadastra ou edita um ramo.</summary>
    public class SaveSupplierSegmentHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<SaveSupplierSegmentCommand, SupplierSegmentDto>
    {
        /// <inheritdoc/>
        public async Task<SupplierSegmentDto> Handle(
            SaveSupplierSegmentCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;
            var actor = currentUser.Code.ToString();

            SupplierSegment segment;
            var isNew = request.Code is null;

            if (isNew)
            {
                segment = SupplierSegment.Create(idTenant, request.Name, request.Position, actor);
            }
            else
            {
                segment = await unitOfWork.SupplierSegmentRepository
                    .GetByCodeAsync(idTenant, request.Code!.Value, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new NotFoundException("Ramo inexistente.");

                segment.Rename(request.Name);
                segment.MoveTo(request.Position);
                segment.UpdateAuditInfo(actor);
            }

            if (await unitOfWork.SupplierSegmentRepository
                    .NameExistsAsync(idTenant, request.Name, isNew ? null : segment.Id, cancellationToken)
                    .ConfigureAwait(false))
            {
                throw new BusinessRuleException($"A revenda já tem um ramo chamado {request.Name.Trim()}.");
            }

            if (isNew)
            {
                unitOfWork.SupplierSegmentRepository.Add(segment);
            }
            else
            {
                unitOfWork.SupplierSegmentRepository.Update(segment);
            }

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                idTenant, currentUser.Id, nameof(SupplierSegment), segment.Code,
                isNew ? AuditAction.Create : AuditAction.Update, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            var suppliers = isNew
                ? 0
                : await unitOfWork.SupplierSegmentRepository
                    .CountSuppliersAsync(segment.Id, cancellationToken)
                    .ConfigureAwait(false);

            return SupplierMapper.ToDto(segment, suppliers);
        }
    }

    /// <summary>Exclui um ramo, logicamente.</summary>
    public class DeleteSupplierSegmentHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<DeleteSupplierSegmentCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(DeleteSupplierSegmentCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var segment = await unitOfWork.SupplierSegmentRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Ramo inexistente.");

            var suppliers = await unitOfWork.SupplierSegmentRepository
                .CountSuppliersAsync(segment.Id, cancellationToken)
                .ConfigureAwait(false);

            if (suppliers > 0)
            {
                throw new BusinessRuleException(
                    suppliers == 1
                        ? "Este ramo tem 1 fornecedor. Mude o ramo dele antes de excluir."
                        : $"Este ramo tem {suppliers} fornecedores. Mude o ramo deles antes de excluir.");
            }

            var actor = currentUser.Code.ToString();

            unitOfWork.SupplierSegmentRepository.Remove(segment, actor);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(SupplierSegment), segment.Code,
                AuditAction.Delete, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Quanto foi para cada fornecedor num período, do maior para o menor.
    ///
    /// A soma vem do banco (decisão 5 do M18). Fornecedor sem gasto no período fica de fora: o
    /// ranking responde "com quem gastei", e quem ficou em zero responde outra pergunta.
    /// </summary>
    public class ListSupplierSpendingHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ListSupplierSpendingQuery, IReadOnlyList<SupplierSpendDto>>
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<SupplierSpendDto>> Handle(
            ListSupplierSpendingQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            return SupplierSpending.ReadAsync(
                unitOfWork, currentUser.IdTenant, request.From, request.To, cancellationToken);
        }
    }

    /// <summary>A ficha de um fornecedor: totais, quebra por tipo e cada gasto com o carro.</summary>
    public class GetSupplierStatementHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<GetSupplierStatementQuery, SupplierStatementDto>
    {
        /// <inheritdoc/>
        public async Task<SupplierStatementDto> Handle(
            GetSupplierStatementQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;

            var supplier = await unitOfWork.SupplierRepository
                .GetByCodeAsync(idTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Fornecedor inexistente.");

            var segment = await unitOfWork.SupplierSegmentRepository
                .ListByTenantAsync(idTenant, cancellationToken)
                .ConfigureAwait(false);

            var lines = await unitOfWork.SupplierRepository
                .ListExpensesAsync(idTenant, supplier.Id, request.From, request.To, cancellationToken)
                .ConfigureAwait(false);

            var types = await unitOfWork.ExpenseTypeRepository
                .ListByTenantAsync(idTenant, cancellationToken)
                .ConfigureAwait(false);

            var typeNames = types.ToDictionary(type => type.Id, type => type.Name);

            // A quebra por tipo sai das linhas que a ficha já carregou para mostrar: é um
            // fornecedor só, e somar de novo no banco seria pedir duas vezes a mesma coisa.
            var byType = lines
                .GroupBy(line => typeNames.GetValueOrDefault(line.IdExpenseType, "Outros"))
                .Select(group => new SupplierTypeSpendDto(
                    group.Key,
                    group.Where(line => line.IsPaid).Sum(line => line.Amount),
                    group.Count()))
                .OrderByDescending(row => row.PaidTotal)
                .ThenBy(row => row.ExpenseTypeName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var expenses = lines
                .Select(line => new SupplierExpenseDto(
                    line.Code,
                    line.Date,
                    line.Description,
                    typeNames.GetValueOrDefault(line.IdExpenseType, "Outros"),
                    line.Amount,
                    line.IsPaid,
                    line.VehicleCode,
                    line.Plate,
                    VehicleName(line)))
                .ToList();

            return new SupplierStatementDto(
                SupplierMapper.ToDto(
                    supplier,
                    segment.FirstOrDefault(s => s.Id == supplier.IdSupplierSegment),
                    lines.Count),
                lines.Where(line => line.IsPaid).Sum(line => line.Amount),
                lines.Where(line => !line.IsPaid).Sum(line => line.Amount),
                byType,
                expenses);
        }

        private static string VehicleName(SupplierExpenseLine line) =>
            string.IsNullOrWhiteSpace(line.Version)
                ? $"{line.Brand} {line.Model} {line.ModelYear}"
                : $"{line.Brand} {line.Model} {line.Version} {line.ModelYear}";
    }

    /// <summary>O painel do gasto com fornecedores.</summary>
    public class GetSupplierStatisticsHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<GetSupplierStatisticsQuery, SupplierStatisticsDto>
    {
        /// <inheritdoc/>
        public Task<SupplierStatisticsDto> Handle(
            GetSupplierStatisticsQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            return SupplierSpending.StatisticsAsync(
                unitOfWork, currentUser.IdTenant, request.From, request.To, cancellationToken);
        }
    }

    /// <summary>A leitura de gasto por fornecedor, compartilhada com o painel.</summary>
    public static class SupplierSpending
    {
        /// <summary>Quantos meses a série mensal cobre quando o período fica aberto.</summary>
        private const int OpenPeriodMonths = 12;

        /// <summary>
        /// O painel inteiro: totais, ranking e as três somas. Cinco consultas, nenhuma por linha.
        /// </summary>
        /// <param name="unitOfWork">Acesso aos dados.</param>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="from">Primeiro dia, inclusive.</param>
        /// <param name="to">Último dia, inclusive.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>O painel.</returns>
        public static async Task<SupplierStatisticsDto> StatisticsAsync(
            IUnitOfWork unitOfWork,
            int idTenant,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken,
            int? trendMonths = null)
        {
            var bySupplier = await ReadAsync(unitOfWork, idTenant, from, to, cancellationToken)
                .ConfigureAwait(false);

            var statistics = await unitOfWork.SupplierRepository
                .ReadStatisticsAsync(idTenant, from, to, cancellationToken)
                .ConfigureAwait(false);

            var segments = await unitOfWork.SupplierSegmentRepository
                .ListByTenantAsync(idTenant, cancellationToken)
                .ConfigureAwait(false);

            var types = await unitOfWork.ExpenseTypeRepository
                .ListByTenantAsync(idTenant, cancellationToken)
                .ConfigureAwait(false);

            var segmentsById = segments.ToDictionary(segment => segment.Id);
            var typesById = types.ToDictionary(type => type.Id);

            var bySegment = statistics.BySegment
                .Select(slice => new SpendSliceDto(
                    segmentsById.GetValueOrDefault(slice.Key)?.Code.ToString() ?? string.Empty,
                    segmentsById.GetValueOrDefault(slice.Key)?.Name ?? "Sem ramo",
                    slice.PaidTotal, slice.PlannedTotal, slice.ExpenseCount))
                .ToList();

            var byType = statistics.ByType
                .Select(slice => new SpendSliceDto(
                    typesById.GetValueOrDefault(slice.Key)?.Code.ToString() ?? string.Empty,
                    typesById.GetValueOrDefault(slice.Key)?.Name ?? "Outros",
                    slice.PaidTotal, slice.PlannedTotal, slice.ExpenseCount))
                .ToList();

            // O dashboard pede a tendência de doze meses mesmo com o período no mês corrente:
            // uma coluna só é um número, e não uma tendência. A série vem de uma sexta
            // consulta, com o próprio limite; o resto do painel obedece ao período.
            var byMonth = statistics.ByMonth;
            var monthsFrom = from;

            if (trendMonths is { } months)
            {
                var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
                monthsFrom = new DateOnly(end.Year, end.Month, 1).AddMonths(-(months - 1));

                byMonth = await unitOfWork.SupplierRepository
                    .SumByMonthAsync(idTenant, monthsFrom, to, cancellationToken)
                    .ConfigureAwait(false);
            }

            return new SupplierStatisticsDto(
                from,
                to,
                statistics.PaidTotal,
                statistics.PlannedTotal,
                statistics.ExpenseCount,
                bySupplier.Count,
                statistics.VehicleCount,
                statistics.UnassignedPaid,
                bySupplier,
                bySegment,
                byType,
                Months(byMonth, monthsFrom, to));
        }

        /// <summary>
        /// A série mensal em ordem, com os meses vazios preenchidos com zero: um gráfico com
        /// buraco no meio lê como erro, e não como mês sem gasto.
        ///
        /// Com o período aberto, a série cobre os últimos doze meses — e nada além: o total do
        /// painel continua sendo desde o início, mas doze colunas é o que cabe numa tela.
        /// </summary>
        private static List<SpendSliceDto> Months(
            IReadOnlyList<SpendSlice> slices,
            DateOnly? from,
            DateOnly? to)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var last = new DateOnly((to ?? today).Year, (to ?? today).Month, 1);
            var first = from is { } start
                ? new DateOnly(start.Year, start.Month, 1)
                : last.AddMonths(-(OpenPeriodMonths - 1));

            if (first > last)
            {
                first = last;
            }

            var byKey = slices.ToDictionary(slice => slice.Key);
            var months = new List<SpendSliceDto>();

            for (var month = first; month <= last; month = month.AddMonths(1))
            {
                var key = month.Year * 100 + month.Month;
                var slice = byKey.GetValueOrDefault(key);

                months.Add(new SpendSliceDto(
                    month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    month.ToString("MMM/yy", new CultureInfo("pt-BR")).Replace(".", string.Empty, StringComparison.Ordinal),
                    slice?.PaidTotal ?? 0m,
                    slice?.PlannedTotal ?? 0m,
                    slice?.ExpenseCount ?? 0));
            }

            return months;
        }

        /// <summary>Quanto foi para cada fornecedor da revenda, do maior para o menor.</summary>
        /// <param name="unitOfWork">Acesso aos dados.</param>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="from">Primeiro dia, inclusive.</param>
        /// <param name="to">Último dia, inclusive.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>As linhas, com nome e ramo.</returns>
        public static async Task<IReadOnlyList<SupplierSpendDto>> ReadAsync(
            IUnitOfWork unitOfWork,
            int idTenant,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken)
        {
            var sums = await unitOfWork.SupplierRepository
                .SumByTenantAsync(idTenant, from, to, cancellationToken)
                .ConfigureAwait(false);

            if (sums.Count == 0)
            {
                return [];
            }

            var suppliers = await unitOfWork.SupplierRepository
                .ListByTenantAsync(idTenant, cancellationToken)
                .ConfigureAwait(false);

            var segments = await SupplierMapper
                .SegmentsByIdAsync(unitOfWork, idTenant, cancellationToken)
                .ConfigureAwait(false);

            var byId = suppliers.ToDictionary(supplier => supplier.Id);

            return [.. sums
                .Where(sum => byId.ContainsKey(sum.IdSupplier))
                .Select(sum =>
                {
                    var supplier = byId[sum.IdSupplier];

                    return new SupplierSpendDto(
                        supplier.Code,
                        supplier.Name,
                        segments.GetValueOrDefault(supplier.IdSupplierSegment)?.Name ?? string.Empty,
                        sum.PaidTotal,
                        sum.PlannedTotal,
                        sum.ExpenseCount,
                        sum.LastDate);
                })];
        }
    }

    /// <summary>O fornecedor e o ramo como a tela lê.</summary>
    internal static class SupplierMapper
    {
        /// <summary>Os ramos da revenda, por Id, para a lista mostrar o nome sem uma consulta por linha.</summary>
        /// <param name="unitOfWork">Acesso aos dados.</param>
        /// <param name="idTenant">Empresa dona do cadastro.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Os ramos, por Id.</returns>
        public static async Task<IReadOnlyDictionary<int, SupplierSegment>> SegmentsByIdAsync(
            IUnitOfWork unitOfWork,
            int idTenant,
            CancellationToken cancellationToken)
        {
            var segments = await unitOfWork.SupplierSegmentRepository
                .ListByTenantAsync(idTenant, cancellationToken)
                .ConfigureAwait(false);

            return segments.ToDictionary(segment => segment.Id);
        }

        /// <summary>Monta o fornecedor para a tela.</summary>
        /// <param name="supplier">O fornecedor.</param>
        /// <param name="segments">Os ramos da revenda, por Id.</param>
        /// <param name="expenseCount">Quantos gastos apontam para ele.</param>
        /// <returns>O fornecedor como a tela lê.</returns>
        public static SupplierDto ToDto(
            Supplier supplier,
            IReadOnlyDictionary<int, SupplierSegment> segments,
            int expenseCount) =>
            ToDto(supplier, segments.GetValueOrDefault(supplier.IdSupplierSegment), expenseCount);

        /// <summary>Monta o fornecedor para a tela.</summary>
        /// <param name="supplier">O fornecedor.</param>
        /// <param name="segment">O ramo dele, quando ainda existe.</param>
        /// <param name="expenseCount">Quantos gastos apontam para ele.</param>
        /// <returns>O fornecedor como a tela lê.</returns>
        public static SupplierDto ToDto(Supplier supplier, SupplierSegment? segment, int expenseCount) =>
            new(supplier.Code,
                supplier.Name,
                segment?.Code ?? Guid.Empty,
                segment?.Name ?? string.Empty,
                supplier.ContactName,
                supplier.ContactPhone,
                supplier.Document,
                supplier.Notes,
                expenseCount);

        /// <summary>Monta o ramo para a tela.</summary>
        /// <param name="segment">O ramo.</param>
        /// <param name="supplierCount">Quantos fornecedores estão nele.</param>
        /// <returns>O ramo como a tela lê.</returns>
        public static SupplierSegmentDto ToDto(SupplierSegment segment, int supplierCount) =>
            new(segment.Code, segment.Name, segment.Position, supplierCount);
    }
}
