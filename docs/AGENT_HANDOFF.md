# Revenda Pro — handoff para próximo agente

## 1. Objetivo do produto

**Revenda Pro** é um sistema SaaS para donos de revenda e pessoas que compram, recuperam e revendem veículos, sinistrados ou não.

O produto deve centralizar o ciclo de cada veículo:

- compra e origem;
- situação do veículo e sinistro;
- fotos, documentos e comprovantes;
- orçamento e custos de recuperação;
- valor FIPE e preço de venda;
- margem estimada e lucro realizado;
- venda e histórico financeiro.

O primeiro usuário piloto é um revendedor de veículos. Ele hoje organiza fotos, documentos, comprovantes e uma lista manual de gastos por carro.

## 2. Estado atual — importante

O repositório alvo é:

```text
C:\Users\maicon.cardozo\source\repos\RevendaPRO
```

Os projetos abaixo são **somente referência**. Nunca alterá-los:

```text
C:\Users\maicon.cardozo\source\repos\PainelGestao.CPComunica
C:\Users\maicon.cardozo\source\repos\Arquitetura.Global
C:\Users\maicon.cardozo\source\repos\PortalCliente.Global
```

O projeto já contém uma solução .NET, frontend Next.js e `docker-compose.yml`. Os containers locais também foram preparados:

| Serviço | URL/porta local |
|---|---|
| Frontend | `http://localhost:3100` |
| API | `http://localhost:5100` |
| MariaDB | `127.0.0.1:3308` |

Subir todos os serviços:

```powershell
docker compose up --build -d
```

## 3. O que está implementado hoje

### Frontend

Existe uma base visual inspirada diretamente no shell administrativo do CP Comunica:

- sidebar recolhível e menu mobile;
- topbar;
- tema claro/escuro persistido no `localStorage`;
- menu de usuário com ação **Sair do sistema**;
- tela de login;
- dashboard inicial;
- telas de Usuários e Perfis e permissões;
- modais e tabelas seguindo o padrão visual do CP.

Rotas atuais:

```text
/                 redireciona para /dashboard
/login
/dashboard
/usuarios
/perfis
```

### Funcionalidade atual de usuários e perfis

As telas de Usuários e Perfis permitem criar, editar e excluir dados no navegador. Esses dados são armazenados em `localStorage` para demonstrar o fluxo visual.

**Isso ainda não é persistência real nem autorização real.** O próximo agente deve substituir esse armazenamento temporário por chamadas autenticadas à API e ao MariaDB.

### API atual

A API possui:

- endpoint de saúde: `GET /health`;
- endpoint temporário de login: `POST /api/auth/login`;
- CORS para o frontend local;
- validação do login contra variáveis de ambiente locais.

O endpoint atual não possui JWT, banco de usuários, renovação de token, controllers/MediatR completos ou autorização por permissão. Ele é uma ponte temporária para a tela de login.

### Banco e Docker

MariaDB 11.8 está configurado no `docker-compose.yml`. Ainda não há modelos EF Core, migrations, tabelas, seeds ou repositórios funcionais.

## 4. Arquitetura obrigatória

Seguir a mesma arquitetura do repositório `Arquitetura.Global`:

```text
src/
  RevendaPro.Domain/          entidades, enums, contratos de domínio
  RevendaPro.Application/     casos de uso, commands, queries, handlers, validators
  RevendaPro.Infrastructure/  EF Core, MariaDB, Identity/JWT, repositórios
  RevendaPro.Api/             controllers, DI, autenticação, middleware
tests/
  RevendaPro.UnitTests/
  RevendaPro.ArchitectureTests/
frontend/                            Next.js App Router
docs/                                documentação do projeto
```

Dependências permitidas:

```text
API -> Application + Infrastructure
Application -> Domain
Infrastructure -> Domain
Domain -> nenhuma camada interna
```

> **Revogado em 01/09/2026 pelo ADR-0003.** A regra de idioma agora é: todo o código em
> inglês, só o texto que o usuário lê fica em português. As camadas seguem o projeto
> `source/Global/Authentication`. Ver
> `docs/architecture/decisions/ADR-0003-padrao-global.md` e `docs/agent/inspection-report.md`.

Padrões:

- ~~nomes de negócio em português~~ **todo o código em inglês (ADR-0003)**;
- controllers finos;
- casos de uso via MediatR;
- validação com FluentValidation;
- respostas e erros padronizados;
- EF Core + Pomelo + MariaDB;
- migrations versionadas;
- testes unitários e de arquitetura;
- documentação atualizada antes de alterações estruturais.

Documentação de arquitetura já existente:

```text
docs/architecture/overview.md
docs/architecture/layers.md
docs/architecture/dependencies.md
docs/architecture/ADR-0001.md
docs/api/
docs/database/
docs/testing/
```

## 5. Referências de implementação

