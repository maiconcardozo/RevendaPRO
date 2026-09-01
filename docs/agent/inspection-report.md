# Relatório de inspeção — RevendaPro vs. padrão Global

Data: 2026-09-01
Modo: **inspeção**. Nenhum código foi alterado.

---

## Fase 0 — confirmação de leitura

Obrigatória por `Arquitetura.Global/AGENTS.md`.

### Documentos do Arquitetura.Global lidos

- `AGENTS.md`
- `docs/standards/nomenclatura.md`
- `docs/standards/tech-stack.md`
- `docs/standards/architecture.md` (estrutura da Infrastructure, Unit of Work, Queries)

### Projeto de referência lido — PainelGestao.CPComunica

- `AGENTS.md` (regra de idioma ADR-0002)
- `docs/api/responses.md`
- `docs/architecture/database-english-columns.md`
- `src/…Domain/Entities/BaseEntity.cs`, `Entities/Acesso.cs`
- `src/…Domain/Interfaces/` (Repositories e Services)
- `src/…Application/Authentication/`, `Behaviors/`, `Common/`
- `src/…Api/Controllers/AuthController.cs`, `Contracts/SuccessDetails.cs`, `Middlewares/`
- `src/…Infrastructure/` (árvore de pastas)
- `Directory.Build.props`, `.csproj` do Domain

### Projeto alvo lido — RevendaPro

Os 46 arquivos `.cs` de `src/`, `Directory.Packages.props`, `docker-compose.yml`,
`docs/` do próprio repositório.

### Não lido

- `docs/standards/patterns.md`, `best-practices.md`, `api-controllers.md`,
  `api-responses.md`, `code-inspection.md`, `quality-gates.md`, `operability.md`,
  `redis-cache.md` do Arquitetura.Global.
  **Impacto:** o relatório cobre idioma, camadas, estrutura, persistência e contrato HTTP
  com evidência direta. Pode haver achados adicionais de observabilidade, cache e quality
  gates ainda não levantados.
- Pacote `Foundation.Base` (usado pelo CPComunica) — não inspecionado o conteúdo.
  **Impacto:** a recomendação sobre `BaseEntity` assume o contrato observado no
  `BaseEntity.cs` do CPComunica, não a API completa do pacote.

---

## Resumo do padrão a aplicar

| Dimensão | Padrão |
|---|---|
| **Idioma** | **Todo o código em inglês.** Só o texto que o usuário lê fica em português: rótulo de tela, `detail` das respostas HTTP e dado exibido. Vale para entidade, propriedade, enum, handler, DTO, namespace, pasta, nome de arquivo, rota HTTP, claim, código de permissão, tabela e coluna. |
| **Camadas** | `Api → Application + Infrastructure`; `Application → Domain`; `Infrastructure → Domain`. `Domain` não conhece tecnologia. |
| **Identidade** | Toda entidade persistida tem `Id` numérico interno **e** `Code` UUID v7 público. Rotas e contratos expõem `Code`, nunca `Id`. |
| **Exclusão** | Sempre lógica, com filtro global do EF. |
| **Persistência** | EF Core + Unit of Work para escrita; **Dapper para consultas** em `Infrastructure/Queries/{Contexto}/`. Sem `HasColumnName`: a propriedade se chama como a coluna. |
| **Infrastructure** | Pastas fixas: `Configuration/`, `Data/`, `Queries/`, `Repositories/`, `UnitOfWork/`, `Services/`, `Cache/`, `Mappings/`. Nada solto na raiz. |
| **Contrato HTTP** | Sucesso em `SuccessDetails<T>(status, title, detail, instance, data)`; erro em `ProblemDetails`. `[ProducesResponseType]` em toda action. |
| **Controllers** | Finos, com MediatR. O command é o próprio contrato de entrada (`[FromBody] XCommand`). |

---

## Conflito entre as duas referências — precisa da sua decisão

Os dois repositórios de referência **discordam** em dois pontos. Registro para não decidir
por você.

### 1. Idioma

