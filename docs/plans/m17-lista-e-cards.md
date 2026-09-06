# Plano — M17: o mesmo pátio, de dois jeitos

Fonte: a conversa com o stakeholder em 5 de setembro de 2026, depois de usar a listagem com
vinte carros dentro.

> *"Hoje a funcionalidade parece cards, os veículos. Eu quero formas de mostrar como lista,
> igual marketplace, que você pode ver por lista."*

## O que a entrega precisa provar

> A mesma listagem, com os mesmos filtros e os mesmos números, mostrada de **duas** formas: o
> **mosaico** de cards que existe hoje, e a **lista** densa. A troca é um clique, ela **fica
> guardada** para a próxima visita, e nenhuma das duas some com informação que decide compra.

## Por que a lista, se o card já mostra tudo

Card e lista respondem a perguntas diferentes, e é por isso que todo marketplace tem os dois.

| | O mosaico responde | A lista responde |
|---|---|---|
| Pergunta | *"qual é este carro?"* | *"qual destes carros?"* |
| O que domina | a **foto**, grande o bastante para reconhecer o carro | a **linha**, curta o bastante para comparar vinte |
| Custo de leitura | três por tela, e o olho volta ao começo a cada linha | quinze por tela, todos na mesma margem |
| Serve para | conhecer o pátio, mostrar para alguém | procurar um carro, comparar custo e tempo parado |

Com dez carros, o mosaico ganha. Com sessenta — que é o pátio que este sistema promete
aguentar —, procurar um Gol prata de 2015 num mosaico é rolar a tela cinco vezes olhando fotos
parecidas. A lista existe para essa hora.

## Marcos

| # | Marco | Entrega | Pronto quando |
|---|---|---|---|
| **V0** | Plano | Este documento | as quatro decisões abaixo estão tomadas por escrito |
| **V1** | Os dois jeitos | O seletor, a lista e a preferência guardada | trocar de forma mantém filtros e rolagem, e a escolha volta na próxima visita |
| **V2** | Fechamento | Documentação, imagem construída e conferida no ar | `MARCOS.md`, manual e o padrão de controles atualizados |

## Decisões (V0)

**1. Duas formas, e apenas duas.**

Mosaico e lista. A tabela de colunas ordenáveis — o terceiro jeito clássico — fica de fora: ela
pede ordenação por coluna, largura ajustável e rolagem horizontal, e cada uma dessas coisas é um
marco. A lista deste marco é a linha de marketplace, que já resolve o "comparar muitos".

**2. A lista mostra o que decide, e jamais uma foto grande.**

Cada linha carrega, da esquerda para a direita: a **miniatura** pequena (reconhecer o carro
continua valendo), a **placa** com a situação, o **nome** com a versão, a **linha de atributos**
— ano, quilometragem, cor, tempo parado — e, à direita, o **custo** e o **quero receber**, na
mesma margem em toda linha, que é o que permite comparar de cima a baixo sem procurar onde cada
número começa.

**3. O que a lista deixa de fora, e o que ela substitui.**

A **barra de teto de orçamento** sai: ela ocupa três linhas de altura, e a lista existe para
caber muita gente na tela. No lugar dela entra um **selo**, e só quando há o que dizer —
*passou do teto* ou *o previsto estoura*. O aviso que importa continua chegando; o gráfico dele
mora no card e na ficha.

Fora isso, **nada some**: a mesma placa, a mesma situação, o mesmo custo, o mesmo tempo parado e
o mesmo selo de FIPE atrasada.

**4. A escolha é de quem olha, e fica guardada no navegador dele.**

A forma escolhida vai para o `localStorage` — é preferência de leitura de uma pessoa, e jamais
dado da empresa: guardá-la no servidor faria a escolha do vendedor mudar a tela do financeiro.

Ela é lida **depois da montagem**, e não durante: ler `localStorage` no primeiro render faria o
servidor e o navegador desenharem coisas diferentes, que é o erro de hidratação que o React
acusa. O preço é um quadro de mosaico antes da lista aparecer, para quem escolheu lista — e o
preço do outro caminho seria a tela inteira piscando.

## O que a implementação acrescentou ao plano

Três coisas que o V0 não previa, e que o V1 entregou:

- **A contagem de veículos** ao lado do seletor. Ela nasceu do lugar: uma barra entre o filtro e
  o resultado pedia dizer quantos sobraram, e essa é a primeira pergunta de quem acabou de
  filtrar.
- **O seletor some com a lista vazia.** Escolher entre duas formas de mostrar nada é uma
  pergunta sem resposta útil.
- **`ops/demo-photos.sh`**, uma foto em cada carro de demonstração. A lista mostra miniatura, e
  vinte quadrados cinza provariam pouco. Ele é script, e **jamais** parte do semeador: a subida
  da API não pode depender de um site de terceiro, e vinte fotos no repositório seriam megabytes
  de binário no histórico para sempre. Envia pela mesma porta que a tela usa, então a foto passa
  pela conversão em WebP, pelos três tamanhos e pela capa automática. Rodar de novo pula quem já
  tem foto.

## O que fica de fora deste marco

- **Ordenar.** Hoje a ordem vem do servidor, e ela é a mesma nas duas formas. Ordenar por custo,
  por tempo parado ou por sobra é um marco próprio, porque a ordenação pertence à consulta — e
  peneirar em memória é justamente o que a listagem recusa desde o M6.
- **Escolher as colunas.** Faz sentido numa tabela, e a decisão 1 recusou a tabela.
- **Levar as duas formas para as outras telas.** Vendas e Mercado ganham o mesmo seletor no dia
  em que alguém precisar; o componente sai daqui pronto para isso.
