| User | `Id`, `Code`, `IdTenant`, `Name`, `Email`, `PasswordHash`, `Photo`, `Document`, `Phone`, `IsBlocked`, + auditoria |# Mapeamentos de banco

Provider: **MariaDB 11.8** via `MySql.EntityFrameworkCore`.
Modelo definido em `docs/architecture/decisions/ADR-0003-padrao-global.md`.

**O Entity Framework existe aqui só para gerar migration e mapear tabela.** Nenhum
repository, handler ou controller toca o `DbContext`: em runtime, todo acesso a dado vai
por Dapper.

## Convenções

- Toda entidade herda de `Foundation.Domain.Abstractions.Entity`, que traz `Id`, `Code`,
  `IsActive` e a auditoria completa. Este projeto acrescenta apenas o `TenantEntity`.
- **`Id`** é `int` autoincremento, interno, e é sempre a **chave primária**. **`Code`** é
  `char(36)` com índice único, **UUID v7** — ordenável por tempo, para evitar a fragmentação
  de página que um v4 aleatório causa numa coluna indexada. Rotas e DTOs expõem `Code`; o
  `Id` nunca sai da API.
- **Chave estrangeira leva `Id` na frente**, seguido da entidade apontada: `IdTenant`,
  `IdUser`, `IdRole`, `IdScreen`, `IdParentScreen`. Jamais `UserId`. Assim, ordenando as
  colunas por nome, todas as chaves ficam juntas, e a coluna diz para onde aponta antes de
  dizer que é um identificador.
- **Sem `HasColumnName`.** Cada propriedade se chama como a coluna. Se o nome colide com
  palavra reservada, renomeia-se a propriedade — foi o que aconteceu com `Before`/`After`,
  que viraram `OldValues`/`NewValues`.
- Datas em **UTC**, `datetime(6)`.
- Exclusão lógica por `IsActive`. **O EF aplicaria filtro global, mas o Dapper não**: cada
  query carrega `WHERE IsActive = 1`, e o teste `SoftDeleteTests` verifica isso em todo
  `SELECT`.
- Toda entidade de negócio carrega `IdTenant` via `TenantEntity`. Duas tabelas são exceção,
  e pelo mesmo motivo: `Screen` e `FipeQuote` são globais ao sistema. Catálogo de telas e
  cotação da tabela de referência valem igual para toda empresa.
- Um `{Entity}Map : EntityMap<T>` por entidade, em
  `Infrastructure/Persistence/Mappings/`. O `EntityMap` vem do `Foundation.Base` e já mapeia
  `Id`, `Code` único, `IsActive` e auditoria.

## Colunas herdadas de `Entity`

Presentes em **todas** as tabelas:

| Coluna | Tipo | Notas |
|---|---|---|
| Id | int | PK, autoincremento |
| Code | char(36) | UUID v7, índice único, identificador público |
| IsActive | tinyint(1) | falso = excluído logicamente |
| DtCreated | datetime(6) | UTC |
| CreatedBy | varchar(256) | |
| DtUpdated | datetime(6) | nulo até a primeira edição |
| UpdatedBy | varchar(256) | |
| DtDeleted | datetime(6) | preenchido na exclusão lógica |
| DeletedBy | varchar(256) | |

As tabelas abaixo listam apenas as colunas próprias.

## Tabelas

### Tenant

| Coluna | Tipo |
|---|---|
| Name | varchar(160) |

### Screen

Catálogo de permissões **e** do menu. Global, sem `IdTenant`.
Sincronizado a partir do `ScreenCatalog` (código) a cada inicialização da API.

| Coluna | Tipo | Notas |
|---|---|---|
| Key | varchar(60) | **único**; é a permissão. Ex.: `vehicles` |
| Name | varchar(80) | rótulo no menu. **Em português**: é texto de tela |
| Route | varchar(160) | ex.: `/vehicles` |
| Icon | varchar(60) | nome do ícone lucide |
| MenuGroup | varchar(60) | seção da barra lateral. Nulo quando fora do menu |
| Order | int | ordenação dentro do grupo |
| ShowInMenu | tinyint(1) | falso = permissão sem item de menu |
| IdParentScreen | int | FK Screen, nulo na raiz |

