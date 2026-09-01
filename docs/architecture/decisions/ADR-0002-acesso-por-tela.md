# ADR-0002: Acesso por tela, perfil e menu dinâmico

Data: 2026-09-01
Estado: aceito
Substitui parcialmente: ADR-0001 (na parte que adiava a persistência)

## Contexto

O Revenda Pro precisa de controle de acesso mais simples que o do PainelGestao.CPComunica.
O requisito do produto é direto: cada permissão corresponde a **uma tela** que o usuário pode
ou não ver; o menu é montado a partir das permissões do usuário no momento do login; menu e
permissões ficam no banco.

Não existe distinção de ambiente (Admin Master / Cliente). A interface é sempre a mesma, com
mais ou menos itens de menu.

## Decisão

### 1. Permissão, item de menu e tela são a mesma entidade

Uma única tabela `Tela` é o catálogo de permissões **e** o catálogo do menu.

Ficam eliminados: a tabela `Permissao`, a tabela `PerfilPermissao` e as chaves de permissão em
string livre do handoff original (`dashboard.view`, `users.manage`, `roles.manage`). A chave da
tela **é** a permissão.

Uma tela que não deve aparecer no menu — detalhe de veículo, por exemplo — existe na tabela com
`ExibirNoMenu = false` e continua sendo uma permissão verificável pela API.

Não há permissão de ação (editar, excluir) nesta fase. A ligação `PerfilTela` nasce preparada
para receber colunas `PodeEditar` / `PodeExcluir` no futuro, sem remodelagem.

### 2. O agrupamento de usuários se chama Perfil

Mantido `Perfil` no domínio, no banco e na rota `/perfis`, já existentes. Na interface o rótulo
é **"Perfil de acesso"**.

### 3. Usuário e perfil: N:N no banco, um na interface

A tabela `UsuarioPerfil` é N:N e as telas do usuário são a **união** dos perfis dele. A
interface desta fase permite atribuir **um único perfil por usuário**.

O custo de modelar N:N agora é zero; o custo de migrar de 1:N para N:N depois, com dados em
produção, não é.

### 4. O catálogo de telas é declarado em código e sincronizado no startup

O catálogo de telas vive em código, como fonte da verdade sobre **quais telas existem**. Uma
rotina de sincronização roda a cada inicialização da API e reconcilia o banco:

- tela nova no catálogo → **INSERT**, sem migration nova;
- tela existente com nome, ícone, ordem ou grupo alterados → **UPDATE**;
- tela removida do catálogo → `Ativo = false`, **nunca DELETE**;
- toda tela nova é vinculada automaticamente ao perfil **Administrador**.

O vínculo automático com Administrador resolve um impasse: sem ele, uma tela recém-criada não
pertenceria a ninguém, e não haveria como alguém alcançar a tela de perfis para liberá-la.
Nenhum outro perfil recebe a tela automaticamente — a liberação é sempre explícita.

A operação é idempotente. Subir a API duas vezes não duplica nem reverte ajustes de vínculo.

O que é editável pela interface, e portanto não é sobrescrito pela sincronização, fica restrito
a `PerfilTela`. Nome, ícone, ordem e grupo do menu vêm do catálogo.

### 5. As telas do usuário não viajam no JWT

O access token carrega apenas `sub` (usuário), `emp` (empresa) e `exp`.

A resolução das telas é feita a cada request por um serviço com `IMemoryCache` **por perfil**,
invalidado quando o perfil ou seus vínculos mudam.

Motivo: como claim, uma mudança de permissão só valeria na expiração do token, e o token
cresceria junto com o catálogo. Com cache por perfil, a mudança vale no request seguinte.

### 6. O menu é filtrado no servidor

`GET /api/auth/me` devolve o menu já filtrado, agrupado e ordenado. O frontend não recebe o
catálogo completo para esconder itens no cliente.

Esconder item de menu é apresentação. A segurança é a guarda de cada endpoint, que permanece
obrigatória e independente do menu: chamar uma rota protegida diretamente, sem a tela no
perfil, retorna **403**.

### 7. Perfil sem telas é permitido

Salvar um perfil sem nenhuma tela marcada é válido. Ao logar, o usuário desse perfil vê uma
tela dedicada informando que não há telas liberadas e orientando a procurar o administrador —
em vez de um painel vazio.

Pelo mesmo motivo, o redirecionamento pós-login vai para a **primeira tela permitida**, não
necessariamente `/dashboard`: um perfil pode não ter dashboard.

## Consequências

Positivas:

- uma fonte de verdade para menu e permissão, sem risco de divergirem;
- adicionar uma tela ao sistema não exige migration nem edição manual de banco;
- mudança de permissão tem efeito imediato, sem relogar;
- o modelo é pequeno o bastante para ser coberto por uma matriz de testes perfil x endpoint.

Negativas, aceitas conscientemente:

- não há granularidade por ação; se o produto exigir "vê mas não edita", será preciso estender
  `PerfilTela` (previsto, não gratuito);
- a resolução de permissão por request depende do cache estar correto; a invalidação ao salvar
  perfil é um ponto crítico e precisa de teste dedicado;
- nome, ícone e ordem do menu só mudam por deploy.

## Decisões relacionadas ainda em aberto

- fonte oficial de consulta FIPE (bloqueia M8);
- destino dos arquivos de fotos e documentos (bloqueia M6);
- next-auth versus sessão própria em cookie httpOnly — ver `docs/plans/frontend-melhorias.md`.
