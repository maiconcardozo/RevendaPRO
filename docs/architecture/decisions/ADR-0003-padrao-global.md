# ADR-0003: Alinhamento ao padrão Global — idioma, camadas e acesso a dados

Data: 2026-09-01
Estado: aceito
Substitui: a regra de idioma de `docs/agent/context.md` e `docs/agent/instructions.md`

## Contexto

O núcleo de acesso do Revenda Pro (marcos A0–A5) foi construído inteiro em português,
com estrutura de pastas própria, chave primária `Guid`, envelope de resposta próprio e
EF Core em todo o acesso a dados.

A inspeção registrada em `docs/agent/inspection-report.md` comparou o resultado com os
projetos de referência e encontrou divergências de idioma, camadas, identidade de entidade,
persistência e contrato HTTP.

Causa da divergência: `docs/agent/context.md` deste repositório dizia "domínio e regras de
negócio em português brasileiro", e foi seguido em vez das referências que o
`docs/AGENT_HANDOFF.md` mandava ler primeiro.

Referências consultadas:

| Projeto | Serve de referência para |
|---|---|
| `Global/Authentication` | **Camadas, pastas, `EntityMap`, `UnitOfWork`, stack de pacotes** |
| `PainelGestao.CPComunica` | Idioma (ADR-0002), `BaseEntity`, `SuccessDetails` |
| `Autenticacao.Global` | Padrão `SqlQuery` e repositories com Dapper |
| `Arquitetura.Global` | Regras de camada, dependências e estrutura obrigatória |

## Decisão

### 1. Idioma: todo o código em inglês

Adotada a mesma regra do ADR-0002 do PainelGestao.CPComunica:

> Todo o código é em inglês. Só o texto que o usuário lê fica em português.

Vale para entidade, propriedade, enum, handler, DTO, namespace, pasta, nome de arquivo,
rota HTTP, claim, chave de tela, tabela e coluna.

Permanecem em português:

- rótulo e título de tela;
- `detail` das respostas HTTP, porque o frontend exibe ao usuário;
- a coluna `Screens.Name`, que é rótulo do item de menu — mesma exceção que o CPComunica
  registrou para `Permissions.Name` e `Module`;
- os nomes dos perfis de sistema (`Administrador`, `Gestor`, `Financeiro`, `Vendedor`,
  `Oficina`), que são dado exibido;
- nomes de teste que sejam frase.

`RevendaPro` é nome de produto e não se traduz.

Esta decisão **contraria** `Arquitetura.Global/docs/standards/nomenclatura.md`, que prescreve
português para conceitos de negócio. O CPComunica registrou a mesma exceção em ADR próprio,
em 30/08/2026, e é o padrão em uso nos projetos novos.

### 2. Camadas e pastas: padrão do `Global/Authentication`

```txt
src/
  RevendaPro.Api/
    Controllers/  Middleware/  Services/  Swagger/  Config/
  RevendaPro.Application/
    Behaviors/  Commands/{Context}/  Queries/{Context}/  Handlers/{Context}/
    DTOs/{Context}/  Validators/  Resources/
  RevendaPro.Domain/
    Entities/  Interfaces/Repositories/  Interfaces/Security/  Resources/
  RevendaPro.Infrastructure/
    Configuration/
    Database/Contexts/  Database/Factories/  Database/Migrations/
    Persistence/Mappings/
    Queries/{Context}/
    Repositories/
    UnitOfWork/
  RevendaPro.Shared/
    Common/Responses/  Constants/  Enums/  Exceptions/  Helpers/
  tests/RevendaPro.Tests/
    Unit/  Integration/  Fixtures/  Helpers/
```

Nenhum arquivo solto na raiz de camada.

### 3. Stack: .NET 10 e EF Core 10

O `Global/Authentication` resolve o impasse que travava o EF Core 10 aqui:

| Pacote | Versão | Observação |
|---|---|---|
| .NET | `net10.0` | Em todos os projetos |
| `Microsoft.EntityFrameworkCore` | `10.0.5` | |
| `MySql.EntityFrameworkCore` | `10.0.1` | **Provider da Oracle, não o Pomelo** |
| `Foundation.Base` | `3.1.1-rc.1` | |
| `Dapper` | atual | Acesso a dados |

O `Pomelo.EntityFrameworkCore.MySql` fica **fora**: sua última versão estável é 9.0.0 e não
existe release para EF Core 10. Foi o que forçou o CPComunica a copiar o `EntityMap<T>` do
Foundation localmente. Com o provider da Oracle, o EF Core 10 fica disponível e o
`Foundation.Infrastructure` pode ser consumido direto, sem cópia.

### 4. Entidade: `Foundation.Domain.Abstractions.Entity`

Toda entidade persistida herda de `BaseEntity : Entity`, que traz:

- `Id` numérico interno e `Code` `Guid` público — **UUID v7**, via `Guid.CreateVersion7()`;
- auditoria: `DtCreated`, `DtUpdated`, `DtDeleted`, `CreatedBy`, `UpdatedBy`, `DeletedBy`;
- exclusão lógica: `IsActive`, `SoftDelete()`, `Activate()`.

