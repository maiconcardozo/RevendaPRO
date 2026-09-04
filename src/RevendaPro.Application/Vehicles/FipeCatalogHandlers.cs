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
        IFipeQuoteReader quotes,
        IMediator mediator)
        : IRequestHandler<MatchVehicleFipeModelCommand, FipeMatchDto>
    {
        /// <summary>
        /// Quantas perguntas de ano uma busca pode gastar.
        ///
        /// Conferir o ano custa uma chamada por candidato, e é o descarte mais forte que existe:
        /// um Gol 2015 não pode ser a linha que a tabela só precifica de 2019 em diante. O teto
        /// existe para uma marca com centenas de versões não virar centenas de chamadas.
        ///
        /// O custo é pago <b>uma vez</b>: as listas de nome ficam guardadas por doze horas no
        /// adaptador, então o segundo Gol da semana não gasta pergunta nenhuma.
        /// </summary>
        private const int YearQuestions = 30;

        /// <summary>
        /// Até quantos candidatos ganham o preço ao lado do nome.
        ///
        /// O preço é o que decide a escolha: entre duas versões do mesmo carro, quem conhece o
        /// carro reconhece a faixa de preço bem antes de reconhecer a sigla do acabamento. Ele
        /// custa uma pergunta por candidato, e por isso tem teto — uma lista longa demais para
        /// ser lida também é longa demais para ser perguntada.
        /// </summary>
        private const int PricesShownUpTo = 12;

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

            var tiers = FipeModelMatcher.Ranked(models.Value!, vehicle);

            if (tiers.Count == 0)
            {
                return new FipeMatchDto(null, []);
            }

            var candidates = await WithTheYearAsync(brand, tiers, vehicle, cancellationToken)
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
        /// Os candidatos que a tabela precifica <b>no ano deste carro</b>.
        ///
        /// O ano é exigência, e não desempate. A busca desce as camadas de nome — da que mais
        /// repete o carro para a que menos repete — e para na primeira que responde pelo ano.
        ///
        /// É o caso do Gol: "1.6 MSI" acerta em cheio duas linhas da tabela, e as duas só
        /// existem de 2019 em diante. Num Gol 2015 elas cedem para a camada de baixo, onde estão
        /// o Trendline e o Comfortline — que é onde a tabela guarda o mesmo motor daquele ano.
        ///
        /// Gastando o teto de perguntas sem achar o ano, a melhor camada volta <b>sem anos</b>:
        /// é o sinal de que a tela precisa dizer que a tabela segue sem este carro naquele ano,
        /// em vez de oferecer um preço de outra geração.
        /// </summary>
        private async Task<IReadOnlyList<FipeCandidateDto>> WithTheYearAsync(
            FipeNamed brand,
            IReadOnlyList<IReadOnlyList<FipeNamed>> tiers,
            Vehicle vehicle,
            CancellationToken cancellationToken)
        {
            var questions = YearQuestions;
            var found = new List<FipeCandidateDto>();

            foreach (var tier in tiers)
            {
                foreach (var model in tier)
                {
                    if (questions == 0)
                    {
                        break;
                    }

                    questions--;

                    var years = await catalog
                        .ListModelYearsAsync(brand.Code, model.Code, cancellationToken)
                        .ConfigureAwait(false);

                    // A fonte que recusa a lista de um candidato apenas o deixa de fora desta
                    // rodada: quem lê ainda alcança esse modelo pelo caminho longo.
                    if (!years.Ok)
                    {
                        continue;
                    }

                    var matching = FipeModelMatcher.YearsOf(years.Value!, vehicle.ModelYear);

                    if (matching.Count > 0)
                    {
                        found.Add(new FipeCandidateDto(
                            brand.Code,
                            model.Code,
                            model.Name,
                            [.. matching.Select(option =>
                                new FipeOptionDto(option.YearFuel, option.Name))]));
                    }
                }

                // Achou nesta camada: descer mais só traria nomes que repetem menos o carro.
                if (found.Count > 0 || questions == 0)
                {
                    break;
                }
            }

            if (found.Count > 0)
            {
                return await WithThePriceAsync(found, cancellationToken).ConfigureAwait(false);
            }

            return [.. tiers[0].Select(model =>
                new FipeCandidateDto(brand.Code, model.Code, model.Name, []))];
        }

        /// <summary>
        /// O preço de cada candidato, ao lado do nome.
        ///
        /// Pergunta pela primeira linha de ano de cada um — que é a do ano do carro, porque esta
        /// lista já passou pelo descarte do ano. A pergunta é <b>fixada no mês publicado</b>,
        /// como toda pergunta de dinheiro deste sistema (ADR-0005).
        ///
        /// A fonte que recusa o preço de um candidato apenas o deixa sem número: o nome continua
        /// na lista, e escolher aquele modelo continua funcionando.
        /// </summary>
        private async Task<IReadOnlyList<FipeCandidateDto>> WithThePriceAsync(
            IReadOnlyList<FipeCandidateDto> candidates,
            CancellationToken cancellationToken)
        {
            if (candidates.Count > PricesShownUpTo)
            {
                return candidates;
            }

            var table = await quotes.PublishedTableAsync(cancellationToken).ConfigureAwait(false);

            if (!table.Ok)
            {
                return candidates;
            }

            var priced = new List<FipeCandidateDto>(candidates.Count);

            foreach (var candidate in candidates)
            {
                var price = await catalog
                    .GetPriceOfModelAsync(
                        candidate.BrandCode,
                        candidate.ModelCode,
                        candidate.Years[0].Code,
                        table.Value!.Code,
                        cancellationToken)
                    .ConfigureAwait(false);

                priced.Add(price.Ok
                    ? candidate with { Value = price.Value!.Value, FipeCode = price.Value.FipeCode }
                    : candidate);
            }

            return priced;
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
