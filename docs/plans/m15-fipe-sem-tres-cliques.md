# Plano — M15: o botão acha o modelo sozinho, e pergunta só o que sobrar

Fonte: a conversa com o stakeholder em 4 de setembro de 2026, depois de ele cadastrar dez carros
e notar que o botão de consultar a tabela tinha sumido em todos eles.

> *"Mas eu não entendo como você acha o código da FIPE? Não tem como colocar essa implementação
> no botão atualizar?"*

E, quando expliquei que o casamento por texto acerta muito e erra às vezes:

> *"Faz assim: você vai colocar pra sincronizar e, caso apareça mais de um resultado, ele abre um
> modal com todos, código e modelo, e a pessoa escolhe um. Dê a inteligência para tentar buscar o
> menor número de resultados possíveis, mas **sempre busque e dê as opções**."*

## O que a entrega precisa provar

> O botão **jamais some**. Apertar *Consultar agora* num carro sem código faz o sistema procurar
> o modelo na tabela; achando **um**, ele grava e pronto; achando **mais de um**, ele abre a lista
> **do que sobrou** — dois, três, quatro — em vez da lista inteira de cem. E o sistema **jamais**
> escolhe entre dois preços diferentes.

## O terreno

| Peça | Como está hoje |
|---|---|
| Achar o modelo | Três escolhas à mão: marca, modelo e ano. O código só existe depois disso |
| Consultar agora | **Some** quando o carro ainda não tem código — e a ficha não diz por quê |
| A porta da fonte | `IFipeCatalog` já tem tudo: marcas, modelos, anos e o preço por marca+modelo, que é a chamada que **devolve o código** |
| O escolhedor | Pronto desde o M11, e recebe uma lista filtrada sem obra |
| Rotina mensal | Só alcança carro **que já tem código**. Cada carro resolvido aqui entra nela |

## Por que o casamento é de eliminação, e não de adivinhação

A FIPE **não tem busca por texto**, e o que ela chama de "modelo" é a versão inteira. Medido
contra a tabela de verdade, em 4 de setembro de 2026, com os dez carros do pátio:

| Carro | Modelos com o nome | Depois da versão | Resultado |
|---|---|---|---|
| Toyota Corolla 2.0 XEi | 43 | **1** | `Corolla XEi 2.0 Flex 16V Aut.` |
| Jeep Renegade 1.8 Longitude | 32 | **1** | `Renegade Longitude 1.8 4x2 Flex 16V Aut.` |
| Honda Civic 2.0 EXL | 41 | **1** | `Civic Sedan EXL 2.0 Flex 16V Aut.4p` |
| VW Gol 1.6 MSI | 107 | 2 | escolher |
| Renault Sandero 1.0 Expression | 53 | 3 | escolher |
| Chevrolet Onix 1.4 LT | 38 | 4 | escolher |
| Fiat Mobi 1.0 Like | 10 | 2 | escolher |
| Nissan Kicks 1.6 SV | 24 | 2 | escolher |
| Ford Ka 1.0 SE | 47 | 4 | escolher |
| Hyundai HB20 | — | — | a fonte recusou a lista naquele instante |

**Três de dez resolvem sozinhos, e o resto cai de cinquenta opções para duas a quatro.** É esse o
tamanho do ganho, e é ele que o marco entrega: o trabalho de quem usa deixa de ser procurar numa
lista de cem e passa a ser confirmar entre poucos.

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | as cinco decisões abaixo estão tomadas por escrito | — |
| **V1** | O casador | A regra de eliminação, pura e sem rede, com teste sobre nomes de verdade da tabela | os nomes reais de Renegade, Gol e Corolla entram, e o casador diz quantos sobram e por quê | — |
| **V2** | O botão que busca | Endpoint que procura e resolve, o botão deixando de sumir, e o modal com o que sobrou | um carro sem código é resolvido pelo botão; com empate, o modal abre com dois a quatro, e escolher grava | V1 |
| **V3** | Fechamento | Suíte verde, `MARCOS.md`, `ROADMAP.md`, `endpoints.md` e o manual atualizados | `dotnet test`, a imagem do frontend e `docker compose up --build` passam | V1–V2 |

## Decisões (V0)

**1. O botão sempre busca, e jamais some.**

Hoje ele desaparece no carro sem código, e a ficha mostra `Código —` sem ligar uma coisa à outra.
Foi assim que o stakeholder tropeçou. A partir daqui há **um** botão de consulta, e ele funciona
nos dois casos: com código, pergunta o preço direto; sem código, procura o modelo antes.

**2. Empate vira pergunta, e nunca um palpite.**

Duas versões do mesmo carro têm preços diferentes — às vezes dezenas de milhares de diferença.
O sistema resolve sozinho **apenas** quando sobra um candidato. Qualquer outro número abre o
modal com o que sobrou, e quem escolhe é a pessoa.

É a mesma linha do M11: o sistema sugere pela presença, e quem decide dinheiro é a pessoa.

**3. A inteligência é de eliminação.**

Cada sinal só **descarta**, e nenhum inventa. Em ordem: marca, nome do modelo como palavra
inteira (para `Gol` ficar fora de `Golf`), os termos da versão, o câmbio (`Aut.` e `Mec.` estão
escritos nos nomes), o combustível, e por fim o ano, conferido na fonte.

Sobrando zero depois de um descarte, o casador **volta um passo** em vez de responder vazio: é
melhor oferecer quatro candidatos do que nenhum.

**4. O ano é conferido na fonte, e com teto.**

Conferir o ano de um candidato custa uma chamada. Ela vale a pena porque é o descarte mais forte
— mas só até um teto de candidatos, para um modelo com trinta versões não virar trinta chamadas.
Acima do teto, a lista vai para o modal como está, ordenada pela pontuação.

**5. O que o modal mostra é o que a tabela escreve.**

Nome do modelo como a FIPE escreve — `Renegade Longitude 1.8 4x2 Flex 16V Aut.` —, porque é a
única coisa que distingue duas linhas de preço. O código impresso (`004380-0`) só existe depois
que uma delas é escolhida: ele vem na resposta do preço, e é isso que faz a consulta seguinte ser
direta.

## O que fica de fora deste marco

- **Casar por chassi.** O VIN diz montadora, modelo e ano, e resolveria quase tudo. Ele exige uma
  segunda fonte de dados, paga, e é marco próprio.
- **Aprender com a escolha.** "Toda vez que alguém escolheu Renegade 1.8 Longitude, foi esta
  linha" é memória que vale — e é outra tabela, com outro assunto.
- **Rodar o casador na rotina mensal.** A rotina continua alcançando só carro com código. Fazer a
  rotina adivinhar sozinha, sem ninguém olhando, é exatamente o que a decisão 2 recusa.
- **Mexer em preço.** Nada aqui toca *Quero receber*, *Mínimo aceito* ou *Anunciado*, e o teste do
  M11 que segura essa frase continua valendo.
