using RevendaPro.Domain.Enums;

namespace RevendaPro.Infrastructure.Database
{
    /// <summary>
    /// Um lugar onde os carros de demonstração ficam.
    /// </summary>
    /// <param name="Name">O nome, como aparece na tela.</param>
    /// <param name="Kind">Pátio da casa, ou loja de parceiro.</param>
    /// <param name="Position">A ordem na lista.</param>
    internal sealed record DemoPlace(string Name, YardKind Kind, int Position);

    /// <summary>Um fornecedor de demonstração: de quem os carros compram serviço e peça.</summary>
    /// <param name="Name">O nome, como aparece na tela.</param>
    /// <param name="Segment">O ramo, pelo nome do catálogo.</param>
    internal sealed record DemoSupplier(string Name, string Segment);

    /// <summary>Um gasto lançado no carro de demonstração.</summary>
    /// <param name="Description">O que foi feito.</param>
    /// <param name="Type">O tipo de gasto, pelo nome do catálogo.</param>
    /// <param name="Amount">Quanto custou.</param>
    /// <param name="DaysAgo">Há quantos dias.</param>
    /// <param name="Supplier">
    /// De quem foi comprado, pelo nome de <see cref="DemoYard.Suppliers"/>. Nulo de propósito
    /// em taxa e multa: o pátio de demonstração precisa provar que o fornecedor é opcional tanto
    /// quanto precisa provar o ranking.
    /// </param>
    /// <param name="DueInDays">
    /// Daqui a quantos dias vence (M22). Nulo é pago no dia do lançamento, que é o caso da
    /// maioria; negativo é uma conta que já venceu, e o pátio precisa de uma para o caixa ter o
    /// vermelho que ele existe para mostrar.
    /// </param>
    internal sealed record DemoExpense(
        string Description,
        string Type,
        decimal Amount,
        int DaysAgo,
        string? Supplier = null,
        int? DueInDays = null);

    /// <summary>
    /// Uma despesa da loja na demonstração (M22): o que a revenda paga todo mês e que jamais
    /// pertence a um carro.
    /// </summary>
    /// <param name="Description">O que é.</param>
    /// <param name="Type">O tipo, pelo nome do catálogo.</param>
    /// <param name="Amount">Quanto.</param>
    /// <param name="DayOfMonth">Em que dia do mês ela cai.</param>
    /// <param name="DueInDays">Daqui a quantos dias vence. Nulo é já paga.</param>
    internal sealed record DemoStoreExpense(
        string Description,
        string Type,
        decimal Amount,
        int DayOfMonth,
        int? DueInDays = null);

    /// <summary>Um cliente da demonstração (M21): quem comprou ou ofereceu por um carro.</summary>
    /// <param name="Name">Nome.</param>
    /// <param name="Phone">Telefone, só dígitos. É por onde o aproveitamento o reconhece.</param>
    /// <param name="Document">CPF, quando a venda o pediu.</param>
    internal sealed record DemoCustomer(string Name, string Phone, string? Document = null);

    /// <summary>Uma proposta num carro de demonstração (M21).</summary>
    /// <param name="Customer">Quem ofereceu, pelo nome do cliente.</param>
    /// <param name="Amount">Quanto.</param>
    /// <param name="DaysAgo">Há quantos dias.</param>
    /// <param name="Declined">Se a loja recusou.</param>
    internal sealed record DemoProposal(string Customer, decimal Amount, int DaysAgo, bool Declined = false);

    /// <summary>A venda de um carro de demonstração.</summary>
    /// <param name="Amount">Por quanto saiu.</param>
    /// <param name="DaysAgo">Há quantos dias.</param>
    /// <param name="Buyer">Quem comprou.</param>
    /// <param name="Commission">A comissão paga.</param>
    /// <param name="PartnerCutPercent">O repasse da loja parceira, quando a venda saiu por ela.</param>
    /// <param name="Financed">
    /// Se o comprador financiou (M22): o banco paga depois, e a venda nasce com saldo a receber.
    /// O pátio precisa de uma assim para o caixa ter o que mostrar do outro lado.
    /// </param>
    internal sealed record DemoSale(
        decimal Amount,
        int DaysAgo,
        string Buyer,
        decimal Commission,
        decimal? PartnerCutPercent = null,
        bool Financed = false);

