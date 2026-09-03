# Revenda Pro — Marcos de implementação

Documento de planejamento. Fonte: revisão de `docs/AGENT_HANDOFF.md`, `docs/agent/*`,
`docs/architecture/*`, `docs/api/*` e do código real em `src/`, `frontend/` e `docker-compose.yml`.

Data da revisão: 2026-09-01.

---

## 1. Estado real verificado

| Área | Documentado | Real no código |
|---|---|---|
| Camadas .NET | 4 projetos + 2 de teste | Existem, mas `Application` e `Infrastructure` têm só `Class1.cs` |
| Domínio | Empresa, Usuario, Perfil, Permissao | Só `Usuario`, `Perfil`, `TipoUsuario` — anêmicos, sem `EmpresaCodigo` |
| API | Controllers finos + MediatR | `Program.cs` é uma minimal API de uma linha; `WeatherForecastController` ainda presente |
| Autenticação | JWT + refresh | Login compara com variável de ambiente e devolve payload sem token |
| EF Core / MariaDB | Pomelo configurado | Pacote referenciado, **nenhum** DbContext, mapping, migration ou seed |
| Testes | Unit + Architecture | Só `UnitTest1.cs` gerado por template |
| Frontend shell | Sidebar, topbar, tema, modais | Implementado em `PanelShell.tsx` + `globals.css` |
| Usuários / Perfis | CRUD | CRUD **em `localStorage`**, sem API |
| Dashboard | Protegido por permissão | Sem guarda de rota; qualquer um acessa |

### Divergências que precisam de decisão

1. ~~**Nomenclatura de permissões e rotas.**~~ **Resolvido em ADR-0002:** não há mais chaves de
   permissão em string livre. Cada permissão é uma **tela**, e a chave da tela é a permissão
   (`dashboard`, `veiculos`, `custos`, `vendas`, `usuarios`, `perfis`). Rotas em inglês
   (`/api/auth/login`), domínio e rótulos em português.
2. ~~**Multiempresa.**~~ **Resolvido em ADR-0002:** `Empresa` existe como entidade e
   `EmpresaCodigo` está em `Usuario`, `Perfil` e `Auditoria`, com filtro global de query.
   `Tela` é global ao sistema, sem `EmpresaCodigo`.
3. ~~**ADR-0001 diz "sem persistência".**~~ **Substituído por ADR-0002.**
4. ~~**`docs/database/mappings.md` está vazio.**~~ **Resolvido:** o modelo do núcleo de acesso
   está documentado.
5. ~~`.env` versionado~~ — verificado: está no `.gitignore` e não é rastreado pelo git. OK.
6. **`next-auth` está no `package.json` mas não é usado.** Decidir entre next-auth e
   sessão própria com o JWT da API antes do M3.
7. **Versões divergentes:** projetos em `net10.0` com `Microsoft.EntityFrameworkCore 9.0.0`
   e `Pomelo 9.0.0`. Validar compatibilidade ou alinhar para os pacotes 10.x.

---

> **Atualização (2026-09-01):** os marcos M1 a M4 foram refinados e substituídos por
> `docs/plans/acesso-e-menu.md` (marcos A0 a A6), após a decisão de que **permissão = tela =
> item de menu**, sem distinção Admin Master / Cliente. As melhorias de interface estão em
> `docs/plans/frontend-melhorias.md`. M0 e M5 a M8 seguem válidos.

## 2. Marcos

Cada marco só é considerado concluído quando `dotnet build`, `npm run build` e
`docker compose up --build -d` passam, e a documentação em `docs/` foi atualizada.

### M0 — Higienização da base *(pré-requisito)*

Limpar restos de template e travar as decisões pendentes.

- Remover `WeatherForecastController.cs`, `WeatherForecast.cs` e os dois `Class1.cs`.
- Reescrever `Program.cs` em formato legível (hoje é uma linha única).
- Escrever **ADR-0002**: nomenclatura de rotas/permissões, estratégia multiempresa,
  provider de banco (MariaDB + Pomelo) e estratégia de sessão no frontend.
- Alinhar `docs/api/endpoints.md` à decisão do ADR-0002.
- Alinhar versões de EF Core / Pomelo com `net10.0`.
- Substituir `docs/database/mappings.md` pelo modelo real que será criado no M1.

**Pronto quando:** a solução compila sem arquivos de template e as 7 divergências acima
estão decididas por escrito.

---

### M1 — Persistência de acesso *(depende de M0)*

Modelo de dados e schema versionado para o núcleo de acesso.

