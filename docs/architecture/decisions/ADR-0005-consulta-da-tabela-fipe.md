# ADR-0005: Consulta da tabela FIPE — porta no domínio, espelho na infraestrutura

Data: 2026-09-03
Estado: aceito
Relacionado: ADR-0003 (camadas e dependências), ADR-0004 (a mesma forma, para arquivos)

## Contexto

Desde o M6 o veículo guarda **valor**, **mês de referência** e **código FIPE**, os três
digitados à mão. O M8 registrou a consulta automática como marco próprio, com um motivo
escrito: não havia fonte estável.

O levantamento de 3 de setembro de 2026 confirmou o essencial e mudou o acessório.

**A FIPE não publica API.** O acesso oficial é o site e o aplicativo, um modelo por vez. Isso
não mudou, e é improvável que mude: a fundação publica índices, e não serviços.

O que existe são **espelhos de terceiros**. Três sobreviveram à checagem:

| Fonte | Como cobra | O que oferece |
|---|---|---|
| `fipe.parallelum.com.br` (v2) | 500 consultas/dia sem token, 1.000/dia com token gratuito; plano pago para ilimitado e histórico de um ano | consulta por código FIPE, histórico de 3 meses, lista de meses de referência |
| `fipeapi.com.br` | token mediante cadastro | consulta por código FIPE |
| `fipeapi.qagenda.app` | R$ 199 por ano, ilimitado | consulta convencional |

Duas medições feitas com chamadas reais decidem o resto.

**O volume é minúsculo.** A tabela muda uma vez por mês e o pátio tem dezenas de carros. Com a
cotação guardada por modelo, dez carros do mesmo Cruze custam uma consulta. São dezenas de
chamadas por mês contra 1.000 por dia da faixa gratuita.

**A mesma fonte se contradiz sem que se fixe o mês.** Duas chamadas no mesmo minuto:

```
GET /cars/brands/23/models/5635/years/2014-5 → R$ 56.815,00, agosto de 2026
GET /cars/004380-0/years/2014-5              → R$ 56.530,00, setembro de 2026
```

O mesmo carro, dois valores, dois meses — porque cada caminho resolveu "a tabela atual" à sua
maneira. Fixando a referência (`?reference=337`), a resposta repete idêntica.

## Decisão

### 1. A porta mora no domínio, e fala de preço e de mês

```csharp
// Domain/Interfaces/Reference/IFipeCatalog.cs
public interface IFipeCatalog
{
    Task<FipeResult<FipeReference>> GetCurrentReferenceAsync(CancellationToken ct = default);

    Task<FipeResult<FipePrice>> GetPriceAsync(
        string fipeCode, string yearFuel, int reference, CancellationToken ct = default);

    Task<FipeResult<IReadOnlyList<FipeYearOption>>> ListYearsAsync(
        string fipeCode, int reference, CancellationToken ct = default);
}
```

O domínio recebe `decimal` e `DateOnly`. Que a fonte mande `"R$ 56.530,00"` e
`"setembro de 2026"` é problema do adaptador, e de mais ninguém.

`FipeHttpCatalog`, na infraestrutura, é **a única classe do sistema que conhece a forma da
fonte**. Trocar de espelho é uma classe nova ao lado dela.

### 2. Nada disso lança exceção por problema de fonte

`FipeResult<T>` separa três estados que, colapsados, mentem:

| Estado | O que significa | O que o sistema faz |
|---|---|---|
| `Found` | A tabela respondeu | usa o valor |
| `Missing` | A tabela respondeu, e não tem esse carro | fato final: importado, muito antigo, fora da tabela |
| `Unavailable` | A tabela está fora de alcance, estourou o limite ou mudou de formato | mantém o último valor conhecido, marcado como velho |

`Missing` e `Unavailable` são fatos opostos: o primeiro é definitivo, o segundo vale tentar de
novo em uma hora. Um retorno nulo apagaria a diferença.

A única exceção que atravessa é o cancelamento de quem chamou — que não é falha da fonte.

### 3. O mês de referência é sempre fixado

Toda consulta resolve primeiro a lista de tabelas, pega o **código mais alto** e consulta com
ele. Ordenar pelo código, e não confiar na ordem da lista: os códigos crescem de um em um por
mês, e um inteiro é mais difícil de quebrar do que uma ordenação.

O mês guardado é **o que a resposta trouxer**, e jamais o mês em que a consulta aconteceu.

### 4. A tabela é referência, e nunca preço

A consulta escreve valor, mês e origem da referência. `Quero receber`, `Mínimo aceito` e
`Anunciado` seguem sendo digitados por quem entende do carro — a tabela aparece ao lado deles,
junto do custo real, que é a outra metade da decisão.

Consequência de operação: **a espera é curta** (8 segundos por padrão) e nenhuma operação falha
por causa da FIPE. Salvar veículo, lançar gasto e registrar venda não dependem dela.

### 5. Configuração, e nenhuma dependência nova de fornecedor

```
Fipe__Enabled=true
Fipe__BaseUrl=https://fipe.parallelum.com.br/api/v2
Fipe__VehicleType=cars
Fipe__Token=<token gratuito, quando houver>
Fipe__TimeoutInSeconds=8
```

`Enabled=false` devolve o sistema ao estado do M8: valor digitado à mão, e nada tocando a rede.
É o interruptor para o dia em que a fonte sumir, e é o que os testes usam para provar que nada
vaza para a rede.

## Requisitos que sustentam esta decisão

- **RNF-12** — dinheiro em decimal, jamais em ponto flutuante. O preço chega como texto em
  formato brasileiro, e é convertido com cultura pt-BR para `decimal`.
- **RNF-04** — a cotação é dado público de referência, e não dado de empresa: ela não carrega
  `IdTenant`, do mesmo jeito que o catálogo de telas. Isso também é o que faz dez carros de
  duas revendas diferentes compartilharem uma consulta.

## Consequências

**A favor**

- Trocar de fonte custa um adaptador, e nada mais do sistema fica sabendo.
- Não pagar agora é uma decisão reversível: a assinatura entra como token e endereço.
- As cotações guardadas viram histórico do sistema sem trabalho extra — e com isso a resposta
  para "por quanto vendi, contra a tabela do mês em que vendi".

**Contra, e assumido**

- **A fonte é não oficial.** Ela pode sumir, mudar de forma ou passar a cobrar. O interruptor,
  a porta e o valor à mão são as três saídas.
- **O passado não volta.** A faixa gratuita devolve três meses de histórico; venda anterior a
  isso fica sem comparação, e a tela diz isso em vez de inventar número.
- **Uma consulta a mais na tela de cadastro.** Aceitável: ela é opcional, tem tempo limite curto
  e o cadastro conclui sem ela.

## O que esta decisão não decide

- **Consulta pela placa** (qual é o carro, e não quanto vale) — serviço pago, e outra pergunta.
- **Motos e caminhões** — a fonte cobre; `VehicleType` é configuração.
- **Sugestão de preço calculada** a partir da tabela. O stakeholder foi explícito: a tabela
  sugere pela presença, e quem calcula preço é a pessoa.
