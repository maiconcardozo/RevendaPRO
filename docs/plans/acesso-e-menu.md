# Plano — Acesso, permissões de tela e menu dinâmico

Refina e substitui os marcos **M1 a M4** de `docs/ROADMAP.md` para o núcleo de acesso.
**Status:** concluido. A0 a A5 implementados; o A6 (testes) foi absorvido pelo marco R11
de `docs/plans/refatoracao-padrao-global.md`. O modelo descrito abaixo esta em portugues
porque e anterior ao ADR-0003; o schema real esta em `docs/database/mappings.md`.

---

## 1. A decisão central

> "Cada permissão vai ser uma tela que ele pode ou não ver. O menu precisa ser salvo no banco.
> As permissões de cada grupo de usuários também. Ao logar, busca essas informações e carrega
> o menu baseado nas permissões."

Se **permissão = tela** e **item de menu = tela**, então as três coisas são a mesma coisa.
Modelar como três tabelas separadas cria três fontes de verdade que vão divergir.

**Proposta: uma tabela `Tela`.** Ela é simultaneamente o catálogo de permissões e o catálogo
do menu. Uma tela que não deve aparecer no menu (ex.: detalhe de veículo) existe na tabela com
`ExibirNoMenu = false`, mas continua sendo uma permissão verificável.

Isso elimina o par `Permissao` + `PerfilPermissao` do handoff original e as strings soltas
(`dashboard.view`, `users.manage`). A chave da tela **é** a permissão.

### O que isso remove do escopo

- Não existe distinção Admin Master / Cliente. A view é sempre a mesma.
- Não existe permissão de ação (editar, excluir) nesta fase — só "vê a tela ou não vê".
- Não existe hierarquia de perfis nem herança.

### Escape hatch, se um dia precisar de ação

A tabela de ligação `PerfilTela` já nasce com espaço para crescer: hoje a existência da linha
significa "pode ver". Se no futuro precisar de granularidade, acrescentam-se colunas
`PodeEditar` / `PodeExcluir` na mesma linha, sem remodelar nada.

---

## 2. Modelo de dados

```text
Empresa
  Codigo, Nome, Ativo, CriadoEm

Tela                          <- catálogo de permissões E do menu
  Codigo          Guid
  Chave           string   único   ex.: "veiculos"
  Nome            string           ex.: "Veículos"          (rótulo no menu)
  Rota            string           ex.: "/veiculos"
  Icone           string           ex.: "Car"               (nome do ícone lucide)
  GrupoMenu       string           ex.: "Operação"          (cabeçalho da seção)
  Ordem           int
  ExibirNoMenu    bool
  TelaPaiCodigo   Guid?            submenu, null = raiz
  Ativo           bool

Perfil                        <- "grupo de usuários"
  Codigo, EmpresaCodigo, Nome, Descricao, DeSistema, Ativo

PerfilTela                    <- a permissão
  PerfilCodigo, TelaCodigo    (PK composta)

Usuario
  Codigo, EmpresaCodigo, Nome, Email, SenhaHash, Ativo, CriadoEm, ExcluidoEm

UsuarioPerfil
  UsuarioCodigo, PerfilCodigo (PK composta)

RefreshToken
  Codigo, UsuarioCodigo, Token, ExpiraEm, RevogadoEm

Auditoria
  Codigo, EmpresaCodigo, UsuarioCodigo, Entidade, Acao, Antes, Depois, Quando
```

`Tela` é **global**, não por empresa: o conjunto de telas do sistema é o mesmo para todos.
Quem varia por empresa é o `Perfil` e o que ele marca em `PerfilTela`.

### Sincronização do catálogo de telas

O catálogo vive em **código** (`CatalogoDeTelas`) e é a fonte da verdade sobre quais telas
existem. Uma rotina roda a cada inicialização da API e reconcilia o banco:

| Situação | Ação |
|---|---|
| Tela nova no catálogo | `INSERT` + vínculo automático com o perfil **Administrador** |
| Nome, ícone, ordem ou grupo mudou | `UPDATE` |
| Tela saiu do catálogo | `Ativo = false` — **nunca** `DELETE` |
| Tela voltou ao catálogo | `Ativo = true`, com os vínculos antigos preservados |

Criar uma tela nova passa a ser: acrescentar uma linha no catálogo e subir a API. Sem migration
e sem SQL manual.

O vínculo automático com Administrador existe para não criar um impasse: sem ele, uma tela
recém-inserida não pertenceria a nenhum perfil e ninguém conseguiria chegar em `/perfis` para
liberá-la. Nenhum outro perfil recebe telas automaticamente.

A sincronização **não** toca em `PerfilTela` fora desse vínculo inicial — ajustes feitos pelo
administrador na matriz de permissões nunca são sobrescritos por um deploy.

