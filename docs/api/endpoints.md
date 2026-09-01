# Endpoints

A autorização é feita por **chave de tela**, conforme
`docs/architecture/decisions/ADR-0002-acesso-por-tela.md`. Não existem permissões em string
livre: a chave da tela é a permissão.

Todo endpoint não público exige `Authorization: Bearer <access token>` e é guardado
independentemente do menu. Chamada direta sem a tela no perfil retorna **403**, mesmo que o
item não apareça no menu do usuário.

## Autenticação

| Método | Rota | Finalidade | Tela exigida |
|---|---|---|---|
| POST | `/api/auth/login` | Autentica e emite access + refresh token | pública |
| GET | `/api/auth/me` | Usuário, perfis, telas e menu já filtrado | autenticado |
| POST | `/api/auth/refresh` | Renova o access token e rotaciona o refresh | pública (valida o refresh) |
| POST | `/api/auth/logout` | Revoga o refresh token | autenticado |

## Administração

| Método | Rota | Finalidade | Tela exigida |
|---|---|---|---|
| GET | `/api/usuarios` | Lista usuários da empresa | `usuarios` |
| POST | `/api/usuarios` | Cria usuário | `usuarios` |
| PUT | `/api/usuarios/{codigo}` | Edita usuário | `usuarios` |
| PATCH | `/api/usuarios/{codigo}/situacao` | Ativa ou inativa | `usuarios` |
| DELETE | `/api/usuarios/{codigo}` | Exclusão lógica | `usuarios` |
| GET | `/api/perfis` | Lista perfis | `perfis` |
| POST | `/api/perfis` | Cria perfil | `perfis` |
| PUT | `/api/perfis/{codigo}` | Edita perfil e suas telas | `perfis` |
| DELETE | `/api/perfis/{codigo}` | Exclui perfil não de sistema | `perfis` |
| GET | `/api/telas` | Catálogo de telas para a matriz de permissões | `perfis` |

## Operação

Endpoints das fases seguintes. As telas já existem no catálogo com `Ativo = false` e são
ligadas quando o módulo for implementado.

| Método | Rota | Tela exigida | Marco |
|---|---|---|---|
| `/api/veiculos/*` | — | `veiculos` | M6 |
| `/api/custos/*` | — | `custos` | M7 |
| `/api/vendas/*` | — | `vendas` | M8 |

## Infraestrutura

| Método | Rota | Finalidade | Tela exigida |
|---|---|---|---|
| GET | `/health` | Sonda de saúde | pública |

## Resposta de `GET /api/auth/me`

```json
{
  "data": {
    "usuario": { "codigo": "...", "nome": "Administrador", "email": "admin@revendapro.local" },
    "perfis": ["Administrador"],
    "telas": ["dashboard", "veiculos", "custos", "vendas", "usuarios", "perfis"],
    "menu": [
      {
        "grupo": "Operação",
        "itens": [
          { "chave": "dashboard", "nome": "Dashboard", "rota": "/dashboard", "icone": "LayoutDashboard", "filhos": [] },
          { "chave": "veiculos",  "nome": "Veículos",  "rota": "/veiculos",  "icone": "Car",             "filhos": [] }
        ]
      },
      {
        "grupo": "Administração",
        "itens": [
          { "chave": "usuarios", "nome": "Usuários", "rota": "/usuarios", "icone": "Users",       "filhos": [] },
          { "chave": "perfis",   "nome": "Perfis",   "rota": "/perfis",   "icone": "ShieldCheck", "filhos": [] }
        ]
      }
    ]
  }
}
```

`menu` contém apenas telas com `ExibirNoMenu = true` às quais o usuário tem acesso, já
agrupadas e ordenadas. `telas` traz todas as chaves permitidas, inclusive as que não aparecem
no menu, para a guarda de rota no frontend.

Um perfil sem telas devolve `telas` e `menu` vazios — o frontend mostra a tela de sem acesso.