`Arquitetura.Global/docs/standards/nomenclatura.md` diz o contrário do CPComunica:

> "O idioma padrao dos projetos e portugues brasileiro. Todo conceito de negocio, dominio,
> caso de uso, regra, documento e exemplo deve ser escrito em portugues."

`PainelGestao.CPComunica/AGENTS.md`, ADR-0002 de 30/08/2026:

> "todo o código é em inglês; só o texto que o usuário lê fica em português."

O CPComunica é mais recente e registra a mudança como ADR próprio. Seu código confirma:
`class User`, `class Role`, `AuthenticateUserCommand`, `UsersController`.
**Sua instrução é essa, e é a que vou seguir.** Fica pendente registrar um ADR no RevendaPro,
como o CPComunica fez, para o `nomenclatura.md` não ser cobrado depois.

Restos de português no CPComunica são migração incompleta, não padrão a copiar:
`Entities/Acesso.cs` (nome de arquivo) contém `class User`; e
`Common/Exceptions/ConflitoNegocioException.cs` ainda não foi traduzido.

### 2. Dapper

`Arquitetura.Global/docs/standards/tech-stack.md`:

> "Dapper para SQL controlado/bases legadas; EF Core quando houver `DbContext`,
> Unit of Work ou modelo relacional orientado a entidades"

**O CPComunica não usa Dapper em lugar nenhum** — zero referências em `.cs` e `.csproj`.
Ele é 100% EF Core.

Leitura que atende sua instrução e o padrão ao mesmo tempo, e é a que recomendo:
**CQRS de persistência** — EF Core + Unit of Work para escrita (commands), **Dapper para
leitura** (queries), em `Infrastructure/Queries/{Context}/`, conforme a estrutura
obrigatória do `architecture.md`. É o único arranjo em que `Queries/` com SQL versionado
faz sentido.

---

## Resumo executivo

O núcleo de acesso está **funcionalmente correto e testado** — login, JWT, refresh com
rotação, autorização por tela, menu dinâmico, upload de foto com validação de assinatura.
Nada disso precisa ser repensado.

O problema é de **conformidade**: o código foi escrito inteiro em português, incluindo os
sufixos técnicos, e ignora quatro decisões estruturais do padrão. São **46 arquivos** em
`src/`, e praticamente todos precisam de renomeação.

Causa: segui `docs/agent/context.md` deste repositório ("domínio em português brasileiro")
em vez de ler `Arquitetura.Global/AGENTS.md` e o CPComunica, como o próprio
`docs/AGENT_HANDOFF.md` mandava na seção 5. Erro meu, e o documento local está errado
também — entra na correção.

---

## Achados

### ALTO — A1. Todo o código de negócio em português

**Arquivos:** os 46 de `src/`.

**Evidência:**

```csharp
public sealed class Usuario                      // deveria ser User
public interface IRepositorioDeUsuarios          // deveria ser IUserRepository
public sealed class ManipuladorDeEntrar          // deveria ser AuthenticateUserHandler
public sealed record EntrarCommand               // deveria ser AuthenticateUserCommand
public sealed class ComportamentoDeValidacao<,>  // deveria ser ValidationBehavior<,>
public interface IUnidadeDeTrabalho              // deveria ser IUnitOfWork
```

Pastas: `Entidades/`, `Contratos/`, `Excecoes/`, `Comum/`, `Persistencia/`, `Seguranca/`,
`Semeadura/`, `Opcoes/`, `Arquivos/`, `Autorizacao/`, `Telas/`, `Usuarios/`, `Perfis/`,
`Autenticacao/`.

**Regra violada:** ADR-0002 do CPComunica — código em inglês, exceto texto de tela.

**Impacto:** o RevendaPro não pode compartilhar convenção, revisão ou pacote `Foundation`
com os demais `*.Global`. Quanto mais código entrar antes da correção, mais cara ela fica —
hoje são 46 arquivos e nenhum consumidor externo.

**Correção:** renomear tudo. Mapa na seção seguinte.

---