    /// <summary>
    /// Um carro do pátio de demonstração.
    /// </summary>
    /// <param name="Plate">Placa.</param>
    /// <param name="Chassis">Chassi.</param>
    /// <param name="Brand">Marca.</param>
    /// <param name="Model">Modelo, como alguém digitaria no cadastro.</param>
    /// <param name="Version">
    /// A versão — <b>ou a ausência dela</b>, que é o caso mais comum do pátio de verdade e o que
    /// faz a busca da tabela devolver dez, quinze, vinte candidatos.
    /// </param>
    /// <param name="ModelYear">Ano do modelo.</param>
    /// <param name="Fuel">Combustível.</param>
    /// <param name="Transmission">Câmbio.</param>
    /// <param name="Color">Cor.</param>
    /// <param name="Mileage">Quilometragem.</param>
    /// <param name="PurchasePrice">Quanto a revenda pagou.</param>
    /// <param name="BoughtDaysAgo">Há quantos dias ele entrou.</param>
    /// <param name="Supplier">De quem veio.</param>
    /// <param name="Place">Onde ele está, pelo nome do pátio.</param>
    /// <param name="Status">Onde ele parou na esteira.</param>
    /// <param name="Expenses">O que foi gasto nele.</param>
    /// <param name="Sale">A venda, quando ele já saiu.</param>
    /// <param name="Proposals">As propostas que ele recebeu e ainda estão no registro.</param>
    internal sealed record DemoCar(
        string Plate,
        string Chassis,
        string Brand,
        string Model,
        string? Version,
        short ModelYear,
        FuelType Fuel,
        TransmissionType Transmission,
        string Color,
        int Mileage,
        decimal PurchasePrice,
        int BoughtDaysAgo,
        string Supplier,
        string Place,
        VehicleStatus Status,
        DemoExpense[] Expenses,
        DemoSale? Sale = null,
        DemoProposal[]? Proposals = null);

    /// <summary>
    /// O pátio de demonstração: quatro lugares e vinte carros.
    ///
    /// <b>Escolhido, e jamais sorteado.</b> Ele existe para que quatro coisas apareçam juntas na
    /// primeira subida de uma máquina de desenvolvimento:
    ///
    /// <list type="bullet">
    /// <item>a busca da tabela que chega a <b>um</b> candidato — nome e versão que a FIPE escreve
    /// de um jeito só, como <c>Corolla 2.0 XEi</c>;</item>
    /// <item>a busca que chega a <b>dez, quinze, vinte</b> — o carro cadastrado com pressa, só
    /// <c>Gol</c> ou <c>Uno</c>, que é o que o vendedor digita de verdade e onde o medidor de
    /// acurácia mostra a lista inteira empatada em 50%;</item>
    /// <item><b>lucro e prejuízo</b>, com o reparo entrando na conta — dois carros deste pátio
    /// saíram por menos do que custaram, e o custo somado explica por quê;</item>
    /// <item><b>pátios variados</b>: a loja, a oficina do parceiro, o repasse e o consignado;</item>
    /// <item><b>fornecedores</b> (M18): nove, um por ramo, com a Mecânica dividida entre duas
    /// oficinas para o ranking ter dois iguais e um ganhar; taxa de leilão sem fornecedor, de
    /// propósito; e alguns gastos desta semana, para o painel abrir com o bloco preenchido.</item>
    /// </list>
    ///
    /// Placas e chassis são de mentira por construção: <c>DEM…</c> e <c>9DEM…</c>. Os nomes de
    /// fornecedor e de comprador também, e os telefones dos clientes começam em 9999.
    /// </summary>
    internal static class DemoYard
    {
        private const string Silva = "Auto Mecânica Silva";
        private const string Tiao = "Mecânica do Tião";
        private const string Ze = "Funilaria do Zé";
        private const string Central = "Autopeças Central";
        private const string PneusSul = "Pneus Sul";
        private const string Brilho = "Estética Brilho Total";
        private const string Nunes = "Elétrica Nunes";
        private const string Rapido = "Despachante Rápido";
        private const string Guincho = "Guincho 24h";

