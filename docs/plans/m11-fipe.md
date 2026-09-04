# Plano — M11: Consulta automática da FIPE

Fontes: `docs/ROADMAP.md` (M11 e o risco aberto da FIPE), a decisão do M8 de manter a consulta
manual, o levantamento das fontes feito em 3 de setembro de 2026 — com chamadas reais, cujos
números aparecem abaixo — e a leitura do stakeholder na mesma data, que definiu o papel da FIPE
dentro do sistema.

Desde o M6 o veículo guarda **valor**, **mês de referência** e **código FIPE**, os três
digitados à mão. O código foi guardado justamente para este marco: é ele que transforma a
consulta em uma chamada direta, sem ninguém navegar marca, modelo e ano de novo.

## O papel da FIPE neste sistema

> *"O preço da FIPE vai ser uma referência para precificação, e não a precificação final. Ele
> pode sugerir, mas o preço mesmo quem muda é o usuário."*

Isso define o marco inteiro, e três consequências saem daí:

1. **A FIPE jamais escreve num campo de preço.** `Quero receber`, `Mínimo aceito` e
   `Anunciado` continuam sendo digitados por quem entende do carro. A tabela aparece **ao
   lado** deles, como referência visível na hora de decidir.
2. **Os campos ficam separados de propósito.** Referência é uma coisa, preço praticado é outra.
   Misturar os dois apagaria justamente a comparação que interessa.
3. **A comparação é histórica.** A pergunta que o stakeholder quer responder é *"por quanto
   este carro saiu, contra a FIPE do mês em que ele saiu"* — e a mesma pergunta vale para a
   compra. Um sistema que só guarda a FIPE de hoje jamais responde isso.

## O que a entrega precisa provar

> O Cruze passa a mostrar **R$ 56.530 de setembro/2026** sem ninguém digitar, ao lado dos
> preços — que continuam sendo os da pessoa. E o painel responde: **este carro foi vendido por
> R$ 60.000 quando a tabela do mês dizia R$ 56.815 — 5,6% acima.**

## O terreno, levantado com chamadas reais

A FIPE **não publica API**. O acesso oficial é o site e o aplicativo, um modelo por vez. O que
existe são espelhos de terceiros. Os três que sobreviveram ao levantamento:

| Fonte | Como cobra | O que oferece |
|---|---|---|
| `fipe.parallelum.com.br` (v2) | 500 consultas/dia sem token, 1.000/dia com token gratuito; plano pago para ilimitado, histórico de um ano e CSV | consulta por código FIPE, histórico de 3 meses, lista de meses de referência |
| `fipeapi.com.br` | token mediante cadastro; preço fora da documentação | consulta por código FIPE |
| `fipeapi.qagenda.app` | R$ 199 por ano, ilimitado | consulta convencional |

**A conta de volume decide o gasto.** A tabela muda **uma vez por mês**, e o pátio tem dezenas
de carros. Uma consulta por carro por mês são algumas dezenas de chamadas mensais, contra 1.000
por dia da faixa gratuita. E como a cotação é guardada por modelo, dez carros do mesmo Cruze
custam **uma** consulta.

Chamadas feitas para conferir o caminho inteiro:

```
GET /cars/brands                      → 23 = "GM - Chevrolet"
GET /cars/brands/23/models            → 5635 = "CRUZE LT 1.8 16V FlexPower 4p Aut."
GET /cars/brands/23/models/5635/years → 2014-5
GET /cars/004380-0/years/2014-5       → R$ 56.530,00, setembro de 2026
GET /cars/004380-0/.../history        → set/2026 R$ 56.530 | ago R$ 56.815 | jul R$ 57.101
```

