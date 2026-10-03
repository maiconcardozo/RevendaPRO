# Plano — M27: O custo final que é final, e o piso pela loja parceira

Fonte: as quatro perguntas de `docs/DECISOES-PENDENTES.md` sobre custo e projeção, respondidas
com o stakeholder em 2 de outubro de 2026. Duas delas mudam número na tela; as outras duas
confirmaram o que já está no ar.

## O que a entrega precisa provar

> O Rodrigo abre o Cruze, que tem R$ 2.500 de retrovisor previsto e ainda por pagar. A linha
> **Custo final vs FIPE** mostra **71,63%**: o custo depois do retrovisor pago. Ele paga o
> retrovisor, e o número continua em 71,63%.
>
> Ele abre o Argo, que está na Loja do Joãozinho com 8%. Além do **Anúncio sai por
> R$ 58.152,17**, o bloco da loja mostra o **piso pela loja, R$ 54.347,83**: o menor preço de
> anúncio em que a revenda ainda recebe os R$ 50.000 do mínimo aceito. A pergunta do telefone,
> *"consigo fechar por 54?"*, tem a resposta na tela.

## O terreno

| Peça | Como está hoje |
|---|---|
| O percentual contra a tabela | `VehicleCost.PercentOfFipe`, no domínio: `Total ÷ FipeValue`. O `Total` é compra mais gastos **pagos** |
| O custo se tudo for pago | `VehicleCost.Projected`, que já existe e já aparece na tela quando há gasto previsto |
| O alerta do teto | `VehicleCost.WillExceedBudget` já decide pelo `Projected` |
| A projeção pela loja | `throughPartner`, em `CostPanel.tsx`: divide o quero receber por `1 − repasse`, ou soma o valor fechado |
| O mínimo aceito | `Vehicle.MinimumNetPrice`, mostrado como linha solta acima do bloco da loja |

## Decisões

**1. O percentual usa o `Projected`.** Sem gasto previsto, `Projected` é igual ao `Total`, e
nada muda para o carro que tem tudo pago. Com previsto, o número já conta o que vai sair, e para
de saltar no dia do pagamento. O rótulo continua **Custo final vs FIPE**, que agora é literal.

**2. O piso sai da mesma conta do anúncio.** Uma função só, que recebe o líquido e devolve o
preço com o repasse por cima, chamada duas vezes: com o quero receber e com o mínimo aceito.
Assim o anúncio e o piso nunca discordam sobre como o repasse entra.

**3. O piso aparece só quando faz sentido.** Precisa de mínimo aceito maior que zero e, no
repasse por valor fechado, continua sendo uma soma. Sem mínimo aceito, o bloco fica como hoje.

**4. Repasse e comissão continuam fora do custo** (decididos no mesmo dia, sem mudança de código).

## Marcos

| # | Marco | Entrega | Pronto quando |
|---|---|---|---|
| **V0** | Plano e decisões | Este documento e as quatro respostas em `DECISOES-PENDENTES.md` | as decisões registradas |
| **V1** | O percentual pelo custo se tudo for pago | `VehicleCost.PercentOfFipe` sobre `Projected`, com teste do Cruze (67,21% → 71,63%) e do carro sem previsto (igual ao de hoje) | `dotnet test` verde |
| **V2** | O piso pela loja | `throughPartner` devolve o anúncio e o piso; o bloco da loja ganha a linha **Piso pela loja** | `tsc` e `eslint` limpos; a tela do Argo mostra R$ 54.347,83 |
| **V3** | Fechamento | Manual, `MARCOS.md`, `ROADMAP.md`, documento de entrega, PR | a skill `fechar-marco` cumprida |

## O que fica de fora

- **O corte de 90% do "apertado"**: continua pendente, e só o negócio responde.
- **A comissão na projeção**: decidido contra.
- **O piso nos papéis (ficha e proposta)**: o piso é número de negociação da revenda, e jamais
  vai para o cliente.