### ALTO — A2. Chave primária `Guid` público, sem `Id` interno

**Arquivos:** todas as entidades em `Domain/Entidades/`, todas as configurations,
a migration inicial.

**Evidência:**

```csharp
public Guid Codigo { get; private set; }   // PK char(36), exposta na rota
```

```
builder.HasKey(u => u.Codigo);
builder.Property(u => u.Codigo).HasColumnType("char(36)")
```

**Regra violada:** CPComunica `AGENTS.md` — "toda entidade persistida possui `Id` numérico
interno e `Code` UUID v7 público".

**Impacto:** dois problemas reais, não estéticos.
1. `char(36)` como PK clusterizada em MariaDB: índice ~4,5x maior que `int`, e todo índice
   secundário carrega a PK. Cada FK também vira `char(36)`.
2. `Guid.NewGuid()` é aleatório (v4). Como PK clusterizada, provoca fragmentação de página
   a cada insert. O padrão exige **UUID v7**, que é ordenável por tempo, exatamente para
   evitar isso — e o CPComunica usa `Guid.CreateVersion7()`.

**Correção:** `BaseEntity` com `Id` int + `Code` UUID v7. Rotas e DTOs expõem `Code`.
Migration nova. Como não há dado de produção, é recriar o schema.

---

### ALTO — A3. Nenhum uso de Dapper; leitura e escrita ambas em EF Core

**Arquivos:** `Infrastructure/Persistencia/Repositorios/Repositorios.cs`,
`Infrastructure/Seguranca/ServicoDePermissoes.cs`.

**Evidência:** não existe pasta `Queries/`. Não há referência a Dapper no
`Directory.Packages.props` nem em `.csproj` nenhum. Consultas de leitura usam LINQ:

```csharp
var chaves = await contexto.PerfisTelas
    .AsNoTracking()
    .Where(pt => pt.PerfilCodigo == perfilCodigo && pt.Tela!.Ativo)
    .Select(pt => pt.Tela!.Chave)
    .ToListAsync(ct);
```

**Regra violada:** `architecture.md` — `Queries/` é obrigatória quando houver SQL
versionado; `tech-stack.md` lista Dapper como padrão de consulta.

**Impacto:** a montagem do menu e a resolução de permissões rodam a cada request. Em LINQ
com `Include`/join implícito, o SQL fica fora do controle de quem escreve. Com Dapper o SQL
é explícito, revisável e indexável.

**Correção:** `Infrastructure/Queries/SqlQuery.cs` + `Queries/{Context}/` com Dapper para
todas as leituras. EF Core + UnitOfWork permanecem só na escrita.

---

### ALTO — A4. Envelope de resposta fora do contrato

**Arquivos:** `Api/Comum/TratamentoDeExcecoes.cs`, os três controllers.

**Evidência:**

```csharp
public sealed record RespostaDeSucesso<T>(T Data);   // envelope proprio
return Ok(RespostaDeSucesso<Sessao>.De(sessao));     // { "data": {...} }
```

O padrão é `SuccessDetails<T>` com cinco campos:

```csharp
return Ok(new SuccessDetails<AuthenticateUserResult>(
    StatusCodes.Status200OK, "OK", "Autenticação realizada com sucesso.",
    HttpContext.Request.Path, result));
```

**Regra violada:** `docs/api/responses.md` do CPComunica e o próprio
`docs/api/responses.md` deste repositório, que já dizia `SuccessDetails`.

**Impacto:** o frontend do RevendaPro lê `corpo.data`; o de qualquer outro `*.Global` lê o
envelope completo. Um cliente compartilhado não serve os dois.

**Correção:** adotar `SuccessDetails<T>`. As chaves ficam em inglês e o `detail` em
português — é justamente o texto que o usuário lê.

---

### MÉDIO — A5. Estrutura da Infrastructure fora do padrão

**Evidência — atual × exigido:**

