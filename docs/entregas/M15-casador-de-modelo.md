# M15 — O botão acha o modelo sozinho

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md` e do plano
`docs/plans/m15-fipe-sem-tres-cliques.md`.

## O que este marco resolve

Nasceu de um tropeço de verdade. Depois de cadastrar dez carros, o stakeholder perguntou onde
tinha ido parar o botão de consultar a tabela: ele só aparecia no carro que **já tinha código**, e
a ficha mostrava `Código —` sem ligar uma coisa à outra. A pergunta seguinte foi melhor que a
queixa:

> *"Não tem como colocar essa implementação no botão?"*

## As regras

### O casador elimina, e jamais adivinha

O nome do modelo é exigido como **palavra inteira**; o resto — os termos da versão, o câmbio, o
combustível — só pontua. Sobrevive o grupo de maior pontuação, e um descarte que zera tudo volta
um passo.

**Por que é essa:** a tabela de referência não tem busca por texto, e o que ela chama de "modelo"
é a versão inteira: a Jeep responde 110 modelos, 32 deles com a palavra *Renegade*. O trabalho é
jogar fora o que **não pode ser**, e mostrar o que sobrou — e oferecer quatro candidatos vale
mais do que oferecer nenhum.

### O nome tem de casar como palavra inteira

**Por que é essa:** `gol` cabe dentro de `golf` — e um Golf custa quase o dobro de um Gol. A
regra saiu do mundo real, e não da teoria.

### O câmbio manual é reconhecido pela ausência da marca

**Por que é essa:** das duas linhas que a tabela tem para o Gol 1.6 MSI, uma diz `Flex 16V 5p
Aut.` e a outra diz `Flex 8V 5p` e não diz mais nada. Procurar a palavra `Mec.` ali acharia
nada — e separaria nenhuma das duas.

### Empate jamais vira palpite

Sobrando mais de um, abre um modal com o que sobrou, e o nome vai inteiro como a tabela escreve.

**Por que é essa:** duas versões do mesmo carro são **dois preços**, e essa escolha é de quem
conhece o carro. É a mesma linha do M11: o sistema sugere, a pessoa decide dinheiro.

### O que o casador resolve sai pela mesma porta que a pessoa usaria

O comando do escolhedor, e jamais um caminho paralelo.

**Por que é essa:** assim o código gravado, a cotação guardada e a auditoria saem **iguais** nos
dois casos. Um segundo caminho de escrita é um segundo conjunto de regras para manter em dia.

## Como usar

Na ficha do carro, o botão de consultar a tabela — agora presente também no carro **sem** código.
Resolvendo sozinho, ele grava; sobrando mais de um, abre a escolha.

## O que mudou por baixo

- O casador, com pontuação por versão, ano, câmbio e combustível, e o modal de candidatos.

## O que foi conferido

Medido contra a tabela de verdade, nos dez carros do pátio: **cinco resolveram sozinhos** —
Corolla, Renegade, Civic, Mobi e Kicks —, quatro viraram escolha entre um e quatro candidatos, e
um pegou a fonte fora do ar naquele instante. O Chevrolet Onix é o retrato do ganho: de **38
modelos com o nome para dois na tela**, LT e LTZ, os dois de 2017 Flex.

## O que ficou de fora, e por quê

- A **gravação automática do candidato único caiu no M16**, uma semana depois, quando o uso
  mostrou o furo: sobrar um prova que o casador eliminou os outros, e jamais que ele acertou
  este. O parágrafo acima vale como história. Ver `docs/entregas/M16-a-escolha-e-da-pessoa.md`.
