# Plano — M23: A lixeira — devolver o que foi excluído por engano

Fonte: a sequência combinada com o stakeholder em 8 de setembro de 2026, e a pendência
`docs/PENDENCIAS.md` 3.2, aberta desde o M9.

> *"Recuperar veículo e gasto excluídos."* — o terceiro da lista, depois de Clientes e Caixa.

## O que a entrega precisa provar

> O Eduardo apaga o Fox por engano — clicou na lixeira do carro errado. Ele abre **Lixeira**,
> vê o Fox lá com a data em que sumiu e quem o apagou, clica em **Devolver**, e o carro volta
> ao pátio com as fotos, os gastos, os documentos e a linha do tempo intactos. Na mesma tela,
> um gasto de R$ 3.200 apagado semana passada volta para a ficha do Corolla. E quando alguém
> tenta devolver um carro cuja placa já foi cadastrada de novo, o sistema recusa e diz qual
> carro está com ela.

## O terreno

| Peça | Como está hoje |
|---|---|
| A exclusão | **Lógica em tudo** (RNF-08): a linha fica, com `IsActive = 0`, `DtDeleted` e `DeletedBy`. `Activate(actor)` é o caminho de volta, e já existe na base |
| O caminho de volta | Existe para **documento** (tela *Documentos excluídos*, desde o M9) e para **usuário** (em Usuários). Para carro e gasto, só pelo banco |
| Por que o documento veio antes | O arquivo continuava **pago e parado no bucket**, inalcançável. Carro e gasto excluídos ficam apenas invisíveis, e a linha está lá — por isso a fila era outra |
| Excluir um carro | Apaga **só a linha do carro**. Fotos, gastos, documentos e histórico ficam ativos e somem junto, porque toda consulta deles passa pelo carro e filtra `v.IsActive = 1` |
| Excluir um gasto | Apaga só o gasto |
| A placa | Conferida **por consulta**, com `IsActive = 1` — e jamais por índice único, de propósito. A placa de um carro excluído lê como livre, e pode ser cadastrada de novo |
| A tela | `deleted-documents`, em Administração, só para o Administrador: ela mostra o que toda outra leitura esconde (ADR-0002) |
| Apagar de vez | **Não existe, de propósito**, e continua sem existir |

## A pergunta que decide o desenho

**Uma lixeira por tipo, ou uma lixeira só?**

Uma só. Quem apagou por engano tem uma pergunta — *"onde está o que eu apaguei?"* — e três
telas seriam três lugares para procurar a mesma coisa. A tela *Documentos excluídos* cresce
para **Lixeira**, com uma aba por tipo, e a chave da tela continua `deleted-documents`: mudar
a chave desativaria a tela antiga e criaria outra, e toda revenda perderia a permissão que já
concedeu.

E **devolver o carro devolve a ficha inteira**, sem tocar em linha nenhuma dela. É consequência
do desenho que já existe: as fotos, os gastos e os documentos sumiram porque a consulta deles
passa pelo carro, e voltam pelo mesmo motivo. O que foi apagado **antes**, um por um, continua
apagado — e é isso que se espera.

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | as decisões abaixo estão tomadas por escrito | — |
| **V1** | A lixeira enxerga | Consultas que leem o excluído de veículo e de gasto; `DeletedItemDto` com tipo, o que era, quando sumiu e quem apagou; `GET api/trash?kind=`; a tela **Lixeira** com uma aba por tipo, ainda só lendo | o Fox apagado aparece na Lixeira com a data e o nome de quem apagou, e a outra revenda enxerga nada | — |
| **V2** | Devolver | `POST api/trash/{kind}/{code}/restore`: o carro volta com a ficha inteira, o gasto volta para a ficha do carro; as duas recusas — placa ou chassi já em uso, e gasto de carro que continua excluído — com o motivo na tela; auditoria em cada volta | devolver o Fox traz fotos, gastos e documentos junto; devolver um carro cuja placa foi reusada responde 422 dizendo qual carro está com ela | V1 |
| **V3** | Fechamento | Documento de entrega em `docs/entregas/`, `MARCOS.md`, `ROADMAP.md`, `endpoints.md`, o manual, e o merge na `main` por pull request | `dotnet test`, `npm run build` e `docker compose up --build` passam, e a skill `fechar-marco` foi seguida | V2 |

