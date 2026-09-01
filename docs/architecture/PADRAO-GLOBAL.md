# Padrão de arquitetura — projetos `*.Global`

Documento genérico. Descreve **como montar um projeto novo** seguindo o padrão que foi
consolidado no Revenda Pro, e **por quê** cada decisão foi tomada.

Escrito para ser lido antes da primeira linha de código de um projeto novo, para que a
conversa não precise ser refeita.

Referências que originaram o padrão:

| Projeto | Serve de referência para |
|---|---|
| `source/Global/Authentication` | Camadas, pastas, `EntityMap`, `UnitOfWork`, stack de pacotes |
| `repos/PainelGestao.CPComunica` | Idioma (ADR-0002), `BaseEntity`, `SuccessDetails` |
| `repos/Autenticacao.Global` | Padrão `SqlQuery` e repositories com Dapper |
| `repos/Arquitetura.Global` | Regras de camada, dependências e estrutura obrigatória |
| `repos/Foundation` | Pacote `Foundation.Base` |

---

## 1. As cinco decisões que definem o padrão

Se você só puder lembrar de cinco coisas, são estas.

| # | Decisão | Consequência prática |
|---|---|---|
| 1 | **Todo o código em inglês.** Só o texto que o usuário lê fica em português | Entidade, propriedade, enum, handler, DTO, namespace, pasta, arquivo, rota HTTP, claim, tabela e coluna: inglês. Rótulo de tela, `detail` da resposta e dado exibido: português |
| 2 | **Toda entidade herda de `Entity` do Foundation** | `Id` int interno + `Code` UUID v7 público, auditoria e exclusão lógica vêm de graça. Rotas expõem `Code`, nunca `Id` |
| 3 | **EF Core só gera migration e mapeia tabela** | Nenhum repository, handler ou controller toca `DbContext` |
| 4 | **Dapper faz todo o acesso a dado** | Leitura e escrita, com SQL versionado em query objects |
| 5 | **`SuccessDetails<T>` no sucesso, `ProblemDetails` no erro** | Chaves em inglês, `detail` em português, `[ProducesResponseType]` em toda action |

---

## 2. Idioma

### A regra

> Todo o código é em inglês. Só o texto que o usuário lê fica em português.

Vale para: entidade, propriedade, enum, handler, DTO, namespace, pasta, nome de arquivo,
rota HTTP, claim, chave de permissão, tabela e coluna.

### O que fica em português

- rótulo e título de tela;
- `detail` das respostas HTTP, porque o frontend exibe ao usuário;
- colunas que **são** rótulo, como `Screen.Name` — a tabela terá `Key = "vehicles"`,
  `Route = "/vehicles"` e `Name = "Veículos"`;
- nomes de perfis de sistema (`Administrador`, `Gestor`, …), que são dado exibido;
- mensagens de validação;
- nomes de teste que sejam frase.

Nome de produto não se traduz.

### Atenção

`Arquitetura.Global/docs/standards/nomenclatura.md` prescreve o **contrário** — português
para conceitos de negócio. Ele é anterior. O CPComunica registrou a virada em ADR próprio
(ADR-0002, 30/08/2026) e é o que os projetos novos seguem. **Registre um ADR no seu projeto
também**, senão alguém vai cobrar o `nomenclatura.md` depois.

---

## 3. Camadas

```txt
src/
  {Projeto}.Api/
    Controllers/        um por contexto
    Contracts/          SuccessDetails.cs
    Middleware/         ExceptionHandlingMiddleware.cs
    Authorization/      atributos de guarda
    Security/           CurrentUser.cs
    Swagger/
    Program.cs
  {Projeto}.Application/
    Behaviors/          ValidationBehavior.cs
    Configuration/      ServiceCollectionExtensions.cs
    {Contexto}/
      Commands/ Queries/ Handlers/ Validators/ DTOs/ Services/
  {Projeto}.Domain/
    Entities/           BaseEntity, TenantEntity e as entidades
    Enums/
    Interfaces/
      IUnitOfWork.cs
      Repositories/     um arquivo por repository
      Security/         contratos técnicos (hash, token, permissão)
  {Projeto}.Infrastructure/
    Configuration/      ServiceCollectionExtensions.cs
    Data/{Provider}/    connection factory
    Database/
      Contexts/         DbContext (só migration e mapeamento)
      Factories/        IDesignTimeDbContextFactory
      Migrations/
      DbInitializer.cs
      SchemaMigrator.cs
    Persistence/Mappings/   {Entity}Map.cs
    Queries/{Contexto}/     query objects
    Repositories/{Contexto}/
    UnitOfWork/
    Security/
    Services/
  {Projeto}.Shared/
    Common/Responses/  Constants/  Enums/  Exceptions/  Helpers/  Settings/
tests/
  {Projeto}.Tests/    Unit/ Integration/ Fixtures/ Helpers/
```