Índices: único em `Key`; composto em `(MenuGroup, Order)`.

`Key` e `Order` são palavras reservadas no MySQL. O `ScreenRepository` escreve o SQL com
crases, porque o gerador convencional do Foundation é agnóstico de provider.

### Role

| Coluna | Tipo | Notas |
|---|---|---|
| IdTenant | int | FK Tenant |
| Name | varchar(80) | único por tenant. **Em português**: é dado exibido |
| Description | varchar(240) | |
| IsSystem | tinyint(1) | verdadeiro impede exclusão |

### RoleScreen

A permissão. A existência da linha significa "este perfil vê esta tela".

| Coluna | Tipo | Notas |
|---|---|---|
| IdRole | int | FK Role, cascade |
| IdScreen | int | FK Screen, restrict |

Índice único em `(IdRole, IdScreen)` — é o que permite ao `INSERT ... ON DUPLICATE KEY
UPDATE` reativar um vínculo anterior em vez de criar uma segunda linha.

Reservado para quando houver permissão de ação: colunas `CanEdit` e `CanDelete` entram
aqui, sem remodelagem.

### User

| Coluna | Tipo | Notas |
|---|---|---|
| IdTenant | int | FK Tenant |
| Name | varchar(160) | |
| Email | varchar(180) | único por tenant |
| PasswordHash | varchar(255) | **Argon2** via `Foundation.Shared.StringHelper` |
| Photo | varchar(80) | nome do arquivo. A imagem fica fora do banco |
| Document | varchar(14) | CPF ou CNPJ, **somente dígitos**. A máscara vive na tela |
| Phone | varchar(11) | com DDD, somente dígitos |
| IsBlocked | tinyint(1) | verdadeiro impede o login. **Distinto de `IsActive`** |

`IsBlocked` e `IsActive` respondem a perguntas diferentes, e confundi-las já custou um
defeito: bloquear escrevia `IsActive`, então a pessoa inativada sumia da listagem e a
tentativa de trazê-la de volta respondia "Usuário inexistente.".

| Coluna | Pergunta | Efeito na listagem |
|---|---|---|
| `IsBlocked` | esta pessoa pode entrar? | continua aparecendo |
| `IsActive` | esta linha ainda existe? | sai de toda consulta |

A listagem de usuários aceita `includeDeleted=true` e é a **única** leitura autorizada a ver
linhas excluídas, para que a tela possa oferecê-las de volta. `POST /api/users/{code}/restore`
traz a pessoa de volta **bloqueada**, de propósito: quem restaura decide depois se ela volta a
entrar, em vez de uma exclusão virar silenciosamente uma conta aberta.

Índice único composto em `(IdTenant, Email)`.

### UserRole

N:N. As telas do usuário são a união das telas dos perfis dele.
A interface desta fase atribui um único perfil por usuário.

| Coluna | Tipo | Notas |
|---|---|---|
| IdUser | int | FK User, cascade |
| IdRole | int | FK Role, restrict |

Índice único em `(IdUser, IdRole)`.

### RefreshToken

| Coluna | Tipo | Notas |
|---|---|---|
| IdUser | int | FK User, cascade |
| TokenHash | varchar(255) | **hash** do token; o valor emitido nunca é persistido |
| ExpiresAt | datetime(6) | UTC |
| RevokedAt | datetime(6) | nulo = válido |

Índice em `TokenHash`. Rotação no refresh: o token usado é revogado e um novo é emitido,
na mesma transação.

### AuditLog

| Coluna | Tipo | Notas |
|---|---|---|
| IdTenant | int | |
| IdUser | int | autor da ação |
| EntityName | varchar(80) | ex.: `User` |
| RecordCode | char(36) | `Code` do alvo |
| Action | int | Create, Update, Deactivate, Activate, Delete |
| OldValues | json | nulo na criação |
| NewValues | json | nulo na exclusão |

Índice composto em `(EntityName, RecordCode)`.