### Arquitetura backend

Ler primeiro:

```text
C:\Users\maicon.cardozo\source\repos\Arquitetura.Global\AGENTS.md
C:\Users\maicon.cardozo\source\repos\Arquitetura.Global\docs\architecture\
```

Usar como referência de estrutura, não copiar sem adaptar:

```text
C:\Users\maicon.cardozo\source\repos\PainelGestao.CPComunica\src\
```

Especialmente os módulos de autenticação, usuários, perfis, permissões, Program.cs, repositórios e mappings.

### Layout frontend

O usuário pediu que o painel seja **igual ao CP Comunica**. Usar como referência direta:

```text
C:\Users\maicon.cardozo\source\repos\PainelGestao.CPComunica\frontend\components\layout\AppLayout.tsx
C:\Users\maicon.cardozo\source\repos\PainelGestao.CPComunica\frontend\components\layout\Sidebar.tsx
C:\Users\maicon.cardozo\source\repos\PainelGestao.CPComunica\frontend\components\layout\Topbar.tsx
C:\Users\maicon.cardozo\source\repos\PainelGestao.CPComunica\frontend\components\layout\UserMenu.tsx
C:\Users\maicon.cardozo\source\repos\PainelGestao.CPComunica\frontend\components\theme\ThemeToggle.tsx
C:\Users\maicon.cardozo\source\repos\PainelGestao.CPComunica\frontend\components\users\UsersView.tsx
C:\Users\maicon.cardozo\source\repos\PainelGestao.CPComunica\frontend\components\users\RolesPanel.tsx
C:\Users\maicon.cardozo\source\repos\PainelGestao.CPComunica\frontend\app\globals.css
```

O shell criado no RevendaPRO está em:

```text
frontend/components/layout/PanelShell.tsx
frontend/app/globals.css
```

Não redesenhar o painel sem necessidade. Preservar a linguagem visual do CP: tokens, fundo de grid, cards, tabelas, modais, sidebar, topbar, responsividade e modo escuro.

## 6. Requisitos funcionais do MVP

### RF-01 — Autenticação

- Login por e-mail e senha.
- Logout no menu do usuário.
- Sessão persistente e expiração segura.
- Redirecionar não autenticado para `/login`.
- Redirecionar autenticado de `/login` para `/dashboard`.

### RF-02 — Usuários

- Listar usuários da empresa/revenda.
- Criar usuário com nome, e-mail, senha, status e um ou mais perfis.
- Editar usuário.
- Ativar/inativar usuário.
- Excluir/arquivar usuário sem apagar o histórico de negócio.
- Impedir que o usuário atual exclua a própria conta.
- Pesquisar por nome, e-mail e perfil.

### RF-03 — Perfis e permissões

- Listar perfis.
- Criar, editar e excluir perfis customizados.
- Perfis de sistema não podem ser excluídos.
- Permissões controlam menu, rotas e ações da API.
- Mostrar permissões agrupadas por módulo.

Permissões iniciais:

> **Desatualizado — substituído pelo ADR-0002 (2026-09-01).** As permissões em string livre
> abaixo não existem mais. Cada permissão passou a ser uma **tela**, e a chave da tela é a
> permissão: `dashboard`, `veiculos`, `custos`, `vendas`, `usuarios`, `perfis`.
> Ver `docs/architecture/decisions/ADR-0002-acesso-por-tela.md` e
> `docs/plans/acesso-e-menu.md`.

```text
dashboard.view      -> dashboard
users.manage        -> usuarios
roles.manage        -> perfis
vehicles.view       -> veiculos
vehicles.manage     -> veiculos
expenses.manage     -> custos
sales.manage        -> vendas
documents.manage    -> (coberto pela tela do módulo correspondente)
```

Perfis iniciais:

```text
Administrador      acesso integral
Gestor             operação e relatórios
Financeiro         custos, vendas e relatórios financeiros
Vendedor           estoque e vendas
Oficina            orçamento, reparo, fotos e documentos técnicos
```

### RF-04 — Dashboard inicial

Para esta primeira fase pode permanecer simples, mas deve respeitar a tela `dashboard` e mostrar dados reais de usuários/perfis.

### RF-05 — Veículos (próxima fase)

- Cadastro wizard, com avanço/retorno e salvamento progressivo.
- Dados: placa, chassi, marca, modelo, versão, ano/modelo, cor, quilometragem, origem, fornecedor, data e valor de compra.
- Classificação: sem sinistro, recuperado de financiamento, pequena monta, média monta, grande monta/outro.
- Status: em análise, comprado, em transporte, em reparo, pronto para venda, anunciado, vendido, cancelado.
- Fotos por etapa e documentos anexados.

### RF-06 — Custos e orçamento (próxima fase)

