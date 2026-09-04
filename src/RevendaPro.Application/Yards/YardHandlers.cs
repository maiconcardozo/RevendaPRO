using MediatR;
using RevendaPro.Application.Yards.Commands;
using RevendaPro.Application.Yards.DTOs;
using RevendaPro.Application.Yards.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Application.Yards.Handlers
{
    /// <summary>Os pátios da revenda, com quantos carros estão em cada um.</summary>
    public class ListYardsHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<ListYardsQuery, IReadOnlyList<YardDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<YardDto>> Handle(
            ListYardsQuery request,
            CancellationToken cancellationToken)
        {
            var yards = await unitOfWork.YardRepository
                .ListByTenantAsync(currentUser.IdTenant, cancellationToken)
                .ConfigureAwait(false);

            var counted = new List<YardDto>(yards.Count);

            foreach (var yard in yards)
            {
                var vehicles = await unitOfWork.YardRepository
                    .CountVehiclesAsync(yard.Id, cancellationToken)
                    .ConfigureAwait(false);

                counted.Add(YardMapper.ToDto(yard, vehicles));
            }

            return counted;
        }
    }

    /// <summary>Cadastra ou edita um pátio.</summary>
    public class SaveYardHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<SaveYardCommand, YardDto>
    {
        /// <inheritdoc/>
        public async Task<YardDto> Handle(SaveYardCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var idTenant = currentUser.IdTenant;
            var actor = currentUser.Code.ToString();

            Yard yard;
            var isNew = request.Code is null;

            if (isNew)
            {
                yard = Yard.Create(idTenant, request.Name, request.Kind, request.Position, actor);
            }
            else
            {
                yard = await unitOfWork.YardRepository
                    .GetByCodeAsync(idTenant, request.Code!.Value, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new NotFoundException("Pátio inexistente.");

                yard.Rename(request.Name);
                yard.SetKind(request.Kind);
                yard.MoveTo(request.Position);
                yard.UpdateAuditInfo(actor);
            }

            if (await unitOfWork.YardRepository
                    .NameExistsAsync(idTenant, request.Name, isNew ? null : yard.Id, cancellationToken)
                    .ConfigureAwait(false))
            {
                throw new BusinessRuleException($"A revenda já tem um pátio chamado {request.Name.Trim()}.");
            }

            yard.SetContact(request.ContactName, request.ContactPhone, request.Notes);
            yard.SetCut(request.CutPercent, request.CutAmount);

            if (isNew)
            {
                unitOfWork.YardRepository.Add(yard);
            }
            else
            {
                unitOfWork.YardRepository.Update(yard);
            }

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                idTenant, currentUser.Id, nameof(Yard), yard.Code,
                isNew ? AuditAction.Create : AuditAction.Update, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            var vehicles = isNew
                ? 0
                : await unitOfWork.YardRepository
                    .CountVehiclesAsync(yard.Id, cancellationToken)
                    .ConfigureAwait(false);

            return YardMapper.ToDto(yard, vehicles);
        }
    }

    /// <summary>Exclui um pátio, logicamente.</summary>
    public class DeleteYardHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : IRequestHandler<DeleteYardCommand>
    {
        /// <inheritdoc/>
        public async Task Handle(DeleteYardCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var yard = await unitOfWork.YardRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Pátio inexistente.");

            var vehicles = await unitOfWork.YardRepository
                .CountVehiclesAsync(yard.Id, cancellationToken)
                .ConfigureAwait(false);

            if (vehicles > 0)
            {
                // Recusa com o número, e não com um "está em uso": quem lê precisa saber o
                // tamanho do trabalho de mover os carros antes de decidir.
                throw new BusinessRuleException(
                    vehicles == 1
                        ? "Este pátio guarda 1 carro. Mova o carro para outro pátio antes de excluir."
                        : $"Este pátio guarda {vehicles} carros. Mova os carros para outro pátio antes de excluir.");
            }

            var actor = currentUser.Code.ToString();

            unitOfWork.YardRepository.Remove(yard, actor);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(Yard), yard.Code,
                AuditAction.Delete, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>O pátio como a tela lê.</summary>
    internal static class YardMapper
    {
        /// <summary>Monta o pátio para a tela.</summary>
        /// <param name="yard">O pátio.</param>
        /// <param name="vehicleCount">Quantos carros estão nele.</param>
        /// <returns>O pátio como a tela lê.</returns>
        public static YardDto ToDto(Yard yard, int vehicleCount) =>
            new(yard.Code,
                yard.Name,
                yard.Kind,
                yard.ContactName,
                yard.ContactPhone,
                yard.CutPercent,
                yard.CutAmount,
                yard.Notes,
                yard.Position,
                vehicleCount);
    }
}