`OldValues` e `NewValues` nasceram como `Before` e `After` — ambas são palavras reservadas
no MySQL, e a regra é renomear a propriedade em vez de traduzir a coluna.

## Ordem das colunas

O `EntityMap<T>` do Foundation distribui as colunas na ordem em que alguém lê a tabela:

```text
Id | Code | chaves estrangeiras | propriedades da entidade | bloco de auditoria
```

Como está no banco hoje:

| Tabela | Colunas |
|---|---|
| Tenant | `Id`, `Code`, `Name`, + auditoria |
| Screen | `Id`, `Code`, `IdParentScreen`, `Key`, `Name`, `Route`, `Icon`, `MenuGroup`, `Order`, `ShowInMenu`, + auditoria |
| Role | `Id`, `Code`, `IdTenant`, `Name`, `Description`, `IsSystem`, + auditoria |
| RoleScreen | `Id`, `Code`, `IdRole`, `IdScreen`, + auditoria |
| User | `Id`, `Code`, `IdTenant`, `Name`, `Email`, `PasswordHash`, `Photo`, `Document`, `Phone`, + auditoria |
| UserRole | `Id`, `Code`, `IdUser`, `IdRole`, + auditoria |
| RefreshToken | `Id`, `Code`, `IdUser`, `TokenHash`, `ExpiresAt`, `RevokedAt`, + auditoria |
| AuditLog | `Id`, `Code`, `IdTenant`, `IdUser`, `EntityName`, `RecordCode`, `Action`, `OldValues`, `NewValues`, + auditoria |

Onde "auditoria" é sempre `IsActive`, `DtCreated`, `CreatedBy`, `DtUpdated`, `UpdatedBy`,
`DtDeleted`, `DeletedBy`, nessa ordem.

As propriedades da entidade saem na ordem em que foram **declaradas na classe**. Para mudar a
ordem de leitura de uma tabela, mova a propriedade de lugar no arquivo da entidade.

**A ordem chega ao banco na criação da tabela.** Tabela que já existe mantém a ordem com que
nasceu, e alterar o mapeamento não gera migration de reordenação — é preciso recriar o schema.
## Relacionamentos

```text
Tenant 1──n User n──n Role n──n Screen
              │           │        │
              │           └─ RoleScreen (a permissão)
              └─ UserRole

User   1──n RefreshToken
Tenant 1──n AuditLog
Screen 1──n Screen (IdParentScreen, submenu)
```

## Migration

Aplicada pelo `SchemaMigrator`, e **não** por `Database.MigrateAsync`: o migrator do EF
Core 10 toma um lock com `GET_LOCK`, e o provider da Oracle lê o resultado como `long` não
anulável — o MariaDB pode responder `NULL` e a inicialização morre. O `SchemaMigrator` gera
o script e o executa pela conexão Dapper, mantendo o histórico em `__EFMigrationsHistory`,
a mesma tabela que o `dotnet ef` lê.

Criar uma migration nova:

```bash
dotnet dotnet-ef migrations add NomeDaMigration \
  --project src/RevendaPro.Infrastructure \
  --startup-project src/RevendaPro.Infrastructure \
  --output-dir Database/Migrations
```


## Tabelas do veículo

Modelo definido em `docs/plans/m6-cadastro-de-veiculos.md`, a partir do documento de requisitos
e da entrevista com o stakeholder.

### Vehicle