- Lançar custos por veículo: compra, frete, peças, mecânica, funilaria, pintura, documentação, pneus, lavagem, comissão, taxas e outros.
- Anexar comprovante, nota fiscal, orçamento ou imagem.
- Informar fornecedor, data, categoria, valor previsto e valor realizado.
- Comparar orçamento com gasto realizado.
- Calcular custo total do veículo automaticamente.

### RF-07 — FIPE e precificação (próxima fase)

- Consultar valor FIPE por veículo/versionamento da tabela.
- Registrar preço alvo, preço anunciado e preço mínimo.
- Exibir margem estimada: preço projetado - custo total - despesas de venda.
- Guardar data e fonte da consulta FIPE.

### RF-08 — Venda (próxima fase)

- Registrar comprador, data, valor, comissão, forma de pagamento e despesas de venda.
- Calcular lucro bruto e líquido.
- Encerrar veículo como vendido mantendo histórico auditável.

## 7. Requisitos não funcionais

- Interface responsiva, começando por desktop e funcionando em celular.
- Todo endpoint administrativo exige autenticação e autorização por permissão.
- Senhas com hash forte; nunca guardar senha em texto puro.
- JWT com chave e expiração via variáveis de ambiente; refresh token se necessário.
- Segredos somente em `.env`, nunca commitados.
- Documentos e fotos fora do banco; banco armazena metadados e URL/caminho.
- Validar tamanho, tipo e extensão de arquivo no backend.
- Registrar auditoria para criação, edição, inativação, custo e venda.
- Valores monetários com `decimal`, nunca `float`/`double`.
- Datas em UTC no banco e convertidas para exibição.
- LGPD: acesso por empresa, mínimo privilégio, exclusão lógica e rastreabilidade.
- API documentada com OpenAPI/Swagger.
- Testes para regras de lucro, permissões e transições de status.

## 8. Modelo de dados sugerido

### Acesso

```text
Empresa
Usuario
Perfil
Permissao
UsuarioPerfil
PerfilPermissao
SessaoOuRefreshToken
Auditoria
```

### Operação de veículos

```text
Veiculo
VeiculoFoto
VeiculoDocumento
VeiculoHistoricoStatus
Fornecedor
Orcamento
OrcamentoItem
DespesaVeiculo
ConsultaFipe
Venda
Comprador
```

Regras de isolamento:

- toda entidade de negócio deve possuir `EmpresaCodigo`/`EmpresaId`;
- qualquer query deve filtrar a empresa do usuário autenticado;
- administrador da empresa não acessa dados de outra empresa.

## 9. Ordem recomendada de execução

1. Ler `docs/` e a arquitetura de referência sem alterar repositórios externos.
2. Implementar modelos EF Core de Empresa, Usuario, Perfil, Permissao e relacionamentos.
3. Criar migration inicial e seed de permissões/perfis/administrador a partir de variáveis locais.
4. Implementar JWT seguro e endpoints de login/me/refresh/logout.
5. Implementar CRUD real de usuários e perfis com MediatR + FluentValidation.
6. Substituir o `localStorage` das telas por cliente HTTP autenticado.
7. Adicionar guardas de rota e renderização condicional do menu por permissão.
8. Implementar testes de API, domínio e arquitetura.
9. Só então iniciar o módulo de veículos pelo wizard.

## 10. Arquivos que merecem atenção

```text
docker-compose.yml
src/RevendaPro.Api/Program.cs
frontend/app/login/page.tsx
frontend/components/layout/PanelShell.tsx
frontend/app/usuarios/page.tsx
frontend/app/perfis/page.tsx
frontend/app/globals.css
.env.example
.env.Development.example
```

## 11. Alertas para o próximo agente

- Não modificar CP Comunica, Arquitetura.Global ou PortalCliente.Global.
- Não usar login que aceite qualquer e-mail/senha no frontend.
- Não apresentar `localStorage` como banco de dados ou autorização real.
- Não expor ou commitar o arquivo `.env`.
- Não reinventar o frontend: copiar/adaptar o padrão visual do CP Comunica.
- Usar `apply_patch` para editar arquivos.
- Ao finalizar mudanças, rodar pelo menos:

```powershell
dotnet build RevendaPro.slnx --no-restore
npm run build
docker compose up --build -d
```

## 12. Critério de pronto para a fase de acesso

- Usuário consegue fazer login válido contra MariaDB.
- Sessão impede acesso sem login.
- Menu só mostra telas permitidas.
- API bloqueia ação sem permissão, mesmo por chamada direta.
- Usuários e perfis persistem no MariaDB.
- Criar/editar/inativar/excluir usuário funciona e atualiza a tela.
- Criar/editar/excluir perfil e permissões funciona e atualiza a tela.
- Tema claro/escuro cobre toda a área fora da sidebar.
- Logout leva à tela de login.
- Frontend, API e banco sobem juntos no Docker.
