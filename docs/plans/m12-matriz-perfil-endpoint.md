# Plano — M12: A matriz perfil × endpoint, provada com a API no ar

Fontes: `docs/plans/acesso-e-menu.md` (o **A6**, que ficou pendente desde o marco de acesso),
`docs/architecture/decisions/ADR-0002-acesso-por-tela.md` e a linha que o próprio A6 escreveu
sobre este teste — *"é o teste que impede regressão de segurança"*.

O sistema tem hoje **63 endpoints** e **cinco perfis**. A guarda existe e é levada a sério:
todo endpoint declara a tela que o protege, e um teste percorre a montagem da API exigindo
essa declaração — inclusive dos endpoints criados amanhã.

**O que falta é a outra metade.** O `ApiGuardTests` prova que a fechadura está instalada. Ele
jamais prova que ela **tranca**: que o Vendedor recebe 403 ao chamar `/api/market`, que a
Oficina recebe 403 ao registrar uma venda, e — o que mais importa — que a revenda A jamais
alcança o veículo da revenda B.

## O que a entrega precisa provar

> Cinco perfis, 63 endpoints, uma tabela. Cada célula responde **403** ou **passou da
> guarda** — e a resposta bate com a tela que aquele perfil tem. Sem token, tudo responde
> **401**. E o veículo, a venda, o gasto e o documento da empresa B são **inalcançáveis** para
> quem está na empresa A, tanto por leitura quanto por escrita.

## O terreno

| Peça | Como está hoje |
|---|---|
| Enumeração dos endpoints | Já existe: `ApiGuardTests` percorre a montagem da API por reflexão, e acha toda ação de todo controlador |
| Telas de cada perfil | Vêm do banco, semeadas pelo `DbInitializer`, e a API as devolve em `/api/auth/me` |
| Usuários para testar | O sistema já semeia **um usuário por perfil** quando `RevendaPro__SeedDemoUsers` está ligado |
| Isolamento por empresa | Escrito em toda consulta (`IdTenant`), e coberto por teste de unidade em alguns pontos — jamais pela API inteira |
| Suíte | 272 testes, todos de unidade. **Nenhum sobe a API** |

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | as cinco decisões abaixo estão tomadas por escrito | — |
| **V1** | A pilha de teste | `WebApplicationFactory`, banco MariaDB em contêiner descartável, dublês da FIPE e do armazenamento, rotina do pátio desligada | `dotnet test` sobe a API, aplica as migrations, semeia e faz login como Administrador — sem pilha de desenvolvimento no ar | — |
| **V2** | A matriz | Todo endpoint × cinco perfis, mais o anônimo; a expectativa **derivada** das telas que cada perfil tem, e jamais escrita à mão | a matriz fecha verde nos 63 endpoints, e apagar um `[RequireScreen]` faz o teste quebrar | V1 |
| **V3** | O isolamento entre empresas | Duas revendas com dados próprios; leitura e escrita cruzadas em veículo, gasto, foto, documento, proposta e venda | a empresa A recebe 404 em tudo que é da B, e as listagens da A jamais trazem linha da B (RNF-04) | V1 |
| **V4** | Fechamento | Suíte verde, `MARCOS.md`, `ROADMAP.md` e o plano de acesso atualizados; o A6 sai de pendente | `dotnet test` passa numa máquina limpa, e o A6 deixa de existir como dívida | V2, V3 |

## Decisões (V0)

**1. O banco é de verdade, e vive num contêiner descartável.**

Banco em memória está fora de questão, e o motivo é o desenho do sistema: o acesso a dado é
**Dapper com SQL escrito à mão** (ADR-0003), com crase em palavra reservada, `INTERVAL`,
`DATEDIFF` e `DATE_FORMAT`. Um SQLite responderia a um SQL que não é o nosso, e o teste
passaria enquanto a produção quebra — que é pior do que não ter teste.

A pilha de desenvolvimento também está fora: se o teste depender dela no ar, `dotnet test`
deixa de ser um comando só e passa a ser um procedimento. O banco sobe **em contêiner, pelo
próprio teste**, e morre com ele. Docker já é pré-requisito de tudo neste projeto.

**2. A matriz afirma a guarda, e jamais o comportamento.**

Cada célula responde uma pergunta só: **403 ou passou?** Quando o perfil tem a tela, a
resposta pode ser 200, 400, 404 ou 422 — todas significam a mesma coisa aqui: *a autorização
deixou passar*. Exigir 200 obrigaria cada célula a montar um corpo válido e um id existente,
e transformaria um teste de segurança em 63 testes de negócio mal escritos.

