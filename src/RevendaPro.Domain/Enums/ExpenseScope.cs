namespace RevendaPro.Domain.Enums
{
    /// <summary>
    /// Para onde um tipo de gasto serve (M22): o carro, a loja, ou os dois.
    ///
    /// Nasceu quando a loja passou a lançar aluguel e energia. Um segundo catálogo de "tipos"
    /// seria a pessoa perguntando qual é qual a cada lançamento; um catálogo só, com escopo,
    /// deixa Mecânica fora da lista do aluguel e Aluguel fora da lista do carro, sem tirar de
    /// ninguém a liberdade de dizer que Frete serve para os dois.
    ///
    /// É <see cref="FlagsAttribute"/> porque <see cref="Both"/> é literalmente os dois: a
    /// pergunta que as telas fazem é "serve para carro?", e ela se responde com um E lógico em
    /// vez de uma lista de casos.
    /// </summary>
    [Flags]
    public enum ExpenseScope
    {
        /// <summary>O gasto de um carro: mecânica, funilaria, pneu, documentação.</summary>
        Vehicle = 1,

        /// <summary>A despesa da loja: aluguel, energia, salário, imposto.</summary>
        Store = 2,

        /// <summary>Serve nos dois lugares: frete, taxa bancária, material de limpeza.</summary>
        Both = Vehicle | Store
    }
}