### Seed inicial de telas

| Chave | Nome | Rota | Ícone | Grupo | Ordem | Menu |
|---|---|---|---|---|---|---|
| `dashboard` | Dashboard | `/dashboard` | LayoutDashboard | Operação | 1 | sim |
| `veiculos` | Veículos | `/veiculos` | Car | Operação | 2 | sim |
| `custos` | Custos | `/custos` | Receipt | Operação | 3 | sim |
| `vendas` | Vendas | `/vendas` | HandCoins | Operação | 4 | sim |
| `usuarios` | Usuários | `/usuarios` | Users | Administração | 10 | sim |
| `perfis` | Perfis | `/perfis` | ShieldCheck | Administração | 11 | sim |
| `veiculo-detalhe` | Detalhe do veículo | `/veiculos/[codigo]` | — | — | — | **não** |

Telas de fases futuras já entram no seed com `Ativo = false`, e são ligadas quando o módulo
existir. Isso evita uma migration nova a cada módulo.

### Perfis iniciais

| Perfil | Telas |
|---|---|
| Administrador | todas |
| Gestor | dashboard, veiculos, custos, vendas |
| Financeiro | dashboard, custos, vendas |
| Vendedor | dashboard, veiculos, vendas |
| Oficina | dashboard, veiculos, custos |

`DeSistema = true` nos cinco. Não podem ser excluídos; podem ter as telas ajustadas.

---

## 3. Fluxo de login

```text
1. POST /api/auth/login   { email, senha }
   -> valida hash, emite access token (15 min) + refresh token (7 dias)
   -> access token carrega apenas: sub (usuário), emp (empresa), exp
      NÃO carrega a lista de telas

2. GET /api/auth/me       Authorization: Bearer <access>
   -> { usuario: { codigo, nome, email },
        perfis: ["Vendedor"],
        telas:  ["dashboard","veiculos","vendas"],
        menu:   [ { grupo: "Operação",
                    itens: [ { chave, nome, rota, icone, filhos: [] } ] } ] }

3. Frontend guarda a sessão e renderiza o menu a partir de `menu`.
```

### Por que as telas não vão no JWT

Se as permissões viajarem como claims, mudar o perfil de alguém só surte efeito quando o token
expirar — e o token cresce junto com o catálogo de telas. Em vez disso, a API resolve as telas
do usuário a cada request, com `IMemoryCache` **por perfil** (não por usuário). O cache é
invalidado quando o perfil ou seus vínculos mudam. Mudança de permissão passa a valer no
próximo request.

### O menu é montado no servidor

`GET /api/auth/me` já devolve o menu **filtrado e ordenado**. O frontend não recebe a lista
completa de telas para esconder no cliente — recebe só o que pode ver. Esconder no cliente é
apresentação, não segurança.

E independentemente do menu, **todo endpoint carrega sua própria guarda**. Chamar
`GET /api/veiculos` direto, sem passar pelo menu, tem que retornar 403.

---

## 4. Marcos

### A0 — Decisões e ADR-0002 — **concluído em 2026-09-01**

- [x] Fechar as 4 questões da seção 6 deste documento.
- [x] Escrever `docs/architecture/decisions/ADR-0002-acesso-por-tela.md`.
- [x] Substituir as permissões em inglês (`dashboard.view`, `users.manage`) pelas chaves de
      tela em `docs/api/endpoints.md`.
- [x] Atualizar `docs/database/mappings.md` com o modelo desta seção 2.

Concluído junto com o A0: removidos os arquivos de template
(`WeatherForecastController`, `Class1.cs`), `Program.cs` reescrito em formato legível e
versões alinhadas via `Directory.Packages.props` (EF Core 9.0.19 + Pomelo 9.0.0 sobre net10.0 —
Pomelo não tem release para EF Core 10).

---

### A1 — Modelo e schema — **concluido**

- Entidades de domínio: `Empresa`, `Tela`, `Perfil`, `PerfilTela`, `Usuario`, `UsuarioPerfil`,
  `RefreshToken`, `Auditoria`.
- `RevendaProDbContext` + `IEntityTypeConfiguration` por entidade.
- Migration inicial.
- Seed idempotente: telas da tabela acima, 5 perfis, empresa piloto, usuário administrador
  vindo de `REVENDAPRO_ADMIN_EMAIL` / `REVENDAPRO_ADMIN_PASSWORD`.
- Índice único em `Tela.Chave` e em `Usuario.Email` por empresa.

**Pronto quando:** `docker compose up` cria o schema e semeia telas, perfis e administrador;
rodar duas vezes não duplica.

---

### A2 — Login, sessao e `/me` — **concluido**

