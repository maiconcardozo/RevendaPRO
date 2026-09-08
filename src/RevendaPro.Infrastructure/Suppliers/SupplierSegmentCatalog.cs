namespace RevendaPro.Infrastructure.Suppliers
{
    /// <summary>
    /// Os ramos de fornecedor com que uma revenda nasce.
    ///
    /// A lista é farta de propósito: o stakeholder pediu que ninguém precisasse cadastrar ramo
    /// antes de cadastrar o primeiro fornecedor. Ela cobre o que uma revenda de usados contrata
    /// de fato — do mecânico ao chaveiro, do despachante ao leilão. Dali em diante o cadastro é
    /// da revenda: ela renomeia, acrescenta e apaga o que sobrar.
    ///
    /// Os nomes ficam em português porque são lidos na tela, a mesma regra do tipo de gasto.
    /// </summary>
    public static class SupplierSegmentCatalog
    {
        /// <summary>Os ramos iniciais, na ordem em que aparecem.</summary>
        public static readonly string[] Initial =
        [
            "Oficina mecânica",
            "Funilaria e pintura",
            "Autopeças",
            "Elétrica automotiva",
            "Injeção eletrônica",
            "Câmbio automático",
            "Suspensão e freios",
            "Ar-condicionado",
            "Pneus e rodas",
            "Alinhamento e balanceamento",
            "Estética e polimento",
            "Lava-rápido",
            "Martelinho de ouro",
            "Vidros e para-brisas",
            "Estofaria e tapeçaria",
            "Som e multimídia",
            "Insulfilm e envelopamento",
            "Acessórios",
            "Chaveiro",
            "Despachante",
            "Vistoria e laudo",
            "Guincho e transporte",
            "Leilão",
            "Seguradora",
            "Outros"
        ];
    }
}
