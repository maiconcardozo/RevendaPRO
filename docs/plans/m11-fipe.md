# Plano — M11: Consulta automática da FIPE

Fontes: `docs/ROADMAP.md` (M11 e o risco aberto da FIPE), a decisão do M8 de manter a consulta
manual, e o levantamento das fontes disponíveis feito em 3 de setembro de 2026 — com chamadas
reais, cujos números aparecem abaixo.

Desde o M6 o veículo guarda **valor**, **mês de referência** e **código FIPE**, os três
digitados à mão. O código foi guardado justamente para este marco: é ele que transforma a
consulta em uma chamada direta, sem ninguém navegar marca, modelo e ano de novo.

## O que a entrega precisa provar

> O Cruze da planilha, que hoje tem valor de FIPE digitado à mão, passa a mostrar
> **R$ 56.530 de setembro/2026** sem ninguém digitar — e continua mostrando o último valor
> conhecido, marcado como desatualizado, no dia em que a fonte estiver fora do ar.

## O terreno, levantado com chamadas reais

A FIPE **não publica API**. O acesso oficial é o site e o aplicativo, um modelo por vez. O que
existe são espelhos de terceiros. Os três que sobreviveram ao levantamento:

| Fonte | Como cobra | O que oferece |
|---|---|---|
| `fipe.parallelum.com.br` (v2) | 500 consultas/dia sem token, 1.000/dia com token gratuito; plano pago para ilimitado, histórico de um ano e CSV | consulta por código FIPE, histórico de 3 meses, lista de meses de referência |
| `fipeapi.com.br` | token mediante cadastro; preço fora da documentação | consulta por código FIPE |
| `fipeapi.qagenda.app` | R$ 199 por ano, ilimitado | consulta convencional |

**A conta de volume muda a decisão.** A tabela muda **uma vez por mês**, e o pátio tem dezenas
de carros. Uma consulta por carro por mês são algumas dezenas de chamadas mensais, contra
1.000 por dia da faixa gratuita — sobra mais de trinta vezes o necessário em um único dia. E
com cache por modelo, dez carros do mesmo Cruze custam **uma** consulta.

Chamadas feitas para conferir o caminho inteiro:

```
GET /cars/brands                      → 23 = "GM - Chevrolet"
GET /cars/brands/23/models            → 5635 = "CRUZE LT 1.8 16V FlexPower 4p Aut."
GET /cars/brands/23/models/5635/years → 2014-5
GET /cars/004380-0/years/2014-5       → R$ 56.530,00, setembro de 2026
GET /cars/004380-0/.../history        → set/2026 R$ 56.530 | ago R$ 56.815 | jul R$ 57.101
```

O código `004380-0` é o do Cruze de 2014. A partir dele, a consulta é **uma chamada**.

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | as cinco decisões abaixo estão tomadas por escrito | — |
| **V1** | Porta e adaptador | Porta no domínio, adaptador HTTP da fonte, configuração (endereço, token, tempo limite) e a ADR-0005; testes com respostas gravadas, sem rede | o preço `"R$ 56.530,00"` vira `56530.00` em decimal e `"setembro de 2026"` vira `2026-09-01`; a fonte fora do ar responde com falha tratada, e jamais com exceção solta | — |
| **V2** | Cotações guardadas | Tabela `FipeQuote` por código, ano-combustível e mês de referência; migration e repositório | dois carros do mesmo modelo e ano gastam **uma** consulta, e o mês já buscado jamais é buscado de novo | V1 |
| **V3** | Atualizar um veículo | Caso de uso, endpoint e botão na ficha; a origem do valor passa a ser guardada | o Cruze passa a valer R$ 56.530 de setembro sem ninguém digitar, e a ficha diz de onde veio | V2 |
| **V4** | Achar o modelo | Busca marca → modelo → ano para o carro que ainda não tem código, gravando o `FipeCode` | um carro sem código ganha código e valor em três escolhas, e da segunda vez em diante consulta direto | V3 |
| **V5** | O pátio inteiro | Rotina mensal que atualiza os carros sem venda, e aviso de valor desatualizado na ficha e na listagem | uma rodada atualiza o pátio; um valor de dois meses atrás aparece marcado como velho | V3 |
| **V6** | Fechamento | Suíte verde, `docs/api/endpoints.md`, `ROADMAP.md` e o manual atualizados | `dotnet test`, `npm run build` e `docker compose up --build` passam | V1–V5 |