## Decisões (V0)

**1. Uma tela só, chamada Lixeira, com a chave que já existe.**

`deleted-documents` continua sendo a chave; o rótulo passa a ser **Lixeira** e o conteúdo
cresce. Trocar a chave por `trash` faria o sincronizador desativar a tela antiga e criar uma
nova, e toda revenda que já concedeu *Documentos excluídos* a alguém perderia a concessão sem
saber. O nome interno envelhecido é um preço menor do que uma permissão perdida — e fica
escrito aqui para quem estranhar o `deleted-documents` no código.

**2. Devolver o carro devolve a ficha inteira, e nada mais.**

Excluir um carro apaga só a linha dele; a ficha some porque toda consulta dela passa pelo
carro. Devolver a linha devolve tudo junto, sem percorrer filho nenhum — e o que tinha sido
apagado antes, um a um, continua apagado. Percorrer os filhos para "reativar tudo" ressuscitaria
a foto que alguém apagou de propósito na semana passada.

**3. A placa é a única recusa do carro, e ela diz o nome do culpado.**

A placa de um carro excluído lê como livre, e pode ter sido cadastrada de novo. Devolver o
antigo criaria dois carros ativos com a mesma placa — o que o cadastro recusa desde o M6. A
volta é recusada com a placa **e o carro que está com ela**: "A placa ABC1D23 já é do Fiat Uno
2015". Quem quiser mesmo o antigo de volta troca a placa do novo, e é uma decisão da pessoa.

**4. O gasto só volta para um carro que esteja no pátio.**

Um gasto devolvido a um carro excluído voltaria invisível — ativo no banco e ausente de toda
tela —, que é a pior das duas hipóteses. A recusa diz o que fazer: "O Fiat Uno está na
lixeira. Devolva o carro primeiro." A tela mostra o carro de cada gasto, para a ordem ser
óbvia antes do clique.

**5. A lixeira mostra quando sumiu e quem apagou.**

`DtDeleted` e `DeletedBy` já existem em toda linha. Sem eles a lista é um monte de coisas sem
história; com eles a pergunta "quem apagou o Fox?" tem resposta, e a exclusão deixa de ser
anônima. A ordem é da exclusão mais recente para a mais antiga, porque o engano de hoje é o
que se procura.

**6. Apagar de vez continua sem existir.**

A ausência é de projeto, desde o M9: o negócio pediu para guardar, o arquivo jamais saiu do
bucket, e um apagar definitivo desfaria a recuperação administrativa do RNF-08. Uma tela de
lixeira é a tentação óbvia para "esvaziar"; ela segue sem esse botão, e o motivo fica escrito.

**7. Só o Administrador, como já é.**

A tela mostra o que toda outra leitura do sistema esconde (ADR-0002), e por isso nasce só para
o Administrador — como *Documentos excluídos* nasceu. Quem quiser dar a chave a outro perfil
faz isso em Perfis, de propósito e por escrito.

**8. Um endpoint por tipo, com a mesma forma.**

`GET api/trash?kind=` e `POST api/trash/{kind}/{code}/restore`, com `kind` sendo veículo, gasto
ou documento. Uma porta só, com o tipo dizendo em qual entidade bater — é o mesmo desenho da
baixa do caixa no M22, e é o que permite a quarta e a quinta coisa entrarem na lixeira sem
tela nova nem rota nova. O endpoint de documento que já existe continua respondendo, para
quem tiver o endereço antigo.

## O que muda em cada camada

