# Plano — M24: O acesso do parceiro ao próprio pátio

Fonte: a sequência combinada com o stakeholder em 8 de setembro de 2026, e a pendência
`docs/PENDENCIAS.md` 3.1, aberta no M12 e repetida no M14.

> *"O Rodrigo tem o pátio particular dele, que anuncia, e ele deixa outros carros em outras
> revendas."* — e o dono da outra revenda precisa entrar e ver **só os carros que estão com
> ele**.

## O que a entrega precisa provar

> O Joãozinho recebe um usuário da revenda do Rodrigo, preso à **Loja do Joãozinho**. Ele entra,
> e o menu tem **Veículos** e mais nada. A listagem mostra os três carros que estão no pátio
> dele — e jamais os outros vinte e sete. Ele abre a ficha de um deles e vê o carro como o
> comprador vê: fotos, dados, tabela FIPE e preço anunciado. **Custo, compra, sobra, fornecedor
> e anotações ficam de fora.** Digitando na barra do navegador o endereço de um carro que está
> em outro pátio, ele recebe **404** — o carro simplesmente não existe para ele.

## O terreno

| Peça | Como está hoje |
|---|---|
| A fronteira que existe | **Entre empresas**: `IdTenant` em toda consulta, provada no M12 com duas revendas montadas pelo próprio sistema |
| A fronteira que falta | **Dentro** da mesma empresa: o pátio |
| O pátio | Entidade desde o M14, com o carro apontando para um lugar por vez (`Vehicle.IdYard`) |
| A permissão | Por tela (ADR-0002), lida **do banco a cada requisição** pelo `RequireScreen` — e jamais do token |
| A porta do veículo | Todo handler que lê um carro chama `VehicleRepository.GetByCodeAsync(idTenant, code)`; a listagem chama `ListAsync(idTenant, …)`. São **duas portas**, e a ficha inteira entra por elas |
| A montagem do DTO | Um mapeador só, `VehicleMapper.ToDto`, com três chamadores |

## A pergunta que decide o desenho

**O vínculo com o pátio mora no perfil ou na pessoa?**

Na **pessoa**. O perfil diz *o que se pode abrir*; o pátio diz *o que se pode ver dentro do que
foi aberto*. São eixos diferentes: dois parceiros têm o mesmo perfil e pátios diferentes, e um
perfil por pátio faria a revenda criar um perfil novo a cada loja parceira — e conceder telas de
novo, uma a uma, toda vez.

**E a fronteira mora onde ninguém pode esquecê-la.**

O M12 encontrou oito handlers que liam por código sem filtrar a empresa, e um deles **já
conferia** — alguém viu o risco naquele caminho e corrigiu só ali. A lição foi escrita: uma regra
que vive na disciplina de cada handler já falhou em algum lugar, só ninguém sabe ainda em qual.
Por isso a restrição de pátio entra **dentro do repositório de veículo**, nas duas portas, e
jamais como um parâmetro que cada handler lembra de passar.

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | as decisões abaixo estão tomadas por escrito | — |
| **V1** | O vínculo | `User.IdYard`, o perfil de sistema **Parceiro** com `vehicles` e `my-account`, o campo Pátio na tela de Usuários, e o `IYardScope` lendo a restrição do banco a cada requisição | um usuário pode ser preso a um pátio pela tela, e o sistema sabe disso na requisição seguinte | — |
| **V2** | A fronteira | As duas portas do veículo filtrando por pátio dentro do repositório; a ficha do parceiro sem o dinheiro da casa; o 404 no carro de outro pátio; e a matriz provando com a API no ar | o parceiro vê três carros e leva 404 no quarto, e a ficha dele traz preço anunciado e FIPE, e valor de compra nenhum | V1 |
| **V3** | Fechamento | Documento de entrega, `MARCOS.md`, `ROADMAP.md`, `endpoints.md`, o manual, a baixa da pendência 3.1, e o merge na `main` por pull request | `dotnet test`, `npm run build` e `docker compose up --build` passam, e a skill `fechar-marco` foi seguida | V2 |

## Decisões (V0)

**1. O vínculo é da pessoa, e o pátio é opcional.**

`User.IdYard` nulo é o que todo mundo é hoje: quem enxerga o pátio inteiro. Um valor ali prende
a pessoa àquele lugar. Nulo como padrão é o que garante que este marco **não muda nada** para
quem já usa o sistema — e é o que permite a coluna nascer sem `UPDATE` de migração.

**2. A restrição é lida do banco a cada requisição, e jamais do token.**

O `RequireScreen` já faz assim desde o marco de acesso: a permissão vem da tabela, não do JWT.
Colocar o pátio numa claim faria a mudança demorar até quinze minutos para valer — o tempo de
vida do token —, e uma fronteira de segurança que fica quinze minutos desatualizada é uma
fronteira que já vazou. O `IYardScope` responde por requisição, e o valor é memorizado dentro
dela.

**3. A fronteira mora no repositório, nas duas portas do veículo.**

