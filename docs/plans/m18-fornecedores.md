# Plano — M18: Fornecedores, e quanto já foi para cada um

Fonte: a conversa com o stakeholder em 7 de setembro de 2026.

> *"Preciso fazer a implementação de fornecedor e quanto você já gastou em cada fornecedor. Vai
> ser oficina, pintura, autopeças, essas coisas. Precisa vincular aos gastos. E no dash preciso de
> um painel onde eu vou ver quais são os fornecedores que eu mais gastei, e ter um espaço
> exclusivo também para ver essa questão do gasto."*

## O que a entrega precisa provar

> A revenda cadastra a **Auto Mecânica Silva**, a **Funilaria do Zé** e a **Autopeças Central**.
> Cada gasto de carro diz **de quem** foi comprado, além de **o que** foi. O painel responde
> *"com quem eu mais gastei este mês"* num olhar, e a tela **Fornecedores** responde *"quanto já
> foi para a Funilaria do Zé, em que carros, e em quê"* — sem que ninguém precise somar planilha.

## O terreno

| Peça | Como está hoje |
|---|---|
| Fornecedor | **Não existe.** O gasto tem um campo de observações, e o comentário dele diz literalmente: *"keeps the supplier on record without a supplier table"*. Texto livre, redigitado a cada gasto, impossível de somar |
| Tipo de gasto | Existe, é cadastro por revenda (Peças, Mecânica, Funilaria e pintura, Estética...), tem tela própria e sugestão por palavra. Responde **o que** foi comprado, e jamais **de quem** |
| Fornecedor da compra do carro | `SupplierName` no veículo: texto livre para leilão ou particular de quem o carro foi comprado. Outro papel, e outro marco |
| Painel | Soma capital parado, lucro e vendas por período. Nada agrupa gasto por origem |
| Semeador | Vinte carros e cerca de trinta gastos, cada um com tipo, valor e data. Nenhum diz de quem |

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | as sete decisões abaixo estão tomadas por escrito | — |
| **V1** | O cadastro | Entidade `Supplier`, migration, repositório, casos de uso, endpoints e a tela **Fornecedores** com permissão própria | a revenda cadastra a Auto Mecânica Silva com ramo *Oficina* e telefone, edita, e o fornecedor em uso recusa exclusão dizendo quantos gastos apontam para ele | — |
| **V2** | O gasto aponta para o fornecedor | Coluna `IdSupplier` no gasto, a escolha no formulário do gasto, o nome na lista de gastos da ficha do carro | registrar "Troca de embreagem" escolhendo a Auto Mecânica Silva, e a linha do gasto mostra o tipo **e** o fornecedor | V1 |
| **V3** | A leitura | A consulta agregada por fornecedor, o painel **Por fornecedor** no dashboard e a **ficha do fornecedor** na tela Fornecedores | o dashboard mostra os cinco com quem mais se gastou no período, e a ficha da Funilaria do Zé lista total, quebra por tipo e cada gasto com a placa do carro | V2 |
| **V4** | O pátio de demonstração | Fornecedores fake no semeador, cada gasto fake apontando para um deles, e o preenchimento dos gastos que já existem no banco | subir o sistema com o semeador ligado mostra o ranking com números que fazem sentido, sem apagar o banco | V3 |
| **V5** | Fechamento | Suíte verde, `MARCOS.md`, `ROADMAP.md`, `mappings.md`, `endpoints.md` e o manual atualizados | `dotnet test`, `npm run build` e `docker compose up --build` passam | V1–V4 |

## Decisões (V0)

**1. Fornecedor é cadastro próprio, com o ramo dentro — e jamais um tipo de gasto.**

Tipo de gasto diz **o que** foi comprado; fornecedor diz **de quem**. São perguntas diferentes,
e a mesma oficina responde a várias da primeira: a Auto Mecânica Silva cobra Mecânica num carro e
Peças no outro. Juntar as duas coisas obrigaria a escolher entre saber o que se gastou e saber
com quem.