- Entidades: `Empresa`, `Usuario`, `Perfil`, `Permissao`, `UsuarioPerfil`,
  `PerfilPermissao`, `RefreshToken`, `Auditoria`.
- `Usuario` e `Perfil` ganham `EmpresaCodigo`, hash de senha, datas em UTC e exclusão lógica.
- `RevendaProDbContext` + um `IEntityTypeConfiguration` por entidade em `Infrastructure`.
- Migration inicial versionada.
- Seed idempotente: empresa piloto, 8 permissões, 5 perfis e usuário administrador vindo de
  `REVENDAPRO_ADMIN_EMAIL` / `REVENDAPRO_ADMIN_PASSWORD`.
- `docker-compose`: healthcheck no MariaDB e migração aplicada no start da API.

**Pronto quando:** `docker compose up` sobe o banco com as tabelas criadas e permissões,
perfis e administrador semeados; rodar duas vezes não duplica dados.

---

### M2 — Autenticação e autorização reais *(depende de M1)*

- Hash de senha (`PasswordHasher` do ASP.NET Identity ou Argon2).
- JWT assinado com `REVENDAPRO_JWT_CHAVE`, emissor, audiência e expiração via ambiente.
- Refresh token persistido, com rotação e revogação.
- Endpoints: `login`, `me`, `refresh`, `logout`.
- Autorização por permissão (policy sobre claim), não por perfil.
- `ProblemDetails` (RFC 7807) para erros e envelope `data` para sucesso, conforme
  `docs/api/responses.md`.
- Swagger com esquema Bearer.

**Pronto quando:** chamada direta a endpoint protegido sem token retorna 401; com token sem
a permissão retorna 403; token expirado é renovado pelo refresh; logout invalida o refresh.

---

### M3 — Sessão e guardas no frontend *(depende de M2)*

- Cliente HTTP central com injeção do Bearer, refresh automático em 401 e logout em falha.
- Substituir `localStorage.setItem("revenda-pro-session", ...)` por sessão segura
  (cookie httpOnly via route handler do Next, ou next-auth — decidido no ADR-0002).
- Middleware de rota: não autenticado vai para `/login`; autenticado em `/login` vai para
  `/dashboard`.
- Sidebar renderiza apenas os itens cuja permissão o usuário possui.
- Tela de 403 dentro do shell.

**Pronto quando:** nenhuma rota do painel abre sem login; o menu muda conforme o perfil; o
refresh de página mantém a sessão.

---

### M4 — CRUD real de Usuários e Perfis *(depende de M2 e M3)*

- Commands/queries MediatR e validadores FluentValidation para usuários e perfis.
- Regras: e-mail único por empresa; usuário não exclui a própria conta; perfil de sistema
  não é excluível; exclusão é lógica.
- Busca por nome, e-mail e perfil; paginação.
- Permissões agrupadas por módulo na tela de perfis.
- **Remover todo o `localStorage`** de `frontend/app/usuarios/page.tsx` e
  `frontend/app/perfis/page.tsx`.
- Auditoria de criação, edição, inativação e exclusão.

