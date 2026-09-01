# Plano — Refatoração para o padrão Global

Base: `docs/agent/inspection-report.md`.

Referências:
- **`Autenticacao.Global`** — estrutura de camadas, `SqlQuery`, repositories Dapper, `Shared/Settings`.
- **`PainelGestao.CPComunica`** — idioma (ADR-0002), `BaseEntity`, `EntityMap<T>`, `SuccessDetails`.
- **`Foundation.Base 3.1.1-rc.1`** — `Entity`, Argon2, contratos de repositório e UoW.

Nada implementado ainda.

---

## 1. Decisões de arquitetura

| Item | Decisão |
|---|---|
| Idioma | **Todo o código em inglês.** Só o texto que o usuário lê fica em português: rótulo de tela e `detail` da resposta HTTP |
| Base de entidade | `Foundation.Domain.Abstractions.Entity` |
| Hash de senha | `Foundation.Shared.Helpers.StringHelper` — **Argon2** com salt e pepper |
| **EF Core** | **Somente migrations e mapeamento** (`IEntityTypeConfiguration`). Nenhum acesso a dado em runtime |
| **Dapper** | **Todo o acesso a dado**, leitura e escrita, no padrão `SqlQuery` do `Autenticacao.Global` |
| Transação | `IUnitOfWork` sobre `IDbConnection`/`IDbTransaction` do Dapper — não sobre `DbContext` |
| Envelope HTTP | `SuccessDetails<T>(status, title, detail, instance, data)` / `ProblemDetails` |

### Foundation: o que usar e o que não tocar

`Foundation.Base` é um pacote só, mas com quatro assemblies. **Nem todos podem ser usados aqui.**

| Assembly | Uso | Motivo |
|---|---|---|
| `Foundation.Domain` | **Sim** — `Entity` (Id, Code, IsActive, DtCreated/Updated/Deleted, CreatedBy/UpdatedBy/DeletedBy, `SoftDelete`, `Activate`, `UpdateAuditInfo`) | Não depende de EF Core |
| `Foundation.Shared` | **Sim** — `StringHelper.ComputeArgon2Hash` / `VerifyArgon2Hash` | Sem dependência conflitante |
| `Foundation.Infrastructure` | **Não** | Exige **EF Core 10.0.0**. O Pomelo (driver MariaDB) só existe para **EF Core 9**. Referenciar quebra o restore |
| `Foundation.Application` | Não nesta fase | Contratos de repositório EF; aqui o acesso é Dapper |

Isto não é escolha, é restrição verificada: `Foundation.Infrastructure.dll` referencia
`Microsoft.EntityFrameworkCore 10.0.0`; `Pomelo.EntityFrameworkCore.MySql` tem
**9.0.0 como última versão estável** e nenhuma release para EF 10. O CPComunica bateu no
mesmo ponto e resolveu reproduzindo o `EntityMap<T>` localmente — é o que vamos fazer.

**Todos os projetos permanecem em `net10.0`.** O que fica em 9.0.x é o pacote EF Core,
que aqui só gera migration e mapeia tabela. Revisar quando o Pomelo publicar para EF 10.

### Padrão de mapeamento — igual ao CPComunica

```csharp
// Infrastructure/Data/MariaDb/Configurations/EntityMap.cs
public abstract class EntityMap<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired().ValueGeneratedOnAdd().HasColumnOrder(1);
        builder.Property(x => x.Code).HasColumnType("char(36)").IsRequired().HasColumnOrder(2);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.IsActive).IsRequired().HasColumnOrder(3);
        // ... colunas de auditoria
        builder.Ignore(x => x.LstId);            // filtros do Foundation, nao viram coluna
        builder.HasQueryFilter(/* IsActive */);  // exclusao logica
    }
}

// Infrastructure/Data/MariaDb/Configurations/EntityMaps.cs
public sealed class UserMap : EntityMap<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        base.Configure(builder);
        builder.ToTable("Users");
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
    }
}
```

### Padrão de acesso a dado — igual ao Autenticacao.Global

