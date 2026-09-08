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
    public class GetSaleSheetHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser, IFileStorage storage)
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

    /// <summary>As fotos de um carro em bytes, a capa primeiro, para um documento.</summary>
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
