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
- Toda entidade de negócio carrega `IdTenant` via `TenantEntity`. `Screen` é a exceção:
  é global ao sistema.
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

## Tabelas das fases seguintes

Definidas quando os marcos M6 a M8 forem iniciados: `Vehicle`, `VehiclePhoto`,
`VehicleDocument`, `VehicleStatusHistory`, `Supplier`, `Quote`, `QuoteItem`,
`VehicleExpense`, `FipeQuery`, `Sale`, `Buyer`.
