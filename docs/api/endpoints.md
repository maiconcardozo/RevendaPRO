# Endpoints

A autorização é feita por **chave de tela**, conforme
`docs/architecture/decisions/ADR-0002-acesso-por-tela.md`. Não existem permissões em string
livre: a chave da tela é a permissão.

Todo endpoint não público exige `Authorization: Bearer <access token>` e é guardado
independentemente do menu. Chamada direta sem a tela no perfil retorna **403**, mesmo que o
item não apareça no menu do usuário.

O idioma das rotas é inglês (ADR-0003); o `detail` das respostas fica em português, porque o
frontend o exibe.

## Autenticação

| Método | Rota | Finalidade | Tela exigida |
|---|---|---|---|
| POST | `/api/auth/login` | Autentica e emite access + refresh token | pública |
| GET | `/api/auth/me` | Usuário, perfis, telas e menu já filtrado | autenticado |
| POST | `/api/auth/refresh` | Renova o access token e rotaciona o refresh | pública (valida o refresh) |
| POST | `/api/auth/logout` | Revoga os refresh tokens | autenticado |

## Administração

| Método | Rota | Finalidade | Tela exigida |
|---|---|---|---|
| GET | `/api/users` | Lista usuários da empresa. `includeDeleted=true` traz também os excluídos | `users` |
| POST | `/api/users` | Cria usuário | `users` |
| PUT | `/api/users/{code}` | Edita usuário | `users` |
| PATCH | `/api/users/{code}/status` | Ativa ou inativa | `users` |
| DELETE | `/api/users/{code}` | Exclusão lógica | `users` |
| POST | `/api/users/{code}/restore` | Traz de volta um usuário excluído, bloqueado | `users` |
| POST | `/api/users/{code}/photo` | Envia a foto (multipart, campo `file`) | `users` |
| DELETE | `/api/users/{code}/photo` | Remove a foto | `users` |
| GET | `/api/users/{code}/photo` | Baixa a foto | autenticado |
| GET | `/api/roles` | Lista perfis | `roles` |
| POST | `/api/roles` | Cria perfil | `roles` |
| PUT | `/api/roles/{code}` | Edita perfil e suas telas | `roles` |
| DELETE | `/api/roles/{code}` | Exclui perfil que não seja de sistema | `roles` |
| GET | `/api/screens` | Catálogo de telas para a matriz de permissões | `roles` |

`GET /api/users/{code}/photo` fica apenas com `[Authorize]` de propósito: qualquer pessoa
autenticada precisa enxergar o próprio avatar na barra lateral, mesmo sem acesso à
administração de usuários.

O `{code}` das rotas é o **`Code` (UUID v7) público**. O `Id` interno nunca é exposto.


## Ativo, inativo e excluído

Três estados, e duas colunas distintas por trás deles. Confundi-las já custou um defeito:
inativar escrevia a coluna da exclusão lógica, então a pessoa sumia da listagem e a tentativa
de reativá-la respondia **404 "Usuário inexistente."**.

| Estado na tela | `isBlocked` | `isActive` | Aparece na listagem |
|---|---|---|---|
| Ativo | falso | verdadeiro | sim |
| Inativo | verdadeiro | verdadeiro | sim |
| Excluído | qualquer | falso | apenas com `includeDeleted=true` |

- **Inativar** é `PATCH /api/users/{code}/status` com `isBlocked`. A pessoa continua na lista.
- **Excluir** é `DELETE /api/users/{code}`. A linha sai de toda leitura.
- **Restaurar** é `POST /api/users/{code}/restore`, e devolve a pessoa **bloqueada**.

## Operação

Endpoints das fases seguintes. As telas já existem no catálogo e são liberadas por perfil
quando o módulo for implementado.

| Rota | Tela exigida | Marco |
|---|---|---|
| `/api/vehicles/*` | `vehicles` | M6 |
| `/api/costs/*` | `costs` | M7 |
| `/api/sales/*` | `sales` | M8 |

## Infraestrutura

| Método | Rota | Finalidade | Tela exigida |
|---|---|---|---|
| GET | `/health` | Sonda de saúde | pública |

## Envelope

Sucesso em `SuccessDetails<T>`; erro em `ProblemDetails` (RFC 7807).

```json
{
  "status": 200,
  "title": "OK",
  "detail": "Sessão carregada.",
  "instance": "/api/auth/me",
  "data": { }
}
```

## Resposta de `GET /api/auth/me`

```json
{
  "status": 200,
  "title": "OK",
  "detail": "Sessão carregada.",
  "instance": "/api/auth/me",
  "data": {
    "user": {
      "code": "01a05ed4-fdac-73e3-b01f-0ba878204d44",
      "name": "Administrador",
      "email": "admin@revendapro.local",
      "hasPhoto": false
    },
    "roles": ["Administrador"],
    "screens": ["dashboard", "vehicles", "costs", "sales", "users", "roles", "my-account"],
    "menu": [
      {
        "group": "Operação",
        "items": [
          { "key": "dashboard", "name": "Dashboard", "route": "/dashboard", "icon": "LayoutDashboard", "children": [] },
          { "key": "vehicles",  "name": "Veículos",  "route": "/vehicles",  "icon": "Car",             "children": [] }
        ]
      },
      {
        "group": "Administração",
        "items": [
          { "key": "users", "name": "Usuários", "route": "/users", "icon": "Users",       "children": [] },
          { "key": "roles", "name": "Perfis",   "route": "/roles", "icon": "ShieldCheck", "children": [] }
        ]
      }
    ]
  }
}
```

`menu` contém apenas telas com `ShowInMenu = true` às quais o usuário tem acesso, já
agrupadas e ordenadas. `screens` traz todas as chaves permitidas, inclusive as que ficam
fora do menu, para a guarda de rota no frontend.

Repare que `key` e `route` estão em inglês, e `name` em português: a chave é código, o nome
é rótulo de tela.

Um perfil sem telas devolve `screens` e `menu` vazios — o frontend mostra a tela de
sem acesso.

## Erros

| Status | Quando |
|---|---|
| 400 | Validação de entrada, com `errors` por campo |
| 401 | Sem token, token expirado ou credenciais inválidas |
| 403 | Token válido, mas o perfil sem a tela exigida |
| 404 | Registro ausente |
| 422 | Regra de negócio |
| 500 | Falha inesperada, com mensagem genérica |

```json
{
  "title": "Dados inválidos",
  "status": 400,
  "detail": "Os dados informados são inválidos.",
  "instance": "/api/users",
  "errors": {
    "Email": ["E-mail inválido."],
    "Document": ["CPF ou CNPJ inválido."]
  }
}
```