| Coluna | Tipo | Notas |
|---|---|---|
| IdTenant | int | FK Tenant |
| IdCoverPhoto | int | FK VehiclePhoto. A capa fica **aqui**, e não como `IsCover` na foto: assim o banco garante uma capa só |
| Plate | varchar(7) | sem hífen, maiúscula |
| Chassis | varchar(17) | VIN |
| Brand, Model, Version | varchar | |
| ModelYear, ManufactureYear | smallint | o ano do modelo é igual ou posterior ao de fabricação |
| Color | varchar(30) | |
| Mileage | int | só aumenta, salvo correção explícita |
| FuelType, Transmission | int | enum |
| Renavam | varchar(11) | |
| Origin | int | leilão, particular, loja, **troca**, outro |
| HasDamage | tinyint(1) | central nesta operação |
| DamageDescription | varchar(500) | obrigatória quando há sinistro |
| Status | int | máquina de estado, abaixo |
| PurchasePrice | decimal(12,2) | |
| PurchaseDate | date | inicia o tempo em estoque |
| SupplierName | varchar(160) | fornecedor ou leilão |
| PurchasePaymentMethod | int | |
| BudgetCeiling | decimal(12,2) | teto do custo **total** |
| FipeValue | decimal(12,2) | informado à mão |
| FipeReferenceDate | date | a tabela muda todo mês |
| FipeCode | varchar(10) | código do modelo na FIPE, para a integração futura |
| DesiredNetPrice | decimal(12,2) | **quanto a revenda quer receber** |
| MinimumNetPrice | decimal(12,2) | igual ou menor que o desejado |
| AdvertisedPrice | decimal(12,2) | com o repasse do parceiro por cima |
| MarketNotes | varchar(500) | pesquisa de anúncios da região |
| Notes | varchar(1000) | |

Índices em `(IdTenant, Plate)`, `(IdTenant, Chassis)` e `(IdTenant, Status)`.

**A unicidade de placa e chassi fica na consulta, e não em índice único.** Um veículo excluído
mantém a linha: um índice sobre as colunas recusaria uma placa que voltou ao pátio, e um índice
que incluísse `IsActive` deixaria duas linhas ativas com a mesma placa assim que uma terceira
fosse excluída. Quem garante é a regra, com teste.

Máquina de status:

```
UnderReview -> Purchased -> InRepair -> ReadyForSale -> Advertised -> Negotiating -> Sold
```

Voltar é permitido onde o negócio volta: o carro retorna à oficina quando aparece algo depois
de pronto, e uma negociação que desanda devolve o carro ao mercado. `Sold` é terminal — desfazer
uma venda é desfazer o registro dela, e isso pertence ao módulo que o criou.

### ExpenseType

Tipos de gasto, **mantidos pela revenda** (RF-09). Tabela, e não enum: os tipos que faltam só
aparecem no uso — retrovisor, vidro elétrico, ar-condicionado —, e com lista fixa tudo isso
cairia em "Outros", que é onde a análise de gasto para de valer.

| Coluna | Tipo | Notas |
|---|---|---|
| IdTenant | int | cada revenda tem a sua lista |
| Name | varchar(80) | **em português**: é dado exibido |
| Keywords | varchar(500) | palavras que apontam um gasto para este tipo, separadas por vírgula |
| Position | int | ordem na lista |

`Keywords` mora aqui, e não num dicionário no código, para que a sugestão continue funcionando
nos tipos que a revenda criar. Um dicionário no código só serviria aos tipos que alguém previu.

Cada empresa nasce com 13 tipos preenchidos, do `ExpenseTypeCatalog`.

### VehicleExpense

| Coluna | Tipo | Notas |
|---|---|---|
| IdVehicle | int | FK Vehicle, cascade |
| IdExpenseType | int | FK ExpenseType, **restrict** |
| Description | varchar(160) | curta, para ler a lista rápido |
| Amount | decimal(12,2) | |
| Date | date | |
| Notes | varchar(1000) | texto livre: onde comprou, garantia, número da nota |
| IsPaid | tinyint(1) | falso = despesa prevista (RF-11) |

A FK do tipo é **restrict**, e jamais cascade: apagar um tipo de gasto nunca leva junto os
lançamentos que apontam para ele. A regra de negócio recusa a exclusão antes disso, e a
restrição é a rede que impede o estrago se ela falhar.

**A compra fica fora desta tabela**, em `Vehicle.PurchasePrice`, mesmo o stakeholder
escrevendo-a como primeira linha da planilha. A compra tem atributos que uma despesa não tem —
fornecedor, forma de pagamento, data de aquisição — e é ela que inicia o tempo em estoque.
Na tela, a leitura continua sendo a dele: a compra aparece como primeira linha da lista.

### VehiclePhoto