Três meses do Cruze mostram o que o painel vai medir: **a tabela cai cerca de R$ 285 por mês**
neste modelo. Carro parado perde valor de referência todo mês, e hoje ninguém consegue dizer
quanto.

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | as seis decisões abaixo estão tomadas por escrito | — |
| **V1** | Porta e adaptador | Porta no domínio, adaptador HTTP, configuração (endereço, token, tempo limite) e a ADR-0005; testes com respostas gravadas, sem rede | **concluído** — `"R$ 56.530,00"` vira `56530.00` em decimal, `"setembro de 2026"` vira `2026-09-01`, e fonte fora do ar, estourada de limite ou em formato novo devolve resultado tratado; 20 testes com respostas gravadas, e nenhum toca a rede | — |
| **V2** | Cotações guardadas | Tabela `FipeQuote` (código, ano-combustível, mês, valor) e o ano-combustível gravado no veículo; migration e repositório | **concluído** — dois carros do mesmo modelo gastam uma consulta, e o mês guardado volta do banco em 31 ms sem tocar a rede; conferido contra a fonte e o banco de verdade | V1 |
| **V3** | Atualizar quando quiser | Botão **Consultar agora** na ficha, `POST /api/vehicles/{code}/fipe`, origem do valor e auditoria; o ano-combustível é descoberto pelo ano do modelo | **concluído** — o Cruze passou de R$ 66.000 digitados para R$ 56.530 de setembro/2026 num clique, a ficha diz "consulta automática", e `Quero receber`, `Mínimo aceito` e `Anunciado` continuaram intactos | V2 |
| **V4** | Achar o modelo | Escolhedor marca → modelo → ano na ficha, três endpoints de leitura e `POST /api/vehicles/{code}/fipe/model` | **concluído** — o Argo saiu de nada para **R$ 51.757 de setembro/2026** em três escolhas, ganhou o código `001494-0`, e a consulta direta seguinte voltou em 53 ms | V3 |
| **V5** | O pátio inteiro, sozinho | Serviço de fundo na API, `FipeYardRefresher`, e o aviso de valor velho na ficha e na listagem | **concluído** — a rodada de setembro atualizou o Argo sozinha e **sem nenhuma consulta**, porque o mês já estava guardado; um valor digitado à mão ficou onde estava, e a etiqueta *FIPE de 2 meses atrás* apareceu nas duas telas | V3 |
| **V6** | Negociação × FIPE | Tela **Mercado** (`market`), `GET /api/market` e as comparações no domínio | **concluído** — a tela responde as cinco perguntas da decisão 6, e o Cruze aparece nela como o plano prometeu: **vendido por R$ 60.000 quando a tabela do mês dizia R$ 56.530 — 6,14% acima** | V5 |
| **V7** | Fechamento | Suíte verde, `docs/api/endpoints.md`, `ROADMAP.md`, `MARCOS.md`, `mappings.md` e o manual atualizados | **concluído** — 272 testes verdes, as quatro migrations aplicadas numa base criada do zero, e o caminho inteiro conferido nela: veículo novo, três escolhas, R$ 56.530 de setembro e a tela Mercado respondendo | V1–V6 |

## Decisões (V0)

**1. Qual fonte, e pagar ou não.**

Nenhuma é oficial, e essa é a informação mais importante do marco: a FIPE não vende nem publica
API. Qualquer fonte escolhida pode sumir, mudar de formato ou passar a cobrar.

Recomendação: **`fipe.parallelum.com.br` com token gratuito**, e **sem pagar agora**. O plano
pago compra volume ilimitado e histórico de um ano; a operação usa dezenas de chamadas por mês.
A proteção contra a fonte sumir **não é a assinatura, é o desenho**: a consulta entra atrás de
uma porta no domínio, com o adaptador na infraestrutura — a mesma forma da ADR-0004. Trocar de
fonte vira um adaptador novo, e nada do resto do sistema fica sabendo.

**2. O mês de referência é sempre fixado.**

Veio de um susto no levantamento: **duas chamadas à mesma API, no mesmo minuto, devolveram meses
diferentes** — R$ 56.815 de agosto por um caminho, R$ 56.530 de setembro pelo outro. Sem fixar,
o mesmo carro vale dois valores dependendo de como se perguntou.

A rotina resolve primeiro a lista de meses (`/references`), pega o mais recente e consulta
**sempre com o mês fixado**. Repetindo a chamada fixada, o valor volta idêntico — conferido. O
mês guardado é o que a resposta trouxer, e jamais o mês em que a consulta aconteceu.

**3. A FIPE sugere; o preço é da pessoa.**

Nenhum campo de preço é preenchido pela consulta. A ficha mostra a referência ao lado de
`Quero receber` e `Mínimo aceito` — com o custo real do carro junto, que é a outra metade da
decisão. A automação atualiza **apenas** valor, mês e origem da referência.

E a FIPE jamais bloqueia a operação: fonte fora do ar, token estourado ou modelo sem
correspondência mantêm o último valor conhecido, marcado como velho. Salvar veículo, lançar
gasto ou registrar venda nunca falha por causa dela, e a espera tem tempo limite curto.

**4. O valor guarda de onde veio.**

*Entregue no V3, com três regras que só apareceram ao escrever o código:*

- **Só um valor que se moveu é um valor digitado.** O formulário devolve os campos da FIPE
  como estão a cada gravação, então marcar a origem em toda chamada transformaria uma
  consulta em "digitada à mão" assim que alguém editasse a cor do carro.
- **Quem clica no botão sempre passa.** A proteção da decisão 4 é contra a rotina mensal
  do V5 sobrescrever sozinha um valor digitado — `Vehicle.AcceptsAutomaticFipe` é quem diz
  isso. Uma pessoa pedindo a consulta já é a pessoa pedindo.
- **O ano-combustível é descoberto, e jamais digitado.** Todo carro cadastrado antes deste
  marco tem código e ficou sem o par. Pedir para alguém digitar `2014-5` seria pedir que a
  pessoa conheça a forma de um espelho: o sistema lista os anos do modelo e casa pelo ano
  do veículo. Duas versões no mesmo ano (flex e gasolina) viram pergunta para uma pessoa,
  que é o V4.

