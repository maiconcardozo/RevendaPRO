# M14 — Pátios, e o relatório de cada lugar onde o carro está

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md` e do plano
`docs/plans/m14-patios.md`.

## O que este marco resolve

> *"O Rodrigo tem o pátio particular dele, que anuncia, e ele deixa outros carros em outras
> revendas. Ele precisa tirar relatório de cada pátio ou revenda, e um todo junto, mas sempre
> agrupado."*

Até aqui o sistema sabia em que **etapa** cada carro estava, e nada sobre **onde** ele estava. A
loja de terceiro existia como um texto digitado à mão em cada venda, redigitado a cada negócio e
impossível de agrupar.

## As regras

### Um cadastro só, com o tipo dentro

Pátio próprio e loja de terceiro são o mesmo cadastro. O tipo muda o repasse, e só isso.

**Por que é essa:** foi o que o stakeholder descreveu — *"tudo seria pátio"* —, e é o que mantém
a soma possível. Dois cadastros exigiriam somar duas coisas diferentes em todo relatório, e
alguém acabaria somando só uma.

### O carro está em um lugar por vez

Uma coluna no veículo, e tabela de ligação nenhuma.

**Por que é essa:** uma tabela de ligação abriria a porta para um estado que a operação não
tem — o carro em dois pátios ao mesmo tempo —, e todo relatório passaria a ter de escolher qual
dos dois contar.

### A mudança de pátio é evento, e a história jamais se apaga

Ela entra na linha do tempo do M10 como um tipo novo, com o de onde, o para onde, o motivo, a
hora e quem fez. As duas chaves da passagem são **restrict**.

**Por que é essa:** é o que responde *"esse carro ficou dois meses na Loja do Joãozinho e voltou
sem vender"* — a informação que decide se vale deixar carro lá de novo. Um pátio que sai do
cadastro jamais pode levar essa resposta junto.

### O relatório agrupa, e jamais troca o total pelo pedaço

O painel ganhou o bloco *Por pátio* — carros, capital parado e tempo médio em cada lugar — e os
números do topo continuam somando o estoque inteiro. Pátio vazio fica na lista, e os carros sem
lugar ganham linha própria.

**Por que é essa:** a frase dele foi explícita: *"de cada um e um todo junto"*. E "zero carro na
Loja do Joãozinho" **é uma resposta**: some a linha, some a pergunta.

### O repasse é sugerido, e continua sendo decidido por quem vende

A venda de um carro que está na loja de um parceiro já chega com o canal, o nome da loja e o
repasse combinados no cadastro — os três editáveis.

**Por que é essa:** mesmo raciocínio da FIPE no M11: o sistema sugere pela presença, e quem
decide dinheiro é a pessoa. O cálculo do negócio, que é do M8, não mudou em nada.

### Ler é informação; mover é decisão

Ver onde o carro está vem na ficha, para quem tem a tela de veículos. Cadastrar pátio e mover
carro exigem a tela **Pátios**.

**Por que é essa:** exigir a permissão de administração para *ver* esconderia do vendedor onde
está o carro que ele está vendendo.

## Como usar

*Administração → Pátios* cadastra o lugar e o repasse. Na ficha do carro, mover para outro pátio,
com motivo. No painel, o bloco *Por pátio*; na listagem, o filtro.

## O que mudou por baixo

- A entidade de pátio, a coluna no veículo, o histórico de passagem e o bloco do painel.
- De faxina, a classe interna `Yard` do painel virou `Stock`: pátio virou entidade de verdade
  neste marco, e duas coisas com o mesmo nome no mesmo arquivo é como se lê errado.

## O que foi conferido

Duas coisas de segurança ficaram do jeito que estavam **de propósito**, e foram provadas: filtrar
por um pátio que a empresa desconhece responde **lista vazia**, e nunca o estoque inteiro; e
mover um carro para o pátio de outra revenda responde **404**, porque o pátio é procurado por
código **e** por empresa, juntos.

## O que ficou de fora, e por quê

- **O acesso do parceiro ao próprio pátio**: pede login de gente de fora da revenda, que é outra
  conversa de segurança. Está na fila combinada com o stakeholder.
