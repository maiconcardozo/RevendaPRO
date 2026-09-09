# Plano — M22: Caixa — o que vence, o que entrou, o que atrasou

Fonte: a conversa com o stakeholder em 8 de setembro de 2026, ao escolher a sequência dos
próximos marcos.

> *"Vamos fazer 1, 2, 3, 4, 5, mas na sequência."* — e o 2 era: **contas a pagar e a receber**.
> A visão de caixa: o que vence esta semana, o que entrou, o que está atrasado. É o que o dono
> olha toda segunda.

## O que a entrega precisa provar

> É segunda de manhã. O Eduardo abre **Caixa** e vê, em uma tela: **R$ 8.400 a pagar esta
> semana** — a retífica da oficina do Silva vence quinta, o aluguel vence dia 10 —, **R$ 2.300
> atrasados** em vermelho, e **R$ 62.000 a receber**, dos quais R$ 40.000 é o banco do
> financiamento do Argo, previsto para sexta. Ele marca a retífica como paga em dois cliques, e
> o número muda. No fim do mês, a mesma tela diz quanto entrou e quanto saiu de verdade.

## O terreno

| Peça | Como está hoje |
|---|---|
| O gasto do carro | `VehicleExpense` tem `Amount`, `Date`, `IsPaid` e o fornecedor. **Vencimento nenhum**: um gasto previsto é previsto para sempre, e "o que vence esta semana" é pergunta que o sistema recusa. E **data de pagamento nenhuma**: marcar como pago apaga a informação de quando o dinheiro saiu |
| A despesa da loja | **Não existe.** Aluguel, energia, salário e imposto ficam fora do sistema. Um gasto só existe pendurado num carro, porque `VehicleEntity` chega à revenda pelo veículo, de propósito |
| A venda | `Sale` guarda valor, forma de pagamento, comissão, repasse e a troca. **Recebimento nenhum**: a venda registrada é dinheiro no bolso para o sistema, mesmo quando o banco paga em quinze dias |
| O tipo de gasto | Cadastro por revenda, com palavras-chave: Mecânica, Elétrica, Funilaria, Estética, Pneus… Todos de carro |
| O fornecedor | Cadastro por revenda com ramo, desde o M18. O gasto aponta para ele |
| O painel | Mostra pátio, vendas do período e o gasto por fornecedor. **Dinheiro no tempo, nenhum** |
| O que já é derivado | Custo, sobra, margem: somados a cada leitura, e jamais guardados. É a regra da casa desde o M6 |

## A pergunta que decide o desenho

**Uma conta a pagar é uma linha nova, ou é o gasto que já existe com uma data a mais?**

É o gasto. A revenda já lança a retífica de R$ 3.900 como *previsto*; pedir que ela lance de
novo numa tela de contas seria digitar duas vezes a mesma coisa, e deixar as duas discordarem.
O que falta ao gasto é **quando vence** e **quando saiu** — duas datas —, e o que falta ao
sistema é uma **despesa sem carro**, porque uma tela de contas a pagar que recusa o aluguel não
é uma tela de contas a pagar.

Do outro lado, **a conta a receber é a venda que ainda não virou dinheiro**. O valor esperado
já está na venda; o que falta é o que **entrou**, com data — e a diferença entre os dois é o
saldo a receber, calculado a cada leitura, como o custo.

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | as decisões abaixo estão tomadas por escrito | — |
| **V1** | O gasto ganha prazo e baixa | `VehicleExpense` com `DueDate` e `PaidDate`; `MarkAsPaid(data)` e `MarkAsPlanned()`; migration e o aproveitamento — todo gasto pago ganha a data dele como data de pagamento, todo previsto ganha vencimento na data lançada | sobe com o banco de hoje, e nenhum gasto fica sem saber quando venceu ou quando saiu | — |
| **V2** | A despesa da loja | `StoreExpense` por revenda (descrição, tipo, fornecedor, valor, vencimento, pago, anotação); o tipo de gasto ganha **escopo** — carro, loja, ou os dois — e a tela Tipos de gasto passa a dizê-lo; API e tela **Despesas da loja** | o aluguel de outubro entra, aparece na lista com vencimento, e o tipo "Aluguel" fica fora do gasto do carro | V1 |
| **V3** | O que entra | `SaleReceipt`: cada entrada de dinheiro de uma venda, com data, valor, forma e anotação; `Sale.DueDate` para o que ainda falta; a venda à vista nasce com o recebimento do dia, e a financiada nasce prevista | registrar a venda do Argo financiado deixa R$ 40.000 a receber; lançar a entrada do banco zera | V1 |
| **V4** | A tela Caixa | Uma tela em Operação: os totais (a pagar, a receber, atrasado, o saldo do período), **o que vence** por semana, **o que atrasou** em vermelho, o que entrou e o que saiu no período, e a baixa em um clique nos dois lados. Planilha em Excel e CSV | o Eduardo paga a retífica e recebe do banco sem sair da tela, e o total muda na hora | V2, V3 |
| **V5** | O caixa onde o dinheiro aparece | A baixa também na aba Gastos do carro e na venda; o bloco do mês no painel: entrou, saiu, e o que vence nos próximos sete dias | quem está na ficha do carro paga o gasto sem ir ao Caixa | V4 |
| **V6** | Fechamento | Suíte verde, `MARCOS.md`, `ROADMAP.md`, `endpoints.md`, `mappings.md`, o manual, publicação no servidor quando houver rede | `dotnet test`, `npm run build` e `docker compose up --build` passam | V1–V5 |

