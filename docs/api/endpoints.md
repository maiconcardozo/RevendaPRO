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

## Veículos

| Método | Rota | Finalidade | Tela exigida |
|---|---|---|---|
| GET | `/api/vehicles` | Lista o estoque. Filtros `search`, `status`, `origin` | `vehicles` |
| GET | `/api/vehicles/{code}` | Um veículo, com o custo somado e os status para onde ele pode ir | `vehicles` |
| POST | `/api/vehicles` | Cadastra | `vehicles` |
| PUT | `/api/vehicles/{code}` | Edita | `vehicles` |
| PATCH | `/api/vehicles/{code}/status` | Move na esteira, com motivo | `vehicles` |
| GET | `/api/vehicles/{code}/history` | Histórico de status | `vehicles` |
| DELETE | `/api/vehicles/{code}` | Exclusão lógica | `vehicles` |

A placa e o chassi são únicos por empresa; repetir qualquer um dos dois responde **422**. A
esteira recusa salto: de "Em análise" só se vai para "Comprado", e "Vendido" é o fim.

## Gastos

| Método | Rota | Finalidade | Tela exigida |
|---|---|---|---|
| GET | `/api/vehicles/{code}/expenses` | Lançamentos do veículo | `vehicles` |
| POST | `/api/vehicles/{code}/expenses` | Lança um gasto, pago ou previsto | `vehicles` |
| PUT | `/api/vehicles/{code}/expenses/{expenseCode}` | Edita o lançamento | `vehicles` |
| PATCH | `/api/vehicles/{code}/expenses/{expenseCode}/payment` | Marca o previsto como pago | `vehicles` |
| DELETE | `/api/vehicles/{code}/expenses/{expenseCode}` | Exclusão lógica | `vehicles` |
| GET | `/api/vehicles/expense-suggestions?term=` | Sugere descrição e tipo pelo que a revenda já digitou | `vehicles` |
| GET | `/api/expense-types` | Tipos de gasto da empresa | `vehicles` |
| POST | `/api/expense-types` | Cria tipo | `expense-types` |
| PUT | `/api/expense-types/{code}` | Edita tipo | `expense-types` |
| DELETE | `/api/expense-types/{code}` | Exclui tipo que nenhum gasto usa | `expense-types` |

`GET /api/expense-types` é guardado pela tela `vehicles`, e não pela própria: quem lança um
gasto precisa ver a lista para escolher. Mexer na lista é que exige a tela de administração.

## Fotos e documentos

| Método | Rota | Finalidade | Tela exigida |
|---|---|---|---|
| GET | `/api/vehicles/{code}/photos` | Galeria, na ordem que a revenda montou | `vehicles` |
| POST | `/api/vehicles/{code}/photos` | Envia uma foto (multipart: `file`, `kind`) | `vehicles` |
| PATCH | `/api/vehicles/{code}/photos/order` | Reordena a galeria inteira | `vehicles` |
| PATCH | `/api/vehicles/{code}/photos/{photoCode}/kind` | Muda para que serve a foto | `vehicles` |
| PUT | `/api/vehicles/{code}/cover` | Escolhe a capa, ou a deixa vazia | `vehicles` |
| DELETE | `/api/vehicles/{code}/photos/{photoCode}` | Remove a foto **e os bytes** | `vehicles` |
| GET | `/api/vehicles/{code}/documents` | Documentos do veículo | `vehicles` |
| POST | `/api/vehicles/{code}/documents` | Envia um documento (multipart: `file`, `kind`) | `vehicles` |
| PATCH | `/api/vehicles/{code}/documents/{documentCode}/kind` | Muda o que o documento é | `vehicles` |
| DELETE | `/api/vehicles/{code}/documents/{documentCode}` | Tira da listagem; **o arquivo fica** | `vehicles` |

O arquivo passa pela API, e o navegador jamais fala direto com o bucket: é aqui que a empresa
dona é conferida, que o conteúdo é julgado pelos primeiros bytes e que o limite de tamanho
vale. Ver `ADR-0004`.

**Foto.** Vira WebP em três tamanhos — `thumbnail`, `card` e `full` —, tem a orientação do EXIF
aplicada e o restante do EXIF descartado, incluindo a coordenada de GPS. A resposta traz os
três endereços. A primeira foto enviada vira a capa sozinha.

**Documento.** Aceita PDF, JPG e PNG, julgados pelo conteúdo: um executável renomeado para
`.pdf` responde **422**. O PDF jamais é convertido, para preservar texto selecionável e
assinatura.

**Endereço.** Todo endereço devolvido é assinado e expira em quinze minutos, o que é o que a
RNF-06 pede. Sem a assinatura, o bucket responde **403**.

### As duas exclusões são diferentes de propósito

| | Linha | Arquivo no bucket |
|---|---|---|
| `DELETE .../photos/{photoCode}` | exclusão lógica | **apagado** |
| `DELETE .../documents/{documentCode}` | exclusão lógica | **fica, para sempre** |

Uma galeria que guarda todo quadro descartado cresce sem limite, e foto tirada do anúncio não
tem segunda vida. Documento é o contrário: nota fiscal, CRV, papel de leilão e recibo são prova
fiscal e legal, e podem ser cobrados anos depois, de um carro vendido há tempo. Quem arruma uma
tela não é quem decide destruir prova, e os dois jamais podem ser o mesmo clique.

### Tamanho

O limite é configuração — `Storage:MaxUploadSizeInBytes`, 12 MB por padrão —, porque a RNF-09
pede assim. Acima dele a resposta é **413** com a frase e o tamanho aceito, decidida pelo
cabeçalho `Content-Length` antes de o corpo ser lido.

## Fases seguintes

| Rota | Tela exigida | Marco |
|---|---|---|
| `/api/sales/*` | `sales` | M8 |

A tela `costs` continua no catálogo de um tempo em que custo seria um módulo à parte. O M6
juntou custo ao veículo, e nenhuma rota exige essa tela hoje. O que fazer com ela — abrir uma
visão de custo por veículo ou tirá-la do catálogo — fica para o front do M6.

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