**Pronto quando:** todos os itens do §12 do handoff ("critério de pronto para a fase de
acesso") passam de fato, com dados no MariaDB.

---

### M5 — Testes e qualidade *(fecha a fase de acesso)*

- Testes de arquitetura (NetArchTest): `Domain` sem dependências internas, `Application`
  sem referência a `Infrastructure`/`Api`, controllers finos.
- Testes unitários: hash de senha, avaliação de permissão, regras de exclusão e isolamento
  por empresa.
- Testes de integração de API com banco em container.
- Pipeline: `dotnet build` + `dotnet test` + `npm run build` + `npm run lint`.

**Pronto quando:** suíte verde, cobrindo as regras de permissão e o isolamento por empresa.

> **Fim da Fase 1 — Acesso.** Só depois disto começar o módulo de veículos.

---

### M6 — Veículo, custo e arquivos (RF-05 e RF-06) — **concluído**

O M7 foi absorvido aqui. Custo não é um módulo à parte: quem cadastra o carro é quem lança o
gasto, e o custo total é leitura do veículo. Plano completo em
`docs/plans/m6-cadastro-de-veiculos.md`.

- `Vehicle`, `VehicleExpense`, `ExpenseType`, `VehiclePhoto`, `VehicleDocument` e
  `VehicleStatusHistory`, em inglês, conforme a ADR-0003.
- Máquina de status validada no domínio: transição inválida responde 422, e "Vendido" é o fim.
- Tipo de gasto é **tabela**, mantida pela revenda, com palavras-chave que sugerem o tipo a
  partir do que a pessoa digitou.
- Custo somado a cada leitura, jamais guardado — o `GASTOS.docx` real mostra R$ 350 a menos
  justamente por ter o total digitado uma vez.
- Teto de orçamento por veículo, com percentual consumido, quanto ainda cabe e aviso de
  estouro previsto antes de a despesa ser paga.
- Valor e código FIPE preenchidos à mão, prontos para a integração do M8.
- Fotos e documentos fora do banco, em bucket privado, endereço assinado de vida curta, tipo
  julgado pelos primeiros bytes e limite de tamanho configurável. Ver ADR-0004.

**Pronto quando:** um veículo percorre cadastro, gastos e esteira de status com histórico
auditável; a soma bate com a planilha real do stakeholder; vinte fotos sobem e viram WebP em
três tamanhos; e o documento exige URL assinada. **Verificado ponta a ponta contra o Cruze do
`GASTOS.docx`.**

Falta o front (V8) e o fechamento da suíte (V9).

---

### M8 — Proposta, venda e dashboard (RF-18 a RF-24) — **concluído**

Plano completo em `docs/plans/m8-venda-e-proposta.md`.

- `Proposal`: quem ofereceu, quanto, como paga, por qual canal — e **quanto sobra se for
  aceita**, calculado na hora (RF-19).
- `Sale`: preço fechado, comprador, canal, repasse da loja (que vai por cima do que ele quer
  receber, como o stakeholder descreveu), comissão e troca. Lucro bruto e líquido calculados,
  jamais guardados (RF-21).
- Troca cria um veículo novo no estoque, com origem `TradeIn` e o valor acordado como compra.
- "Vendido" só se alcança registrando a venda; a mudança de status recusa esse destino.
- Dashboard com investido, contagem por status, lucro projetado e realizado, e os cinco
  carros de maior investimento, maior margem e maior tempo parado (RF-23, RF-24).
- **FIPE segue manual.** O único acesso gratuito é um espelho comunitário sem contrato; a
  integração ganha marco próprio quando houver fonte estável. O `FipeCode` do M6 é o que vai
  torná-la barata.

**Pronto quando:** um veículo comprado, recuperado e vendido produz lucro líquido correto e
auditável de ponta a ponta — inclusive quando parte do pagamento entrou como carro.

**Verificado ponta a ponta:** o Cruze da planilha, vendido por 55 com 20 em carro, deixa os
mesmos 17.006 que a proposta prometeu; o Argo nasce no pátio a 20 mil; o painel soma 17.006 de
lucro realizado e 61 dias para vender.
---

### M9 — Pronto para produção

Plano completo em `docs/plans/m9-pronto-para-producao.md`.

- Backup do banco (dump diário para o bucket, com retenção e restauração testada) e dos
  arquivos (bucket versionado) — RNF-11.
- Foto do usuário migra para o bucket; o último arquivo fora do `IFileStorage` sai.
- `DateOnlyTypeHandler` sobe para o Foundation.
- Deploy: compose de produção com R2, proxy com HTTPS, variáveis documentadas, checklist de
  subida e de restauração testado numa máquina limpa.

**Pronto quando:** um `DELETE` errado é desfeito com os arquivos junto, e o stakeholder abre o
sistema no celular por um endereço com HTTPS.

---

### M10 — Linha do tempo e filtros (RF-25, RF-26)

- Histórico único da operação na ficha: compra, gastos, anexos, propostas, status e venda,
  em ordem cronológica.
- Filtro por período na listagem de veículos.
- Rotina administrativa para o documento excluído que fica no bucket.

---

### M11 — FIPE

- Consulta automática pelo `FipeCode` guardado desde o M6, quando houver fonte estável ou paga.

---
## 3. Ordem e dependências

```text
M0 -> M1 -> M2 -> M3 -> M4 -> M5 -> M6 -> M8 -> M9 -> M10 -> M11
                              (fim da Fase 1: Acesso)
```

M3 pode começar em paralelo a M2 assim que o contrato do token estiver definido.
O M7 deixou de existir: custo entrou no M6. O M8 depende do M6.

## 4. Riscos abertos

- **Integração FIPE:** sem fonte oficial gratuita. Manual por decisão, com marco próprio
  adiante.
- **Backup:** durabilidade de bucket não é backup. É o V1 e o V2 do M9.
- **Deploy:** hospedagem, domínio e conta no R2 são as decisões do V0 do M9.
- **Multiempresa:** resolvido no M0 — toda tabela de operação carrega `IdTenant`.