## Decisões (V0)

**1. A conta a pagar é o gasto, com duas datas a mais.**

`DueDate` (quando vence) e `PaidDate` (quando o dinheiro saiu). `IsPaid` continua sendo a
verdade sobre o estado, e as datas contam a história: um gasto pago em 3 de outubro que vencia
em 30 de setembro é um atraso, e o sistema só sabe disso com as duas. Nenhuma das duas é
obrigatória — o gasto lançado à vista, pago na hora, segue como sempre foi, e o vencimento cai
na data do gasto quando ninguém informar outra.

**2. A despesa da loja é entidade própria, e não um gasto sem carro.**

`VehicleExpense` chega à revenda **pelo veículo**, de propósito (ADR do M0: um lugar onde a
linha pode ser presa à empresa errada, em vez de cinco). Um gasto com `IdVehicle` nulo quebraria
isso. `StoreExpense` é `TenantEntity`, com `IdTenant` próprio, e reaproveita o **tipo de gasto**
e o **fornecedor** que já existem: aluguel é do tipo Aluguel, pago à Imobiliária Central.

**3. O tipo de gasto ganha escopo, em vez de nascer um segundo catálogo.**

Dois cadastros de "tipos" seria a pessoa perguntando qual é qual. O `ExpenseType` ganha
`Scope`: **carro**, **loja** ou **ambos**. O gasto do carro oferece carro + ambos; a despesa da
loja oferece loja + ambos. Os tipos que já existem nascem como *carro*, porque é o que eles
são, e a tela Tipos de gasto passa a mostrar e editar a coluna. Quatro tipos de loja entram no
catálogo semeado — Aluguel, Energia, Salário e Imposto —, como os ramos do M18.

**4. A conta a receber é calculada: o esperado da venda menos o que entrou.**

O esperado em **dinheiro** é `Amount − TradeInValue − PartnerCut`: o carro que entrou na troca
já está no pátio e jamais será depósito, e o repasse da loja parceira é dela. `SaleReceipt`
guarda cada entrada, com data, valor e forma. O saldo é subtração feita a cada leitura, como o
custo — nada de coluna `Recebido` para discordar da soma.

**5. A venda nasce com o recebimento que ela mesma descreve.**

À vista, transferência, Pix e cartão: o dinheiro entrou no dia, e o recebimento nasce junto com
a venda. Financiamento: o banco paga depois, e a venda nasce **prevista**, com `DueDate` — sete
dias, editável na hora. Troca pura: dinheiro nenhum a receber. Troca com volta: só a volta.
Quem discordar edita, e é isso que a tela do Caixa permite.

**6. A tela se chama Caixa, e fica em Operação.**

*Financeiro* soa a departamento, e uma revenda de vinte carros tem um dono e um caderno. Caixa
é a palavra da loja. Fica no grupo Operação, depois de Vendas, e nasce para **Gestor** e
**Financeiro**; o Vendedor vê o que vendeu, e não o que a loja deve.

**7. Atrasado é vencido e sem pagamento — e é a única cor forte da tela.**

Vermelho para o que passou do vencimento, dos dois lados. O que vence nos próximos sete dias
tem destaque próprio, e o resto é lista. Uma tela onde tudo grita não avisa nada.

**8. Nada de parcelamento com carnê, nem de conciliação bancária.**

Uma venda financiada tem **um** recebimento do banco, e não trinta e seis: as parcelas são
problema do banco com o comprador. Cartão em três vezes vira três recebimentos se a pessoa
quiser lançar, e um só se ela preferir. Conciliar extrato bancário é outro produto.

**9. A compra do carro fica fora deste marco.**

Ela já entra no custo no dia em que o carro entra, e transformá-la em conta a pagar pediria
decidir o que o custo faz enquanto ela está aberta — pergunta que merece o seu próprio marco,
com o leilão e o prazo dele em cima da mesa.

## O que muda em cada camada

