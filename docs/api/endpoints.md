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
| GET | `/api/vehicles` | Lista, com busca, situação, origem, pátio (`yard`) e período de compra (`from`, `to`) | `vehicles` |
| GET | `/api/vehicles/{code}` | Um veículo, com o custo somado e os status para onde ele pode ir | `vehicles` |
| POST | `/api/vehicles` | Cadastra | `vehicles` |
| PUT | `/api/vehicles/{code}` | Edita | `vehicles` |
| PATCH | `/api/vehicles/{code}/status` | Move na esteira, com motivo | `vehicles` |
| PATCH | `/api/vehicles/{code}/yard` | Muda o carro de pátio, com motivo, e registra a passagem | `vehicles` |
| GET | `/api/vehicles/{code}/timeline` | A operação inteira em ordem: compra, gastos, anexos, propostas, status e venda | `vehicles` |
| POST | `/api/vehicles/{code}/fipe` | Consulta a tabela de referência e grava valor, mês, modelo e origem — e **nenhum preço** | `vehicles` |
| POST | `/api/vehicles/{code}/fipe/match` | Procura o modelo deste carro na tabela e resolve sozinho quando sobra um só | `vehicles` |
| POST | `/api/vehicles/{code}/fipe/model` | Aponta o veículo para um modelo escolhido (marca, modelo, ano) e aprende o código da tabela | `vehicles` |
| GET | `/api/fipe/brands` | Marcas da tabela de referência | `vehicles` |
| GET | `/api/fipe/brands/{brand}/models` | Modelos de uma marca | `vehicles` |
| GET | `/api/fipe/brands/{brand}/models/{model}/years` | Anos e combustíveis de um modelo | `vehicles` |
| DELETE | `/api/vehicles/{code}` | Exclusão lógica | `vehicles` |

`fipe/match` é o caminho do carro **sem código**. Ele lista as marcas e os modelos da marca,
descarta o que não pode ser este carro — nome como palavra inteira, termos da versão, câmbio e
combustível — e então **exige o ano**: ele desce as camadas de nome, da que mais repete o carro
para a que menos repete, e para na primeira que a tabela precifica no ano dele. O gasto tem teto
de trinta perguntas, e as listas de nome ficam guardadas por doze horas.

A resposta tem dois campos que **jamais** vêm preenchidos juntos: `applied`, quando sobrou um
candidato com um ano só e o carro já foi apontado para ele; e `candidates`, quando a escolha é
de quem lê. Os dois vazios querem dizer que a tabela segue sem este carro.

**Empate jamais vira palpite.** Duas versões do mesmo carro são dois preços, às vezes dezenas de
milhares distantes. Quando resolve sozinho, a escrita sai pelo mesmo `fipe/model` que a pessoa
usaria — mesmo código gravado, mesma cotação guardada, mesma auditoria.

O período (`from`, `to`) é lido sobre a **data de compra**: a pergunta desta listagem é o
que entrou no pátio no intervalo. Quem quer o que saiu tem a listagem de vendas, que filtra
pela data da venda. Um veículo sem data de compra fica de fora sempre que um período é pedido.

A placa e o chassi são únicos por empresa; repetir qualquer um dos dois responde **422**. A
esteira recusa salto: de "Em análise" só se vai para "Comprado", e "Vendido" é o fim.

O filtro por pátio (`yard`) recebe o **código** do pátio, e é aplicado no banco. Um código
que a empresa desconhece responde **lista vazia**, e jamais o estoque inteiro: um filtro
desconhecido virando "sem filtro" é como um vazamento começa.

Mudar de pátio é endpoint próprio, e não um campo da edição, porque é outro ato — e porque a
passagem fica registrada. Mover o carro para o pátio onde ele já está responde **422**, e um
código de pátio de outra revenda responde **404**.

A linha do tempo lê as tabelas da operação, e jamais a auditoria. Fotos e documentos
enviados pela mesma pessoa no mesmo dia vêm contados num evento só, com `quantity` maior
que 1 e sem `code`: o envio de um lote é um ato, e vinte linhas iguais afogariam a
história. Cada evento traz o nome de quem o fez, inclusive de quem já saiu da revenda.

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

## Propostas e venda

| Método | Rota | Finalidade | Tela exigida |
|---|---|---|---|
| GET | `/api/vehicles/{code}/proposals` | Propostas do carro, cada uma com **quanto sobra se for aceita** | `sales` |
| GET | `/api/vehicles/{code}/deal-preview?amount=&channel=&partnerCutPercent=&partnerCutAmount=&commission=` | Simula um negócio antes de gravar | `sales` |
| POST | `/api/vehicles/{code}/proposals` | Registra uma proposta | `sales` |
| PATCH | `/api/vehicles/{code}/proposals/{proposalCode}/decline` | Recusa; a proposta fica no registro | `sales` |
| DELETE | `/api/vehicles/{code}/proposals/{proposalCode}` | Exclusão lógica de proposta lançada por engano | `sales` |
| GET | `/api/vehicles/{code}/sale` | A venda do carro, ou `data: null` enquanto está no pátio | `sales` |
| POST | `/api/vehicles/{code}/sale` | Registra a venda. **A única porta para "Vendido"** | `sales` |
| DELETE | `/api/vehicles/{code}/sale` | Cancela a venda; o carro volta para Pronto | `sales` |

