using MediatR;

namespace RevendaPro.Application.Vehicles.Queries
{
    /// <summary>
    /// Every brand the reference table prices, for the car nobody has a code for yet.
    /// </summary>
    public sealed record ListFipeBrandsQuery
        : IRequest<IReadOnlyList<DTOs.FipeOptionDto>>;

    /// <summary>Every model of one brand.</summary>
    /// <param name="BrandCode">Code of the brand, as the table names it.</param>
    public sealed record ListFipeModelsQuery(string BrandCode)
        : IRequest<IReadOnlyList<DTOs.FipeOptionDto>>;

    /// <summary>Every year and fuel combination of one model.</summary>
    /// <param name="BrandCode">Code of the brand.</param>
    /// <param name="ModelCode">Code of the model.</param>
    public sealed record ListFipeModelYearsQuery(string BrandCode, string ModelCode)
        : IRequest<IReadOnlyList<DTOs.FipeOptionDto>>;
}

namespace RevendaPro.Application.Vehicles.Commands
{
    /// <summary>
    /// Points the vehicle at a model chosen from the table, and reads its value.
    ///
    /// It is the door for the car with no code: three choices — brand, model, year — and from
    /// then on every lookup is a direct call. See ADR-0005.
    /// </summary>
    /// <param name="Code">Public identifier of the vehicle.</param>
    /// <param name="BrandCode">Code of the brand that was chosen.</param>
    /// <param name="ModelCode">Code of the model that was chosen.</param>
    /// <param name="YearFuel">Year and fuel that was chosen.</param>
    public sealed record SetVehicleFipeModelCommand(
        Guid Code,
        string BrandCode,
        string ModelCode,
        string YearFuel) : IRequest<DTOs.FipeReferenceDto>;

    /// <summary>
    /// Procura o modelo deste carro na tabela, e resolve sozinho quando sobra um só.
    ///
    /// É o caminho do carro sem código: em vez de mandar a pessoa escolher entre as cem linhas
    /// de uma marca, o sistema descarta o que não pode ser ele e mostra o que sobrou. Sobrando
    /// um, com um ano só, ele grava — porque escolha nenhuma restou para fazer.
    ///
    /// <b>Empate jamais vira palpite.</b> Duas versões do mesmo carro são dois preços, às vezes
    /// dezenas de milhares distantes, e essa escolha é de quem conhece o carro.
    /// </summary>
    /// <param name="Code">Public identifier of the vehicle.</param>
    public sealed record MatchVehicleFipeModelCommand(Guid Code)
        : IRequest<DTOs.FipeMatchDto>;
}

namespace RevendaPro.Application.Vehicles.DTOs
{
    /// <summary>
    /// One choice of the chooser: what the source expects back, and what a person reads.
    /// </summary>
    /// <param name="Code">What goes back to the source (<c>23</c>, <c>5635</c>, <c>2014-5</c>).</param>
    /// <param name="Name">What appears on the screen (<c>GM - Chevrolet</c>, <c>2014 Flex</c>).</param>
    public sealed record FipeOptionDto(string Code, string Name);

    /// <summary>
    /// Um modelo da tabela que pode ser este carro.
    /// </summary>
    /// <param name="BrandCode">A marca, para a escolha voltar sem uma segunda busca.</param>
    /// <param name="ModelCode">O modelo, como a fonte espera receber de volta.</param>
    /// <param name="Name">
    /// O nome como a tabela escreve — <c>Renegade Longitude 1.8 4x2 Flex 16V Aut.</c> É a única
    /// coisa que distingue duas linhas de preço, e por isso vai inteiro para a tela.
    /// </param>
    /// <param name="Years">
    /// As linhas de ano e combustível deste modelo que servem para o ano do carro. Vem vazia
    /// quando a tabela segue sem este modelo no ano do carro.
    /// </param>
    /// <param name="Value">
    /// O que a tabela cobra por esta linha, no mês publicado.
    ///
    /// É o número que decide a escolha: entre duas versões do mesmo carro, quem conhece o carro
    /// reconhece a faixa de preço muito antes de reconhecer a sigla do acabamento. Nulo quando
    /// sobraram candidatos demais para perguntar o preço de cada um, ou quando a fonte recusou
    /// aquele em particular.
    /// </param>
    /// <param name="FipeCode">
    /// O código impresso da tabela (<c>004380-0</c>), que só existe depois de perguntar o preço.
    /// Nulo pelo mesmo motivo do valor.
    /// </param>
    public sealed record FipeCandidateDto(
        string BrandCode,
        string ModelCode,
        string Name,
        IReadOnlyList<FipeOptionDto> Years,
        decimal? Value = null,
        string? FipeCode = null);

    /// <summary>
    /// O que a busca respondeu: ou ela resolveu, ou ela mostra o que sobrou.
    ///
    /// Os dois campos jamais vêm preenchidos juntos. <see cref="Applied"/> quer dizer que sobrou
    /// um candidato só, com um ano só, e o carro já está apontado para ele. <see cref="Candidates"/>
    /// quer dizer que a escolha é da pessoa — e as duas listas vazias querem dizer que a tabela
    /// segue sem este carro.
    /// </summary>
    /// <param name="Applied">O que foi gravado, quando a busca resolveu sozinha.</param>
    /// <param name="Candidates">Os modelos que sobraram, quando a escolha é de quem lê.</param>
    public sealed record FipeMatchDto(
        FipeReferenceDto? Applied,
        IReadOnlyList<FipeCandidateDto> Candidates);
}