O fornecedor carrega um **ramo** (`SupplierSegment`): oficina mecânica, funilaria e pintura,
autopeças, despachante. O ramo é **cadastro da revenda**, e não enum — a versão inicial deste
plano propunha um enum, e o stakeholder pediu cadastro no mesmo dia: *"o ramo precisa ser um
cadastro também, mas já coloque bastante para não precisar ficar cadastrando"*. A razão é a
mesma do tipo de gasto: o ramo que falta só aparece no uso, e uma lista fixa mandaria a
estofaria e o chaveiro para "Outros". A revenda nasce com **25 ramos** prontos, do
`SupplierSegmentCatalog`, e administra a lista de dentro da própria tela Fornecedores — uma
tela "Ramos" seria uma permissão a mais para uma coisa só.

Quem classifica dinheiro continua sendo o tipo de gasto. O ramo serve para ler e agrupar a lista
de fornecedores.

Os campos do fornecedor: **nome** (obrigatório, único na revenda), **ramo**, **contato**,
**telefone**, **CNPJ ou CPF** (opcional) e **observações**. Seis campos, e nenhum a mais: a regra
RNF-02 diz que o cadastro tem de ganhar da planilha.

**2. O gasto aponta para um fornecedor, e o fornecedor é opcional.**

Uma coluna `IdSupplier` no gasto, que pode ficar vazia. IPVA, multa e taxa de leilão são gastos
sem fornecedor, e obrigar a escolher um criaria o fornecedor "Governo" em toda revenda. A chave
estrangeira é **restritiva**, igual à do tipo de gasto: apagar um fornecedor jamais apaga gasto.

O campo de observações continua livre. Ele guardava o fornecedor por falta de lugar; agora
guarda o que continua sem coluna — garantia, quem indicou, número da nota.

**3. Fornecedor em uso recusa exclusão, e diz quantos.**

A mesma regra do tipo de gasto e do pátio: *"Este fornecedor está em 12 gastos."* Exclusão é
lógica, como toda exclusão do sistema. Excluir um fornecedor com histórico apagaria a resposta
para a pergunta que este marco existe para responder.

**4. "Quanto gastei" é o que foi pago. O previsto aparece à parte.**

O gasto tem `IsPaid` desde o M6, e o custo real do carro só soma o que foi pago (RF-11). O
ranking e o total por fornecedor seguem a mesma regra — misturar orçamento com pagamento faria o
ranking subir por uma cotação que talvez nunca vire serviço. A ficha do fornecedor mostra o
**previsto** numa linha própria, porque saber que há dois mil reais orçados com a funilaria
também interessa.

**5. A soma é feita pelo banco, e nunca em memória.**

O painel quer cinco linhas e um total; a ficha quer um fornecedor. Isso é uma consulta com
`GROUP BY`, e não a lista inteira de gastos da revenda carregada para somar no servidor — que é
justamente o que a listagem recusa desde o M6. A consulta passa pelo veículo para chegar à
revenda, porque o gasto não carrega `IdTenant` de propósito (o isolamento vem do carro).

**6. O painel segue o período do dashboard. A tela Fornecedores começa em "desde o início".**

São duas perguntas. O dashboard é a leitura do mês: *"com quem eu mais gastei este mês"*, e por
isso o bloco **Por fornecedor** obedece ao mesmo `de`/`até` das vendas, com os **cinco maiores**
em barra, o valor e a quantidade de gastos de cada um, e uma linha *"e mais N fornecedores"*
somando o resto — para que o total continue sendo o total, como o M14 exigiu.

A tela Fornecedores é o espaço exclusivo que o stakeholder pediu, e a pergunta dela é
acumulada: *"quanto já foi para a Funilaria do Zé"*. Por isso ela abre **sem período**, com o
mesmo filtro disponível para quem quiser o trimestre. Cada card mostra o **total pago**, a
**quantidade de gastos** e o **último gasto**. Clicar abre a **ficha**: total pago, previsto,
quebra por tipo de gasto e cada gasto com data, descrição, valor e a **placa** do carro, que leva
à ficha do veículo.