| Camada | Muda |
|---|---|
| **Domain** | Nada de entidade nova. `IVehicleRepository` e `IVehicleExpenseRepository` ganham a leitura do excluído (`ListDeletedAsync`, `GetByCodeIncludingDeletedAsync`), e o veículo ganha a conferência de identificador **entre os ativos**, que já existe, usada agora na volta |
| **Infrastructure** | Consultas em `Queries/Vehicles` que leem `IsActive = 0` — e que o `SoftDeleteTests` conhece como exceção declarada, ao lado das que já existem para documento |
| **Application** | `Trash/`: `ListDeletedItemsQuery(kind)`, `RestoreDeletedItemCommand(kind, code)`, com as duas recusas; `DeletedItemDto` |
| **Api** | `TrashController` (`api/trash`), guardado por `deleted-documents`. O `DeletedDocumentsController` fica, delegando |
| **Frontend** | `deleted-documents` vira **Lixeira**: abas Veículos, Gastos e Documentos, cada linha com o que era, quando sumiu, quem apagou, e **Devolver** |
| **Testes** | Unidade: a recusa da placa reusada, a recusa do gasto de carro excluído, e a volta que jamais toca nos filhos. Integração: apagar um carro com gasto e foto, devolvê-lo, e conferir que a ficha voltou inteira; a outra revenda enxergando nada |
| **Docs** | O documento de entrega, `endpoints.md`, `MARCOS.md`, `ROADMAP.md`, o manual, e a baixa da pendência 3.2 |

## O que fica de fora deste marco

- **Apagar de vez** (decisão 6).
- **Devolver cliente, fornecedor, pátio e tipo de gasto**: os quatro recusam exclusão quando
  têm história, então o engano possível é pequeno; entram na mesma tela quando alguém precisar.
- **Devolver a venda cancelada**: cancelar uma venda já é a porta dela, e ela devolve o carro
  para a esteira — outra história, com outra regra.
- **Prazo de retenção** ("some da lixeira depois de 90 dias"): guardar é o que o negócio pediu,
  e um prazo é uma decisão do dono, e não do sistema.
- **Desfazer em lote**: devolver um por um é o que o engano pede; em lote é o que o engano
  seguinte pede.

---

## O que a implementação acrescentou

Escrito depois do V2, com o que o plano ainda não sabia.

**A rota também ficou.** A decisão 1 falava só da chave; na implementação a rota
`/deleted-documents` ficou junto, pelo mesmo raciocínio levado um passo adiante: trocá-la
quebraria o endereço já salvo em algum navegador, e o ganho seria cosmético. O nome interno
envelhecido está explicado em três lugares — o catálogo, o controlador e a página.

**O chassi recusa como a placa.** A decisão 3 falava só da placa. O chassi é único pela mesma
consulta e pelo mesmo motivo, então a recusa cobre os dois e **diz qual dos dois** está ocupado.

**A recusa da placa aponta a saída.** Nomear o culpado não bastava: sem dizer o que fazer, a
mensagem é uma parede. Ela termina em *"Troque o identificador desse carro para devolver este"*.

**A lista de gastos lê o carro sem filtro, de propósito.** Só assim um gasto de um carro que
também está na lixeira aparece — e é ele que precisa aparecer, porque é a linha que ensina a
ordem. O `SoftDeleteTests` recebeu esse motivo por escrito.

**O guarda de colunas do M18 ganhou uma lista de exceções declaradas.** A consulta de gastos
apagados projeta em `DeletedVehicleExpense` e não materializa a entidade, então ela seria
reprovada por ler poucas colunas. Renomeá-la para escapar do teste seria exatamente o defeito
que o M12 documentou — mudar a etiqueta e ficar verde —, então a exceção entrou com o motivo
escrito, como o `SoftDeleteTests` já fazia.

**A volta do documento reaproveita o caminho do M10.** O handler da lixeira despacha o comando
que já existia, em vez de repetir a leitura, a conferência de revenda e a auditoria. Dois
caminhos de escrita para a mesma linha seriam duas regras para manter em dia.

**A tela carrega cada aba no clique, e sem `useEffect`.** Foi o que a manteve fora dos avisos de
`react-hooks/set-state-in-effect` que o resto do frontend ainda acumula. Uma devolução descarta
o que foi guardado das três abas, porque devolver um carro muda a aba de gastos junto.

**Um teste antigo cobrou a limpeza.** Os testes que devolvem o carro o deixavam no pátio
compartilhado, e dois testes afirmam que o pátio começa vazio. O encerramento do teste passou a
reapagar o carro.

**Números do fechamento:** 797 testes verdes, 483 de unidade e 314 de integração. Seis consultas
novas, cinco delas lendo linha excluída de propósito.
