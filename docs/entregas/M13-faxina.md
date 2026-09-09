# M13 — Faxina

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md` e do plano
`docs/plans/m13-faxina.md`.

## O que este marco resolve

Quatro itens pequenos, todos do mesmo tipo: **coisa que mente para quem lê**. Um marco sem
funcionalidade nova, e com um defeito visível a menos.

## As regras

### Dependência que ninguém usa sai do projeto

`next-auth` saiu do `package.json`. Estava lá desde antes de a sessão virar cookie httpOnly com o
JWT da API, e tinha **zero** referências no código.

**Por que é essa:** uma dependência declarada é uma promessa de que ela faz parte do desenho.
Quem chega lê "autenticação é next-auth" e procura no lugar errado — além de continuar
atualizando e auditando um pacote por nada.

### Configuração que não é lida sai do arquivo

Seis chaves do `appsettings.json` — `Cors:Origens`, `Jwt:Emissor`, `Jwt:Audiencia` e outras — não
mapeavam para propriedade nenhuma desde a ADR-0003.

**Por que é essa:** eram inertes, e valiam os padrões do código. Alguém mudaria `Jwt:Emissor`
numa madrugada de incidente e veria **nada acontecer**. O que ficou tem o nome certo e é lido de
verdade.

### Divergência aberta se fecha dizendo **como** se resolveu

As divergências 6 e 7 do ROADMAP, abertas desde o M0. A 7 já estava resolvida havia tempo: hoje é
EF Core 10.0.5 com o provider da Oracle, e o Pomelo ficou de fora por escrito.

**Por que é essa:** apagar a linha faria a decisão desaparecer junto com a dúvida — e a mesma
discussão voltaria em seis meses, sem a resposta.

### Cálculo com data exige os dois lados, e valor padrão nenhum

`DaysInStock` passou a exigir hoje **e** o dia da saída.

**Por que é essa:** a listagem dizia "ficou 63 dias no pátio" para um carro vendido em 02/09, com
o número crescendo toda manhã, enquanto a faixa da venda na mesma tela dizia 61. O padrão
silencioso era o defeito: ele fazia todo chamador novo repeti-lo sem perceber.

## Como usar

Nada muda na tela, exceto o carro vendido, que parou de contar dias.

## O que mudou por baixo

Além dos quatro itens, fecha o marco a **ADR-0006**, que registra a decisão de isolamento por
cliente: cliente diferente ganha pilha própria — mesmo código, outro `docker compose -p`, outro
banco. O `IdTenant` continua por baixo, porque tirá-lo seria trabalho para **perder** uma opção.

## O que foi conferido

A suíte inteira, e a listagem com o carro vendido, cujo número de dias parou onde devia — e
concorda com a faixa da venda na mesma tela.

## O que ficou de fora, e por quê

- **Remover o `IdTenant`**: ver a ADR-0006. Ele custa pouco hoje e mantém aberta a opção de
  várias revendas na mesma pilha, que é cara de recuperar depois.
