# Plano — M16: a escolha é sempre da pessoa, e dá para desfazer

Fonte: a conversa com o stakeholder em 5 de setembro de 2026, depois de usar o botão que o M15
entregou.

> *"Eu estou querendo deixar aquela parte onde consulta agora ele fazer para todos, mesmo se
> achar um ele abrir o popup para a pessoa ver o detalhe e ter uma possibilidade. E também ele
> poder desvincular a consulta removendo o código."*

E, no mesmo fôlego, o que fazer com a inteligência que já existe:

> *"Caso consiga, de inteligência para ele deixar em destaque o recomendado, como se fosse um
> medidor de acuracidade — mas quem seleciona é o usuário 100% dos casos."*

Mais o pedido de terreno para enxergar tudo isso funcionando:

> *"Crie uns carros com nomes meio genéricos para mais testes, pode criar mais uns 20 em pátios
> variados com variações de lucro e prejuízo. Quero que tenha testes de tabela FIPE para ver 1
> exemplo ou para ver 10, 20 exemplos para escolher."*

## O que a entrega precisa provar

> O botão **jamais grava sozinho**. Achando um candidato ou vinte, ele abre o pop-up com o que
> achou — nome, preço, código e ano —, marca em destaque o que a nota de acurácia aponta, e
> espera. Quem aperta *Usar este modelo* é sempre a pessoa. E o que foi vinculado **se
> desvincula**: um clique tira código, valor, mês e origem da ficha, e o carro volta a ser um
> carro sem tabela.

## O que muda em relação ao M15

O M15 decidiu que *"sobrando um candidato com um ano só, o sistema grava, porque escolha nenhuma
restou para fazer"*. **Essa decisão cai aqui**, e o motivo veio do uso: sobrar um candidato prova
que o casador eliminou os outros, e jamais que ele acertou este. Quem cadastrou o carro reconhece
o acabamento numa olhada; o casador só sabe o que o nome digitado repete. Gravar sem mostrar
tirava da pessoa exatamente a conferência que ela faz melhor — e, quando errava, deixava um preço
de outra versão na ficha sem ninguém ter visto passar.

A inteligência do M15 continua inteira. O que muda é o **destino** dela: em vez de decidir, ela
ordena a lista e assina o quanto confia. É a mesma linha do M11 e do M15 — o sistema sugere pela
presença, e quem decide dinheiro é a pessoa —, agora aplicada também ao caso de um candidato só.

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | as quatro decisões abaixo estão tomadas por escrito | — |
| **V1** | A nota | A acurácia de cada candidato, calculada dos mesmos sinais que já eliminam, e o recomendado por maioria estrita | dois candidatos empatados voltam **sem** recomendado, e o Renegade Longitude volta com nota cheia | — |
| **V2** | A busca sempre pergunta | O fim do `applied`: um candidato ou vinte, a resposta é a lista | o carro com um candidato só volta com ele na lista, com preço e código, e **nada** gravado | V1 |
| **V3** | Desvincular | O domínio, o comando e o `DELETE /api/vehicles/{code}/fipe` que apagam a consulta inteira | a ficha volta a "—" nos quatro campos, a rotina mensal deixa de alcançar o carro, e a auditoria registra | — |
| **V4** | O pop-up | O destaque do recomendado, o medidor ao lado de cada nome e o botão de desvincular na ficha | achando um só, o pop-up abre com ele marcado e o medidor visível; desvincular pede confirmação | V1–V3 |
| **V5** | O pátio de demonstração | Vinte carros em pátios variados, com lucro, prejuízo e casos de 1 e de muitos candidatos | subir a pilha limpa entrega os vinte, e a busca de cada um cai na faixa que o plano previu | V2 |
| **V6** | Fechamento | Suíte verde, `MARCOS.md`, `endpoints.md`, o manual e o binário conferido no contêiner | `dotnet test` verde e o símbolo novo achado dentro do contêiner | V1–V5 |

## Decisões (V0)

**1. O pop-up abre sempre, e o `applied` deixa de existir.**

A resposta da busca passa a ter **uma** forma: a lista do que sobrou. Um candidato é uma lista de
um, e ela abre o pop-up como qualquer outra. O campo `applied` sai do contrato em vez de virar um
campo que jamais vem preenchido — um campo morto no JSON é uma pergunta a mais para quem lê a
API daqui a seis meses.

O carro que **já tem código** segue como está: *Consultar agora* pergunta o preço direto, sem
pop-up. A busca existe para achar o modelo, e esse carro já achou o dele.

