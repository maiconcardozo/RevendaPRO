# M27 — O custo final que é final, e o piso pela loja parceira

Entregue em 2 de outubro de 2026. Plano em `docs/plans/m27-custo-e-piso.md`.

## O que este marco resolve

Quatro perguntas sobre custo e projeção esperavam uma resposta do negócio em
`docs/DECISOES-PENDENTES.md` desde 4 de setembro. Foram respondidas com o stakeholder em 2 de
outubro. Duas mudaram número na tela, e as outras duas confirmaram o que já estava no ar.

> O Rodrigo abre o Cruze, com R$ 2.500 de retrovisor previsto. **Custo final vs FIPE** mostra
> o custo depois do retrovisor pago, e o número continua o mesmo no dia em que ele paga. No
> Argo, que está na Loja do Joãozinho com 8%, o bloco da loja mostra o **Piso pela loja,
> R$ 54.347,83**, e a pergunta *"consigo fechar por 54?"* tem a resposta na tela.

## As regras

### "Custo final vs FIPE" conta o que está previsto

O percentual é o **custo se tudo for pago** dividido pela tabela. Sem gasto previsto, é o mesmo
número de antes.

**Por que é essa:** o rótulo diz "final", e o negócio lê "final" como "depois de tudo pago".
Medido sobre o custo de hoje, o número saltava no dia em que o gasto previsto era pago, sem nada
ter mudado no carro, e discordava do alerta do teto de orçamento, que já decidia pelo projetado.

### O piso pela loja sai da mesma conta do anúncio

**Piso pela loja** é o menor preço de anúncio que ainda deixa para a revenda o **mínimo
aceito**. Com repasse em percentual, é o mínimo dividido por `1 − repasse`; com valor fechado,
é o mínimo mais o valor. Aparece só quando o carro tem mínimo aceito.

**Por que é essa:** é o número da negociação. Na loja parceira, o piso de verdade é maior que
os R$ 50.000 da tela, porque o repasse sai do preço de venda, e quem respondia "consigo fechar
por 54?" fazia uma divisão de cabeça. A mesma função calcula o anúncio e o piso, para os dois
jamais discordarem sobre como o repasse entra.

### O repasse da loja continua fora do custo real

**Por que é essa:** o repasse é condição comercial de onde o carro está, e some no dia em que
ele volta para o pátio da casa. Somado ao custo, ele faria o custo de um carro parado mudar
sozinho, e o percentual contra a tabela passaria a medir duas coisas.

### A projeção continua ignorando a comissão

**Por que é essa:** a comissão é de cada negócio, depende de quem trouxe o comprador, e muitas
vendas têm nenhuma. Ela entra na tela de venda, onde é decidida.

## Como usar

1. Abra um carro com gasto previsto. A linha **Custo final vs FIPE** já conta o previsto, logo
   abaixo de **Custo se tudo for pago**.
2. Abra um carro que está numa loja parceira, com **Mínimo aceito** preenchido. O bloco da loja
   mostra **Anúncio sai por**, **A loja fica com** e **Piso pela loja**.
3. Para o piso aparecer num carro que ainda está sem ele, preencha **Mínimo aceito** na edição
   do carro.

## O que mudou por baixo

- `VehicleCost.PercentOfFipe` divide o `Projected`, e não o `Total`. Sem migration e sem
  endpoint novo: o campo `percentOfFipe` da ficha do veículo muda de valor só para carro com
  gasto previsto.
- `throughPartner`, em `frontend/components/vehicles/CostPanel.tsx`, devolve o piso junto com o
  anúncio.

## O que foi conferido

- A suíte inteira, verde: **821 testes**, 489 de unidade e 332 que sobem a API contra banco em contêiner, com o teste novo do Cruze: 71,63% antes e depois de pagar o
  retrovisor.
- `tsc` e `eslint` limpos no frontend.
- A pilha local, reconstruída com o código do marco. Na tela: o Fox, com R$ 890 previstos, mostra
  75,21% (eram 73,32%); o Argo, com 8% de repasse e R$ 50.000 de mínimo, mostra o piso de
  R$ 54.347,83.

## O que ficou de fora, e por quê

- **O corte de 90% do "apertado"**: continua em `DECISOES-PENDENTES.md`. Só quem compra em
  leilão sabe onde aperta.
- **O piso na ficha e na proposta em PDF**: é número de negociação da revenda, e jamais vai
  para o cliente.
- **A comissão padrão por pátio**: decidida contra.
