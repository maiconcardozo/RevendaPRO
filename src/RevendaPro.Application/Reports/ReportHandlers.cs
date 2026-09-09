using MediatR;
using RevendaPro.Application.Company.Handlers;
using RevendaPro.Application.Reports.DTOs;
using RevendaPro.Application.Reports.Queries;
using RevendaPro.Application.Vehicles.Handlers;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Application.Reports.Handlers
{
    /// <summary>
    /// A ficha do carro para venda: o carro, a revenda e as fotos, prontos para virar papel.
    ///
    /// Quem transforma isto em PDF é a camada da API (ADR-0007). Aqui fica a regra: o que
    /// entra, o que fica de fora, e quantas fotos cabem.
    /// </summary>
    public class GetSaleSheetHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFileStorage storage,
        ICompanyLogoStorage logos)
        : IRequestHandler<GetSaleSheetQuery, SaleSheetDto>
    {
        /// <summary>A capa e mais seis: uma página cabe isso sem virar mosaico de selos.</summary>
        private const int MaxPhotos = 7;

        /// <inheritdoc/>
        public async Task<SaleSheetDto> Handle(GetSaleSheetQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await unitOfWork.VehicleRepository
                .GetByCodeAsync(currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Veículo inexistente.");

            var tenant = await CompanyContext
                .TenantOrRefuseAsync(unitOfWork, currentUser, cancellationToken)
                .ConfigureAwait(false);

            var photos = await ReportPhotos
                .ReadAsync(unitOfWork, storage, vehicle, MaxPhotos, cancellationToken)
                .ConfigureAwait(false);

            return new SaleSheetDto(
                CompanyContext.ToDto(tenant),
                await ReportLogo.ReadAsync(logos, tenant, cancellationToken).ConfigureAwait(false),
                vehicle.Plate,
                vehicle.Brand,
                vehicle.Model,
                vehicle.Version,
                vehicle.ModelYear,
                vehicle.ManufactureYear,
                vehicle.Color,
                vehicle.Mileage,
                vehicle.FuelType,
                vehicle.Transmission,
                vehicle.AdvertisedPrice,
                vehicle.FipeValue,
                vehicle.FipeReferenceDate,
                photos,
                DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3)));
        }
    }

    /// <summary>
    /// A proposta em papel timbrado. A proposta tem de ser deste carro, e o carro desta revenda:
    /// um código de outra empresa lê como inexistente, e jamais vira documento.
    /// </summary>
    public class GetProposalDocumentHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFileStorage storage,
        ICompanyLogoStorage logos)
        : IRequestHandler<GetProposalDocumentQuery, ProposalDocumentDto>
    {
        /// <summary>Sete dias: o prazo que uma proposta de carro usado costuma valer.</summary>
        private const int ValidityDays = 7;

        /// <inheritdoc/>
        public async Task<ProposalDocumentDto> Handle(
            GetProposalDocumentQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await unitOfWork.VehicleRepository
                .GetByCodeAsync(currentUser.IdTenant, request.VehicleCode, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Veículo inexistente.");

            var proposal = await unitOfWork.ProposalRepository
                .GetByCodeAsync(request.ProposalCode, cancellationToken)
                .ConfigureAwait(false);

            if (proposal is null || proposal.IdVehicle != vehicle.Id)
            {
                throw new NotFoundException("Proposta inexistente.");
            }

            var tenant = await CompanyContext
                .TenantOrRefuseAsync(unitOfWork, currentUser, cancellationToken)
                .ConfigureAwait(false);

            var photos = await ReportPhotos
                .ReadAsync(unitOfWork, storage, vehicle, 1, cancellationToken)
                .ConfigureAwait(false);

            var name = string.IsNullOrWhiteSpace(vehicle.Version)
                ? $"{vehicle.Brand} {vehicle.Model}"
                : $"{vehicle.Brand} {vehicle.Model} {vehicle.Version}";

            // O cliente (M21) empresta o nome atual, o CPF e o endereço: é o que falta para o
            // papel valer como aceite. A proposta antiga sem cliente sai como saía.
            var customer = proposal.IdCustomer is { } idCustomer
                ? (await unitOfWork.CustomerRepository
                    .ListByIdsAsync(currentUser.IdTenant, [idCustomer], cancellationToken)
                    .ConfigureAwait(false)).FirstOrDefault()
                : null;

            return new ProposalDocumentDto(
                CompanyContext.ToDto(tenant),
                await ReportLogo.ReadAsync(logos, tenant, cancellationToken).ConfigureAwait(false),
                proposal.Code,
                customer?.Name ?? proposal.ProspectName,
                customer?.Phone ?? proposal.ProspectPhone,
                customer?.Document,
                customer?.Address,
                name,
                vehicle.Plate,
                vehicle.ModelYear,
                vehicle.ManufactureYear,
                vehicle.Mileage,
                vehicle.Color,
                proposal.Amount,
                proposal.PaymentMethod,
                proposal.Date,
                proposal.Date.AddDays(ValidityDays),
                proposal.Notes,
                photos.Count > 0 ? photos[0] : null);
        }
    }

    /// <summary>A planilha de gastos: cada gasto do período com o carro, o tipo e o fornecedor por nome.</summary>
    public class ListExpenseLinesHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ListExpenseLinesQuery, IReadOnlyList<ExpenseLineDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<ExpenseLineDto>> Handle(
            ListExpenseLinesQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;

            var lines = await unitOfWork.VehicleExpenseRepository
                .ListForExportAsync(idTenant, request.From, request.To, cancellationToken)
                .ConfigureAwait(false);

            var types = await ExpenseContext.TypesByIdAsync(unitOfWork, idTenant, cancellationToken)
                .ConfigureAwait(false);

            var suppliers = await ExpenseContext.SuppliersByIdAsync(unitOfWork, idTenant, cancellationToken)
                .ConfigureAwait(false);

            return [.. lines.Select(line => new ExpenseLineDto(
                line.Date,
                line.Plate,
                string.IsNullOrWhiteSpace(line.Version)
                    ? $"{line.Brand} {line.Model} {line.ModelYear}"
                    : $"{line.Brand} {line.Model} {line.Version} {line.ModelYear}",
                line.Description,
                types.GetValueOrDefault(line.IdExpenseType)?.Name ?? "Outros",
                line.IdSupplier is { } idSupplier ? suppliers.GetValueOrDefault(idSupplier)?.Name : null,
                line.Amount,
                line.IsPaid))];
        }
    }

    /// <summary>As fotos de um carro em bytes, a capa primeiro, para um documento.</summary>
    /// <summary>O logotipo da revenda como bytes, para o timbre (M25). Nulo quando ela não subiu um.</summary>
    internal static class ReportLogo
    {
        public static async Task<byte[]?> ReadAsync(
            ICompanyLogoStorage logos,
            Tenant tenant,
            CancellationToken cancellationToken)
        {
            if (tenant.Logo is null)
            {
                return null;
            }

            var stored = await logos.ReadAsync(tenant.Id, tenant.Logo, cancellationToken).ConfigureAwait(false);

            if (stored is null)
            {
                return null;
            }

            await using var content = stored.Content;
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

            return buffer.ToArray();
        }
    }

    internal static class ReportPhotos
    {
        /// <summary>
        /// Lê até <paramref name="limit"/> fotos no tamanho de card: grande o bastante para uma
        /// página, pequeno o bastante para o PDF ficar leve. Foto que sumiu do armazenamento é
        /// pulada, e jamais derruba o documento.
        /// </summary>
        /// <param name="unitOfWork">Acesso aos dados.</param>
        /// <param name="storage">Onde as fotos moram.</param>
        /// <param name="vehicle">O carro.</param>
        /// <param name="limit">Quantas, no máximo.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Os bytes de cada foto, em ordem.</returns>
        public static async Task<IReadOnlyList<byte[]>> ReadAsync(
            IUnitOfWork unitOfWork,
            IFileStorage storage,
            Vehicle vehicle,
            int limit,
            CancellationToken cancellationToken)
        {
            var photos = await unitOfWork.VehiclePhotoRepository
                .ListByVehicleAsync(vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            var ordered = photos
                .OrderByDescending(photo => photo.Id == vehicle.IdCoverPhoto)
                .ThenBy(photo => photo.Position)
                .ThenBy(photo => photo.Id)
                .Take(limit);

            var bytes = new List<byte[]>();

            foreach (var photo in ordered)
            {
                var key = VehicleStorageKeys.Rendition(photo.StorageKey, ImageSize.Card);

                await using var stream = await storage
                    .OpenReadAsync(key, FileVisibility.Private, cancellationToken)
                    .ConfigureAwait(false);

                if (stream is null)
                {
                    continue;
                }

                await using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);

                if (memory.Length > 0)
                {
                    bytes.Add(memory.ToArray());
                }
            }

            return bytes;
        }
    }
}