Cadastro e leitura moram na mesma tela, e não em duas. Uma tela "Fornecedores" e outra "Gastos
por fornecedor" seriam duas permissões para uma coisa só, e quem cadastra o fornecedor é quem
quer saber quanto foi para ele.

**7. Quem registra gasto lê a lista de fornecedores; quem administra, edita.**

A tela `suppliers` entra em Administração, ao lado de Tipos de gasto e Pátios, e o sincronizador
a concede ao Administrador. Gestor e Financeiro a recebem no mapa inicial. A **leitura da lista**
é guardada pela tela `vehicles`, exatamente como o tipo de gasto: quem registra um gasto precisa
escolher o fornecedor, mesmo sem poder cadastrá-lo. Criar, editar, excluir e ver a ficha com os
valores exigem `suppliers`.

## O pátio de demonstração (V4)

O semeador ganha nove fornecedores, com nomes que não se confundem com os pátios que já existem:

| Fornecedor | Ramo | Recebe os gastos de |
|---|---|---|
| Auto Mecânica Silva | Oficina | a maior parte da Mecânica |
| Mecânica do Tião | Oficina | o resto da Mecânica, para o ranking ter dois iguais e um ganhar |
| Funilaria do Zé | Funilaria e pintura | Funilaria e pintura |
| Autopeças Central | Autopeças | Peças |
| Pneus Sul | Pneus | Pneus e Alinhamento |
| Estética Brilho Total | Estética | Estética |
| Elétrica Nunes | Elétrica | Elétrica |
| Despachante Rápido | Despachante | Documentação |
| Guincho 24h | Guincho | Frete |

Taxas ficam **sem fornecedor**, de propósito: o pátio de demonstração precisa provar a decisão 2
tanto quanto o ranking. O registro `DemoExpense` ganha o campo `Supplier`, e `DemoYardTests`
passa a conferir que todo fornecedor citado existe no catálogo e que ao menos um gasto vem sem.

O semeador é idempotente por placa, então os vinte carros que já estão no banco de quem usa o
sistema **não** seriam tocados. O V4 preenche o fornecedor dos gastos de demonstração que já
existem, casando placa, descrição e tipo com o catálogo — para que ninguém precise apagar o
banco para ver o painel funcionando. Só gastos dos carros `DEM1A01`–`DEM1A20` e só os que estão
sem fornecedor.

## O que muda em cada camada

| Camada | Novo | Alterado |
|---|---|---|
| Domínio | `Supplier`, `SupplierSegment`, `ISupplierRepository`, `ISupplierSegmentRepository` | `VehicleExpense` ganha `IdSupplier` em `Create` e `Update`; `IUnitOfWork` |
| Aplicação | `Suppliers/SuppliersUseCases.cs` e `SupplierHandlers.cs` (listar com totais, salvar, excluir, ficha) | DTO e comando do gasto ganham `SupplierCode`/`SupplierName`; dashboard ganha `BySupplier` e `SuppliersTotal` |
| Infra | `SupplierMap`, `SupplierSegmentMap`, `SupplierSegmentCatalog`, migration `Suppliers`, `SupplierQueries` (lista, por código, contagem em uso, **soma por fornecedor**, **ficha**), `SupplierRepository`, `SupplierSegmentRepository`, linha no `ScreenCatalog` | `VehicleExpenseMap` (FK restritiva), `DbContext`, registro de repositório, `DemoYard`, `DbInitializer` |
| API | `SuppliersController`: `GET api/suppliers`, `POST`, `PUT {code}`, `DELETE {code}`, `GET api/suppliers/{code}/expenses`; `SupplierSegmentsController` com o CRUD do ramo | gasto aceita `supplierCode` |
| Frontend | `app/(panel)/suppliers/page.tsx`, `components/suppliers/SuppliersView.tsx` (cards, formulário, ficha) e `SegmentsModal.tsx` (o cadastro de ramos) | `types.ts`, ícone `Store` no `PanelShell`, `Select` de fornecedor no `ExpensesPanel`, bloco **Por fornecedor** no `DashboardView` |
| Testes | `Unit/SupplierTests.cs` com um `World`; `Unit/DashboardSupplierTests.cs` | `DemoYardTests`, `TenantIsolationTests` (a ficha e o ranking jamais trazem gasto da outra revenda), matriz de permissão por descoberta |
| Docs | este plano | `mappings.md`, `endpoints.md` (`## Fornecedores` e a coluna nova em `## Gastos`), manual (`### Fornecedores`), `MARCOS.md`, `ROADMAP.md` |

