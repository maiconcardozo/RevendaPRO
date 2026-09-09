using MediatR;
using RevendaPro.Application.Trash.DTOs;
using RevendaPro.Application.Trash.Queries;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.Interfaces.Storage;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Application.Trash.Handlers
{
    /// <summary>
    /// A lixeira: o que foi apagado, e como se reconhece cada coisa (M23).
    ///
    /// Toda exclusão deste sistema é lógica (RNF-08): a linha fica, com <c>IsActive = 0</c>, o
    /// dia e o código de quem apagou. O que faltava era a porta de volta para o carro e para o
    /// gasto — o documento já tinha a dele desde o M10, porque o arquivo dele continuava pago e
    /// parado no bucket.
    ///
    /// Esta versão apenas enxerga. A volta é a seguinte.
    /// </summary>
    public class ListDeletedItemsHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFileStorage storage)
        : IRequestHandler<ListDeletedItemsQuery, IReadOnlyList<DeletedItemDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<DeletedItemDto>> Handle(
            ListDeletedItemsQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            return request.Kind switch
            {
                TrashKind.Vehicle => await ListVehiclesAsync(cancellationToken).ConfigureAwait(false),
                TrashKind.Expense => await ListExpensesAsync(cancellationToken).ConfigureAwait(false),
                TrashKind.Document => await ListDocumentsAsync(cancellationToken).ConfigureAwait(false),
                _ => throw new BusinessRuleException("Tipo desconhecido para a lixeira.")
            };
        }

        private async Task<IReadOnlyList<DeletedItemDto>> ListVehiclesAsync(
            CancellationToken cancellationToken)
        {
            var vehicles = await unitOfWork.VehicleRepository
                .ListDeletedAsync(currentUser.IdTenant, cancellationToken)
                .ConfigureAwait(false);

            if (vehicles.Count == 0)
            {
                return [];
            }

            var names = await NamesAsync(cancellationToken).ConfigureAwait(false);

            return [.. vehicles.Select(vehicle => new DeletedItemDto(
                TrashKind.Vehicle,
                vehicle.Code,
                vehicle.Plate,
                Describe(vehicle.Brand, vehicle.Model, vehicle.Version, vehicle.ModelYear),
                vehicle.PurchasePrice,
                Date: null,
                vehicle.DeletedAt,
                NameOf(names, vehicle.DeletedByCode),
                // O carro é a coisa apagada, e a ficha dele ainda não abre: link nenhum aqui.
                VehicleCode: null,
                VehiclePlate: null,
                VehicleName: null,
                VehicleIsInYard: null,
                DocumentKind: null,
                SizeInBytes: null,
                FileUrl: null))];
        }

        private async Task<IReadOnlyList<DeletedItemDto>> ListExpensesAsync(
            CancellationToken cancellationToken)
        {
            var expenses = await unitOfWork.VehicleExpenseRepository
                .ListDeletedByTenantAsync(currentUser.IdTenant, cancellationToken)
                .ConfigureAwait(false);

            if (expenses.Count == 0)
            {
                return [];
            }

            var names = await NamesAsync(cancellationToken).ConfigureAwait(false);

            return [.. expenses.Select(expense => new DeletedItemDto(
                TrashKind.Expense,
                expense.Code,
                expense.Description,
                expense.TypeName,
                expense.Amount,
                expense.Date,
                expense.DeletedAt,
                NameOf(names, expense.DeletedByCode),
                expense.VehicleCode,
                expense.Plate,
                $"{expense.Brand} {expense.Model}",
                expense.VehicleIsActive,
                DocumentKind: null,
                SizeInBytes: null,
                FileUrl: null))];
        }

        private async Task<IReadOnlyList<DeletedItemDto>> ListDocumentsAsync(
            CancellationToken cancellationToken)
        {
            var documents = await unitOfWork.VehicleDocumentRepository
                .ListDeletedByTenantAsync(currentUser.IdTenant, cancellationToken)
                .ConfigureAwait(false);

            if (documents.Count == 0)
            {
                return [];
            }

            var names = await NamesAsync(cancellationToken).ConfigureAwait(false);

            return [.. documents.Select(document => new DeletedItemDto(
                TrashKind.Document,
                document.Code,
                document.FileName,
                Subtitle: null,
                Amount: null,
                Date: null,
                document.DeletedAt,
                NameOf(names, document.DeletedByCode),
                document.VehicleCode,
                document.Plate,
                $"{document.Brand} {document.Model}",
                // A consulta de documentos já deixa de fora os de carro excluído: o documento
                // que aparece aqui pende sempre de um carro que está no pátio.
                VehicleIsInYard: true,
                document.Kind,
                document.SizeInBytes,
                storage.GetUrl(document.StorageKey, FileVisibility.Private).ToString()))];
        }

        /// <summary>
        /// O nome de quem apagou, por código de usuário, incluindo quem já saiu da revenda.
        ///
        /// Os usuários são lidos de uma vez, e jamais uma consulta por linha: a lixeira de uma
        /// faxina grande teria dezenas de linhas apagadas pela mesma pessoa.
        /// </summary>
        private async Task<Dictionary<string, string>> NamesAsync(CancellationToken cancellationToken)
        {
            var people = await unitOfWork.UserRepository
                .ListByTenantAsync(currentUser.IdTenant, null, includeDeleted: true, cancellationToken)
                .ConfigureAwait(false);

            return people
                .GroupBy(user => user.Code.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().Name,
                    StringComparer.OrdinalIgnoreCase);
        }

        private static string? NameOf(Dictionary<string, string> names, string? code) =>
            code is not null && names.TryGetValue(code, out var name) ? name : null;

        /// <summary>O carro por extenso, como a lista o apresenta.</summary>
        private static string Describe(string brand, string model, string? version, int modelYear)
        {
            var parts = string.IsNullOrWhiteSpace(version)
                ? $"{brand} {model}"
                : $"{brand} {model} {version}";

            return $"{parts} {modelYear}";
        }
    }
}