**Nenhum arquivo solto na raiz de uma camada.**

### Dependências

```txt
Api            -> Application, Infrastructure, Shared
Application    -> Domain, Shared
Infrastructure -> Domain, Shared
Domain         -> Shared (e Foundation.Domain)
Shared         -> nada
```

`Application` **nunca** referencia `Infrastructure`. Os contratos que a Application precisa
vivem em `Domain/Interfaces/`.

---

## 4. Stack

| Pacote | Versão | Observação |
|---|---|---|
| .NET | `net10.0` | |
| `Microsoft.EntityFrameworkCore` | `10.0.5` | só migration e mapeamento |
| `MySql.EntityFrameworkCore` | `10.0.1` | **provider da Oracle, não o Pomelo** |
| `MySql.Data` | `9.6.0` | exigido pelo provider acima |
| `Foundation.Base` | `3.2.0-rc.2`+ | |
| `Dapper` | `2.1.79` | |
| `Konscious.Security.Cryptography.Argon2` | `1.3.1` | o Foundation usa, mas **não declara** |
| `MediatR` | `14.x` | licença comercial com faixa gratuita — conferir |
| `FluentValidation` | `12.x` | |
| `FluentAssertions` | **`7.2.2`** | **não subir para 8.x**: licença paga para uso comercial |

### Por que o provider da Oracle e não o Pomelo

O `Pomelo.EntityFrameworkCore.MySql` tem **9.0.0 como última estável** e não existe release
para EF Core 10. Isso importa porque o `Foundation.Infrastructure` é compilado contra EF
Core 10: quem fica no Pomelo **não consegue usar o `EntityMap<T>` do Foundation** e precisa
copiá-lo localmente, como o CPComunica fez.

Com `MySql.EntityFrameworkCore` o EF Core 10 fica disponível e o `EntityMap` vem do pacote.

**O custo:** o provider da Oracle tem divergências com MariaDB. Duas apareceram e estão
resolvidas na seção 8.

### Versionamento central

Use `Directory.Packages.props` com `ManagePackageVersionsCentrally`. Os `.csproj` referenciam
sem versão.

---

## 5. Domain

### `BaseEntity`

```csharp
public abstract class BaseEntity : Entity   // Foundation.Domain.Abstractions
{
    protected BaseEntity()
    {
        Code = Guid.CreateVersion7();
        SetCreatedBy(SystemActor);
    }

    public const string SystemActor = "System";

    public bool IsDeleted => !IsActive;

    public void Delete(string deletedBy = SystemActor) { if (!IsDeleted) SoftDelete(deletedBy); }
    public void Restore(string updatedBy = SystemActor) { Activate(); UpdateAuditInfo(updatedBy); }
}

public abstract class TenantEntity : BaseEntity
{
    protected TenantEntity(int tenantId) => TenantId = tenantId;

    public int TenantId { get; protected set; }
}
```

**Por que o `BaseEntity` existe:** o `Entity` do Foundation gera `Code` com
`Guid.NewGuid()`, que é **v4 aleatório**. Como coluna indexada isso fragmenta página a cada
insert. O construtor troca por **UUID v7**, que é ordenável por tempo. Verificado: sem o
`BaseEntity`, o `Code` sai com dígito de versão `4`.

O `Entity` já traz `Id`, `Code`, `IsActive`, `DtCreated/Updated/Deleted`,
`CreatedBy/UpdatedBy/DeletedBy`, `SoftDelete()`, `Activate()`, `UpdateAuditInfo()`.

### Entidades: rich domain

```csharp
public class User : TenantEntity
{
    private User() { }                       // para o materializador
    private User(int tenantId) : base(tenantId) { }

    public string Name { get; private set; } = string.Empty;

    public static User Create(int tenantId, string name, /*...*/ string createdBy = SystemActor)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("...", nameof(name));

        var user = new User(tenantId) { Name = name.Trim() };
        user.SetCreatedBy(createdBy);
        return user;
    }

    public void Update(string name, string updatedBy = SystemActor)
    {
        Name = name.Trim();
        UpdateAuditInfo(updatedBy);
    }
}
```

