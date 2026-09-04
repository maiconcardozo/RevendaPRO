using MediatR;
using RevendaPro.Application.Fipe;
using RevendaPro.Application.Vehicles.Commands;
using RevendaPro.Application.Vehicles.DTOs;
using RevendaPro.Application.Vehicles.Queries;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.Interfaces;
using RevendaPro.Domain.Interfaces.Reference;
using RevendaPro.Domain.Interfaces.Security;
using RevendaPro.Domain.ValueObjects;
using RevendaPro.Shared.Exceptions;

namespace RevendaPro.Application.Vehicles.Handlers
{
    /// <summary>Every brand the table prices.</summary>
    public class ListFipeBrandsHandler(IFipeCatalog catalog)
        : IRequestHandler<ListFipeBrandsQuery, IReadOnlyList<FipeOptionDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<FipeOptionDto>> Handle(
            ListFipeBrandsQuery request,
            CancellationToken cancellationToken)
        {
            var read = await catalog.ListBrandsAsync(cancellationToken).ConfigureAwait(false);

            return FipeChooser.OptionsOf(read);
        }
    }

    /// <summary>Every model of one brand.</summary>
    public class ListFipeModelsHandler(IFipeCatalog catalog)
        : IRequestHandler<ListFipeModelsQuery, IReadOnlyList<FipeOptionDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<FipeOptionDto>> Handle(
            ListFipeModelsQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var read = await catalog
                .ListModelsAsync(request.BrandCode, cancellationToken)
                .ConfigureAwait(false);

            return FipeChooser.OptionsOf(read);
        }
    }

    /// <summary>Every year and fuel combination of one model.</summary>
    public class ListFipeModelYearsHandler(IFipeCatalog catalog)
        : IRequestHandler<ListFipeModelYearsQuery, IReadOnlyList<FipeOptionDto>>
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<FipeOptionDto>> Handle(
            ListFipeModelYearsQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var read = await catalog
                .ListModelYearsAsync(request.BrandCode, request.ModelCode, cancellationToken)
                .ConfigureAwait(false);

            if (!read.Ok)
            {
                throw FipeChooser.Refused(read.Outcome);
            }

            return [.. read.Value!.Select(option => new FipeOptionDto(option.YearFuel, option.Name))];
        }
    }

    /// <summary>
    /// Points the vehicle at a model chosen from the table (RF-14).
    ///
    /// This is the one call that learns <b>the code of the model</b>, which is what turns
    /// every later lookup into a direct call. What it writes is the reference and the model —
    /// and no price, like everything else in this milestone.
    /// </summary>
    public class SetVehicleFipeModelHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFipeCatalog catalog,
        IFipeQuoteReader quotes)
        : IRequestHandler<SetVehicleFipeModelCommand, FipeReferenceDto>
    {
        /// <inheritdoc/>
        public async Task<FipeReferenceDto> Handle(
            SetVehicleFipeModelCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await unitOfWork.VehicleRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Veículo inexistente.");

            var table = await quotes.PublishedTableAsync(cancellationToken).ConfigureAwait(false);

            if (!table.Ok)
            {
                throw FipeChooser.Refused(table.Outcome);
            }

            // Pinned, unlike the three listing calls that led here: this one answers money.
            var price = await catalog
                .GetPriceOfModelAsync(
                    request.BrandCode, request.ModelCode, request.YearFuel,
                    table.Value!.Code, cancellationToken)
                .ConfigureAwait(false);

            if (!price.Ok)
            {
                throw FipeChooser.Refused(price.Outcome);
            }

            var previous = vehicle.FipeValue;
            var actor = currentUser.Code.ToString();

            vehicle.ApplyFipeReference(
                price.Value!.Value,
                price.Value.Reference,
                price.Value.FipeCode,
                price.Value.YearFuel,
                actor);

            unitOfWork.VehicleRepository.Update(vehicle);

            // The quote goes in through the same door as every other one, so the next car of
            // this model costs nothing.
            await quotes.KeepAsync(price.Value, cancellationToken).ConfigureAwait(false);

            unitOfWork.AuditLogRepository.Add(AuditLog.Create(
                currentUser.IdTenant, currentUser.Id, nameof(Vehicle), vehicle.Code,
                AuditAction.Update, oldValues: null, newValues: null));

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new FipeReferenceDto(
                price.Value.Value,
                price.Value.Reference,
                price.Value.FipeCode,
                price.Value.YearFuel,
                FipeSource.Automatic,
                price.Value.Brand,
                price.Value.Model,
                previous);
        }
    }

    /// <summary>
    /// Procura o modelo do carro na tabela, descartando o que não pode ser ele.
    ///
    /// Duas chamadas de lista levam ao terreno — as marcas e os modelos da marca —, e o
    /// <see cref="FipeModelMatcher"/> faz o resto sem rede. O ano é o descarte mais forte que
    /// existe, e o mais caro: uma chamada por candidato. Por isso ele só roda quando já sobrou
    /// pouca gente.
    ///
    /// Sobrando um candidato com um ano só, esta classe manda a escolha pela <b>mesma porta</b>
    /// que a pessoa usaria — o comando do escolhedor —, e não por um caminho paralelo. Assim o
    /// código gravado, a cotação guardada e a auditoria saem iguais nos dois casos.
    /// </summary>
    public class MatchVehicleFipeModelHandler(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IFipeCatalog catalog,
        IMediator mediator)
        : IRequestHandler<MatchVehicleFipeModelCommand, FipeMatchDto>
    {
        /// <summary>
        /// Até quantos candidatos valem uma chamada cada um para conferir o ano.
        ///
        /// Um modelo com trinta versões viraria trinta chamadas numa fonte de terceiros com
        /// limite de uso. Acima disto a lista vai como está, e quem lê escolhe com o nome — que
        /// já traz o ano na prática, porque a versão muda de nome entre gerações.
        /// </summary>
        private const int YearsCheckedUpTo = 8;

        /// <inheritdoc/>
        public async Task<FipeMatchDto> Handle(
            MatchVehicleFipeModelCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var vehicle = await unitOfWork.VehicleRepository
                .GetByCodeAsync(currentUser.IdTenant, request.Code, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Veículo inexistente.");

            var brands = await catalog.ListBrandsAsync(cancellationToken).ConfigureAwait(false);

            if (!brands.Ok)
            {
                throw FipeChooser.Refused(brands.Outcome);
            }

            var brand = FipeModelMatcher.FindBrand(brands.Value!, vehicle.Brand);

            if (brand is null)
            {
                return new FipeMatchDto(null, []);
            }

            var models = await catalog
                .ListModelsAsync(brand.Code, cancellationToken)
                .ConfigureAwait(false);

            if (!models.Ok)
            {
                throw FipeChooser.Refused(models.Outcome);
            }

            var narrowed = FipeModelMatcher.Narrow(models.Value!, vehicle);

            if (narrowed.Count == 0)
            {
                return new FipeMatchDto(null, []);
            }

            var candidates = await WithYearsAsync(brand, narrowed, vehicle, cancellationToken)
                .ConfigureAwait(false);

            // Um candidato com um ano só é o caso em que escolha nenhuma sobrou para fazer.
            if (candidates.Count == 1 && candidates[0].Years.Count == 1)
            {
                var applied = await mediator.Send(
                    new SetVehicleFipeModelCommand(
                        vehicle.Code,
                        candidates[0].BrandCode,
                        candidates[0].ModelCode,
                        candidates[0].Years[0].Code),
                    cancellationToken)
                    .ConfigureAwait(false);

                return new FipeMatchDto(applied, []);
            }

            return new FipeMatchDto(null, candidates);
        }

        /// <summary>
        /// Os candidatos com as linhas de ano que servem para este carro.
        ///
        /// Quem ficou sem nenhuma linha do ano sai da lista — a tabela jamais precificou aquela
        /// versão naquele ano. Saindo todos, a lista original volta: é melhor oferecer quatro
        /// candidatos do que responder vazio depois de ter achado quatro.
        /// </summary>
        private async Task<IReadOnlyList<FipeCandidateDto>> WithYearsAsync(
            FipeNamed brand,
            IReadOnlyList<FipeNamed> narrowed,
            Vehicle vehicle,
            CancellationToken cancellationToken)
        {
            if (narrowed.Count > YearsCheckedUpTo)
            {
                return [.. narrowed.Select(model =>
                    new FipeCandidateDto(brand.Code, model.Code, model.Name, []))];
            }

            var candidates = new List<FipeCandidateDto>(narrowed.Count);

            foreach (var model in narrowed)
            {
                var years = await catalog
                    .ListModelYearsAsync(brand.Code, model.Code, cancellationToken)
                    .ConfigureAwait(false);

                // A fonte que recusa a lista de um candidato tira o ano dele da conta, e jamais
                // o candidato: quem lê ainda pode escolher esse modelo pelo nome.
                IReadOnlyList<FipeOptionDto> options = years.Ok
                    ? [.. FipeModelMatcher.YearsOf(years.Value!, vehicle.ModelYear)
                        .Select(option => new FipeOptionDto(option.YearFuel, option.Name))]
                    : [];

                candidates.Add(new FipeCandidateDto(brand.Code, model.Code, model.Name, options));
            }

            var priced = candidates.Where(candidate => candidate.Years.Count > 0).ToList();

            return priced.Count > 0 ? priced : candidates;
        }
    }

    /// <summary>
    /// What the three steps of the chooser have in common: a list, or a refusal that says
    /// which of the two things happened.
    /// </summary>
    internal static class FipeChooser
    {
        /// <summary>Turns a reading of names into options, or throws the reason.</summary>
        /// <param name="read">What the source answered.</param>
        /// <returns>The options.</returns>
        public static IReadOnlyList<FipeOptionDto> OptionsOf(FipeResult<IReadOnlyList<FipeNamed>> read)
        {
            if (!read.Ok)
            {
                throw Refused(read.Outcome);
            }

            return [.. read.Value!.Select(named => new FipeOptionDto(named.Code, named.Name))];
        }

        /// <summary>
        /// A table that stayed quiet, said as a business refusal: the operation was declined
        /// with a reason, and nothing was written. The technical detail stays in the log,
        /// where the adapter already put it.
        /// </summary>
        /// <param name="outcome">What happened.</param>
        /// <returns>The exception to throw.</returns>
        public static BusinessRuleException Refused(FipeOutcome outcome) =>
            outcome == FipeOutcome.Missing
                ? new BusinessRuleException(
                    "A tabela FIPE respondeu sem opções para esta escolha. "
                    + "Tente por outra marca ou modelo.")
                : new BusinessRuleException(
                    "A tabela FIPE está fora de alcance agora. Tente de novo em alguns minutos.");
    }
}