```csharp
// Infrastructure/Queries/SqlQuery.cs
internal abstract class SqlQuery
{
    public abstract string GetSql();
}

// Infrastructure/Queries/Authentication/FindUserByEmailQuery.cs
internal sealed class FindUserByEmailQuery : SqlQuery
{
    public FindUserByEmailQuery(string email) => Email = email;

    public string Email { get; }

    public override string GetSql() => """
        SELECT Id, Code, Name, Email, PasswordHash, IsActive
        FROM Users
        WHERE Email = @Email AND IsActive = 1
        """;
}

// Infrastructure/Repositories/Authentication/UserRepository.cs
var query = new FindUserByEmailQuery(email);
using var conn = _connectionFactory.CreateConnection();

return await conn.QueryFirstOrDefaultAsync<User>(
    new CommandDefinition(query.GetSql(), query, cancellationToken: ct));
```

A escrita segue o mesmo caminho: `InsertUserQuery`, `UpdateUserQuery`,
`SoftDeleteUserQuery`, executadas com `ExecuteAsync` dentro da transação do `IUnitOfWork`.

---

## 2. Estrutura alvo

```txt
src/
  RevendaPro.Global.Api/
    Controllers/        AuthController, UsersController, RolesController, ScreensController
    Contracts/          SuccessDetails.cs
    Middleware/         ExceptionHandlingMiddleware.cs
    Authorization/      RequireScreenAttribute.cs
    Security/           CurrentUser.cs
    Swagger/
    Program.cs
  RevendaPro.Global.Application/
    Authentication/     Commands/ Handlers/ Validators/ DTOs/
    Users/              Commands/ Queries/ Handlers/ Validators/ DTOs/
    Roles/              idem
    Screens/            idem
    Behaviors/          ValidationBehavior.cs
    Common/Exceptions/
    Configuration/      ServiceCollectionExtensions.cs
  RevendaPro.Global.Domain/
    Entities/           BaseEntity, TenantEntity, User, Role, Screen,
                        RoleScreen, UserRole, Tenant, RefreshToken, AuditLog
    Enums/              AuditAction, ...
    Exceptions/         BusinessRuleException
    Interfaces/
      IUnitOfWork.cs
      Repositories/     IUserRepository, IRoleRepository, IScreenRepository, ...
      Services/         IPasswordHasher, ITokenService, IPermissionService,
                        IPhotoStorageService
  RevendaPro.Global.Infrastructure/
    Configuration/      ServiceCollectionExtensions.cs
    Data/MariaDb/       RevendaProDbContext.cs        (migrations + maps, so)
                        IMySqlConnectionFactory.cs / MySqlConnectionFactory.cs
                        Configurations/EntityMap.cs + EntityMaps.cs
                        DbInitializer.cs
                        Migrations/
    Queries/            SqlQuery.cs + {Context}/
    Repositories/       {Context}/{Entity}Repository.cs   (Dapper)
    UnitOfWork/         RevendaProUnitOfWork.cs           (IDbTransaction)
    Security/           JwtTokenService, PasswordHasherService, PermissionService
    Services/Storage/   DiskPhotoStorageService.cs
    Screens/            ScreenCatalog.cs, ScreenSynchronizer.cs
  RevendaPro.Global.Shared/
    Settings/           JwtSettings.cs, RevendaProSettings.cs
```

---