        /// <summary>
        /// Os nove fornecedores, com nomes que jamais se confundem com os pátios: a "Oficina do
        /// Baiano" é um lugar onde carro fica, e não alguém que a revenda paga.
        /// </summary>
        /// <summary>
        /// Os clientes (M21): os seis compradores e três pessoas que ofereceram e ainda estão
        /// por aí. O Marcos comprou uma Hilux e ofereceu pelo Creta, para a ficha dele ter uma
        /// compra e uma proposta juntas — que é o que a tela Clientes existe para mostrar.
        /// </summary>
        public static readonly DemoCustomer[] Customers =
        [
            new("Marcos Vinícius Prado", "51999990001", "39053344705"),
            new("Eduardo Sampaio", "51999990002"),
            new("Jonas Ferreira", "51999990003", "12345678909"),
            new("Rita Camargo", "51999990004"),
            new("Patrícia Lemos", "51999990005", "11144477735"),
            new("Ubirajara Pinto", "51999990006"),
            new("Helena Duarte", "51999990007"),
            new("Cristiano Bueno", "51999990008"),
            new("Lúcia Farias", "51999990009"),
        ];

        /// <summary>
        /// O que a loja paga (M22): o mês passado inteiro pago, e o mês corrente com o aluguel
        /// vencendo e a energia já vencida. É o que faz o caixa abrir contando uma história.
        /// </summary>
        public static readonly DemoStoreExpense[] StoreExpenses =
        [
            new("Aluguel do mês", "Aluguel", 4_800m, 10, DueInDays: 5),
            new("Energia elétrica", "Energia e água", 890m, 5, DueInDays: -3),
            new("Água", "Energia e água", 180m, 5),
            new("Salários da equipe", "Salários e encargos", 12_400m, 5),
            new("Simples Nacional", "Impostos e contador", 3_150m, 20, DueInDays: 12),
            new("Honorários do contador", "Impostos e contador", 890m, 10),
        ];

        public static readonly DemoSupplier[] Suppliers =
        [
            new(Silva, "Oficina mecânica"),
            new(Tiao, "Oficina mecânica"),
            new(Ze, "Funilaria e pintura"),
            new(Central, "Autopeças"),
            new(PneusSul, "Pneus e rodas"),
            new(Brilho, "Estética e polimento"),
            new(Nunes, "Elétrica automotiva"),
            new(Rapido, "Despachante"),
            new(Guincho, "Guincho e transporte"),
        ];

        /// <summary>Os quatro lugares onde estes carros estão.</summary>
        public static readonly DemoPlace[] Places =
        [
            new("Pátio da loja", YardKind.Own, 0),
            new("Oficina do Baiano", YardKind.Partner, 1),
            new("Repasse Zona Sul", YardKind.Partner, 2),
            new("Consignado Vila Rica", YardKind.Partner, 3),
        ];

