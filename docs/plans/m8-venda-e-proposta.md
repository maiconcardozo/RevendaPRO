# Plano — M8: Proposta, venda e dashboard

Fontes: `Revenda PRO requisitos.docx` (RF-18 a RF-24) e
`docs/TRANSCRICOES_ENTREVISTA_REVENDAPRO.md`. Custo e veículo em
`docs/plans/m6-cadastro-de-veiculos.md`.

A tela `sales` já existe no catálogo e já é liberada por perfil. Falta o que ela mostra.

## O que a entrega precisa provar

O alvo do documento de requisitos, segunda metade:

> O usuário consegue cadastrar um veículo, lançar tudo que gastou, consultar seu custo e
> **decidir se uma proposta vale a pena.**

E o decisor, nas palavras do stakeholder:

> *"Eu quero R$ 58 no carro, mas o carro me custa R$ 40 e o cara me manda R$ 55 no dinheiro.
> Pô, ganhar R$ 15 mil, tu acha que eu não dou-lhe fogo?"*

Três números, uma decisão de segundos. O M6 entregou o custo. O M8 entrega os outros dois:
**quanto sobra nesta proposta** e **quanto sobrou nesta venda**.

## O que a entrevista decidiu

**A loja põe a dela em cima.** *"É 66 mil de FIPE, eu quero 58 para mim. A loja põe dela em
cima lá."* Quando a venda sai por loja parceira, o repasse da loja **soma ao preço anunciado**,
e o que ele recebe continua sendo o que ele quer. Isso fecha a pergunta que ficou aberta no M6.
O que ficou sem resposta é se a loja trabalha com percentual ou com valor fixo — o modelo aceita
os dois, e a tela mostra o outro calculado.

**A forma de pagamento move o preço aceito.** *"O cara me manda R$ 55 no dinheiro."* A proposta
carrega a forma de pagamento, e a decisão é sobre o que sobra, não sobre o número cheio.

**Troca gera veículo.** Do próprio dono do projeto: *"pode ser também troca, que gera uma
entrada, ou um carro e um dinheiro."* Uma venda com troca cria **um veículo novo no estoque**,
com origem `TradeIn` e preço de compra igual ao valor acordado pelo carro que entrou. O lucro
realizado deixa de ser só dinheiro: parte vira estoque.

**FIPE continua manual.** Decisão desta rodada: o único acesso gratuito é um espelho
comunitário sem contrato, e o stakeholder disse que manual serve. A integração ganha marco
próprio quando houver fonte estável. O `FipeCode` guardado no M6 é o que vai tornar isso barato.

## Modelo

PK é `Id`; chave estrangeira leva `Id` na frente. Tudo herda `VehicleEntity`, como no M6.

### Proposal (RF-18, RF-19)

Uma proposta é o que alguém ofereceu pelo carro, e o que sobraria se fosse aceita.

| Coluna | Tipo | Notas |
|---|---|---|
| IdVehicle | int | FK Vehicle |
| ProspectName | varchar(120) | quem ofereceu |
| ProspectPhone | varchar(20) | dígitos, opcional |
| Amount | decimal(12,2) | o que foi oferecido |
| Date | date | |
| PaymentMethod | int | dinheiro, transferência, financiamento, cartão, troca, troca com volta |
| Channel | int | **Direct** ou **PartnerStore** |
| PartnerCutPercent | decimal(5,2) | nulo quando direta, ou quando a loja informou valor |
| PartnerCutAmount | decimal(12,2) | nulo quando direta, ou quando a loja informou percentual |
| Status | int | **Open**, **Accepted**, **Declined** |
| Notes | varchar(500) | |

O que a tela mostra ao registrar (RF-19), e que **jamais é guardado**:

```
recebe        = Amount − repasse da loja
lucro líquido = recebe − custo total do veículo
margem        = lucro líquido ÷ Amount
```

Aceitar uma proposta abre a venda já preenchida com os dados dela. Uma proposta aceita marca
as outras abertas do mesmo carro como recusadas — o carro tem um comprador só.

### Sale (RF-20, RF-21, RF-22)

| Coluna | Tipo | Notas |
|---|---|---|
| IdVehicle | int | FK Vehicle, **única entre as ativas** — um carro vende uma vez |
| IdProposal | int | FK Proposal, nula quando a venda entrou direto |
| Date | date | |
| Amount | decimal(12,2) | preço fechado, o que o comprador pagou |
| PaymentMethod | int | |
| Channel | int | Direct ou PartnerStore |
| PartnerStoreName | varchar(120) | nula quando direta |
| PartnerCutPercent | decimal(5,2) | |
| PartnerCutAmount | decimal(12,2) | sempre preenchido quando há loja: é o número que sai da conta |
| Commission | decimal(12,2) | comissão de vendedor ou intermediário, zero quando nenhuma |
| CommissionNotes | varchar(200) | para quem, por quê |
| BuyerName | varchar(120) | |
| BuyerDocument | varchar(14) | CPF ou CNPJ, dígitos |
| BuyerPhone | varchar(20) | dígitos |
| TradeInValue | decimal(12,2) | nulo sem troca; parte do `Amount` que entrou como carro |
| IdTradeInVehicle | int | FK Vehicle, o carro que entrou, nulo sem troca |
| Notes | varchar(500) | |

