# M22 — Caixa: o que vence, o que entrou, o que atrasou

Entregue em 8 de setembro de 2026. Plano em `docs/plans/m22-caixa.md`.

## O que este marco resolve

O sistema sabia quanto cada carro custou, e sabia nada sobre **quando o dinheiro sai e quando
ele entra**. Um gasto previsto era previsto para sempre — sem prazo, "o que vence esta semana"
era pergunta sem resposta. O aluguel, a energia e o salário ficavam fora do sistema, porque um
gasto só existia pendurado num carro. E uma venda registrada era dinheiro no bolso, mesmo
quando o banco paga em quinze dias.

Agora há uma tela — **Caixa** — que responde as três perguntas da segunda de manhã: quanto eu
devo, quanto tenho a receber, e o que já passou do prazo.

## As regras

### A conta a pagar é o gasto que já existe, com duas datas a mais

Toda despesa tem **quando vence** e, depois de paga, **quando o dinheiro saiu**. Sem prazo
informado, ela vence na data do lançamento.

**Por que é essa:** a revenda já lançava a retífica como *previsto*. Pedir que ela lançasse de
novo numa tela de contas a pagar seria digitar duas vezes a mesma coisa — e deixar as duas
discordarem no primeiro mês. E a segunda data existe porque, sem ela, marcar como pago apagava
a informação de quando o dinheiro saiu: um gasto pago em 3 de outubro que vencia em 30 de
setembro é um atraso, e só com as duas o sistema sabe disso.

O estado e a data **jamais discordam**: só a entidade as move, nos quatro caminhos possíveis, e
um teste percorre os quatro.

### A despesa da loja é um cadastro próprio

Aluguel, energia, salário, imposto e contador entram em **Caixa → Despesas da loja**, e jamais
na ficha de um carro.

**Por que é essa:** o gasto do carro chega à revenda **pelo veículo**. É uma decisão do começo
do projeto: existe **um** lugar onde uma linha pode ser presa à empresa errada, em vez de
cinco. Um gasto sem carro quebraria isso, e toda consulta que hoje chega ao dono pelo carro
passaria a ter dois caminhos. Uma tela de contas a pagar que recusa o aluguel também não seria
uma tela de contas a pagar — então a despesa da loja ganhou casa própria, com o dono dela
escrito na própria linha.

### Um catálogo de tipos, com escopo

O tipo de gasto diz para onde serve: **carro**, **loja**, ou **os dois**. A lista do aluguel
jamais oferece Funilaria, e a do gasto do carro jamais oferece Aluguel.

**Por que é essa:** dois cadastros de "tipos" seria a pessoa perguntando qual é qual a cada
lançamento. Os treze tipos que já existiam nasceram de carro, porque é o que eles são, e quatro
de loja entraram no catálogo. Quem quiser dizer que Frete serve para os dois muda isso em
*Tipos de gasto*.

### O saldo a receber é subtração, e jamais um número guardado

O esperado em dinheiro de uma venda menos o que já entrou. O esperado tira o que **jamais vira
depósito**: o carro que entrou na troca, que já está no pátio, e o repasse da loja parceira,
que é dela.

**Por que é essa:** é a mesma regra do custo do carro, desde o começo do projeto — o que se
calcula a cada leitura jamais discorda de si mesmo. Uma coluna `Recebido` seria mais um número
para brigar com a soma das entradas na primeira vez que alguém apagasse uma.

### A venda nasce com a entrada que ela mesma descreve

À vista, transferência, Pix e cartão: o dinheiro entrou no dia, e a entrada nasce junto. No
financiamento a venda nasce **esperando o banco**, com prazo de uma semana. Troca pura tem
dinheiro nenhum a receber; troca com volta tem só a volta.

**Por que é essa:** a forma de pagamento já diz tudo isso. Perguntar de novo, no momento em que
a pessoa acabou de escolher "financiamento", seria pedir que ela repetisse o que acabou de
dizer. Quem discordar edita — e é para isso que a tela permite registrar, apagar e mudar o
prazo.

### Vencido é a única cor forte da tela

Vermelho só para o que passou do prazo. O que vence nos próximos sete dias tem destaque
próprio, sem cor; o resto é lista.

**Por que é essa:** uma tela onde tudo grita avisa nada. O vermelho vale enquanto for raro.

### O que se deve vem sem período

Os filtros de data mudam **só o que já se moveu** — quanto entrou e quanto saiu. O total a
pagar e o total a receber são sempre inteiros.

**Por que é essa:** "quanto eu devo" é a pergunta de hoje. Uma janela de datas a transformaria
em outra pergunta — "quanto eu devo com vencimento em setembro" —, e o dono que filtrasse o mês
passado veria uma dívida menor do que a real.

### A baixa tem uma porta só; o recebimento tem outra

Pagar é um clique, e a mesma ação serve para o gasto do carro e para a despesa da loja.
Receber, não: a entrada é registrada na ficha do carro.

