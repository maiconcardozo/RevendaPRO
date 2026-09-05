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
    /// Procura o modelo deste carro na tabela, e mostra o que achou.
    ///
    /// É o caminho do carro sem código: em vez de mandar a pessoa escolher entre as cem linhas
    /// de uma marca, o sistema descarta o que não pode ser ele e mostra o que sobrou — com a
    /// nota de acurácia de cada um, e o recomendado em destaque quando a nota aponta um só.
    ///
    /// <b>E jamais grava.</b> Achando um candidato ou vinte, a resposta é a lista, e quem
    /// escolhe é quem conhece o carro. Sobrar um prova que o casador eliminou os outros, e
    /// jamais que ele acertou este.
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
    /// <param name="Accuracy">
    /// O quanto deste carro este nome responde, de 0 a 100.
    ///
    /// Sai dos mesmos sinais que eliminam — os termos da versão, o câmbio, o combustível e o
    /// ano conferido na fonte —, dita como fração do que era possível conferir. Um carro
    /// cadastrado sem versão tem pouco a conferir, e a nota baixa de todos os candidatos dele
    /// diz exatamente isso a quem lê.
    /// </param>
    /// <param name="Recommended">
    /// Se este é o candidato que a nota aponta.
    ///
    /// Vem em <b>um</b> candidato, e apenas quando a nota dele é maior que a do segundo. Empate
    /// volta sem recomendado nenhum: onde o sistema empata, ele pergunta em vez de apontar.
    ///
    /// É destaque, e jamais escolha — quem aperta o botão que grava é a pessoa, sempre.
    /// </param>
    public sealed record FipeCandidateDto(
        string BrandCode,
        string ModelCode,
        string Name,
        IReadOnlyList<FipeOptionDto> Years,
        decimal? Value = null,
        string? FipeCode = null,
        int Accuracy = 0,
        bool Recommended = false);

    /// <summary>
    /// O que a busca achou, do mais provável para o menos.
    ///
    /// <b>Uma forma só.</b> Um candidato é uma lista de um, e ela abre o mesmo pop-up que uma
    /// lista de vinte: sobrar um prova que o casador eliminou os outros, e jamais que ele
    /// acertou este. A lista vazia quer dizer que a tabela segue sem este carro pelo nome que
    /// ele tem cadastrado.
    ///
    /// Até o M15 existia aqui um campo <c>Applied</c>, com o que a busca havia gravado sozinha
    /// quando sobrava um candidato com um ano só. O M16 tirou a gravação automática, e tirou o
    /// campo junto: um campo que jamais vem preenchido é uma pergunta a mais para quem lê a API
    /// daqui a seis meses.
    /// </summary>
    /// <param name="Candidates">
    /// Os modelos que sobraram, ordenados pela acurácia, com o recomendado marcado quando a
    /// nota aponta um só. Quem escolhe é a pessoa, em 100% dos casos.
    /// </param>
    public sealed record FipeMatchDto(IReadOnlyList<FipeCandidateDto> Candidates);
}
