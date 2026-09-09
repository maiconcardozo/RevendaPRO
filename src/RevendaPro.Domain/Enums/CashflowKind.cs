namespace RevendaPro.Domain.Enums
{
    /// <summary>
    /// De onde uma linha do caixa veio (M22).
    ///
    /// As três origens moram em tabelas diferentes, com donos e regras diferentes; no extrato
    /// elas viram linhas do mesmo tipo, e é este valor que diz de onde cada uma saiu — para a
    /// tela abrir o carro certo, e para a baixa bater na porta certa.
    /// </summary>
    public enum CashflowKind
    {
        /// <summary>Um gasto de um carro: mecânica, funilaria, documentação.</summary>
        VehicleExpense = 1,

        /// <summary>Uma despesa da loja: aluguel, energia, salário, imposto.</summary>
        StoreExpense = 2,

        /// <summary>O que ainda falta receber de uma venda.</summary>
        SaleReceivable = 3
    }
}