```txt
ATUAL                              EXIGIDO
Persistencia/                      Configuration/ServiceCollectionExtensions.cs
  RevendaProDbContext.cs           Data/MariaDb/
  Configuracoes/                   Queries/SqlQuery.cs + Queries/{Context}/
  Repositorios/Repositorios.cs     Repositories/{Context}/{Entity}Repository.cs
Seguranca/                         UnitOfWork/RevendaProUnitOfWork.cs
Semeadura/                         Services/
Opcoes/                            Cache/
Arquivos/                          Mappings/
Telas/
InjecaoDeDependencia.cs  <- solto na raiz
```

**Regra violada:** `architecture.md` — "O agente nao deve criar arquivos soltos diretamente
na raiz da Infrastructure".

**Impacto:** `Repositorios.cs` concentra 6 repositories num arquivo só. Sem `UnitOfWork/`,
a transação fica implícita no `SaveChangesAsync` de cada handler.

---

### MÉDIO — A6. `[ProducesResponseType]` ausente e command não é o contrato

**Arquivos:** os três controllers.

**Evidência:**

```csharp
public sealed record EntrarRequest(string Email, string Senha);   // duplica o command

[HttpPost("login")]                                                // sem ProducesResponseType
public async Task<IActionResult> Entrar(EntrarRequest requisicao, CancellationToken ct)
```

**Regra violada:** padrão do `AuthController` do CPComunica — `[FromBody] XCommand` direto
e `[ProducesResponseType]` para cada status possível.

**Impacto:** Swagger não descreve os retornos; e existem dois contratos para a mesma
entrada, que vão divergir.

---

### MÉDIO — A7. Exclusão lógica só em `User`, sem filtro global nas demais

**Evidência:** `RevendaProDbContext` aplica `HasQueryFilter` apenas em `Usuario`.
`Perfil`, `Tela`, `Empresa` não têm exclusão lógica; `ExcluirPerfilCommand` faz
`perfis.Remover(perfil)` — DELETE físico.

**Regra violada:** CPComunica `AGENTS.md` — "toda exclusão é lógica e deve respeitar o
filtro global do EF".

**Impacto:** excluir um perfil apaga a linha e leva junto o histórico de quem o teve.

---

### MÉDIO — A8. Multiempresa por `Guid`, divergente do padrão `TenantEntity`

**Evidência:** `EmpresaCodigo` (Guid) repetido em cada entidade, filtrado à mão em cada
repository. O padrão tem `TenantEntity : BaseEntity` com `TenantId` int e o isolamento
centralizado.

**Impacto:** o filtro por empresa depende de cada consulta lembrar de aplicá-lo. Uma query
nova que esqueça vaza dado entre empresas. Esse é o risco que o `TenantEntity` existe para
eliminar.

---

### BAIXO — A9. Sem `Foundation.Base`

O CPComunica herda `BaseEntity : Entity` de `Foundation.Base 3.1.1-rc.1`, que já traz
`Code`, `IsActive`, `DtCreated`, `DtUpdated`, `SoftDelete`, `UpdateAuditInfo`.
O RevendaPro reimplementou auditoria e exclusão à mão, de forma incompleta.

**Correção:** avaliar consumir `Foundation.Base`. Depende de o feed do pacote estar
acessível a este repositório — a verificar antes de decidir.

---

### BAIXO — A10. Documentação interna induz ao erro

**Arquivos:** `docs/agent/context.md`, `docs/agent/instructions.md`, `docs/AGENT_HANDOFF.md`.

**Evidência:**

```
docs/agent/context.md:      "Dominio e regras de negocio em portugues brasileiro."
docs/agent/instructions.md: "3. Preserve o dominio em portugues brasileiro."
docs/AGENT_HANDOFF.md:      "nomes de negócio em português"
```

**Impacto:** foi o que me levou ao erro. Enquanto estiver assim, o próximo agente repete.

---

## Mapa de renomeação

### Domain