`GetByCodeAsync` e `ListAsync` aplicam a restrição sozinhos. Um handler novo, escrito daqui a
seis meses por alguém que jamais leu este documento, nasce filtrado. É a correção do M12 levada
um passo adiante: lá, ler por código passou a **pedir** a empresa; aqui, a leitura já **traz** o
pátio.

**4. O carro de outro pátio responde 404, e jamais 403.**

É o que a isolação entre empresas já faz (RNF-04): para quem está preso a um pátio, o carro que
está em outro **simplesmente não existe**. Um 403 confirmaria que aquele código é de um carro de
verdade, e um parceiro curioso enumeraria o estoque inteiro contando as recusas.

**5. Na listagem, a restrição vence o filtro.**

A tela de veículos tem um filtro por pátio desde o M14. Quando a pessoa está presa a um lugar, a
restrição sobrescreve o que o filtro pediu — e a tela dela mostra o seletor de pátio com um valor
só. Ignorar o pedido é mais gentil do que devolver lista vazia, e igualmente seguro: ela jamais
enxerga outro pátio de qualquer forma.

**6. O parceiro vê o carro como o comprador vê.**

Fotos, marca, modelo, versão, ano, quilometragem, cor, combustível, câmbio, placa, situação,
tabela FIPE e **preço anunciado**. Ficam de fora: compra, fornecedor, forma de pagamento, teto de
orçamento, custo, sobra, *quero receber*, *mínimo aceito*, anotações de mercado e anotações
internas.

**Por quê:** é a mesma lista do M19, quando a *ficha para venda* foi desenhada — *"é um papel
para o comprador"*. O parceiro está do lado de fora da revenda: quanto o Rodrigo pagou no carro e
quanto ele quer tirar dele são o poder de barganha do Rodrigo, e mostrá-los ao dono da loja que
vai negociar com ele é entregar a mesa.

**7. O corte do dinheiro é um parâmetro obrigatório do mapeador.**

`VehicleMapper.ToDto` ganha um parâmetro **sem valor padrão**. São três chamadores, e cada um
passa a ter de decidir — em vez de herdar em silêncio a decisão de quem escreveu o método. É a
lição do `DaysInStock`, no M13: um padrão silencioso é o que faz todo chamador novo repetir o
defeito.

**8. O perfil Parceiro nasce com `vehicles` e `my-account`, e mais nada.**

Sem `dashboard`: o painel soma **capital parado do estoque inteiro**, e mesmo filtrado por pátio
ele responde uma pergunta que é do dono da revenda. Sem `sales`, `cashflow`, `market`,
`customers`, `suppliers` — todos são a casa. Cortar por tela é o que reduz a superfície da
fronteira nova a **um** lugar: os endpoints do veículo.

**9. O parceiro lê, e escreve nada.**

Nem gasto, nem foto, nem proposta, nem mudança de situação. Escrita pede uma segunda rodada de
perguntas — *o gasto do parceiro entra no custo do Rodrigo?* —, e a fronteira de leitura é a que
foi pedida. O que ele pode escrever fica para quando alguém pedir.

**10. Trocar o pátio de uma pessoa fica na auditoria.**

Como toda mudança de usuário já fica. É a resposta para *"quem soltou o Joãozinho no pátio
inteiro?"*.

## O que muda em cada camada

| Camada | Muda |
|---|---|
| **Domain** | `User.IdYard` e `BindToYard`; `IYardScope` com a restrição do momento |
| **Infrastructure** | Migration com a coluna e a chave **restrict**; as duas consultas de veículo ganhando a restrição; `YardScope` lendo o usuário; o perfil **Parceiro** no `DbInitializer` |
| **Application** | O pátio no comando e no DTO de usuário; o corte do dinheiro no `VehicleMapper` |
| **Api** | Nada de rota nova: a fronteira é de dado, e não de endereço |
| **Frontend** | O campo **Pátio** no formulário de usuário; a ficha e a listagem escondendo o bloco de custo quando ele vem vazio |
| **Testes** | Unidade: o mapeador cortando o dinheiro, e a restrição vencendo o filtro. Integração: um parceiro de verdade, com a API no ar — a listagem dele, o 404 no carro do outro pátio, a ficha sem os campos da casa, e a lista curta de "jamais alcança" ganhando o perfil novo |
| **Docs** | O documento de entrega, `MARCOS.md`, `ROADMAP.md`, `endpoints.md`, o manual, e a baixa da pendência 3.1 |

## O que fica de fora deste marco

- **O parceiro escrevendo** qualquer coisa (decisão 9).
- **Vários pátios para a mesma pessoa.** Uma pessoa, um lugar: é o que a operação descreve, e uma
  tabela de ligação abriria um estado que ela não tem — a mesma decisão do carro no M14.
- **Um login separado, fora da revenda.** O parceiro é um usuário da revenda com o alcance
  reduzido; um cadastro próprio seria uma segunda base de identidade para manter.
- **Relatório e planilha do parceiro.** Ele lê a tela; exportar é decisão seguinte.
- **Avisar o parceiro** quando um carro chega ou sai do pátio dele.