        /// <summary>Os vinte carros.</summary>
        public static readonly DemoCar[] Cars =
        [
            // ── A tabela responde com UM candidato ────────────────────────────────────────
            // Nome e versão que a FIPE escreve de um jeito só. É o caso em que o pop-up abre
            // com uma linha, nota cheia, e a pessoa confirma o que já estava certo.
            new("DEM1A01", "9DEMTYT2020000001", "Toyota", "Corolla", "2.0 XEi", 2020,
                FuelType.Flex, TransmissionType.Automatic, "Prata", 48_300, 92_000m, 214,
                "Leilão Bandeirantes", "Pátio da loja", VehicleStatus.Sold,
                [
                    new("Higienização interna e polimento", "Estética", 850m, 205, Brilho),
                    new("Transferência e licenciamento", "Documentação", 1_200m, 200, Rapido),
                ],
                new(104_900m, 168, "Marcos Vinícius Prado", 1_500m)),

            new("DEM1A02", "9DEMJEP2020000002", "Jeep", "Renegade", "1.8 Longitude", 2020,
                FuelType.Flex, TransmissionType.Automatic, "Branco", 61_400, 86_500m, 96,
                "Particular — Rogério Maia", "Pátio da loja", VehicleStatus.Advertised,
                [
                    new("Jogo de pneus", "Pneus", 3_400m, 88, PneusSul),
                    new("Revisão completa", "Mecânica", 1_950m, 85, Tiao),
                ]),

            new("DEM1A03", "9DEMHND2019000003", "Honda", "Civic", "2.0 EXL", 2019,
                FuelType.Flex, TransmissionType.Automatic, "Cinza", 72_900, 98_000m, 187,
                "Particular — Cláudia Nunes", "Consignado Vila Rica", VehicleStatus.Sold,
                [
                    new("Troca de embreagem", "Mecânica", 2_400m, 176, Silva),
                    new("Jogo de pneus", "Pneus", 3_200m, 174, PneusSul),
                ],
                new(112_000m, 141, "Eduardo Sampaio", 1_800m, PartnerCutPercent: 3m)),

            new("DEM1A04", "9DEMHYU2021000004", "Hyundai", "Creta", "1.6 Pulse Plus", 2021,
                FuelType.Flex, TransmissionType.Automatic, "Preto", 39_700, 88_900m, 54,
                "Leilão Bandeirantes", "Pátio da loja", VehicleStatus.ReadyForSale,
                [
                    new("Martelinho de ouro na porta traseira", "Estética", 640m, 47, Brilho),
                    new("Polimento técnico", "Estética", 420m, 3, Brilho),
                    // Vencida há quatro dias: é o vermelho do caixa (M22).
                    new("Revisão dos 40 mil", "Mecânica", 1_180m, 6, Silva, DueInDays: -4),
                ],
                Proposals:
                [
                    new("Marcos Vinícius Prado", 96_000m, 9),
                    new("Helena Duarte", 92_500m, 20, Declined: true),
                ]),

            // ── A tabela responde com POUCOS: duas a quatro versões ───────────────────────
            // A versão está escrita, e ainda assim a tabela guarda mais de uma linha com ela.
            // Dois preços diferentes para o mesmo carro é exatamente o que o M16 recusa
            // decidir sozinho.
            new("DEM1A05", "9DEMCHV2020000005", "Chevrolet", "Onix", "1.4 LT", 2020,
                FuelType.Flex, TransmissionType.Manual, "Branco", 55_100, 58_400m, 71,
                "Particular — Tiago Bastos", "Pátio da loja", VehicleStatus.InRepair,
                [
                    new("Retífica do cabeçote", "Mecânica", 3_900m, 60, Silva),
                    new("Kit de embreagem", "Peças", 1_450m, 58, Central),
                ]),

            new("DEM1A06", "9DEMRNL2018000006", "Renault", "Sandero", "1.0 Expression", 2018,
                FuelType.Flex, TransmissionType.Manual, "Vermelho", 88_600, 39_800m, 133,
                "Repasse Zona Sul", "Repasse Zona Sul", VehicleStatus.Advertised,
                [
                    new("Alinhamento e balanceamento", "Alinhamento", 320m, 120, PneusSul),
                ]),

            new("DEM1A07", "9DEMNSS2019000007", "Nissan", "Kicks", "1.6 SV", 2019,
                FuelType.Flex, TransmissionType.Automatic, "Cinza", 64_200, 71_500m, 78,
                "Particular — Helena Prado", "Pátio da loja", VehicleStatus.Negotiating,
                [
                    new("Revisão dos freios", "Mecânica", 1_180m, 70, Tiao),
                    new("Documentação e vistoria", "Documentação", 890m, 66, Rapido),
                ]),

            new("DEM1A08", "9DEMFRD2019000008", "Ford", "Ka", "1.0 SE", 2019,
                FuelType.Flex, TransmissionType.Manual, "Prata", 76_400, 41_200m, 22,
                "Leilão Paulista", "Oficina do Baiano", VehicleStatus.Purchased,
                [
                    // Taxa sem fornecedor, e o guincho com: o fornecedor é opcional, e o pátio
                    // de demonstração prova isso tanto quanto prova o ranking.
                    new("Taxa do leilão", "Taxas", 650m, 21),
                    new("Guincho do leilão até a oficina", "Frete", 380m, 20, Guincho),
                ]),

            new("DEM1A09", "9DEMFTA2021000009", "Fiat", "Mobi", "1.0 Like", 2021,
                FuelType.Flex, TransmissionType.Manual, "Branco", 41_900, 43_600m, 44,
                "Particular — Sandra Vilela", "Pátio da loja", VehicleStatus.ReadyForSale,
                [
                    new("Higienização completa", "Estética", 480m, 38, Brilho),
                    new("Troca de óleo e filtros", "Mecânica", 390m, 4, Silva),
                    // Vence nesta semana e na que vem: é o "o que vence" do caixa (M22).
                    new("Jogo de pneus dianteiros", "Pneus", 1_640m, 2, PneusSul, DueInDays: 3),
                ],
                Proposals: [new("Cristiano Bueno", 46_000m, 5)]),

            // ── A tabela responde com MUITOS: dez, quinze, vinte ──────────────────────────
            // O carro cadastrado com pressa, só com o nome. É o caso mais comum do pátio de
            // verdade, e é aqui que o medidor de acurácia mostra a lista inteira empatada em
            // 50% — porque metade do carro segue por conferir, e recomendado nenhum aparece.
            new("DEM1A10", "9DEMVWG2015000010", "Volkswagen", "Gol", null, 2015,
                FuelType.Flex, TransmissionType.Manual, "Branco", 128_400, 31_500m, 246,
                "Leilão Paulista", "Pátio da loja", VehicleStatus.Sold,
                [
                    new("Troca do câmbio", "Mecânica", 3_800m, 232, Silva),
                    new("Funilaria da lateral direita", "Funilaria e pintura", 2_600m, 228, Ze),
                ],
                // O carro que ensina a olhar o custo somado antes do preço da vitrine.
                new(34_000m, 191, "Jonas Ferreira", 500m)),

            new("DEM1A11", "9DEMFTA2014000011", "Fiat", "Uno", null, 2014,
                FuelType.Flex, TransmissionType.Manual, "Vermelho", 142_800, 24_900m, 63,
                "Particular — Adriano Melo", "Oficina do Baiano", VehicleStatus.InRepair,
                [
                    new("Suspensão dianteira completa", "Mecânica", 2_150m, 51, Silva),
                    new("Par de amortecedores", "Peças", 780m, 51, Central),
                ]),

            new("DEM1A12", "9DEMFTA2013000012", "Fiat", "Palio", null, 2013,
                FuelType.Flex, TransmissionType.Manual, "Prata", 156_300, 21_400m, 118,
                "Repasse Zona Sul", "Repasse Zona Sul", VehicleStatus.Advertised,
                [
                    new("Pintura do capô", "Funilaria e pintura", 1_400m, 104, Ze),
                ]),

            new("DEM1A13", "9DEMCHV2012000013", "Chevrolet", "Celta", null, 2012,
                FuelType.Flex, TransmissionType.Manual, "Preto", 171_500, 17_800m, 87,
                "Leilão Paulista", "Pátio da loja", VehicleStatus.ReadyForSale,
                [
                    new("Bateria nova", "Peças", 520m, 80, Central),
                    new("Alinhamento", "Alinhamento", 180m, 79, PneusSul),
                ]),

            new("DEM1A14", "9DEMCHV2010000014", "Chevrolet", "Corsa", null, 2010,
                FuelType.Flex, TransmissionType.Manual, "Azul", 198_700, 18_500m, 205,
                "Leilão Bandeirantes", "Repasse Zona Sul", VehicleStatus.Sold,
                [
                    new("Motor: junta do cabeçote", "Mecânica", 4_200m, 190, Silva),
                    new("Chicote elétrico", "Elétrica", 1_300m, 188, Nunes),
                ],
                // O segundo prejuízo, e por outro motivo: o reparo comeu a margem, e ainda
                // saiu pela loja parceira, que fica com a parte dela.
                new(21_000m, 152, "Rita Camargo", 400m, PartnerCutPercent: 5m)),

            new("DEM1A15", "9DEMFRD2014000015", "Ford", "Fiesta", null, 2014,
                FuelType.Flex, TransmissionType.Manual, "Prata", 134_900, 28_600m, 97,
                "Particular — Wesley Antunes", "Pátio da loja", VehicleStatus.Advertised,
                [
                    new("Revisão geral", "Mecânica", 1_320m, 90, Tiao),
                ]),

            new("DEM1A16", "9DEMVWG2016000016", "Volkswagen", "Fox", null, 2016,
                FuelType.Flex, TransmissionType.Manual, "Branco", 112_300, 33_900m, 58,
                "Particular — Núbia Castro", "Consignado Vila Rica", VehicleStatus.ReadyForSale,
                [
                    new("Higienização e cristalização", "Estética", 690m, 50, Brilho),
                    new("Retoque de pintura no para-choque", "Funilaria e pintura", 890m, 1, Ze, DueInDays: 9),
                ],
                Proposals: [new("Lúcia Farias", 36_000m, 12, Declined: true)]),

            new("DEM1A17", "9DEMHYU2017000017", "Hyundai", "HB20", null, 2017,
                FuelType.Flex, TransmissionType.Manual, "Cinza", 98_100, 38_400m, 41,
                "Repasse Zona Sul", "Pátio da loja", VehicleStatus.Negotiating,
                [
                    new("Par de pneus dianteiros", "Pneus", 1_180m, 33, PneusSul),
                    new("Alinhamento", "Alinhamento", 180m, 2, PneusSul),
                ]),

            new("DEM1A18", "9DEMRNL2016000018", "Renault", "Logan", null, 2016,
                FuelType.Flex, TransmissionType.Manual, "Bege", 121_600, 29_700m, 17,
                "Leilão Paulista", "Oficina do Baiano", VehicleStatus.Purchased, []),

            new("DEM1A19", "9DEMTYT2018000019", "Toyota", "Etios", null, 2018,
                FuelType.Flex, TransmissionType.Manual, "Branco", 89_400, 42_000m, 173,
                "Particular — Fábio Rezende", "Pátio da loja", VehicleStatus.Sold,
                [
                    new("Higienização interna", "Estética", 600m, 165, Brilho),
                ],
                // Financiada (M22): o banco ainda deve, e é o que o caixa mostra a receber.
                new(49_900m, 4, "Patrícia Lemos", 900m, Financed: true)),

            new("DEM1A20", "9DEMVWG2015000020", "Volkswagen", "Voyage", null, 2015,
                FuelType.Flex, TransmissionType.Manual, "Prata", 145_200, 33_000m, 159,
                "Repasse Zona Sul", "Repasse Zona Sul", VehicleStatus.Sold,
                [
                    new("Correia dentada e bomba d'água", "Mecânica", 1_900m, 148, Silva),
                ],
                new(39_500m, 112, "Ubirajara Pinto", 700m, PartnerCutPercent: 4m)),
        ];
    }
}
