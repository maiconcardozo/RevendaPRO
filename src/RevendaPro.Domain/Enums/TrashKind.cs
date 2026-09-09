namespace RevendaPro.Domain.Enums
{
    /// <summary>
    /// O que se procura na lixeira (M23).
    ///
    /// Quem apagou por engano tem uma pergunta só — "onde está o que eu apaguei?" — e uma tela
    /// por tipo seriam três lugares para procurar a mesma coisa. A lixeira é uma, com uma aba
    /// por tipo, e é este valor que diz em qual tabela bater: na leitura e, depois, na volta.
    /// </summary>
    public enum TrashKind
    {
        /// <summary>Um carro apagado. Devolvê-lo devolve a ficha inteira junto.</summary>
        Vehicle = 1,

        /// <summary>Um gasto apagado da ficha de um carro.</summary>
        Expense = 2,

        /// <summary>Um documento tirado da ficha de um carro, com o arquivo ainda no bucket.</summary>
        Document = 3
    }
}
