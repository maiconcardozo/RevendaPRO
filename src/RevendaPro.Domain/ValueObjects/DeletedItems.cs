namespace RevendaPro.Domain.ValueObjects
{
    /// <summary>
    /// Um carro que foi apagado, como a lixeira precisa mostrá-lo (M23).
    ///
    /// A exclusão do veículo apaga só a linha dele: as fotos, os gastos, os documentos e a
    /// história continuam ativos, e somem da tela porque toda consulta deles passa pelo carro.
    /// Por isso o que a lixeira mostra aqui é o carro, e nada da ficha — devolver a linha
    /// devolve o resto junto.
    ///
    /// Carrega a placa porque é por ela que se reconhece o carro, e o preço de compra porque é
    /// o que diz, numa olhada, se o engano foi grande.
    /// </summary>
    /// <param name="Code">Identificador público do veículo.</param>
    /// <param name="Plate">A placa, como foi cadastrada.</param>
    /// <param name="Brand">A marca.</param>
    /// <param name="Model">O modelo.</param>
    /// <param name="Version">A versão, quando cadastrada.</param>
    /// <param name="ModelYear">O ano do modelo.</param>
    /// <param name="PurchasePrice">Quanto ele custou na entrada.</param>
    /// <param name="DeletedAt">Quando ele foi apagado.</param>
    /// <param name="DeletedByCode">Quem apagou, como as tabelas guardam: o código do usuário.</param>
    public sealed record DeletedVehicle(
        Guid Code,
        string Plate,
        string Brand,
        string Model,
        string? Version,
        int ModelYear,
        decimal? PurchasePrice,
        DateTime? DeletedAt,
        string? DeletedByCode);

    /// <summary>
    /// Um gasto que foi apagado da ficha de um carro (M23).
    ///
    /// Traz o carro a que ele pertence, e se esse carro está no pátio: um gasto devolvido a um
    /// carro excluído voltaria invisível — ativo no banco e ausente de toda tela —, então a
    /// tela precisa mostrar a ordem antes do clique, e a volta precisa recusar.
    /// </summary>
    /// <param name="Code">Identificador público do gasto.</param>
    /// <param name="Description">O que foi gasto.</param>
    /// <param name="TypeName">O tipo sob o qual ele foi lançado, quando o tipo continua ativo.</param>
    /// <param name="Amount">Quanto.</param>
    /// <param name="Date">Quando.</param>
    /// <param name="IsPaid">Pago, ou apenas previsto.</param>
    /// <param name="DeletedAt">Quando ele foi apagado.</param>
    /// <param name="DeletedByCode">Quem apagou, como as tabelas guardam: o código do usuário.</param>
    /// <param name="VehicleCode">Identificador público do carro, para a tela abrir a ficha.</param>
    /// <param name="Plate">A placa do carro.</param>
    /// <param name="Brand">A marca do carro.</param>
    /// <param name="Model">O modelo do carro.</param>
    /// <param name="VehicleIsActive">Se o carro está no pátio, e não também na lixeira.</param>
    public sealed record DeletedVehicleExpense(
        Guid Code,
        string Description,
        string? TypeName,
        decimal Amount,
        DateOnly Date,
        bool IsPaid,
        DateTime? DeletedAt,
        string? DeletedByCode,
        Guid VehicleCode,
        string Plate,
        string Brand,
        string Model,
        bool VehicleIsActive);
}
