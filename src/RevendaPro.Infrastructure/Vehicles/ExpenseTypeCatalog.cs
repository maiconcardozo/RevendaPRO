using RevendaPro.Domain.Enums;

namespace RevendaPro.Infrastructure.Vehicles
{
    /// <summary>
    /// The types of expense a dealership starts with (RF-09).
    ///
    /// Nobody registers fourteen types before entering the first expense, so a new tenant is
    /// born with the list ready. From there it belongs to the dealership: it edits, adds and
    /// reorders as its own work demands.
    ///
    /// The names are in Portuguese because they are read on screen, the same rule that applies
    /// to a menu label or a system role. The keywords are what the suggestion matches against,
    /// and they are drawn from the real spending sheet the business keeps today.
    /// </summary>
    public static class ExpenseTypeCatalog
    {
        /// <summary>
        /// Name, keywords and scope of each initial type, in the order they appear.
        ///
        /// O escopo chegou no M22, com a despesa da loja: os treze tipos que já existiam são de
        /// carro, porque é o que eles são, e quatro tipos de loja entram no fim — o que toda
        /// revenda paga todo mês, e que jamais pertence a um carro.
        /// </summary>
        public static readonly (string Name, string Keywords, ExpenseScope Scope)[] Initial =
        [
            ("Peças",
                "peça, peca, lanterna, farol, parachoque, para-choque, paralama, amortecedor, "
                + "filtro, lampada, correia, moldura, pisca, ressonador, banco, retrovisor, "
                + "vidro, bateria, escapamento, radiador, embreagem, disco, pastilha", ExpenseScope.Vehicle),

            ("Mecânica",
                "mecanica, motor, cambio, suspensao, freio, injecao, revisao", ExpenseScope.Vehicle),

            ("Elétrica",
                "eletrica, eletricista, chicote, alternador, modulo, sensor", ExpenseScope.Vehicle),

            ("Funilaria e pintura",
                "lata, lataria, funilaria, pintura, massa, polimento de risco, amassado", ExpenseScope.Vehicle),

            ("Estética",
                "polimento, higienizacao, lavagem, cristalizacao, martelinho, limpeza", ExpenseScope.Vehicle),

            ("Pneus",
                "pneu, roda, calota", ExpenseScope.Vehicle),

            ("Alinhamento",
                "alinhamento, balanceamento, cambagem, geometria", ExpenseScope.Vehicle),

            ("Mão de obra",
                "mao de obra, servico, instalacao, montagem", ExpenseScope.Vehicle),

            ("Frete",
                "frete, guincho, transporte, reboque, patio", ExpenseScope.Vehicle),

            ("Documentação",
                "documentacao, documento, transferencia, licenciamento, ipva, crlv, vistoria", ExpenseScope.Vehicle),

            ("Despachante",
                "despachante", ExpenseScope.Vehicle),

            ("Taxas",
                "taxa, multa, leilao, comissao, patio do leilao", ExpenseScope.Vehicle),

            ("Outros", "", ExpenseScope.Both),

            // O que a loja paga todo mês (M22).
            ("Aluguel", "aluguel, locacao, imobiliaria, condominio", ExpenseScope.Store),

            ("Energia e água", "energia, luz, agua, saneamento, esgoto", ExpenseScope.Store),

            ("Salários e encargos", "salario, ordenado, folha, fgts, inss, ferias, decimo, vale",
                ExpenseScope.Store),

            ("Impostos e contador", "imposto, simples, das, contador, contabilidade, alvara, iss",
                ExpenseScope.Store)
        ];
    }
}