**Comprador dentro da venda, e não em tabela própria.** O roteiro previa `Comprador`. Na
primeira fase não existe CRM, e uma tabela com uma linha por venda é cerimônia. Fica inline,
com a coluna de documento e telefone marcadas para a LGPD (RNF-13): entram só no privado, e
saem em qualquer exportação que for feita. Quando existir lista de compradores, migra.

**Despesa de venda é despesa.** Documentação de transferência, lavagem para entrega, guincho:
lançam-se pela tabela de gastos que já existe, com o tipo que a revenda quiser. Entram no custo
total, e portanto no lucro. Uma segunda tabela para "gasto de venda" duplicaria a mesma coisa.

O que a venda calcula, e **jamais guarda**:

```
recebido        = Amount − PartnerCutAmount
lucro bruto     = Amount − custo total
lucro líquido   = recebido − Commission − custo total
margem          = lucro líquido ÷ Amount
dias em estoque = Date − PurchaseDate
```

Com troca: `Amount` é o total acordado; `TradeInValue` é a parte que entrou como carro; o
dinheiro é a diferença. O lucro líquido não muda com isso — mudou a forma, não o valor. O que
muda é que o carro novo nasce com `PurchasePrice = TradeInValue`, origem `TradeIn`, e uma
linha de histórico dizendo de qual venda ele veio.

### Vehicle: o que muda

- **Vender é um caso de uso, e não uma mudança de status.** `PATCH /status` passa a recusar
  `Sold`. A única porta para "Vendido" é registrar a venda, que move o status e escreve o
  histórico com a razão "Venda registrada".
- **Vende-se de `ReadyForSale`, `Advertised` ou `Negotiating`.** A esteira do M6 só chegava a
  Vendido por Negociando. Na vida real o comprador aparece na loja e leva o carro pronto; exigir
  uma passagem por "Em negociação" viraria um clique de mentira.
- **Cancelar a venda** exclui logicamente a venda, devolve o carro para `ReadyForSale` com
  histórico "Venda cancelada", e **mantém** o carro de troca que entrou — ele existe de verdade
  no pátio, e cabe a quem cancelou decidir o que fazer com ele. A ficha dele passa a mostrar de
  onde veio.

### Dashboard (RF-23, RF-24)

Tudo lido e somado na hora, com um filtro de período para o que é realizado:

| Número | Fórmula |
|---|---|
| Investido no estoque | soma do custo total dos carros sem venda |
| Por status | contagem |
| Lucro projetado | soma de (preço desejado − custo) dos carros sem venda que têm preço |
| Lucro realizado | soma do lucro líquido das vendas do período |
| Vendas no período | contagem e soma |
| Maior investimento | os 5 carros de maior custo no pátio |
| Maior margem projetada | os 5 de maior lucro projetado |
| Mais tempo parado | os 5 com mais dias |

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | Modelo aprovado | — |
| **V1** | Domínio | `Proposal`, `Sale`, `SaleResult` (calculado), `Vehicle.Sell` e `CancelSale`, veículo de troca | Vender de "Em análise" lança regra de negócio; lucro líquido do Cruze a 55 no dinheiro dá 17.006 | — |
| **V2** | Persistência | Mapeamentos, migration, query objects, repositories | Migration aplica; venda única por veículo é índice | V1 |
| **V3** | Proposta | Registrar, listar, aceitar, recusar; lucro projetado na resposta; API | Aceitar uma recusa as outras | V2 |
| **V4** | Venda | Registrar, cancelar; troca cria veículo; status vai a Vendido pela venda e por mais nada | `PATCH /status` para Vendido responde 422 | V3 |
| **V5** | Dashboard e vendas | Indicadores de RF-23 e RF-24; listagem de vendas com período; API | Lucro realizado bate com a soma das vendas | V4 |
| **V6** | Front | Aba Propostas na ficha, modal de venda, faixa de vendido, tela Vendas, dashboard real | Build do Next; conferido em desktop e celular | V5 |
| **V7** | Testes | Lucro nas duas pontas, troca, cancelamento, porta única para Vendido | Suíte verde | V6 |

## Fora deste marco

- **Integração FIPE.** Decidido acima.
- **Lista de compradores / CRM.** O comprador fica na venda.
- **Anúncio público do veículo.** O `FileVisibility.Public` do M6 continua reservado.
- **Relatório fiscal.** Nota de venda entra como documento, e só.