Consequência boa: como os ids são **aleatórios** e os corpos **vazios**, a matriz jamais cria
nem destrói uma linha. Ela bate na fechadura, e não entra na casa.

**3. A expectativa é derivada, e não escrita à mão.**

A tabela do que cada perfil pode fazer **não** entra no teste como uma lista. O teste entra
como cada perfil, pergunta ao próprio sistema quais telas ele tem (`/api/auth/me`), lê a tela
que cada endpoint declara, e cruza os dois.

Uma lista escrita à mão envelhece no primeiro endpoint novo, e envelhece em silêncio —
passando verde justamente onde deveria falhar. Derivando, o endpoint criado amanhã já entra na
matriz no dia em que nasce.

**3-A. A matriz derivada tem um limite, e ele é coberto à mão.**

*Descoberto ao construir o V2, e provado por mutação.*

A expectativa derivada prova que **a fechadura combina com a própria etiqueta**: quem tem a
tela passa, quem não tem leva 403. Ela **não** prova que a etiqueta é a certa. Trocar
`[RequireScreen("market")]` por `[RequireScreen("vehicles")]` deixa a matriz inteira verde e
abre a tela de Mercado para o Vendedor e para a Oficina, em silêncio — conferido, e foi
exatamente o que aconteceu.

Por isso existe uma segunda lista, **curta e escrita à mão**, que declara em português o que
cada perfil jamais pode alcançar: fechar venda, ler o resultado das vendas, ver a tela de
Mercado, administrar usuário ou perfil, abrir a tela de documentos excluídos e mexer no
catálogo de gastos. Ela não repete o mapa de permissões — repetir envelheceria. Ela cobre os
poucos lugares onde errar custa caro: dinheiro, dado pessoal e o próprio controle de acesso.

Com a etiqueta trocada, essa lista fica vermelha na hora.

**4. Fonte externa nenhuma é tocada.**

A FIPE e o armazenamento entram como dublês. Três motivos, e todos valem: um teste que
depende da internet falha por motivo errado; um teste que gasta consulta da faixa gratuita
gasta o que a operação precisa; e a rotina mensal do pátio, acordando no meio da suíte,
tornaria o resultado dependente do relógio. Ela fica **desligada** por configuração — o
interruptor já existe desde o M11.

**5. O isolamento entre empresas é teste dirigido, e não matriz.**

A matriz responde *"este perfil pode chamar este endpoint?"*. O isolamento responde outra
coisa: *"este dado é meu?"* — e a resposta certa é **404**, e jamais 403, porque para a
empresa A o registro da B simplesmente não existe.

São testes escritos um a um, com duas revendas montadas pelo próprio sistema — pelas mesmas
entidades e repositórios que a API usa, e jamais por `INSERT` na mão, que criaria linha que o
sistema não sabe criar.

O caso que mais interessa é o do documento e da foto: eles **não carregam empresa**, e pendem
do veículo (ver `VehicleEntity`). A isolação deles depende do join, e é exatamente o tipo de
lugar onde um `WHERE` esquecido não aparece em teste de unidade.

## O que o V3 encontrou

**Oito handlers liam pelo código público sem filtrar a empresa.** O caso mais grave respondeu
**204**: o administrador da revenda A excluiu um usuário da revenda B. Os outros sete deixavam
editar, bloquear, restaurar e trocar a foto de gente de outra revenda, e editar e excluir o
perfil dela — e perfil concede tela, então isso abriria o sistema inteiro do vizinho.

Curiosidade que explica o defeito: o `RestoreUserHandler` **já conferia** a empresa. Alguém
viu o risco naquele caminho e corrigiu só ali, o que é exatamente o que acontece quando a
regra vive na disciplina de cada handler em vez de viver no contrato.

Por isso o conserto foi na raiz: `IUserRepository` e `IRoleRepository` ganharam
`GetByCodeAsync(idTenant, code)`, com a consulta filtrando `IdTenant`, e o
`GetUserPhotoHandler` — que nem recebia a sessão — passou a receber. A leitura por código
agora **pede** a empresa.

## O que fica de fora deste marco

- **Testes de interface.** O frontend continua conferido por build e captura de tela. Marco
  próprio, quando fizer sentido.
- **Teste de carga.** A pergunta aqui é de correção, e não de desempenho.
- **Cobertura de negócio pela API.** Registrar uma venda completa por HTTP é outro assunto: o
  cálculo já é coberto por unidade, e a matriz existe para a fechadura.
- **Autenticação em si** — expiração, rotação de refresh, bloqueio. Já tem teste de unidade, e
  o que este marco acrescenta é o 401 do anônimo em todo endpoint.