Hoje nada diz se aquele número foi digitado ou consultado. Passa a guardar a **origem**, para a
ficha dizer *"FIPE de setembro/2026, atualizada automaticamente"* ou *"informada à mão"*.

O valor à mão continua existindo e sendo respeitado: carro raro, importado ou fora da tabela é
caso real, e nesses o número vem de quem entende do mercado. A automação sobrescreve o que ela
mesma escreveu; para sobrescrever um valor digitado, a pessoa pede.

**5. A comparação histórica sai da tabela de cotações, e não de cópias espalhadas.**

A tentação seria carimbar a FIPE dentro da venda no dia em que ela acontece. É desnecessário: a
`FipeQuote` guarda **(código, ano-combustível, mês, valor)**, e a venda já tem data. Cruzar as
duas responde *"vendido por R$ 60.000 quando a tabela de agosto dizia R$ 56.815"* para sempre, e
sem um número repetido em dois lugares — que é como o custo do M6 tinha ficado errado.

Uma cotação de mês fechado **jamais muda**: ela é fato histórico, e a tabela vira o histórico do
sistema sem nenhum trabalho extra.

**Limite honesto:** o sistema passa a guardar de agora em diante. Do passado, só o que a fonte
devolver — e a faixa gratuita devolve **três meses**. Carro vendido em janeiro fica sem
comparação, e a tela vai dizer isso em vez de inventar um número.

**Como a leitura acontece (V2).** `FipeQuoteReader`, na camada de aplicação, procura nesta
ordem: o que já resolveu no próprio escopo, o que está guardado no banco e, só então, a fonte.
Ele **enfileira** a cotação nova como qualquer outra escrita — quem chamou é que faz o commit —,
e o mês guardado é sempre o que a resposta trouxer.

Duas consequências que valem para os próximos submarcos:

- **A rotina do pátio (V5) roda em um escopo só.** A lista de tabelas publicadas é resolvida
  uma vez por escopo; um escopo por carro dobraria o gasto de consultas sem nenhum ganho.
- **A lista de tabelas custa uma chamada por operação que consulta a FIPE.** É aceitável
  porque a consulta é pedida — botão ou rotina mensal —, e jamais a cada ficha aberta: a
  ficha lê o valor guardado no veículo. Se um dia ela passar a ser aberta a cada leitura,
  guardar a tabela publicada em memória por algumas horas resolve, e é uma linha.

**6. Onde mora o painel de negociação × FIPE.**

Recomendação: **tela própria**, e não mais um bloco no painel — que já está denso e responde
outra pergunta (dinheiro parado e lucro). Pela ADR-0002, tela é permissão: `market`, rótulo
**Mercado**, no grupo Operação, nascendo para o Administrador e concedida a Gestor e Financeiro.

O que ela responde:

| Pergunta | Como |
|---|---|
| Compramos abaixo da tabela? | Preço de compra contra a FIPE do mês da compra, por carro e na média — é a vantagem do leilão, medida |
| Vendemos acima ou abaixo? | Preço fechado contra a FIPE do mês da venda, em reais e em percentual |
| A proposta na mesa é boa? | Valor oferecido contra a tabela do mês corrente |
| Quanto custa segurar o carro? | Queda da referência desde a compra — os R$ 285 por mês do Cruze, multiplicados pelos dias parados |
| Quem está pedindo acima da tabela? | Preço desejado contra a FIPE de hoje, para o pátio inteiro |

## Duas coisas que a fonte ensinou no V4

**A tabela escreve zero quilômetro como o ano 32000.** A fonte devolve `32000-5` com o nome
`32000 Flex`, cru. É convenção da tabela, e viraria uma opção incompreensível numa lista que
alguém precisa entender — a tela escreve **Zero km Flex**.

**O código do modelo vem da resposta, e jamais da escolha.** É a chamada de preço por marca e
modelo que imprime `001494-0`, e é esse valor que fica guardado. Guardar o que a tela escolheu
deixaria o sistema com um código que a tabela talvez tenha normalizado.

## O que fica de fora deste marco

- **Consulta pela placa.** Serviço pago de terceiros, e responde outra pergunta — qual é o
  carro, e não quanto ele vale. Entra se a revenda quiser cadastro por placa.
- **Motos e caminhões.** A fonte cobre os três; o sistema trata carro. Quando a operação
  precisar, o adaptador já sabe o caminho.
- **Preencher o passado.** Comparar vendas anteriores a este marco depende de histórico que a
  fonte gratuita não dá.
- **Sugestão de preço calculada** (FIPE menos um percentual, por exemplo). O stakeholder foi
  claro: a tabela sugere pela presença, e quem calcula preço é a pessoa.