## Decisões (V0)

**1. Qual fonte, e pagar ou não.**

Nenhuma é oficial, e essa é a informação mais importante deste marco: a FIPE não vende nem
publica API. Qualquer fonte escolhida pode sumir, mudar de formato ou começar a cobrar.

Recomendação: **`fipe.parallelum.com.br` com o token gratuito**, e **sem pagar agora**. O plano
pago compra volume ilimitado e histórico de um ano; a operação usa dezenas de chamadas por mês e
o histórico de três meses que a faixa gratuita já devolve. Pagar antes de precisar é comprar o
problema errado.

A proteção contra a fonte sumir **não é a assinatura, é o desenho**: a consulta entra atrás de
uma porta no domínio, com o adaptador na infraestrutura — a mesma forma do armazenamento de
arquivos na ADR-0004. Trocar de fonte vira um adaptador novo, e nada do resto do sistema sabe
que a fonte mudou.

**2. O mês de referência é sempre fixado.**

Isto veio de um susto no levantamento: **duas chamadas à mesma API, no mesmo minuto, devolveram
meses diferentes** — R$ 56.815 de agosto pelo caminho de marca e modelo, e R$ 56.530 de setembro
pelo código. Sem fixar, o mesmo carro vale dois valores dependendo do caminho.

Então: a rotina resolve primeiro a lista de meses (`/references`), pega o mais recente, e
consulta **sempre com o mês fixado**. Repetindo a chamada fixada, o valor volta idêntico —
conferido. O mês que a resposta trouxer é o que fica guardado, e jamais o mês em que a consulta
aconteceu.

**3. A FIPE jamais bloqueia a operação.**

Ela é referência, e não regra de negócio: o preço quem decide é a pessoa. Fonte fora do ar,
token estourado ou modelo sem correspondência **mantêm o último valor conhecido**, marcado como
desatualizado. Salvar um veículo, lançar um gasto ou registrar uma venda nunca falha por causa
da FIPE, e a espera pela fonte tem tempo limite curto.

**4. O valor guarda de onde veio.**

Hoje o veículo tem valor, mês e código, e nada diz se aquilo foi digitado ou consultado. Passa a
guardar a **origem**, para a ficha poder dizer *"FIPE de setembro/2026, atualizada
automaticamente"* ou *"informada à mão"*.

O valor à mão continua existindo, e continua sendo respeitado: carro raro, importado ou fora da
tabela é caso real, e nesses o número vem da cabeça de quem entende do mercado. A atualização
automática sobrescreve o que ela mesma escreveu; para sobrescrever um valor digitado, a pessoa
pede.

**5. Uma cotação é guardada por modelo, e não por carro.**

A tabela `FipeQuote` guarda código, ano-combustível, mês de referência e valor. Dez carros do
mesmo modelo leem a mesma linha, e o mês já consultado nunca volta à rede. De brinde, ela vira
histórico: com dois meses guardados, a ficha pode dizer quanto o carro desvalorizou desde a
compra — o que hoje ninguém consegue responder sem abrir o site da FIPE.

## O que fica de fora deste marco

- **Consulta pela placa.** Existe como serviço pago de terceiros, e responde outra pergunta —
  qual é o carro, e não quanto ele vale. Entra se a revenda quiser cadastro por placa.
- **Motos e caminhões.** A fonte cobre os três, e o sistema hoje trata carro. Quando a operação
  precisar, o adaptador já sabe o caminho.
- **Gráfico de desvalorização.** O histórico passa a existir no V2; desenhar a curva é tela, e
  entra quando alguém pedir.
- **Alerta de carro que passou da FIPE.** O painel já mostra custo sobre a FIPE; transformar isso
  em aviso ativo é decisão de produto, e não de integração.
