# ADR-0006: Isolamento por cliente — uma pilha para cada, com `IdTenant` por baixo

Data: 2026-09-04
Estado: aceito
Relacionado: ADR-0002 (acesso por tela), ADR-0003 (padrão global), M12 (a isolação provada)

## Contexto

A pergunta veio do stakeholder, em 4 de setembro de 2026, com um exemplo concreto:

> *"O Rodrigo e o Thiago usam sistemas diferentes, são universos diferentes. Eu pensava em um
> banco separado para cada cliente."*

E, no mesmo dia, um segundo caso que parece o mesmo e **não é**:

> *"O Rodrigo tem o pátio particular dele e deixa outros carros em outras revendas. Ele precisa
> tirar relatório de cada pátio e um todo junto."*

São duas perguntas, e confundi-las custaria caro:

| | O que é | Onde se resolve |
|---|---|---|
| Rodrigo × Thiago | **Clientes diferentes**, que jamais se encontram | Isolamento — esta ADR |
| Pátio próprio × Loja do Joãozinho | **Um cliente**, com carros em lugares diferentes | Funcionalidade — o cadastro de pátio |

O sistema já nasceu multiempresa: toda tabela de operação carrega `IdTenant` desde o M0, e
toda consulta filtra por ele. O **M12** transformou isso de disciplina em prova: duas revendas
montadas pelas próprias entidades do sistema, com leitura e escrita cruzadas em veículo, gasto,
foto, documento, proposta, venda, pessoa e perfil.

**E a prova encontrou oito vazamentos** — handlers que liam pelo código público sem filtrar a
empresa, um deles excluindo o usuário de outra revenda com HTTP 204. Isso não invalida o
modelo; é exatamente o motivo pelo qual a decisão abaixo mantém as duas camadas.

## Decisão

### 1. Cliente diferente ganha **pilha própria**

Mesmo código, outro `docker compose -p`, outro banco, outro domínio:

```
docker compose -p revenda-rodrigo -f docker-compose.prod.yml up -d
docker compose -p revenda-thiago  -f docker-compose.prod.yml up -d
```

Isso **já funciona hoje, sem uma linha de código nova** — é o mesmo procedimento usado para
provar o caminho do zero no M9 e no M11, com nomes de contêiner, portas e volumes próprios.

O argumento decisivo é comercial, e não técnico: *"o banco é seu"* é uma frase que o cliente
entende, e vale mais do que qualquer explicação sobre `WHERE IdTenant`.

### 2. O `IdTenant` **continua**, mesmo com uma pilha por cliente

Ele já existe, já está testado e custa zero manter. O que ele compra:

- **A saída para o dia em que a conta apertar.** Juntar três clientes pequenos numa pilha só
  passa a ser decisão de operação, e não reescrita.
- **A segunda camada.** Se um dia dois clientes dividirem uma pilha, a isolação por consulta
  está lá — e provada.

Tirá-lo agora seria trabalho para **perder** uma opção.

### 3. Toda leitura por código público carrega a empresa

Regra de código, e não de disciplina: `GetByCodeAsync` **pede** o `IdTenant`. Foi o conserto
do M12, feito no contrato do repositório em vez de handler a handler — porque um dos oito
handlers já conferia a empresa, e corrigir só o caminho onde alguém olhou é como o defeito
nasce.

### 4. O meio-termo fica registrado, e recusado por ora

"Um app, N bancos" — resolver a conexão pelo cliente da sessão — é o desenho que ganha **em
escala**. Ele custa código de verdade: roteamento de conexão por sessão, migration orquestrada
por banco, e cadastro de cliente virando provisionamento.

Com 1 a 5 clientes, a pilha por cliente ganha de longe, porque custa **zero código**. O gatilho
para revisar está escrito abaixo.

## O custo, aceito de olhos abertos

- **Memória.** Cada pilha pede ~1 GB (banco ~400 MB, API ~200 MB, frontend ~200 MB). Um VPS de
  4 GB segura duas ou três com folga.
- **Release multiplicada.** Cada versão nova é aplicada N vezes. Com 3 clientes é um script;
  com 30 é um problema de frota — e é aí que a decisão 4 muda.
- **Backup e certificado por cliente.** Ambos já automatizados: o backup roda no compose de
  cada pilha, e o Caddy emite o certificado sozinho por domínio.

## Quando revisar

Quando **qualquer uma** destas for verdade:

- passar de ~10 clientes, e a release virar operação de frota;
- a conta de servidor pesar mais do que o valor do isolamento físico;
- aparecer um cliente que exija ver várias revendas suas numa tela só — aí o compartilhado
  volta a ser a resposta, com o `IdTenant` que ficou guardado.

## O que esta decisão não decide

- **Filial e pátio.** Duas lojas do mesmo dono são **um** cliente. Isso é cadastro de pátio, e
  tem plano próprio.
- **Acesso do parceiro.** O dono da loja onde o carro está poderia entrar e ver só os carros
  que estão com ele. É uma fronteira nova **dentro** da mesma empresa, e o sistema hoje não
  tem — marco próprio, anotado no ROADMAP.
