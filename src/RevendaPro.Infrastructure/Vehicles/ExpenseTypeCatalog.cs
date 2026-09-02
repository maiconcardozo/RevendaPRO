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
        /// <summary>Name and keywords of each initial type, in the order they appear.</summary>
        public static readonly (string Name, string Keywords)[] Initial =
        [
            ("Peças",
                "peça, peca, lanterna, farol, parachoque, para-choque, paralama, amortecedor, "
                + "filtro, lampada, correia, moldura, pisca, ressonador, banco, retrovisor, "
                + "vidro, bateria, escapamento, radiador, embreagem, disco, pastilha"),

            ("Mecânica",
                "mecanica, motor, cambio, suspensao, freio, injecao, revisao"),

            ("Elétrica",
                "eletrica, eletricista, chicote, alternador, modulo, sensor"),

            ("Funilaria e pintura",
                "lata, lataria, funilaria, pintura, massa, polimento de risco, amassado"),

            ("Estética",
                "polimento, higienizacao, lavagem, cristalizacao, martelinho, limpeza"),

            ("Pneus",
                "pneu, roda, calota"),

            ("Alinhamento",
                "alinhamento, balanceamento, cambagem, geometria"),

            ("Mão de obra",
                "mao de obra, servico, instalacao, montagem"),

            ("Frete",
                "frete, guincho, transporte, reboque, patio"),

            ("Documentação",
                "documentacao, documento, transferencia, licenciamento, ipva, crlv, vistoria"),

            ("Despachante",
                "despachante"),

            ("Taxas",
                "taxa, multa, leilao, comissao, patio do leilao"),

            ("Outros", "")
        ];
    }
}
