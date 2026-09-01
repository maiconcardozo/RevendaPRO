# Revenda Pro

Base inicial do sistema de gestao de operacoes de revenda de veiculos.

## Estado atual

Esta primeira entrega cria a fundacao do produto: autenticacao, usuarios, perfis, permissoes e um dashboard protegido ainda sem indicadores de negocio.

## Padrao arquitetural

O projeto segue `Arquitetura.Global`. Antes de alterar o codigo, leia a documentacao em `docs/` e depois os padroes centrais em `C:/Users/maicon.cardozo/source/repos/Arquitetura.Global`.

## Documentacao

- `docs/ROADMAP.md` (marcos de implementacao e o que falta)
- `docs/architecture/PADRAO-GLOBAL.md` — **padrao generico para projetos novos, leia primeiro**
- `docs/PADRAO-DE-TEXTO.md` — **texto de tela sempre na afirmativa**
- `docs/architecture/decisions/ADR-0003-padrao-global.md` (idioma, camadas, Dapper)
- `docs/agent/inspection-report.md` (divergencias vs. padrao Global)
- `docs/plans/refatoracao-padrao-global.md` (marcos R0 a R11)
- `docs/plans/acesso-e-menu.md` (permissoes de tela e menu dinamico)
- `docs/plans/frontend-melhorias.md` (revisao de design)
- `docs/AGENT_HANDOFF.md`
- `docs/agent/context.md`
- `docs/agent/instructions.md`
- `docs/architecture/overview.md`
- `docs/architecture/layers.md`
- `docs/api/endpoints.md`
- `docs/api/responses.md`

## Pendencias iniciais

- Definir provider de banco e estrategia de deploy.
- Definir integracao oficial para consulta FIPE.
- Definir armazenamento de fotos e documentos.