## O que a implementação acrescentou ao plano

- **O ramo virou cadastro** (decisão 1, revista no mesmo dia). Entrou `SupplierSegment`, com
  25 ramos semeados por revenda, o CRUD em `api/supplier-segments` e o modal **Ramos** dentro
  da tela Fornecedores. Ramo com fornecedor dentro recusa exclusão, como tudo o mais.
- **Duas listas, e não uma.** `GET api/suppliers` (tela `vehicles`) continua sem dinheiro, para
  quem só escolhe o fornecedor num gasto; `GET api/suppliers/spending` (tela `suppliers`) é a
  que traz os valores. O plano dizia "ver a ficha com os valores exige `suppliers`", e uma lista
  só com totais teria vazado o valor para quem não deveria ler.
- **A quebra por tipo sai das linhas da ficha**, e não de uma segunda consulta: é um fornecedor
  só, e somar de novo no banco seria pedir duas vezes a mesma coisa. A decisão 5 vale para o
  ranking, que é onde a lista inteira de gastos seria carregada à toa.
- **As linhas do Dapper são classes com propriedades**, e não records posicionais: o driver
  entrega `COUNT` como `Int64` e a flag como o que a versão dele quiser, e o mapeamento por nome
  converte em vez de exigir o tipo exato. É a lição do M10 e do M11, aprendida pelo outro lado.
- **O pátio de demonstração ganhou cinco gastos**: três desta semana, para o painel abrir com o
  bloco preenchido no mês corrente (o padrão do dashboard é o mês, e todos os gastos antigos
  cairiam fora dele); uma taxa de leilão **sem** fornecedor; e um guincho **com**, no ramo que
  faltava. O ranking também mostra a Mecânica dividida entre duas oficinas.
- **A tela Fornecedores ordena pelo pago**, do maior para o menor, e mostra o total do período
  em cima. O plano dizia "cada card mostra o total"; a ordem nasceu ao ver vinte cards iguais.
- **O gasto ganhou `AssignSupplier`**, um método só para o semeador preencher o fornecedor dos
  gastos antigos sem passar por `Update` com todos os campos. A tela continua indo pelo `Update`.

## O que fica de fora deste marco

- **O fornecedor da compra do carro.** `Vehicle.SupplierName` continua texto livre. É de quem o
  carro foi comprado — leilão, particular, outra loja —, um papel diferente de quem presta
  serviço no carro. Virar cadastro é decisão própria, e talvez o cadastro certo seja outro.
- **Sugerir o fornecedor pelo tipo.** A ideia é boa: escolheu Mecânica, o sistema oferece a
  oficina de sempre. Mas ela pede a memória do "de sempre", que só existe depois de meses de uso.
  Fica para quando houver histórico de verdade para sugerir.
- **Contas a pagar por fornecedor.** Vencimento, parcela e boleto são um módulo financeiro, e o
  sistema hoje só sabe pago ou previsto. O previsto na ficha é o máximo que este marco promete.
- **Relatório exportável.** A ficha na tela responde à pergunta; planilha e PDF entram no dia em
  que alguém precisar mandar isso para o contador.