| Camada | Muda |
|---|---|
| **Domain** | `VehicleExpense` com `DueDate`, `PaidDate`, `MarkAsPaid`, `MarkAsPlanned`; `ExpenseType.Scope`; `StoreExpense` (TenantEntity) com `MarkAsPaid`/`MarkAsPlanned`; `SaleReceipt` (VehicleEntity, pendurado na venda) e `Sale.DueDate`; `IStoreExpenseRepository`, `ISaleReceiptRepository`, e o resumo do caixa por período |
| **Infrastructure** | Mapeamentos e migration `Caixa`; `Queries/Cashflow` com as somas por período (`GROUP BY` no banco, jamais lista carregada para somar aqui); o aproveitamento no `DbInitializer`; o pátio de demonstração com contas vencendo, uma atrasada e recebimentos |
| **Application** | `Cashflow/`: o resumo do período, as contas a pagar e a receber com filtro (vence, atrasado, pago, tudo), a baixa dos dois lados; `StoreExpenses/`; o recebimento na venda; o bloco do mês no painel |
| **Api** | `CashflowController` (`api/cashflow`, `.../payables`, `.../receivables`, baixa), `StoreExpensesController`, recebimentos em `api/vehicles/{code}/sale/receipts`, e `api/exports/cashflow`. Tela `cashflow` no catálogo |
| **Frontend** | `app/(panel)/cashflow`, `components/cashflow/*` (CaixaView, o cartão de totais, as listas, o modal de baixa), `components/expenses/StoreExpensesView`, a baixa na aba Gastos e na venda, o bloco no painel, ícone `Wallet` |
| **Testes** | Unidade: as duas datas e o atraso, o escopo do tipo, o saldo a receber com troca e repasse, a venda que nasce recebida ou prevista. Integração: o aproveitamento, a baixa que muda o total, a outra revenda enxergando nada, a planilha. O guarda de colunas cobre as tabelas novas |
| **Docs** | `endpoints.md`, `mappings.md`, `MARCOS.md`, `ROADMAP.md`, manual (capítulo *Caixa*) |

## O que a implementação acrescentou ao plano

- **O aproveitamento foi para dentro da migration**, e não para uma rotina de subida: é uma
  correção de uma vez só, e na mesma transação do schema ela jamais deixa a coluna sem valor.
  Rodou em 80 gastos reais do banco local — zero sem vencimento, zero pago sem data.
- **`BrazilTime.Today` nasceu em `Shared`.** O contêiner roda em UTC, e às vinte e uma horas de
  Porto Alegre o UTC já virou o dia: uma conta que vence hoje apareceria vencida. O fuso é fixo
  em −3, e jamais lido do sistema — a imagem `alpine` tem base de fusos nenhuma.
- **`ConfirmPayment` saiu**, e `MarkAsPaid`/`MarkAsPlanned` ficaram: duas portas para o mesmo
  estado era uma a mais.
- **O `DEFAULT` de uma coluna nova jamais preenche linha antiga** — o provider grava o padrão do
  *tipo*, e não o da coluna. O `Scope` ficou em zero nas treze linhas existentes, que é um tipo
  invisível nas duas telas. As duas migrations levam o `UPDATE` explícito.
- **O `Scope` era gravado e jamais lido**: ficou de fora dos dois SELECT de `ExpenseType`, o
  mesmo defeito do M18. O guarda de colunas passou a cobrir `ExpenseType` e `StoreExpense`.
- **A lista de tipos parou de custar uma consulta por linha.** Eram dezessete idas ao banco para
  escrever dezessete números, e virariam trinta e quatro com a loja; agora é uma consulta
  agrupada que soma os dois lados — e que faz a exclusão enxergar o aluguel.
- **A regra da primeira entrada é do domínio**, e não do handler: "o que a forma de pagamento
  descreve" é regra de negócio, e mora em `Sale.FirstReceipt`.
- **Sem aproveitamento das vendas antigas**, de propósito: uma venda antiga foi paga de um jeito
  que o sistema jamais registrou, e inventar uma entrada para cada uma seria escrever no passado.
  O pátio de demonstração, esse sim, ganhou as entradas — e uma venda financiada em aberto.
- **A tela ficou com duas abas e um período só.** Dois pares de datas na mesma tela eram um a
  mais.
- **A publicação no servidor ficou pendente**, junto do M19, do M20 e do M21.

## O que fica de fora deste marco

- **A compra do carro como conta a pagar** (decisão 9).
- **Despesa recorrente**: o aluguel de novembro é digitado em novembro. Repetir sozinho pede
  regra de recorrência, e regra de recorrência erra em fevereiro.
- **Conciliação bancária, boleto e cobrança**: outro produto, e outra licença.
- **Fluxo de caixa projetado** para além do que está lançado: previsão pede premissa, e
  premissa pede quem responda por ela.
- **Centro de custo e plano de contas**: uma revenda de vinte carros tem tipo de gasto, e isso
  basta até doer.