- Hash de senha com `PasswordHasher`.
- `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/refresh`, `POST /api/auth/logout`.
- Refresh token persistido, com rotação e revogação no logout.
- `/me` devolve usuário, perfis, chaves de tela e o menu já montado e ordenado.
- Envelope `data` para sucesso e `ProblemDetails` para erro.

**Pronto quando:** login devolve token; `/me` devolve o menu correto para cada um dos 5 perfis;
logout invalida o refresh.

---

### A3 — Autorizacao no servidor — **concluido**

- `ServicoDePermissao`: dado o usuário, devolve o conjunto de chaves de tela. `IMemoryCache`
  por perfil, com invalidação ao salvar perfil ou vínculo.
- Atributo/policy `[ExigeTela("veiculos")]` aplicado em todo endpoint não público.
- 401 sem token, 403 com token sem a tela.
- Swagger com esquema Bearer.

**Pronto quando:** um usuário do perfil Vendedor recebe 403 ao chamar `GET /api/usuarios`
direto, mesmo com token válido; e ao ganhar a tela, o acesso passa a funcionar sem relogar.

---

### A4 — Menu dinamico e guardas no frontend — **concluido**

- Cliente HTTP central: injeta o Bearer, renova em 401, desloga em falha do refresh.
- Sessão em cookie httpOnly (route handler do Next), substituindo
  `localStorage.setItem("revenda-pro-session", ...)`.
- `PanelShell` deixa de ter o array `nav` hardcoded e passa a renderizar o menu de `/me`,
  agrupado por `GrupoMenu`.
- Skeleton do menu enquanto `/me` não responde — sem spinner e sem deslocar o layout.
- Middleware de rota: sem sessão vai para `/login`; com sessão em `/login` vai para a
  primeira tela permitida (não necessariamente `/dashboard` — um perfil pode não ter dashboard).
- Tela 403 dentro do shell, para acesso direto por URL a uma rota sem permissão.
- Remover `http://localhost:5100` hardcoded em `frontend/app/login/page.tsx`; usar variável
  de ambiente.

**Pronto quando:** trocar o perfil de um usuário muda o menu dele no próximo login, sem
alterar código; digitar uma rota não permitida na barra de endereços mostra 403.

---

### A5 — Telas de administracao — **concluido**

- **Perfis:** listar, criar, editar, excluir (perfil de sistema não é excluível). A edição é
  uma **matriz de telas** agrupada por `GrupoMenu`, com marcar/desmarcar tudo por grupo.
- **Usuários:** listar com busca por nome, e-mail e perfil; criar, editar, ativar/inativar,
  excluir logicamente. Usuário não exclui a própria conta. Um usuário sem nenhum perfil não
  consegue logar — bloquear no salvamento.
- Remover todo o `localStorage` de `frontend/app/usuarios/page.tsx` e
  `frontend/app/perfis/page.tsx`.
- Auditoria de criar, editar, inativar e excluir.
- *(Opcional, decisão em A0)* tela de manutenção do próprio menu: nome, ícone, ordem, grupo.

**Pronto quando:** dá para criar um perfil novo, marcar telas, criar um usuário nele, logar
com esse usuário e ver exatamente aquelas telas no menu.

---

### A6 — Testes — **PENDENTE**

- Arquitetura (NetArchTest): `Domain` sem dependências internas, `Application` sem referência
  a `Infrastructure`/`Api`.
- Unidade: resolução de telas do usuário, invalidação do cache, regra de perfil de sistema,
  regra de autoexclusão, isolamento por empresa.
- Integração: matriz perfil x endpoint — para cada um dos 5 perfis, todo endpoint responde
  200 ou 403 conforme esperado. Este é o teste que impede regressão de segurança.

**Pronto quando:** a matriz perfil x endpoint está verde e cobre todos os endpoints não
públicos.

---

## 5. Dependências

```text
A0 -> A1 -> A2 -> A3 ─┐
                A2 -> A4 ─┴─> A5 -> A6
```

A4 pode andar em paralelo a A3 assim que o contrato de `/me` estiver fechado.

---

## 6. Questões fechadas

Decididas em 2026-09-01 e registradas em
`docs/architecture/decisions/ADR-0002-acesso-por-tela.md`.

| # | Questão | Decisão |
|---|---|---|
| 1 | "Perfil" ou "Grupo de usuários"? | **Perfil** no código, banco e rota `/perfis`; rótulo "Perfil de acesso" na interface |
| 2 | Um perfil ou vários por usuário? | **N:N no banco**, um único perfil na interface nesta fase |
| 3 | Menu editável ou seed? | **Seed sincronizado no startup** — tela nova no catálogo vira INSERT automático |
| 4 | Perfil sem telas? | Permitido salvar; o usuário vê tela de "sem acesso" ao logar |
