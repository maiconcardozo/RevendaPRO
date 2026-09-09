# M11 — A tabela consultada sozinha, e a negociação medida contra ela

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md` e do plano
`docs/plans/m11-fipe.md`.

## O que este marco resolve

Desde o M6 o veículo guardava valor, mês e código FIPE — os três digitados à mão. Este marco
busca o preço sozinho, guarda o que foi buscado, e passa a medir cada negócio contra a tabela
**do mês em que ele aconteceu**.

## As regras

### A fonte da tabela fica atrás de uma porta no domínio

Interface no domínio, adaptador na infraestrutura, e um interruptor de configuração que devolve o
sistema ao valor digitado à mão sem tocar a rede.

**Por que é essa:** a FIPE **não publica API**. O acesso oficial é o site, um modelo por vez; o
que existe são espelhos de terceiros, e qualquer um deles pode sumir, mudar de forma ou passar a
cobrar. Com a porta no domínio, trocar de fonte é uma classe nova — e nada mais do sistema fica
sabendo. É a mesma forma do armazenamento de arquivos, pelo mesmo motivo.

### A tabela sugere; o preço é da pessoa

A consulta escreve valor, mês, modelo e origem — e **campo de preço nenhum**. *Quero receber*,
*Mínimo aceito* e *Anunciado* continuam sendo de quem entende do carro. Um teste segura essa
frase.

**Por que é essa:** a tabela é referência de mercado, e jamais o preço daquele carro, com aquela
quilometragem, naquele estado. Deixar a consulta escrever preço faria o sistema decidir
dinheiro — que é exatamente o que ele nunca faz.

### Cotação guardada por modelo e mês, e mês fechado jamais muda

Dez carros do mesmo Cruze custam **uma** consulta, e um mês já buscado jamais volta à rede. A
entidade da cotação tem fábrica e método de instância nenhum.

**Por que é essa:** a cotação de um mês fechado é **fato histórico**. Um método que a alterasse
seria uma porta para reescrever o passado — e é contra o passado que este marco mede.

### O mês é o que a resposta trouxe, e jamais o mês em que se perguntou

**Por que é essa:** duas chamadas à mesma fonte, no mesmo minuto, chegaram a devolver meses
diferentes para o mesmo carro. Carimbar o mês do relógio guardaria a cotação de julho debaixo do
rótulo agosto — e o erro só apareceria meses depois, numa comparação.

### Cada negócio é comparado contra a tabela do mês dele

Compra, venda, pedido e propostas, cada um contra o mês daquele negócio. Quem está parado tem a
perda de referência medida à parte, que é o custo de segurar o carro.

**Por que é essa:** comparar uma venda de agosto com a tabela de hoje mediria a **passagem do
tempo** e chamaria isso de resultado do vendedor.

## Como usar

Na ficha do carro, o botão de consultar a tabela. Sem código FIPE, três escolhas — marca, modelo
e ano; da segunda vez em diante a consulta é direta. A tela **Mercado** mostra o pátio inteiro
contra a referência.

## O que mudou por baixo

- Porta no domínio e adaptador na infraestrutura; cotação guardada por modelo e mês.
- A rotina mensal que atualiza o pátio sozinho, respeitando o valor digitado à mão — carro raro
  ou fora da tabela é precificado por quem conhece aquele mercado. Valor velho aparece marcado na
  ficha e na listagem.

## O que foi conferido

O Cruze fechou o marco como o plano prometia: **vendido por R$ 60.000 quando a tabela do mês
dizia R$ 56.530 — 6,14% acima**.

## O que ficou de fora, e por quê

- **O passado anterior a três meses.** O sistema guarda de agora em diante; do passado, só o que
  a fonte devolve, e a faixa gratuita devolve três meses. Negócio mais antigo aparece como *sem
  comparação* e fica **de fora das médias** — uma média com buraco mentiria mais do que a
  ausência.