| Atual | Novo |
|---|---|
| `Entidades/Usuario.cs` | `Entities/User.cs` |
| `Entidades/Perfil.cs` | `Entities/Role.cs` |
| `Entidades/Tela.cs` | `Entities/Screen.cs` |
| `Entidades/PerfilTela.cs` | `Entities/RoleScreen.cs` |
| `Entidades/UsuarioPerfil.cs` | `Entities/UserRole.cs` |
| `Entidades/Empresa.cs` | `Entities/Tenant.cs` |
| `Entidades/RefreshToken.cs` | `Entities/RefreshToken.cs` |
| `Entidades/Auditoria.cs` | `Entities/AuditLog.cs` |
| `Enums/AcaoDeAuditoria.cs` | `Enums/AuditAction.cs` |
| `Excecoes/RegraDeNegocioException.cs` | `Exceptions/BusinessRuleException.cs` |
| `Contratos/IRepositorios.cs` | `Interfaces/Repositories/I{Entity}Repository.cs` (um por arquivo) |
| `Contratos/IServicosDeSeguranca.cs` | `Interfaces/Services/IPasswordHasher.cs`, `ITokenService.cs`, `IPermissionService.cs` |
| `Contratos/IArmazenamentoDeFotos.cs` | `Interfaces/Services/IPhotoStorageService.cs` |
| `Documentos.cs` | `ValueObjects/` ou `Validation/BrazilianDocuments.cs` |
| — | `Interfaces/IUnitOfWork.cs` |
| — | `Entities/BaseEntity.cs`, `Entities/TenantEntity.cs` |

Propriedades: `Codigo→Code` (+ `Id` novo), `Nome→Name`, `Email→Email`, `SenhaHash→PasswordHash`,
`Ativo→IsActive`, `CriadoEm→DtCreated`, `ExcluidoEm→DtDeleted`, `Foto→Photo`,
`Documento→Document`, `Telefone→Phone`, `Chave→Key`, `Rota→Route`, `Icone→Icon`,
`GrupoMenu→MenuGroup`, `Ordem→Order`, `ExibirNoMenu→ShowInMenu`, `DeSistema→IsSystem`,
`EmpresaCodigo→TenantId`.

### Application

| Atual | Novo |
|---|---|
| `Autenticacao/EntrarCommand.cs` | `Authentication/Commands/AuthenticateUserCommand.cs` + `Handlers/` + `Validators/` |
| `Autenticacao/RenovarCommand.cs` | `Authentication/Commands/RenewSessionCommand.cs` |
| `Autenticacao/MontadorDeSessao.cs` | `Authentication/Services/SessionBuilder.cs` |
| `Autenticacao/Contratos.cs` | `Authentication/Results/` |
| `Usuarios/CasosDeUso.cs` | `Users/Commands/`, `Queries/`, `Handlers/`, `Validators/` |
| `Usuarios/Foto.cs` | `Users/Commands/UploadUserPhotoCommand.cs` etc. |
| `Perfis/CasosDeUso.cs` | `Roles/…` |
| `Telas/ListarTelasQuery.cs` | `Screens/Queries/ListScreensQuery.cs` |
| `Comum/ComportamentoDeValidacao.cs` | `Behaviors/ValidationBehavior.cs` |
| `Comum/ExcecaoDeValidacao.cs` | `Common/Exceptions/ValidationException.cs` etc. |
| `Comum/InjecaoDeDependencia.cs` | `Configuration/ServiceCollectionExtensions.cs` |

### Infrastructure

