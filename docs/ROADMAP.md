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

### M6 — Veículos (RF-05)

- Entidades: `Veiculo`, `VeiculoFoto`, `VeiculoDocumento`, `VeiculoHistoricoStatus`,
  `Fornecedor`.
- Enums de classificação (sem sinistro, recuperado de financiamento, pequena/média/grande
  monta) e de status (em análise, comprado, em transporte, em reparo, pronto para venda,
  anunciado, vendido, cancelado), com máquina de estados validada no domínio.
- Wizard de cadastro com salvamento progressivo (rascunho persistido no servidor).
- Upload de fotos e documentos: arquivo fora do banco, metadados e caminho no banco,
  validação de tipo, extensão e tamanho no backend.
- Tela `veiculos` acrescentada ao catálogo (`CatalogoDeTelas`) e liberada por perfil; a guarda
  vale em menu, rota e API.

**Pronto quando:** um veículo percorre o wizard completo, muda de status com histórico
auditável e transições inválidas são rejeitadas pela API.

---

### M7 — Custos e orçamento (RF-06)

- `Orcamento`, `OrcamentoItem` e `DespesaVeiculo` com categorias (compra, frete, peças,
  mecânica, funilaria, pintura, documentação, pneus, lavagem, comissão, taxas, outros).
- Valor previsto x realizado, com comparativo por item e total.
- Anexo de comprovante ou nota fiscal por lançamento.
- Custo total do veículo calculado e recalculado a cada lançamento.
- Todos os valores monetários em `decimal`.

**Pronto quando:** o custo total de um veículo bate com a soma dos lançamentos e o
comparativo orçado x realizado é exibido por categoria.

---

### M8 — FIPE, precificação e venda (RF-07 e RF-08)

- Integração FIPE (fonte oficial definida em ADR próprio), com `ConsultaFipe` guardando
  valor, data e fonte.
- Preço alvo, anunciado e mínimo; margem estimada = preço projetado − custo total −
  despesas de venda.
- `Venda` e `Comprador`: data, valor, comissão, forma de pagamento e despesas de venda.
- Lucro bruto e líquido; encerramento do veículo como vendido preservando o histórico.
- Dashboard passa a mostrar indicadores reais (estoque, custo imobilizado, margem, vendas).

**Pronto quando:** um veículo comprado, recuperado e vendido produz lucro líquido correto e
auditável de ponta a ponta.

---

## 3. Ordem e dependências

```text
M0 -> M1 -> M2 -> M3 -> M4 -> M5 -> M6 -> M7 -> M8
                              (fim da Fase 1: Acesso)
```

M3 pode começar em paralelo a M2 assim que o contrato do token estiver definido.
M7 depende de M6; M8 depende de M6 e M7.

## 4. Riscos abertos

- **Integração FIPE:** fonte oficial ainda não escolhida (bloqueia M8).
- **Armazenamento de arquivos:** disco local, S3 ou Azure Blob não decidido (bloqueia M6).
- **Deploy:** estratégia de hospedagem e banco de produção não definida.
- **Multiempresa:** decidir no M0 evita retrabalho de migration no M6.
