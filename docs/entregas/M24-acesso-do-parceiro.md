# M24 — O acesso do parceiro ao próprio pátio

Entregue em 9 de setembro de 2026. Plano em `docs/plans/m24-acesso-do-parceiro.md`.

## O que este marco resolve

> *"O Rodrigo tem o pátio particular dele, que anuncia, e ele deixa outros carros em outras
> revendas."*

O dono da outra revenda passa a entrar no sistema e ver **só os carros que estão com ele**.

Até aqui o sistema tinha **uma** fronteira de segurança: a que separa empresas, com o `IdTenant`
em toda consulta, provada no M12 com duas revendas montadas pelo próprio sistema. Esta é a
primeira fronteira **dentro** da mesma empresa — e é por isso que ela foi tratada como um marco
próprio, e não como um item de cadastro. Fecha a pendência 3.1, aberta no M12 e repetida no M14.

## As regras

### O vínculo com o pátio é da pessoa, e jamais do perfil

`User.IdYard`, nulo em todo mundo que já existe.

**Por que é essa:** são eixos diferentes. O perfil diz **o que se pode abrir**; o pátio diz **o
que se enxerga dentro do que foi aberto**. Dois parceiros têm o mesmo perfil e pátios diferentes,
e um perfil por pátio faria a revenda criar um perfil novo a cada loja — e conceder telas de
novo, uma a uma, toda vez. Nulo como padrão é o que garante que este marco **não muda nada** para
quem já usa o sistema, e é por isso que a migration não tem `UPDATE` nenhum.

### A restrição é lida do banco a cada requisição, e jamais do token

Ela entrou no mesmo serviço que já resolve as telas, com o mesmo cache e a **mesma invalidação**:
salvar o usuário derruba as duas.

**Por que é essa:** uma claim no JWT faria a mudança esperar o token expirar — até quinze
minutos. Uma fronteira de segurança desatualizada por quinze minutos é uma fronteira que **já
vazou**, e seria mais fraca do que a fronteira de telas que o sistema já tinha. Um teste prova
isso pelo lado difícil: o parceiro **entra antes** de ser preso ao pátio, e a restrição já vale
na chamada seguinte, com o mesmo token na mão.

### A fronteira mora onde ninguém pode esquecê-la

Duas peças, e **nenhuma delas é a disciplina de quem escreve o próximo handler**:

1. **O repositório de veículo** aplica sozinho o pátio de quem está chamando, nas duas portas por
   onde a ficha inteira entra — a busca por código e a listagem.
2. **O middleware** recusa com 403 tudo o que não estiver numa lista curta e declarada.

**Por que é essa:** o M12 encontrou oito handlers que liam por código sem filtrar a empresa — e
um deles **já conferia**, porque alguém tinha visto o risco naquele caminho e corrigido só ali.
A lição ficou escrita: uma regra que vive na disciplina já falhou em algum lugar, só ninguém sabe
ainda em qual. Aqui, um handler novo escrito daqui a seis meses por alguém que jamais leu este
documento **nasce filtrado**.

### A lista do middleware é uma permissão, e não uma proibição

`POST` de login/refresh/logout, `GET` da sessão, `GET` da listagem, `GET` da ficha de um carro e
`GET` das fotos dele. Cada linha diz por que está lá.

**Por que é essa:** o endpoint escrito amanhã **nasce recusado** para o parceiro, e só passa
quando alguém escrever que ele pode. Uma lista de proibições envelhece em silêncio — fica verde
justamente sobre o que ninguém conferiu —, que é exatamente o defeito que o M12 provou existir.

### O carro de outro pátio responde 404, e jamais 403

**Por que é essa:** é o que a isolação entre empresas já faz (RNF-04): para quem está preso a um
lugar, o carro que está em outro **simplesmente não existe**. Um 403 confirmaria que aquele
código é de um carro de verdade, e um parceiro curioso enumeraria o estoque inteiro contando as
recusas.

### Na listagem, a restrição vence o filtro

**Por que é essa:** ignorar o pedido é mais gentil do que devolver lista vazia, e igualmente
seguro — ela jamais enxerga outro pátio de qualquer forma.

### O parceiro vê o carro como o comprador vê

Fotos, dados, situação, tabela FIPE e **preço anunciado**. Ficam de fora: compra, fornecedor,
forma de pagamento, teto de orçamento, custo, sobra, *quero receber*, *mínimo aceito* e as
anotações.

**Por que é essa:** é a mesma lista do M19, quando a *ficha para venda* foi desenhada — *"é um
papel para o comprador"*. O parceiro está do lado de fora da revenda: quanto o Rodrigo pagou no
carro e quanto ele quer tirar dele são o **poder de barganha do Rodrigo**, e mostrá-los ao dono
da loja que vai negociar com ele é entregar a mesa.