| Atual | Novo |
|---|---|
| `Persistencia/RevendaProDbContext.cs` | `Data/MariaDb/RevendaProDbContext.cs` |
| `Persistencia/Configuracoes/*.cs` | `Data/MariaDb/Configurations/{Entity}Map.cs` |
| `Persistencia/Repositorios/Repositorios.cs` | `Repositories/{Context}/{Entity}Repository.cs` |
| `Persistencia/FabricaDeContexto…` | `Data/MariaDb/DesignTimeDbContextFactory.cs` |
| `Seguranca/HashDeSenhaIdentity.cs` | `Services/Security/PasswordHasherService.cs` |
| `Seguranca/GeradorDeTokenJwt.cs` | `Services/Security/JwtTokenService.cs` |
| `Seguranca/ServicoDePermissoes.cs` | `Services/Security/PermissionService.cs` |
| `Semeadura/SemeadorInicial.cs` | `Data/MariaDb/DbInitializer.cs` |
| `Telas/CatalogoDeTelas.cs` | `Screens/ScreenCatalog.cs` |
| `Telas/SincronizadorDeTelas.cs` | `Screens/ScreenSynchronizer.cs` |
| `Arquivos/ArmazenamentoDeFotosEmDisco.cs` | `Services/Storage/DiskPhotoStorageService.cs` |
| `Opcoes/OpcoesDeJwt.cs` | `Configuration/JwtOptions.cs` |
| `InjecaoDeDependencia.cs` | `Configuration/ServiceCollectionExtensions.cs` |
| — | `Queries/SqlQuery.cs` + `Queries/{Context}/` (Dapper) |
| — | `UnitOfWork/RevendaProUnitOfWork.cs` |

### Api

| Atual | Novo |
|---|---|
| `Controllers/AutenticacaoController.cs` | `Controllers/AuthController.cs` |
| `Controllers/UsuariosController.cs` | `Controllers/UsersController.cs` |
| `Controllers/PerfisController.cs` | `Controllers/RolesController.cs` + `ScreensController.cs` |
| `Autorizacao/ExigeTelaAttribute.cs` | `Authorization/RequireScreenAttribute.cs` |
| `Comum/TratamentoDeExcecoes.cs` | `Middlewares/ExceptionHandlingMiddleware.cs` |
| `Comum/UsuarioAtual.cs` | `Security/CurrentUser.cs` |
| — | `Contracts/SuccessDetails.cs` |

### Banco e rotas

Tabelas e colunas em inglês: `User`, `Role`, `Screen`, `RoleScreen`, `UserRole`, `Tenant`,
`RefreshToken`, `AuditLog`.

Chaves de tela viram inglês: `dashboard`, `vehicles`, `costs`, `sales`, `users`, `roles`.
Rotas: `/veiculos → /vehicles`, `/usuarios → /users`, `/perfis → /roles`.

**Atenção:** o rótulo do menu (`Name`) continua em português — é texto de tela. Igual à
exceção que o CPComunica registrou para `Permissions.Name` e `Module`.

---

## Ordem recomendada de correção

Sem dado de produção e sem consumidor externo, o schema pode ser recriado. Isso torna a
correção muito mais barata agora do que depois do módulo de veículos.

| # | Etapa | Depende de |
|---|---|---|
| 1 | **ADR-0003**: idioma inglês, `Id`+`Code`, Dapper na leitura, `SuccessDetails`. Corrigir `docs/agent/*` e `AGENT_HANDOFF.md` (achado A10) | — |
| 2 | Decidir sobre `Foundation.Base`: verificar acesso ao feed | 1 |
| 3 | `BaseEntity`/`TenantEntity` com `Id` + `Code` UUID v7 e soft delete (A2, A7, A9) | 2 |
| 4 | Domain em inglês: entidades, enums, interfaces (A1) | 3 |
| 5 | Infrastructure na estrutura obrigatória + `UnitOfWork/` (A5) | 4 |
| 6 | `Queries/` com Dapper para todas as leituras (A3) | 5 |
| 7 | Application em inglês, com `Commands/Handlers/Validators` por contexto (A1, A6) | 4 |
| 8 | Api: `SuccessDetails`, `ProducesResponseType`, command como contrato (A4, A6) | 7 |
| 9 | Migration única recriando o schema em inglês | 5 |
| 10 | Frontend: rotas, chaves de tela e tipos acompanham o inglês; rótulos seguem em português | 8, 9 |
| 11 | Rodar a suíte de verificação de ponta a ponta que já usamos (login, 403, menu por perfil, upload) | 10 |

O passo 11 não é opcional: a fase de acesso está validada hoje, e a renomeação precisa
terminar com a mesma bateria passando.