| Coluna | Tipo | Notas |
|---|---|---|
| IdVehicle | int | FK Vehicle, cascade |
| Kind | int | **dano**, reparo, finalizado, outro |
| StorageKey | varchar(200) | prefixo comum aos três tamanhos |
| ContentType | varchar(40) | sempre `image/webp` após o processamento |
| SizeInBytes | int | os três tamanhos somados |
| Width, Height | smallint | da imagem cheia |
| Position | int | ordem na galeria, arrastável |

`Kind` existe porque a foto do dano tem função própria: ela é enviada ao comprador para
explicar o histórico de um carro de leilão.

`Position` chama-se assim, e não `Order`, porque **`Order` é palavra reservada no MySQL** — a
regra do projeto é renomear a propriedade em vez de escrever SQL com crase em volta dela.

### VehicleDocument

| Coluna | Tipo | Notas |
|---|---|---|
| IdVehicle | int | FK Vehicle, cascade |
| Kind | int | nota, comprovante, documento de leilão, termo, vistoria, despachante, comprovante de residência, documento pessoal, outro |
| StorageKey | varchar(200) | **bucket privado**, sempre |
| FileName | varchar(160) | nome original, só para exibir. Jamais vira chave |
| ContentType | varchar(80) | PDF, JPEG ou PNG |
| SizeInBytes | int | |

### VehicleStatusHistory

| Coluna | Tipo | Notas |
|---|---|---|
| IdVehicle | int | FK Vehicle, cascade |
| FromStatus | int | nulo no primeiro registro |
| ToStatus | int | |
| Reason | varchar(240) | |

Sem ela, o tempo em cada etapa se perde a cada mudança — e a RF-24 pede tempo em estoque.

### O que estas tabelas jamais guardam

Custo total, custo previsto, percentual do orçamento, percentual sobre FIPE, lucro e margem
**não têm coluna**. Todos saem de `VehicleCost`, calculados a cada leitura.

O motivo está na planilha real do stakeholder: o total foi digitado uma vez, três despesas
entraram embaixo dele depois, e o documento seguiu mostrando **R$ 350 a menos** do que o carro
custava. Total guardado fica certo até a próxima despesa, e errado a partir dali, em silêncio.
## Tabelas da venda

Modelo definido em `docs/plans/m8-venda-e-proposta.md`. As duas herdam `VehicleEntity`, e o
tenant chega pelo veículo — toda consulta por empresa faz o join e filtra ali.

### Proposal

| Coluna | Tipo | Notas |
|---|---|---|
| IdVehicle | int | FK Vehicle, cascade |
| ProspectName | varchar(120) | quem ofereceu |
| ProspectPhone | varchar(20) | dígitos, opcional |
| Amount | decimal(12,2) | |
| Date | date | |
| PaymentMethod | int | a forma move o preço aceito |
| Channel | int | `Direct` ou `PartnerStore` |
| PartnerCutPercent | decimal(5,2) | nulo quando direta, ou quando a loja deu valor |
| PartnerCutAmount | decimal(12,2) | nulo quando direta, ou quando a loja deu percentual |
| Status | int | `Open`, `Accepted`, `Declined` |
| Notes | varchar(500) | |

Índice em `(IdVehicle, Status)`: a ficha lista as abertas antes das demais.

### Sale

| Coluna | Tipo | Notas |
|---|---|---|
| IdVehicle | int | FK Vehicle, cascade. **Uma venda ativa por carro**, garantida pela consulta |
| IdProposal | int | FK Proposal, restrict; nula quando a venda entrou direto |
| IdTradeInVehicle | int | FK Vehicle, restrict; o carro que entrou na troca |
| Date | date | |
| Amount | decimal(12,2) | preço fechado, carro incluído quando há troca |
| PaymentMethod | int | |
| Channel | int | |
| PartnerStoreName | varchar(120) | |
| PartnerCutPercent | decimal(5,2) | como foi acertado |
| PartnerCutAmount | decimal(12,2) | **sempre preenchido** quando há loja: é o que saiu da conta |
| Commission | decimal(12,2) | zero quando nenhuma |
| CommissionNotes | varchar(200) | |
| BuyerName | varchar(120) | |
| BuyerDocument | varchar(14) | CPF ou CNPJ, dígitos — dado pessoal (RNF-13) |
| BuyerPhone | varchar(20) | dígitos — dado pessoal |
| TradeInValue | decimal(12,2) | parte do `Amount` que entrou como carro; nulo sem troca |
| Notes | varchar(500) | |