Rotas e DTOs expõem **`Code`**, nunca `Id`.

O isolamento por empresa usa `TenantEntity : BaseEntity` com `TenantId`, em vez do
`EmpresaCodigo` filtrado à mão em cada consulta.

### 5. Mapeamento: `EntityMap<T>` do Foundation

`Infrastructure/Persistence/Mappings/{Entity}Map.cs`, no padrão do `Global/Authentication`:

```csharp
public class UserMap : EntityMap<User>, IEntityTypeConfiguration<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User");
        base.Configure(builder);
        builder.Property(e => e.Email).IsRequired().HasMaxLength(180);
        builder.HasIndex(e => e.Email).IsUnique();
    }
}
```

Sem `HasColumnName`: cada propriedade se chama como a coluna. Se o nome não servir,
renomeia-se a propriedade.

### 6. EF Core só para schema; Dapper para dados

| Responsabilidade | Tecnologia |
|---|---|
| Migrations e criação do schema | EF Core |
| Mapeamento (`IEntityTypeConfiguration`) | EF Core |
| **Leitura e escrita em runtime** | **Dapper** |

O SQL fica versionado em `Infrastructure/Queries/{Context}/`, no padrão `SqlQuery` do
`Autenticacao.Global`:

```csharp
internal abstract class SqlQuery
{
    public abstract string GetSql();
}

internal sealed class FindUserByEmailQuery : SqlQuery
{
    public FindUserByEmailQuery(string email) => Email = email;

    public string Email { get; }

    public override string GetSql() => """
        SELECT Id, Code, Name, Email, PasswordHash, IsActive
        FROM User
        WHERE Email = @Email AND IsActive = 1
        """;
}
```

O `IUnitOfWork` **estende `Foundation.Domain.Interfaces.UnitOfWork.IBaseUnitOfWork`**, para
manter a mesma assinatura dos demais projetos Global. O contrato vive na `Foundation.Domain`
e nao nomeia tecnologia, entao vale sobre Dapper como vale sobre EF Core.

O que NAO se aproveita e a implementacao `BaseUnitOfWork` do Foundation: verificado por
reflexao, ela recebe `DbContext` no construtor e seu `Commit()` e o `SaveChanges()`. Aqui a
implementacao fica sobre `IDbConnection` e `IDbTransaction`.

Semantica sobre Dapper: cada chamada de repository grava na hora e devolve as linhas
afetadas; a unidade de trabalho acumula esse total e `Commit()` confirma a transacao aberta
e devolve a soma. `BeginAsync` e `RollbackAsync` sao acrescimos ao contrato do Foundation,
que pressupoe o ciclo de vida do EF Core.

**Divergência consciente:** o `Global/Authentication` usa `EntityRepository<T>` do Foundation,
com EF Core em todo o acesso a dados, e não usa Dapper. Aqui o acesso é Dapper por decisão
explícita, para manter o SQL revisável e sob controle. As camadas, o `EntityMap` e o formato
do Unit of Work seguem a referência.

### 7. Senha: Argon2 do Foundation

`Foundation.Shared.Helpers.StringHelper.ComputeArgon2Hash` / `VerifyArgon2Hash`,
substituindo o `PasswordHasher` do ASP.NET Identity.

### 8. Resposta HTTP

Sucesso em `SuccessDetails<T>(status, title, detail, instance, data)`; erro em
`ProblemDetails` (RFC 7807). Chaves em inglês, `detail` em português.
`[ProducesResponseType]` em toda action. O command é o contrato de entrada
(`[FromBody] XCommand`), sem record de request duplicando o command.

## Consequências

Positivas:

- o RevendaPro passa a compartilhar convenção, revisão e o pacote `Foundation` com os
  demais projetos Global;
- `Id` int + `Code` UUID v7 elimina a PK `char(36)` aleatória, que fragmentava página a
  cada insert e inflava todo índice secundário;
- exclusão lógica e auditoria deixam de ser código manual e passam a vir do `Entity`;
- o SQL de leitura fica explícito e indexável, em vez de gerado por LINQ.

Negativas, aceitas:

- **a refatoração toca os 46 arquivos de `src/`**. É barata agora, sem dado de produção nem
  consumidor externo, e cara depois do módulo de veículos;
- **trocar o hash para Argon2 invalida as senhas existentes**. Hoje só existem o
  administrador semeado e dois usuários de teste; o seed regera tudo. Depois de haver
  usuário real, isso exigiria migração de hash;
- trocar Pomelo por `MySql.EntityFrameworkCore` muda o provider do MariaDB. O
  `Global/Authentication` roda contra MariaDB com esse provider, mas o comportamento
  precisa ser confirmado na primeira migration;
- `Screen.Key` e as rotas passam a inglês, quebrando as URLs atuais. Não há usuário externo.

## Pendências

- Confirmar `MySql.EntityFrameworkCore 10.0.1` contra MariaDB 11.8 na geração da primeira
  migration (marco R3).
- Avaliar `IRedisService` do Foundation quando houver cache distribuído.
