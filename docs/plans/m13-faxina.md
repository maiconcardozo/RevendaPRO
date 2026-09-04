# Plano — M13: Faxina, e um número que mente na tela

Fontes: o levantamento de pendências de 4 de setembro de 2026, conferido item a item contra o
código; as divergências 6 e 7 do `docs/ROADMAP.md`, abertas desde o M0; e a foto da listagem
de veículos do M11, onde o defeito apareceu.

Este marco é curto de propósito. Os quatro itens são pequenos e todos do mesmo tipo:
**coisa que mente para quem lê**. Uma dependência que ninguém usa, uma configuração que
configura nada, um documento que descreve um problema resolvido, e um número errado na tela.

Nenhum deles quebra o sistema hoje. Todos custam confiança — e confiança é o que faz alguém
acreditar no resto do que está escrito no repositório.

## O que a entrega precisa provar

> O `package.json` só lista o que o sistema usa. O `appsettings.json` só declara chave que o
> sistema lê. O ROADMAP só descreve problema que ainda existe. E o cartão do carro vendido
> mostra **os dias que ele ficou parado**, e não os dias desde que foi comprado.

## O terreno, conferido

| Item | O que a checagem mostrou |
|---|---|
| `next-auth` | Está no `package.json` e tem **zero** referências em `app/`, `lib/` e `components/`. Sobrou de quando a sessão ia ser dele; hoje é cookie httpOnly com o JWT da API |
| `appsettings.json` | Seis chaves em português — `Cors:Origens`, `Jwt:Emissor`, `Jwt:Audiencia`, `Jwt:MinutosDeValidadeDoAccessToken`, `Jwt:DiasDeValidadeDoRefreshToken`, `RevendaPro:EmpresaPiloto` — que **não mapeiam para propriedade nenhuma** desde a ADR-0003. São inertes: valem os padrões do código |
| Divergência 7 do ROADMAP | Diz que os projetos estão em EF Core 9 com Pomelo. Hoje é **EF Core 10.0.5** com o provider da Oracle, e o Pomelo ficou de fora por escrito. Texto envelhecido |
| Dias em estoque | `Vehicle.DaysInStock` conta de hoje contra a compra, **sempre**. Para o carro vendido, o cartão diz "Ficou 63 dias no pátio" enquanto a venda diz 61 — e o número cresce todo dia depois de o carro já ter saído |

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | os quatro itens estão conferidos contra o código, e não contra a memória | — |
| **V1** | A configuração que engana | Sai o `next-auth`; saem as seis chaves mortas do `appsettings.json`; as divergências 6 e 7 do ROADMAP são fechadas com o que de fato aconteceu | `npm run build` e a pilha sobem igual, e toda chave que sobrou no `appsettings.json` corresponde a uma propriedade lida pelo sistema | — |
| **V2** | O carro vendido para de contar | `DaysInStock` passa a receber o dia em que o carro saiu; a listagem carrega a venda dos carros que mostra, numa consulta só | o cartão do Cruze vendido diz **61 dias**, batendo com a faixa da venda, e o número para de crescer | — |
| **V3** | Fechamento | `MARCOS.md`, `ROADMAP.md` e o manual conferidos; suíte verde | `dotnet test` e `docker compose up --build` passam | V1, V2 |

## Decisões (V0)

**1. Chave morta sai, e não vira comentário.**

A tentação é deixar as chaves em português com um comentário explicando que não valem. Isso
dobra o problema: quem lê passa a ter de entender **duas** coisas — a chave e o motivo de ela
estar ali sem servir. O `appsettings.json` fica só com o que o sistema lê, e o que não é lido
some.

**2. O que sai do ROADMAP é registrado, e não apagado.**

As divergências 6 e 7 estão abertas desde o M0. Elas não somem: passam a dizer **como foram
resolvidas** e em qual marco. Um roteiro que apaga o problema resolvido perde a memória de por
que a decisão foi tomada — e é essa memória que impede a decisão de ser desfeita sem querer.

**3. O dia em que o carro saiu é quem encerra a contagem.**

`DaysInStock` passa a exigir os dois lados: hoje, e o dia da venda quando existe. Sem parâmetro
com valor padrão — um padrão silencioso deixaria todo chamador novo repetindo o defeito, que é
exatamente como ele nasceu.

A listagem carrega a venda dos carros que mostra em **uma** consulta, do mesmo jeito que já
carrega os gastos e a capa. Uma consulta por carro transformaria um pátio de cinquenta carros
em cinquenta idas ao banco para desenhar uma linha de texto.

**4. O número da tela Mercado já está certo, e continua onde está.**

O M11 resolveu isso lá dentro, no SQL, com `COALESCE(s.Date, @Today)`. Este marco alinha a
listagem à mesma regra, e jamais mexe no que já responde certo.

## O que fica de fora deste marco

- **Testes de interface.** Continua fazendo sentido quando houver mais de uma pessoa mexendo
  no frontend.
- **Recuperar veículo e gasto excluídos.** Segue anotado: ninguém precisou, e só o documento
  tinha arquivo pago parado no bucket.
- **Acesso do parceiro ao próprio pátio.** Anotado no M12, e é fronteira de segurança nova —
  marco próprio.
- **Pátios.** É a funcionalidade que vem depois desta faxina, com plano próprio.