### O corte do dinheiro é um parâmetro sem valor padrão

**Por que é essa:** são três chamadores do mapeador, e cada um passou a ter de **decidir** — em
vez de herdar em silêncio a decisão de quem escreveu o método. É a lição do `DaysInStock`, no
M13: um padrão silencioso é o que faz todo chamador novo repetir o defeito. O compilador recusou
o código até os três dizerem o que queriam.

### O perfil Parceiro nasce com `vehicles` e `my-account`, e sem o painel

**Por que é essa:** o painel soma **capital parado do estoque inteiro**, e mesmo recortado por
pátio responde uma pergunta que é do dono da revenda. Cortar por tela é o que reduz a superfície
da fronteira nova a **um** lugar: os endpoints do veículo.

### O parceiro lê, e escreve nada

**Por que é essa:** escrita pede uma segunda rodada de perguntas — *o gasto do parceiro entra no
custo do Rodrigo?* —, e a fronteira de leitura é a que foi pedida. Fica para quando alguém
precisar.

### A tela esconde exatamente o que a API recusa — e o reconhece pelo que o servidor deixou de mandar

Sem bloco de custo, a ficha mostra só a aba de fotos, sem os botões de ação e sem o enviar; a
listagem perde o cadastrar, as planilhas e o capital parado.

**Por que é essa:** deduzir da própria resposta, em vez de um sinalizador à parte, é o que faz a
tela e a API **jamais discordarem**. E esconder é apresentação, e nunca a guarda: quem guarda é a
API, e ela recusa do mesmo jeito para quem digitar o endereço na barra do navegador — a mesma
regra do menu, desde o marco de acesso.

## Como usar

*Administração → Usuários → Novo usuário*: perfil **Parceiro**, e no campo **Pátio** escolha o
lugar. O padrão é *O pátio inteiro*, que é o que todo mundo era antes deste marco. Para soltar
alguém, volte o campo para *O pátio inteiro* — vale na requisição seguinte.

## O que mudou por baixo

- `User.IdYard` com chave **restrict**, a migration sem `UPDATE`, e o campo na tela de Usuários.
- `IPermissionService.GetYardRestrictionAsync`, o `YardScopeMiddleware`, e `ICurrentUser.IdYard`.
- O repositório de veículo recebendo quem está chamando; `FindVehicleByCodeQuery` com a restrição
  no `WHERE`.
- `VehicleMapper.ToDto` com o corte do dinheiro; `VehicleDto.Cost` e `PurchasePrice` passaram a
  ser nulos.
- O perfil **Parceiro** no semeador, e o pátio no `SessionUserDto`.
- O guarda de colunas do M18 passou a cobrir `User`: uma lista de colunas esquecida faria o
  `IdYard` ser gravado e jamais lido — e a fronteira deste marco simplesmente não existiria, em
  silêncio.

## O que foi conferido

Com a API no ar, e um parceiro de verdade: ele vê **só os três carros do pátio dele**, leva
**404** no quarto, recebe a ficha **sem o dinheiro da casa** — e a mesma ficha continua inteira
para a revenda, que é o guarda contra o teste passar em falso —, e leva **403** em vinte e um
endereços listados à mão, em português, incluindo toda escrita e a planilha. Soltá-lo do pátio
vale na requisição seguinte, com o mesmo token.

A lista de recusas é **escrita à mão**, de propósito. Derivá-la da lista do middleware seria
escrever o teste com a mesma frase que ele deveria conferir: os dois ficariam verdes juntos no
dia em que a lista crescesse por engano. É a mesma razão da segunda lista do M12.

No navegador: o menu do parceiro com **Veículos** e nada mais, a listagem com os três carros e
sem os botões que a API recusa, e a ficha com a aba de fotos apenas.

Suíte: **808 verdes**.

## O que ficou de fora, e por quê

- **O parceiro escrevendo** qualquer coisa — gasto, foto, proposta, situação.
- **Vários pátios para a mesma pessoa**: uma pessoa, um lugar. É o que a operação descreve, e uma
  tabela de ligação abriria um estado que ela não tem — a mesma decisão do carro no M14.
- **Um login separado, fora da revenda**: o parceiro é um usuário da revenda com o alcance
  reduzido; um cadastro próprio seria uma segunda base de identidade para manter.
- **Relatório e planilha do parceiro**: ele lê a tela; exportar é decisão seguinte, e por isso a
  planilha está entre as recusas.
- **Avisar o parceiro** quando um carro chega ou sai do pátio dele.
- **Um teste de unidade do corte do dinheiro**: o mapeador é `internal`, e torná-lo público só
  para o teste seria mexer no desenho por causa da ferramenta — a mesma escolha do M22. O corte é
  provado com a API no ar, campo por campo, que é mais forte.

## Pendente

A publicação no servidor da rede continua aberta, junto com a de M19 a M23.