**Por que é essa:** uma conta a pagar se paga inteira. Uma venda recebe em partes, e cada
entrada tem valor, data e forma próprios — metade do banco hoje, o resto na semana que vem. A
API diz isso quando alguém tenta o caminho errado, em vez de fingir que sabe qual era a
intenção.

## Como usar

**Ver o caixa.** *Caixa* → aba **Resumo**. Em cima, quatro números: a pagar, vencido, a
receber, e entrou menos saiu no período. Embaixo, as contas a pagar do carro e da loja numa
lista só, ordenadas pelo vencimento, e as vendas com saldo.

**Pagar uma conta.** O visto à direita da linha. A conta sai da lista e os totais mudam na
hora. O mesmo visto existe na aba Gastos da ficha do carro, e a seta ao lado desfaz.

**Lançar o aluguel.** Aba **Despesas da loja** → *Lançar despesa*. Descrição, tipo, valor,
data, e **Vence em** quando ainda vai ser paga. Sem prazo, ela vence na própria data.

**Receber de uma venda.** Na lista de contas a receber, o botão do carro abre a ficha dele; no
bloco *Recebimento*, *Registrar entrada* com quanto entrou, quando e como. Quando o saldo zera,
a venda some da lista de a receber.

**Levar para o contador.** Os botões *Excel* e *CSV* no alto da tela baixam os dois lados com o
período escolhido.

## O que mudou por baixo

- `VehicleExpense` ganhou `DueDate` e `PaidDate`, com `MarkAsPaid` e `MarkAsPlanned`.
- `StoreExpense`: entidade nova, por revenda, com o mesmo desenho de datas.
- `ExpenseType` ganhou `Scope` (1 carro, 2 loja, 3 os dois).
- `SaleReceipt`: entidade nova, pendurada na venda; `Sale` ganhou `DueDate` e `ExpectedCash`.
- Três migrations: `ExpenseDueAndPaidDates`, `StoreExpenses`, `SaleReceipts` — as três com o
  aproveitamento das linhas antigas dentro delas.
- `BrazilTime` em `Shared`: o contêiner roda em UTC, e às vinte e uma horas de Porto Alegre uma
  conta que vence hoje apareceria vencida.
- Endpoints: `api/cashflow`, `api/store-expenses`, `api/vehicles/{code}/sale/receipts`,
  `api/exports/cashflow`. Detalhes em `docs/api/endpoints.md`, seção *Caixa*; as tabelas em
  `docs/database/mappings.md`.

## O que foi conferido

- **765 testes verdes**, 26 deles novos — unidade para as regras das datas e do saldo, e
  integração contra MariaDB de verdade para a lista, a baixa, o recebimento e o isolamento
  entre revendas.
- **O aproveitamento rodou em 80 gastos reais** do banco local: zero sem vencimento, zero pago
  sem data de pagamento, zero previsto com data.
- **Na tela**, com a pilha no ar: a baixa em um clique tirou R$ 2.500 do total a pagar e a lista
  se refez; o bloco do painel bateu com o Caixa; a venda de R$ 37.920 a receber virou *Tudo
  recebido* depois da entrada.

**Três defeitos apareceram no caminho**, e os três eram da mesma família — coluna gravada e
jamais lida, o mesmo defeito do M18. O `Scope` ficou de fora dos dois `SELECT` de tipo de
gasto, e o `DEFAULT` de uma coluna nova jamais preenche linha antiga, porque o provider grava o
padrão do *tipo*, e não o da coluna. O guarda de colunas passou a cobrir as tabelas novas, e as
migrations levam o `UPDATE` explícito.

## O que ficou de fora, e por quê

- **A compra do carro como conta a pagar.** Ela já entra no custo no dia em que o carro entra;
  virar conta pediria decidir o que o custo faz enquanto ela está aberta — pergunta que merece o
  próprio marco, com o leilão e o prazo dele na mesa.
- **Despesa recorrente.** O aluguel de novembro é digitado em novembro. Repetir sozinho pede
  regra de recorrência, e regra de recorrência erra em fevereiro.
- **Parcelamento com carnê.** Uma venda financiada tem **uma** entrada do banco, e não trinta e
  seis: as parcelas são problema do banco com o comprador.
- **Conciliação bancária, boleto e cobrança.** Outro produto, e outra licença.
- **Fluxo de caixa projetado** além do que está lançado: previsão pede premissa, e premissa pede
  quem responda por ela.
- **Centro de custo e plano de contas.** Uma revenda de vinte carros tem tipo de gasto, e isso
  basta até doer.

## Pendente

**A publicação no servidor da rede.** O M22 está na `main`, e o servidor `192.168.1.24`
continua na versão anterior — junto do M19, do M20 e do M21. Publicar exige estar na LAN. O
roteiro está em `docs/operations/deploy.md`, e o que o M20 mudou no servidor (HTTPS, e a raiz
do certificado em cada celular) está em `docs/operations/celular-na-rede.md`.