A unicidade da venda por veículo segue o mesmo raciocínio da placa: uma venda cancelada fica
excluída logicamente e continua na tabela, então um índice único sobre `IdVehicle` recusaria
vender de novo um carro cuja venda foi desfeita. Quem garante é `FindSaleByVehicleQuery`, com
teste.

**Comprador dentro da venda, sem tabela própria.** Sem CRM na primeira fase, uma tabela com
uma linha por venda seria cerimônia. As duas colunas de dado pessoal saem só para a tela
privada, e ficam fora de qualquer exportação.

**O que estas tabelas jamais guardam:** recebido, lucro bruto, lucro líquido e margem. Todos
saem de `DealResult`, calculados a cada leitura — pelo mesmo motivo do custo.

## Tabela da referência

Modelo definido em `docs/architecture/decisions/ADR-0005-consulta-da-tabela-fipe.md`.

### FipeQuote

O que a tabela FIPE disse sobre **um modelo, num mês**. Global, sem `IdTenant`: cotação é
dado público de referência, e não dado de empresa — é isso que faz dez carros do mesmo
modelo, em duas revendas, custarem **uma** consulta.

| Coluna | Tipo | Notas |
|---|---|---|
| FipeCode | varchar(10) | código do modelo, como a tabela imprime |
| YearFuel | varchar(10) | ano e combustível da linha precificada (`2014-5`) |
| ReferenceMonth | date | **sempre o dia 1**: a tabela é mensal, e o dia carrega zero significado |
| Value | decimal(12,2) | dinheiro em decimal, jamais em ponto flutuante (RNF-12) |
| ModelYear | smallint | o ano sozinho, para filtrar sem quebrar o par |
| Brand | varchar(60) | como a tabela escreve (`GM - Chevrolet`) |
| Model | varchar(160) | como a tabela escreve, versão incluída |

Índice **único** em `(FipeCode, YearFuel, ReferenceMonth)`: uma cotação por modelo e mês.
Aqui o índice único é seguro, ao contrário do que aconteceria com a placa, porque o sistema
jamais exclui uma cotação — e ele importa duas vezes, como regra e como garantia de que a
leitura devolve uma linha só.

**O ano sozinho seria ambíguo.** O mesmo modelo e ano existem como flex e como gasolina, com
preços diferentes. O par é o que a tabela precifica, e é por isso que ele é coluna própria e
entra na chave.

**Uma cotação de mês fechado jamais muda.** A entidade tem fábrica e nenhum método de
instância — um teste segura isso fechado. É o que sustenta a comparação histórica do M11:
*vendido por R$ 60.000 quando a tabela daquele mês dizia R$ 56.815* continua verdadeiro anos
depois, sem o número ser copiado para dentro da venda. Foi copiando número que o custo do M6
tinha ficado errado.

**Sem chave estrangeira para `Vehicle`.** A cotação existe por si, e vale para todo carro
daquele modelo — inclusive os que ainda serão cadastrados.

### O que o veículo guarda da FIPE

| Coluna | Tipo | Notas |
|---|---|---|
| FipeValue | decimal(12,2) | valor de referência do carro |
| FipeReferenceDate | date | de que mês veio |
| FipeCode | varchar(10) | código do modelo na tabela |
| FipeYearFuel | varchar(10) | ano-combustível do modelo; pertence ao código |

`FipeYearFuel` é escrito pela consulta, e jamais digitado. Trocar `FipeCode` **solta** o
ano-combustível: o par pertence ao código, e mantê-lo mandaria a próxima consulta pedir a
linha de um carro que aquele veículo deixou de ser.

Nenhuma dessas colunas é preço. `DesiredNetPrice`, `MinimumNetPrice` e `AdvertisedPrice`
continuam sendo de quem entende do carro — a tabela aparece ao lado deles.

