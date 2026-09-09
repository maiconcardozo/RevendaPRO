# M23 — A lixeira: devolver o que foi excluído por engano

Entregue em 9 de setembro de 2026. Plano em `docs/plans/m23-lixeira.md`.

## O que este marco resolve

Toda exclusão deste sistema sempre foi **lógica**: a linha fica na tabela, com o dia e o código
de quem apagou (RNF-08). Faltava a porta de volta. O documento tinha a dele desde o M10, porque o
arquivo continuava **pago e parado no bucket**; o carro e o gasto ficavam apenas invisíveis, e
voltar era mexer no banco.

> O Eduardo apaga o Fox por engano — clicou na lixeira do carro errado. Ele abre **Lixeira**, vê
> o Fox lá com a data em que sumiu e quem o apagou, clica em **Devolver**, e o carro volta ao
> pátio com as fotos, os gastos, os documentos e a linha do tempo intactos.

## As regras

### Uma lixeira só, com uma aba por tipo

A tela *Documentos excluídos* cresceu para **Lixeira**, com as abas Veículos, Gastos e
Documentos.

**Por que é essa:** quem apagou por engano tem **uma** pergunta — *"onde está o que eu
apaguei?"* — e três telas seriam três lugares para procurar a mesma coisa. Ninguém procura "o
gasto que eu apaguei"; procura o que sumiu.

### A chave da tela continua sendo `deleted-documents`

O rótulo virou Lixeira, o conteúdo cresceu, e a chave de permissão — e a rota — ficaram as de
antes.

**Por que é essa:** trocar a chave por `trash` faria o sincronizador **desativar a tela antiga e
criar outra**, e toda revenda que já concedeu *Documentos excluídos* a alguém perderia a
concessão sem saber. O nome interno envelhecido é um preço menor do que uma permissão perdida — e
fica escrito no catálogo, no controlador e na página, para quem estranhar o `deleted-documents`
no código. A subida confirmou: `0 inserted, 1 updated`, ou seja, só o rótulo mudou.

### Devolver o carro devolve a ficha inteira — e nada mais

A volta reativa **só a linha do carro**. As fotos, os gastos, os documentos e a história voltam
junto; o que tinha sido apagado antes, um a um, continua apagado.

**Por que é essa:** é consequência do desenho que já existe. Excluir um carro apaga só a linha
dele; a ficha some porque **toda consulta dela passa pelo carro**, e volta pelo mesmo motivo.
Percorrer os filhos para "reativar tudo" ressuscitaria a foto que alguém tirou da ficha de
propósito na semana passada — e ninguém entenderia por quê. Um teste de unidade segura essa
frase.

### A placa é a única recusa do carro, e ela diz o nome do culpado

*"A placa ABC1D23 já é do Fiat Uno 2015. Troque o identificador desse carro para devolver este."*

**Por que é essa:** a conferência de identificador é **por consulta** desde o M6, e jamais por
índice único, porque a linha excluída fica na tabela — um índice recusaria uma placa que voltou a
estar livre. Devolver da lixeira é o momento em que essa escolha cobra o preço: a placa do carro
apagado pode ter sido cadastrada de novo enquanto ele esteve fora, e devolvê-lo criaria dois
carros ativos com a mesma placa. A recusa **nomeia o culpado** porque uma recusa sem saída é uma
parede: quem quiser mesmo o antigo de volta troca a placa do novo, e a decisão é da pessoa. O
chassi recusa do mesmo jeito, dizendo que é o chassi.

### O gasto só volta para um carro que esteja no pátio

*"O Volkswagen Fox está na lixeira. Devolva o carro primeiro."*

**Por que é essa:** um gasto devolvido a um carro excluído voltaria **invisível** — ativo no
banco e ausente de toda tela —, que é a pior das duas hipóteses: o sistema diria que deu certo, e
a pessoa procuraria o gasto para sempre. A tela mostra o carro de cada gasto, e escreve *"Este
carro está na lixeira"* na linha, para a ordem ser óbvia **antes** do clique.

### A lixeira mostra quando sumiu e quem apagou

Da exclusão mais recente para a mais antiga.

**Por que é essa:** `DtDeleted` e `DeletedBy` já existiam em toda linha, e sem eles a tela seria
um monte de coisas sem história. Com eles, *"quem apagou o Fox?"* tem resposta, e a exclusão
deixa de ser anônima. A ordem é essa porque **o engano de hoje é o que se procura**.