## 3. Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **R0** | Decisão registrada | ADR-0003: inglês, Foundation, EF só migration/map, Dapper no acesso, `SuccessDetails`. Corrigir `docs/agent/context.md`, `instructions.md` e `AGENT_HANDOFF.md`, que hoje mandam o oposto | Nenhum doc diz mais "domínio em português" | — |
| **R1** | Pacotes e Shared | `Foundation.Base`, `Dapper`, `Pomelo`; projeto `RevendaPro.Global.Shared/Settings/`. Confirmar que o restore resolve sem puxar `Foundation.Infrastructure` | `dotnet restore` limpo; nenhum conflito EF 9/10 | R0 |
| **R2** | Domain em inglês | `BaseEntity : Entity` e `TenantEntity`; 8 entidades; enums; exceptions; `Interfaces/Repositories/` e `Interfaces/Services/`; `IUnitOfWork` | Build; zero identificador de negócio em pt; PK `Id` int + `Code` UUID v7 | R1 |
| **R3** | Mapeamento EF | `EntityMap<T>` reproduzido para Pomelo/EF9; um `{Entity}Map` por entidade; `RevendaProDbContext` só com `DbSet` e `ApplyConfigurationsFromAssembly` | Migration gera; filtro global de `IsActive` em todas | R2 |
| **R4** | Migration e seed | Migration única criando o schema em inglês: `Users`, `Roles`, `Screens`, `RoleScreens`, `UserRoles`, `Tenants`, `RefreshTokens`, `AuditLogs`. `DbInitializer` + `ScreenCatalog`/`ScreenSynchronizer` | `docker compose up` cria e semeia; rodar 2x não duplica | R3 |
| **R5** | Acesso a dado com Dapper | `IMySqlConnectionFactory`; `SqlQuery`; `Queries/{Context}/` para **toda** leitura e escrita; `Repositories/{Context}/`; `UnitOfWork` sobre `IDbTransaction` | Nenhuma consulta ou gravação via `DbContext` em runtime | R4 |
| **R6** | Segurança | `PasswordHasherService` com **Argon2 do Foundation** (substitui `PasswordHasher` do Identity); `JwtTokenService`; `PermissionService` com cache por role | Login funciona com hash Argon2; senha antiga do seed regerada | R5 |
| **R7** | Application em inglês | Por contexto: `Commands/ Handlers/ Validators/ DTOs/`; `ValidationBehavior`; `Common/Exceptions`; `Configuration/ServiceCollectionExtensions` | Build; zero nome em pt; validators de CPF/CNPJ e telefone mantidos | R2 |
| **R8** | Api | `SuccessDetails<T>`; `ExceptionHandlingMiddleware`; `Swagger/` com os filtros de resposta; `[ProducesResponseType]` em toda action; command como contrato de entrada; `RequireScreenAttribute` | Swagger descreve todos os status; envelope de 5 campos | R6, R7 |
| **R9** | Frontend | Rotas `/users`, `/roles`, `/vehicles`, `/costs`, `/sales`; tipos e chaves de tela em inglês; leitura do novo envelope. **Todo rótulo visível segue em português** | Build do Next; menu monta igual; nenhuma rota quebrada | R8, R4 |
| **R10** | Revalidação | A bateria que já passou: login admin e vendedor, 403 por perfil, 401 sem token, permissão valendo sem relogar, upload de foto (roundtrip + 3 ataques), idempotência do seed | Tudo verde, igual a antes da refatoração | R9 |
| **R11** | Testes (marco A6) | NetArchTest de camadas + matriz perfil × endpoint + unidade das regras | Suíte verde | R10 |

### Paralelismo

```txt
R0 ─> R1 ─> R2 ─┬─> R3 ─> R4 ─> R5 ─> R6 ─┬─> R8 ─> R9 ─> R10 ─> R11
                │                          │
                └────────> R7 ─────────────┘
```

R7 (Application) anda em paralelo a R3–R6 assim que o Domain estiver pronto.

---

## 4. Pontos de atenção

**`Screen.Name` fica em português.** A tabela terá `Key = "vehicles"`, `Route = "/vehicles"`
e `Name = "Veículos"`. Chave e rota são código; o nome é rótulo de tela. É a mesma exceção
que o CPComunica registrou para `Permissions.Name` e `Module`.

**Troca do hash de senha.** Sair do `PasswordHasher` do Identity para Argon2 invalida os
hashes existentes. Como só existe o administrador semeado e dois usuários de teste, o seed
regera tudo — mas isso precisa estar no ADR, porque depois de ter usuário real exigiria
migração de hash.

**`Guid.CreateVersion7()`.** O `Code` precisa ser v7, ordenável por tempo. O `Entity` do
Foundation expõe `Code` com setter; quem gera é o construtor do `BaseEntity`, como no
CPComunica.

**Sem `HasColumnName`.** Cada propriedade se chama como a coluna. Se o nome não servir,
renomeia-se a propriedade.