- construtor privado sem parâmetros;
- `{ get; private set; }`;
- factory estática `Create` que valida;
- mutação só por método de domínio, sempre recebendo `updatedBy`.

**Dapper materializa isso.** Verificado: construtor privado e setters `private`/`protected`
funcionam.

### Contratos

`Domain/Interfaces/Repositories/` — um arquivo por repository, cada um estendendo
`IDapperRepository<T>` do Foundation e acrescentando só o que é específico.
**Nenhum `IQueryable`, nenhum `Expression`.**

`Domain/Interfaces/IUnitOfWork.cs` — estende `IDapperUnitOfWork` (que estende
`IBaseUnitOfWork`) e expõe os repositories.

---

## 6. Persistência

### A divisão

| Responsabilidade | Tecnologia |
|---|---|
| Migrations e schema | EF Core |
| Mapeamento (`IEntityTypeConfiguration`) | EF Core |
| **Leitura e escrita em runtime** | **Dapper** |

O `DbContext` existe, é registrado no DI e serve ao `dotnet ef` e ao `SchemaMigrator`.
**Nada mais o resolve.**

### Mapeamento

```csharp
public class UserMap : EntityMap<User>, IEntityTypeConfiguration<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User");
        base.Configure(builder);          // Id, Code único, IsActive, auditoria

        builder.Property(e => e.Email).IsRequired().HasMaxLength(180);
        builder.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
    }
}
```

`ToTable` primeiro, depois `base.Configure`, depois o específico.

**Sem `HasColumnName`.** Cada propriedade se chama como a coluna. Se o nome não serve,
**renomeie a propriedade**.

### Query objects

```csharp
internal sealed class FindUserByEmailQuery(string email) : SqlQuery
{
    public string Email { get; } = email;

    public override string GetSql() => $"""
        SELECT {UserColumns.All}
        FROM User
        WHERE Email = @Email AND IsActive = 1
        """;
}
```

O objeto carrega os parâmetros como propriedades e é entregue ao Dapper como o objeto de
parâmetros — statement e parâmetros não podem divergir.

Mantenha uma constante com a lista de colunas por entidade, para toda query devolver o mesmo
formato.

### Repositories

```csharp
public class UserRepository(IDapperUnitOfWork unitOfWork)
    : DapperRepository<User>(unitOfWork), IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        QuerySingleAsync(new FindUserByEmailQuery(email), ct);

    public void ReplaceRoles(int userId, IReadOnlyCollection<int> roleIds, string actor)
    {
        Enqueue(new ClearUserRolesQuery(userId, actor));
        foreach (var id in roleIds.Distinct()) Enqueue(new GrantRoleToUserQuery(userId, id, actor));
    }
}
```

O `DapperRepository<T>` do Foundation já dá `GetByIdAsync`, `GetByCodeAsync`, `GetAllAsync`,
`GetAllIncludingDeletedAsync`, `GetByIdsAsync`, `CountAsync`, `ExistsAsync`, `Add`,
`AddRange`, `Update`, `Remove` (soft delete), `HardDelete`, e os helpers `Enqueue`,
`QuerySingleAsync`, `QueryAsync`, `ExecuteScalarAsync<T>`, `QueryColumnAsync<T>`.

### Unit of Work

```csharp
public class MeuUnitOfWork(ISqlConnectionFactory factory, /* factories */)
    : DapperUnitOfWork(factory), IUnitOfWork
{
    public IUserRepository UserRepository => _user ??= _userFactory(this);
    // ...
}
```

Nenhuma lógica transacional própria: buffer, `Commit`, `Rollback`,
`ExecuteInTransaction`, rollback no dispose — tudo do Foundation.

### O comportamento que mais confunde

**Com EF, nada vai ao banco até o `Commit`. Com o `DapperUnitOfWork`, é igual** — o
`Add`/`Update`/`Remove` só enfileira, e o `Commit` abre transação, executa na ordem e
confirma. Foi projetado assim para os dois serem intercambiáveis.

**Mas há duas diferenças que mordem:**