### Apagar de vez continua sem existir

**Por que é essa:** a ausência é de projeto desde o M9 — o negócio pediu para guardar, o arquivo
jamais saiu do bucket, e um apagar definitivo desfaria isso e a recuperação administrativa da
RNF-08. Uma tela de lixeira é a tentação óbvia para um botão "esvaziar"; ela segue sem ele, e o
motivo está escrito no controlador para quem for tentado a acrescentá-lo.

### Só o Administrador, como já era

**Por que é essa:** a tela mostra o que **toda outra leitura do sistema esconde** (ADR-0002), e
por isso nasceu só para o Administrador — como *Documentos excluídos* nasceu. Quem quiser dar a
chave a outro perfil faz isso em Perfis, de propósito e por escrito.

### Uma porta só, com o tipo dizendo em qual tabela bater

`GET api/trash?kind=` e `POST api/trash/{kind}/{code}/restore`.

**Por que é essa:** é o mesmo desenho da baixa do caixa no M22, e é o que permite a quarta e a
quinta coisa entrarem na lixeira **sem tela nova nem rota nova**. O endereço antigo
(`api/deleted-documents`) continua respondendo, para quem o tiver salvo — e a volta do documento
passa pelo caminho do M10, que a lixeira reaproveita: dois caminhos de escrita para a mesma linha
seriam duas regras para manter em dia.

## Como usar

*Administração → Lixeira*. Escolha a aba, confira o que sumiu, e **Devolver**. Quando a recusa
aparecer, ela diz o que fazer em seguida.

## O que mudou por baixo

- `TrashKind`, os objetos de valor `DeletedVehicle` e `DeletedVehicleExpense`, e o
  `DeletedItemDto` — um formato só para os três tipos, porque a tela é uma.
- Seis consultas novas em `Queries/Vehicles`. **Cinco leem linha excluída de propósito**, e cada
  uma entrou no `SoftDeleteTests` com o motivo escrito; a sexta procura o dono atual da placa, e
  essa filtra `IsActive = 1` justamente porque é o pátio que ela precisa enxergar. Na listagem de
  gastos o carro vem **sem filtro**, também de propósito: é a coluna que diz à pessoa que o carro
  volta primeiro.
- `Application/Trash` com a leitura e a volta; `TrashController`; e a tela `TrashView`, que
  carrega cada aba no clique — sem `useEffect`, o que a manteve fora dos avisos do lint.
- Auditoria em cada volta, com `AuditAction.Activate`.
- O guarda de colunas do M18 ganhou uma **lista de projeções declaradas**, com o motivo por
  escrito, para que o nome de uma consulta jamais seja o jeito de escapar dele.

## O que foi conferido

Contra o MariaDB real: o Fox apagado aparece com o dia e o nome de quem apagou; devolver o carro
traz o gasto que ficou e **mantém fora** o que foi apagado à parte, com o custo somando de novo
os R$ 22.890; a placa reusada recusa dizendo de quem ela é; o gasto só volta depois do carro; e a
outra revenda leva **404** nas duas portas e enxerga nada nas três abas. No navegador, as três
abas, as duas recusas e as duas voltas. Suíte: **797 verdes**.

Um detalhe que só a suíte inteira mostrou: os testes que devolvem o carro o deixavam **no pátio
compartilhado**, e dois testes antigos afirmam que o pátio começa vazio. O encerramento do teste
agora reapaga o carro — a pilha volta ao estado em que foi encontrada.

## O que ficou de fora, e por quê

- **Apagar de vez**: ver a regra acima.
- **Devolver cliente, fornecedor, pátio e tipo de gasto**: os quatro **recusam exclusão quando
  têm história**, então o engano possível é pequeno. Entram na mesma tela quando alguém precisar,
  e é para isso que o endpoint tem um `kind`.
- **Devolver a venda cancelada**: cancelar uma venda já é a porta dela, e ela devolve o carro
  para a esteira — outra história, com outra regra.
- **Prazo de retenção** ("some da lixeira depois de 90 dias"): guardar é o que o negócio pediu, e
  um prazo é decisão do dono, e jamais do sistema.
- **Desfazer em lote**: devolver um por um é o que o engano pede; em lote é o que o engano
  seguinte pede.

## Pendente

A publicação no servidor da rede continua aberta, junto com a de M19 a M22.