`PATCH /api/vehicles/{code}/status` com `Vendido` responde **422** e manda registrar a venda:
um status trocado à mão deixaria o carro sem comprador, sem preço e sem lucro.

**Quanto sobra** é calculado pelo servidor a cada leitura, com a mesma conta antes e depois
da venda (`DealResult`): recebido = valor − repasse da loja; lucro líquido = recebido − comissão
− custo total. Nada disso tem coluna.

**Repasse da loja** vai por cima do que o vendedor quer receber, como o stakeholder descreveu.
Informa-se percentual **ou** valor; os dois juntos respondem 422.

**Troca.** Com `paymentMethod` 5 (troca) ou 6 (troca com volta), `tradeInValue` é a parte do
preço que entrou como carro, e `tradeIn` descreve o carro. A venda o cadastra no pátio com
origem Troca, compra igual ao valor acordado e uma linha de histórico dizendo de qual carro
veio. Cancelar a venda **mantém** esse carro: ele existe de verdade.

Vender de novo um carro já vendido responde 422; aceitar uma proposta recusa as outras abertas.
## Painel e listagem de vendas

| Método | Rota | Finalidade | Tela exigida |
|---|---|---|---|
| GET | `/api/dashboard?from=&to=` | Investido, contagem por status, **quanto está parado em cada pátio**, lucro projetado e realizado, os cinco de maior investimento, maior margem e mais tempo parado, últimas vendas | `dashboard` |
| GET | `/api/sales?from=&to=` | Vendas do período, cada uma com custo, líquido, margem e dias até vender | `sales` |

O agrupamento por pátio vem **junto** dos números do topo, e jamais no lugar deles: a
pergunta do stakeholder foi "de cada um e um todo junto". Carro vendido fica de fora, pelo
mesmo motivo do capital parado — aquele dinheiro voltou. Pátio vazio continua na lista, porque
"zero carro na Loja do Joãozinho" é uma resposta, e os carros sem lugar ganham uma linha
própria, que é a diferença entre o total e a soma dos pátios.

O período delimita **só o que é realizado** (vendas, lucro realizado, dias médios para
vender). O pátio é sempre o de agora. Tudo é somado no momento da chamada, em cinco consultas
para o pátio inteiro — nunca uma por carro.
## Documentos excluídos

| Método | Rota | Finalidade | Tela exigida |
|---|---|---|---|
| GET | `/api/deleted-documents` | Documentos excluídos da revenda, com o veículo de cada um e o endereço assinado do arquivo | `deleted-documents` |

## Pátios

| Método | Rota | Finalidade | Tela exigida |
|---|---|---|---|
| GET | `/api/yards` | Os pátios da revenda, com quantos carros estão em cada um | `yards` |
| POST | `/api/yards` | Cadastra | `yards` |
| PUT | `/api/yards/{code}` | Edita | `yards` |
| DELETE | `/api/yards/{code}` | Exclusão lógica | `yards` |

Um cadastro só, com o tipo dentro: pátio da revenda e loja de terceiro são a mesma coisa para a
operação — um lugar onde o carro fica. O tipo muda o repasse, e pátio da casa **jamais** carrega
repasse: pedir isso responde **422**.

O repasse é combinado em percentual **ou** em valor, nunca nos dois — com os dois preenchidos a
venda não saberia qual usar.

Excluir um pátio com carro dentro responde **422**, e a mensagem diz **quantos** carros são:
quem lê precisa saber o tamanho do trabalho de mover os carros antes de decidir.

Quem tem a tela `vehicles` mas não a `yards` continua **vendo** onde cada carro está — isso
vem na ficha do veículo. O que ele não faz é cadastrar pátio nem mover carro: ler é informação,
mover é decisão.

## Mercado

| Método | Rota | Finalidade | Tela exigida |
|---|---|---|---|
| GET | `/api/market` | A revenda contra a tabela de referência: compra, venda, pedido e propostas, **cada um contra a cotação do mês dele**, mais a perda de referência do pátio | `market` |

Uma leitura só para a tela inteira: ela responde cinco perguntas sobre o mesmo conjunto de
carros, e perguntar cinco vezes leria o mesmo pátio cinco vezes.
| POST | `/api/deleted-documents/{code}/restore` | Devolve o documento à ficha do veículo | `deleted-documents` |

**Exclusão definitiva jamais é oferecida**, e a ausência é o desenho: guardar documento para
sempre foi requisito, o objeto nunca saiu do bucket, e um apagar de vez desfaria isso e a
recuperação administrativa da RNF-08.

O documento pende do veículo, e é o veículo que diz de quem ele é: a devolução lê o veículo
pelo tenant de quem pede, então o documento de outra revenda responde **404** (RNF-04).
Devolver um documento que já está na ficha responde **422**.

## Telas que saíram

A tela `costs` **deixou de existir**. Ela vinha de um tempo em que custo seria um módulo à
parte; o M6 juntou custo ao veículo, e nenhuma rota jamais exigiu essa chave. O sincronizador
desativa a tela sozinho na próxima subida, e os perfis que a tinham passaram a receber
`expense-types` no lugar. Ver ADR-0002.

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
    "screens": ["dashboard", "vehicles", "sales", "users", "roles", "expense-types", "deleted-documents", "my-account"],
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
