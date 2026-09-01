# Contexto do Revenda Pro

## Tipo de projeto

Aplicacao web composta por API HTTP .NET e frontend Next.js desacoplado.

## Objetivo

Oferecer a revendas e investidores controle da compra, preparacao, custo, precificacao e
venda de veiculos. A fundacao atual se limita a autenticacao, usuarios, perfis, permissoes
e dashboard protegido.

## Contextos atuais

- Authentication: login, emissao e renovacao de sessao.
- Users: contas internas da empresa.
- Roles / Screens: perfis e permissoes de tela, com menu dinamico vindo do banco.

## Fora do escopo inicial

Veiculos, despesas, FIPE, anexos, vendas, integracoes externas, cache distribuido e alertas
operacionais.

## Convencoes

Autoridade: `docs/architecture/decisions/ADR-0003-padrao-global.md`.

- **Idioma: todo o codigo em ingles.** So o texto que o usuario le fica em portugues —
  rotulo de tela, `detail` da resposta HTTP, `Screens.Name` e nome de perfil de sistema.
- Camadas e pastas seguem `C:\Users\maicon.cardozo\source\Global\Authentication`.
- `net10.0` em todos os projetos; EF Core 10 com `MySql.EntityFrameworkCore` (nao Pomelo).
- Toda entidade herda de `BaseEntity : Foundation.Domain.Abstractions.Entity`:
  `Id` interno, `Code` UUID v7 publico, auditoria e exclusao logica.
- **EF Core so para migrations e mapeamento**; **Dapper para todo acesso a dado**, no
  padrao `SqlQuery` de `Autenticacao.Global`.
- Senha com Argon2 via `Foundation.Shared.Helpers.StringHelper`.
- Resposta de sucesso em `SuccessDetails<T>`; erro em `ProblemDetails`.
- Controllers finos, com MediatR. O command e o contrato de entrada.

> **Historico:** ate 01/09/2026 este documento mandava escrever o dominio em portugues.
> A regra foi revogada pelo ADR-0003. Ver `docs/agent/inspection-report.md` para o que a
> divergencia causou.