1. **Não existe change tracker.** Depois de inserir, a entidade em memória **não recebe o
   `Id`** que o banco atribuiu. Se você precisa do `Id` (para vincular numa tabela de
   ligação, por exemplo), faça `Commit` e **releia**:

   ```csharp
   unitOfWork.UserRepository.Add(user);
   await unitOfWork.CommitAsync(ct);

   var saved = await unitOfWork.UserRepository.GetByEmailAsync(email, ct);
   unitOfWork.UserRepository.ReplaceRoles(saved.Id, roleIds, actor);
   await unitOfWork.CommitAsync(ct);
   ```

2. **Exclusão lógica não é garantida pelo banco.** O EF tem filtro global de query; o Dapper
   não. **Toda query escrita à mão precisa carregar `WHERE IsActive = 1`** — e em cada tabela
   do JOIN. Um `WHERE` esquecido devolve linha excluída ou permissão revogada.

Operações que alteram mais de uma tabela devem passar por `ExecuteInTransactionAsync`.

---

## 7. Application

Por contexto, com `Commands/ Queries/ Handlers/ Validators/ DTOs/`.

- **O command é o contrato de entrada.** O controller faz `[FromBody] SaveUserCommand` e,
  quando há id na rota, `command with { Code = code }`. Não crie um record de request que
  duplica o command.
- `ValidationBehavior` no pipeline do MediatR roda os validators antes de qualquer handler.
- Mensagens de validação em português.
- DTOs expõem `Code`, nunca `Id`.

---

## 8. Api

### Envelope

```csharp
public sealed record SuccessDetails<T>(
    int Status, string Title, string Detail, string Instance, T Data);
```

```csharp
return Ok(new SuccessDetails<UserDto>(
    StatusCodes.Status200OK, "OK", "Usuario criado com sucesso.",
    HttpContext.Request.Path, user));
```

Erro em `ProblemDetails` (RFC 7807). O middleware traduz exceção em status:

| Exceção | HTTP |
|---|---|
| `InputValidationException` | 400 + `errors` por campo |
| `UnauthenticatedException` | 401 |
| `NotFoundException` | 404 |
| `BusinessRuleException` | 422 |
| qualquer outra | 500 com mensagem genérica |

`[ProducesResponseType]` para cada status possível, em toda action.

### Autorização

Guarda por atributo, resolvida por request:

```csharp
[ApiController]
[Route("api/users")]
[Authorize]
[RequireScreen("users")]
public sealed class UsersController(IMediator mediator) : ControllerBase
```

**A guarda é independente do menu.** Esconder item na barra lateral é apresentação; chamar
a rota direto sem a permissão retorna 403.

### Claims

O access token carrega **apenas** `sub`, `user_code`, `tenant_id` e `exp`. As permissões
**não** são claims: são resolvidas por request, com cache. Assim, mudança de permissão vale
no request seguinte, sem relogar, e o token não cresce com o catálogo.

---

## 9. Armadilhas conhecidas

Cada uma destas custou tempo. Estão resolvidas — não descubra de novo.

### Foundation não declara todas as dependências

O pacote empacota as DLLs com `PrivateAssets="all"`, então dependências de terceiros **não
fluem**. Referencie no seu projeto:

- `Konscious.Security.Cryptography.Argon2` — sem ele, o primeiro hash lança
  `FileNotFoundException`;
- `Microsoft.EntityFrameworkCore` e o provider — o `Foundation.Infrastructure` precisa;
- `Dapper` e `Microsoft.Extensions.DependencyInjection.Abstractions` já são declarados a
  partir do `3.2.0-rc.2`.

### Palavras reservadas do MySQL

`Key`, `Order`, `Before`, `After`, `Group`, `Status`, `Rank` são reservadas. O gerador de SQL
convencional do Foundation **não** coloca crase (ele é agnóstico de provider, e crase é
específica do MySQL).

Duas saídas, nesta ordem de preferência:

1. **Renomeie a propriedade.** `Before`/`After` → `OldValues`/`NewValues`. É o que o padrão
   manda ("sem `HasColumnName`: se o nome não serve, renomeie").
2. Quando o nome é realmente o certo — `Screen.Key`, `Screen.Order` — **sobrescreva
   `Add`/`Update`/`Remove` e os reads** no repository com SQL explícito e crases.

### Migration trava no MariaDB

