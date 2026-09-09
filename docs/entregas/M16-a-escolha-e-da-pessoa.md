# M16 — A escolha é sempre da pessoa, e dá para desfazer

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md` e do plano
`docs/plans/m16-a-escolha-e-da-pessoa.md`.

## O que este marco resolve

O M15 tinha uma frase que parecia óbvia: *"sobrando um candidato com um ano só, o sistema grava,
porque escolha nenhuma restou para fazer"*. O uso mostrou o furo em uma semana.

> *"Eu estou querendo deixar aquela parte onde consulta agora ele fazer para todos, mesmo se
> achar um ele abrir o popup para a pessoa ver o detalhe e ter uma possibilidade."*

## As regras

### O pop-up abre sempre — com um candidato ou com vinte

O campo `applied` saiu do contrato, em vez de virar um campo que jamais vem preenchido.

**Por que é essa:** **sobrar um prova que o casador eliminou os outros, e jamais que ele acertou
este.** Quem cadastrou o carro reconhece o acabamento numa olhada; o casador só sabe o que o nome
digitado repete. Gravar sem mostrar tirava da pessoa exatamente a conferência que ela faz melhor
— e, quando errava, deixava um preço de outra versão na ficha sem ninguém ter visto passar.

### A inteligência assina o quanto confia, e para de decidir

A nota de acurácia sai dos mesmos sinais que já eliminavam — versão 4, ano 2, câmbio 1,
combustível 1 —, sobre o que havia para conferir.

| O carro cadastrado como | A nota do melhor candidato | O que a tela mostra |
|---|---|---|
| `Renegade 1.8 Longitude` | 100% | um candidato, recomendado, tudo conferido |
| `Renegade Longitude` sem o motor | 75% | o destaque, e o que ficou por conferir |
| `Gol`, e nada mais | 50% | vinte candidatos empatados, e **recomendado nenhum** |

**Por que é essa:** a nota diz à pessoa *quanto do que ela digitou foi realmente conferido* — que
é a informação de que ela precisa para decidir se olha a lista ou confia no destaque.

### O peso da versão é fixo, mesmo no carro que veio sem versão

**Por que é essa:** foi a única forma de o medidor ser honesto. Um `Gol` sem mais nada bateria
tudo o que havia para bater e devolveria vinte candidatos marcando **100%** — a única leitura que
essa tela jamais pode dar.

### O recomendado só existe por maioria estrita

Empate volta sem destaque nenhum.

**Por que é essa:** é a regra do M15 dita em nota — onde o sistema empata, ele pergunta em vez de
apontar. E o destaque muda o que se lê primeiro, e nada mais: o botão que grava é o da pessoa, em
100% dos casos.

### O que foi vinculado se desvincula, e por inteiro

`DELETE /api/vehicles/{code}/fipe` apaga código, ano-combustível, valor, mês e origem.

**Por que é essa:** guardar metade seria pior do que guardar nada. O valor veio do modelo que
está sendo desfeito, e ficaria na ficha um preço sem nada que o explique — ainda alimentando o
painel de custo e a projeção de sobra. Depois disso a rotina mensal deixa de alcançar o carro:
consequência desejada, e não efeito colateral.

## Como usar

Na ficha, consultar a tabela abre a lista com a nota e o destaque; o botão de gravar é sempre da
pessoa. Para desfazer, *Desvincular da tabela*.

## O que mudou por baixo

- A nota de acurácia, o modal que abre sempre, e o endpoint de desvincular.
- O **pátio de demonstração**: quatro lugares e vinte carros atrás de
  `RevendaPro__SeedDemoVehicles`, ligado só na pilha de desenvolvimento.

## O que foi conferido

O catálogo de demonstração tem teste próprio, **porque catálogo é dado**: placa válida e única,
pátio que existe, e carro vendido que a esteira alcança. Ele existe para mostrar as duas pontas
da busca no mesmo estoque — o nome que casa com uma linha da tabela e o nome digitado com pressa
que casa com vinte —, com lucro, prejuízo e carro em cada degrau da esteira.

## O que ficou de fora, e por quê

- **Aprender com a escolha da pessoa** para pontuar melhor da próxima vez: seria um sistema que
  muda de opinião sozinho, e a regra deste marco é exatamente a oposta.
