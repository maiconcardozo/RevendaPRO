# M17 — O mesmo pátio, de dois jeitos

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md` e do plano
`docs/plans/m17-lista-e-cards.md`.

## O que este marco resolve

Nasceu do próprio M16: com vinte carros no pátio de demonstração, o mosaico de cards deixou de
ser confortável.

> *"Hoje parece cards, os veículos. Eu quero formas de mostrar como lista, igual marketplace."*

## As regras

### Card e lista respondem perguntas diferentes, e por isso existem os dois

**Por que é essa:** o mosaico responde *"qual é este carro?"* — a foto grande é o que faz
reconhecer. A lista responde *"qual destes carros?"* — a linha curta é o que faz comparar. Com
dez carros o mosaico ganha; com sessenta, procurar um Gol prata de 2015 num mosaico é rolar a
tela cinco vezes olhando foto parecida. Todo lugar que vende coisa tem os dois pelo mesmo motivo.

### A margem direita é o que faz a lista valer

Custo e *quero receber* caem no mesmo lugar em toda linha.

**Por que é essa:** comparar vinte carros vira **descer o olho** — e não caçar onde cada número
começa. Uma lista com os números em posições diferentes é um mosaico mais feio.

### Aviso vira selo, e só quando há o que avisar

A barra de teto do card ficou de fora da linha.

**Por que é essa:** ela tem três linhas de altura, e a lista existe para caber gente na tela. E
**selo permanente vira parte do fundo** e para de ser lido — o aviso que aparece sempre não avisa
nada.

### A escolha mora no navegador de quem olha, e jamais no servidor

**Por que é essa:** guardá-la na empresa faria a preferência do vendedor mudar a tela do
financeiro. É preferência de pessoa, e não configuração da loja.

### Ela é lida depois da montagem, e aceita o preço disso

**Por que é essa:** ler `localStorage` no primeiro render faria o servidor e o navegador
desenharem coisas diferentes — o erro de hidratação que o React acusa em voz alta. O preço é um
quadro de mosaico antes de a lista aparecer; o preço do outro caminho seria a tela inteira
piscando.

## Como usar

Na listagem de veículos, o botão que troca entre mosaico e lista. A escolha fica guardada naquele
navegador.

## O que mudou por baixo

- A lista, o selo, e a preferência em `localStorage`.
- **`ops/demo-photos.sh`**: uma foto em cada carro de demonstração, pela mesma porta que a tela
  usa.

## O que foi conferido

As duas visões com os vinte carros do pátio de demonstração, nos dois temas.

## O que ficou de fora, e por quê

- **A foto de demonstração dentro do semeador.** Ele é script de propósito: a subida da API não
  pode depender de um site de terceiro, e vinte fotos no repositório seriam megabytes de binário
  no histórico para sempre.
