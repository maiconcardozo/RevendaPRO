# Instrucoes para agentes

## Fase 0 — obrigatoria antes de qualquer coisa

`Arquitetura.Global/AGENTS.md` exige uma Fase 0 antes de qualquer inspecao, relatorio,
plano ou correcao. Nao pule.

1. Leia, **nesta ordem**:
   - `C:\Users\maicon.cardozo\source\repos\Arquitetura.Global\AGENTS.md` e
     `docs/standards/`;
   - `C:\Users\maicon.cardozo\source\Global\Authentication` — **referencia de camadas,
     pastas, `EntityMap`, `UnitOfWork` e stack de pacotes**;
   - `C:\Users\maicon.cardozo\source\repos\PainelGestao.CPComunica\AGENTS.md` — regra de
     idioma;
   - `C:\Users\maicon.cardozo\source\repos\Autenticacao.Global` — padrao `SqlQuery` e
     repositories com Dapper;
   - `docs/architecture/decisions/ADR-0003-padrao-global.md` deste repositorio.
2. Liste o que foi lido e o que nao conseguiu ler, com o impacto na analise.
3. Resuma o padrao que sera aplicado antes de escrever codigo.

> Um agente ja ignorou esta ordem, seguiu a convencao local em vez das referencias e
> escreveu 46 arquivos fora do padrao. O prejuizo esta em `docs/agent/inspection-report.md`.

## Regras

1. **Todo o codigo em ingles.** So o texto que o usuario le fica em portugues.
   Autoridade: ADR-0003.
2. Respeite `Api -> Application -> Domain` e `Infrastructure -> Domain`.
   `Application` nunca referencia `Infrastructure`.
3. Toda entidade persistida herda de `Entity` do Foundation, com `Id` interno e `Code`
   UUID v7 publico. Rotas e DTOs expoem `Code`. Chave estrangeira e `IdTenant`, `IdUser`,
   `IdRole` - `Id` na frente, jamais `UserId`.
4. Toda exclusao e logica.
5. **EF Core so gera migration e mapeia tabela.** Acesso a dado e Dapper, com o SQL
   versionado em `Infrastructure/Queries/{Context}/`.
6. Sem `HasColumnName`: a propriedade se chama como a coluna.
7. Documente decisao estrutural em ADR antes de implementar.
8. Ao terminar, rode `dotnet build`, `npm run build` e a bateria de verificacao de ponta a
   ponta descrita em `docs/plans/refatoracao-padrao-global.md`.