**2. A nota é dos sinais que já eliminam, e ela jamais decide.**

A acurácia sai dos mesmos sinais que o M15 usa para eliminar — os termos da versão, o câmbio e o
combustível —, mais o ano conferido na fonte, dita como fração do que **havia para conferir**:

```
versão      = 4 × (termos achados ÷ termos do carro), e 0 quando o carro veio sem versão
ano         = 2 quando a tabela precifica este modelo no ano do carro
câmbio      = 1 quando o nome confirma o câmbio do carro
combustível = 1 quando a tabela escreve esse combustível e o nome o traz

acurácia    = (versão + ano + câmbio + combustível) ÷ (4 + 2 + 1 + [combustível])
```

O peso da versão é **fixo**, e vale mesmo no carro que veio sem versão nenhuma. É a diferença
entre *"o quanto do carro foi conferido"* e *"o quanto do que foi digitado bateu"*: o Gol
cadastrado só como `Gol` bateria tudo o que havia para bater, e devolveria vinte candidatos
marcando 100% — a única leitura que esta tela jamais pode dar. Sem versão, a nota fica em 50%, e
a lista inteira empata ali: exatamente o caso em que ninguém deve ser recomendado.

O ano vale por dois porque é o descarte mais forte que existe, e porque um candidato que voltou
**sem** ano é justamente o que a tela precisa mostrar como o mais frágil da lista. Câmbio e
combustível valem um cada: confirmam sem distinguir, porque metade da tabela é flex.

O **recomendado** é o de maior nota, e **apenas** quando ele é maior que o segundo. Empate volta
sem recomendado nenhum — a mesma regra do M15, agora dita em nota: onde o sistema empata, ele
pergunta em vez de apontar.

E a nota **jamais** grava nada. Ela ordena, destaca e explica; o botão que escreve é o da pessoa.

**3. Desvincular apaga a consulta inteira.**

Código, ano-combustível, valor, mês e origem saem juntos. Metade disso seria pior do que nada: o
valor veio do modelo que está sendo desfeito, e deixá-lo na ficha manteria um preço sem nada que
o explique, ainda por cima alimentando o painel de custo e a projeção de sobra.

Depois de desvincular, o carro fica como recém-cadastrado para a tabela: a rotina mensal deixa de
alcançá-lo (ela só toca em carro com código), o botão volta a procurar o modelo, e a auditoria
guarda quem desfez e quando.

**4. Os vinte carros são de demonstração, e vivem atrás do mesmo interruptor dos usuários.**

Eles nascem com `RevendaPro:SeedDemoVehicles`, ligado apenas na pilha de desenvolvimento — o
irmão do `SeedDemoUsers`, que já existe. Produção sobe sem nenhum deles.

> O V0 chamou esse interruptor de `SeedDemoData`. Virou `SeedDemoVehicles` na implementação,
> para ficar do lado do `SeedDemoUsers` com o mesmo nível de precisão: cada um liga o que o
> nome diz, e um interruptor genérico acabaria ligando coisas que ninguém pediu.

O pátio de demonstração precisa mostrar quatro coisas ao mesmo tempo, e por isso a lista é
escolhida, e jamais sorteada:

| O que precisa aparecer | Como os vinte cobrem |
|---|---|
| **Um candidato só** | Nome e versão que a tabela escreve de um jeito só — `Corolla 2.0 XEi`, `Renegade 1.8 Longitude` |
| **Dez a vinte candidatos** | Nome genérico e versão vazia — `Gol`, `Uno`, `Onix`, `Palio` —, que é o carro que o vendedor cadastra com pressa |
| **Lucro e prejuízo** | Vendidos acima e abaixo do custo somado, com gasto de reparo entrando na conta |
| **Pátios variados** | A loja, a oficina parceira, o pátio de repasse e o consignado |

## O que fica de fora deste marco

- **Aprender com a escolha.** "Toda vez que alguém escolheu Renegade 1.8 Longitude, foi esta
  linha" continua sendo outra tabela, com outro assunto — e agora vale ainda mais, porque toda
  escolha passa a ser da pessoa e fica registrada.
- **Casar por chassi.** Segue exigindo uma segunda fonte, paga.
- **Rodar o casador na rotina mensal.** A rotina continua alcançando só carro com código, e agora
  isso tem uma consequência nova e desejada: desvincular tira o carro dela.
- **Mexer em preço.** Nada aqui toca *Quero receber*, *Mínimo aceito* ou *Anunciado*.