`Database.MigrateAsync()` no EF Core 9+ toma um lock exclusivo com `GET_LOCK`, e o provider
da Oracle lê o resultado como `long` não anulável. O MariaDB pode responder `NULL` ali, e a
inicialização morre com:

```
Unable to cast object of type 'System.DBNull' to type 'System.Int64'
```

Solução no `SchemaMigrator`: gerar o script com `IMigrator.GenerateScript()` e executá-lo
pela conexão Dapper, mantendo o histórico em `__EFMigrationsHistory` — a mesma tabela que o
`dotnet ef` lê, então o tooling continua funcionando. O lock consultivo é tomado com
`GET_LOCK` lido como `long?`.

### `Replace` para qualificar colunas

Não derive a lista com alias por substituição de string. `All.Replace("Id,", "u.Id,")`
transforma `TenantId,` em `Tenantu.Id,`. Escreva as duas listas.

### Licenças

- **FluentAssertions 8+** exige licença **paga** para uso comercial. Fique na `7.2.2`.
- **MediatR 13+** tem licença comercial com faixa gratuita por faturamento. Confira se a
  empresa se enquadra.

---

## 10. Frontend (Next.js)

Quando houver painel web:

- **Sessão em cookie httpOnly**, gravada por route handler do Next. O token nunca chega ao
  JavaScript da página.
- **Proxy** em `/api/backend/[...path]` injeta o Bearer. Trafegue **bytes**
  (`arrayBuffer`), não texto — o mesmo caminho serve JSON, upload multipart e download de
  imagem.
- **Menu montado no servidor.** O `/me` devolve o menu já filtrado; o frontend não recebe o
  catálogo completo para esconder no cliente.
- **Guarda de rota** no server component, redirecionando para uma tela de "sem permissão".
- Rotas e identificadores em inglês; **todo rótulo visível em português**.

### Armadilha de CSS que vale saber

`animation-fill-mode: both` com um quadro final que tem `transform: translateY(0)` deixa o
transform aplicado — e **qualquer transform diferente de `none` faz o elemento virar
containing block para `position: fixed`**. Um modal `fixed inset-0` dentro dele passa a se
posicionar contra o conteúdo, não contra a janela. Termine a animação com `transform: none`
e coloque o modal num **portal no `body`**.

---

## 11. Checklist para um projeto novo

```txt
[ ] ADR-0001 registrando: idioma inglês, EF só schema, Dapper no acesso, SuccessDetails
[ ] Directory.Packages.props com versionamento central e as versões da seção 4
[ ] 5 projetos: Api, Application, Domain, Infrastructure, Shared (+ Tests)
[ ] Shared/Settings com as options; Shared/Exceptions com as 4 exceções
[ ] BaseEntity : Entity com Guid.CreateVersion7()
[ ] Entidades rich domain, factory Create, setters privados
[ ] Interfaces de repository estendendo IDapperRepository<T>, sem IQueryable
[ ] IUnitOfWork : IDapperUnitOfWork expondo os repositories
[ ] {Entity}Map : EntityMap<T> — ToTable, base.Configure, específico
[ ] DbContext só com DbSet e ApplyConfigurationsFromAssembly
[ ] SchemaMigrator (não use MigrateAsync com MariaDB)
[ ] Queries/{Contexto}/ com query objects; TODA query com IsActive = 1
[ ] Repositories : DapperRepository<T>
[ ] UnitOfWork : DapperUnitOfWork
[ ] Application por contexto; command como contrato de entrada
[ ] ValidationBehavior no pipeline
[ ] SuccessDetails, ExceptionHandlingMiddleware, ProducesResponseType
[ ] Guarda de permissão por atributo, independente do menu
[ ] Seed idempotente
[ ] Testes: arquitetura (NetArchTest) + matriz perfil x endpoint
```

---

## 12. O erro que originou este documento

O Revenda Pro foi construído inteiro em português, com estrutura própria, chave primária
`Guid`, envelope inventado e EF Core em todo o acesso a dados. Foram **46 arquivos** que
precisaram ser reescritos.

A causa: o `docs/agent/context.md` do próprio repositório dizia "domínio em português
brasileiro", e foi seguido em vez das referências que o handoff mandava ler primeiro.

**A lição operacional:** a documentação local de um projeto pode estar desatualizada em
relação ao padrão. Leia as referências **antes**, e quando elas divergirem entre si, o mais
recente com ADR próprio vence — e registre um ADR no seu projeto dizendo qual você seguiu.
