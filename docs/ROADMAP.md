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
6. ~~**`next-auth` está no `package.json` mas não é usado.**~~ **Resolvido no M3 e limpo no
   M13:** a sessão é própria — cookie httpOnly com o JWT da API, montado pelo servidor. O
   pacote ficou no `package.json` sem nenhuma referência no código até o M13, quando saiu.
7. ~~**Versões divergentes:** projetos em `net10.0` com `Microsoft.EntityFrameworkCore 9.0.0`
   e `Pomelo 9.0.0`.~~ **Resolvido:** hoje é **EF Core 10.0.5** com o provider da Oracle
   (`MySql.EntityFrameworkCore` 10.0.1). O Pomelo ficou de fora por escrito — a última versão
   estável dele é a 9.0.0, sem release para o EF Core 10, e foi o que prendeu o CPComunica no
   EF 9. Ver `Directory.Packages.props`.

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
- **FIPE seguia manual até aqui.** O único acesso gratuito é um espelho comunitário sem
  contrato; a integração ganhou marco próprio, o M11. O `FipeCode` do M6 é o que a tornou
  barata.

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

### M10 — Linha do tempo e filtros (RF-25, RF-26) — **concluído**

Plano completo em `docs/plans/m10-linha-do-tempo-e-filtros.md`.

- Histórico único da operação na ficha: compra, gastos, anexos, propostas, status e venda,
  em ordem cronológica — lido das tabelas do domínio, e não da auditoria, que existe para
  perícia e guarda JSON, e não significado. Fotos e documentos de um mesmo dia entram
  contados num evento só, e cada evento traz o nome de quem o fez, inclusive de quem já
  saiu da revenda.
- Filtro por período na listagem de veículos, pela data de compra, no mesmo vocabulário da
  tela de Vendas. O filtro vai ao banco, e jamais peneira em memória.
- Rotina administrativa para o documento excluído que fica no bucket: lista, abre e devolve
  à ficha. Exclusão definitiva jamais é oferecida.

**Pronto quando:** a pergunta "o que aconteceu com esse carro?" é respondida numa tela só, e
o documento apagado por engano volta para a ficha.

**Verificado ponta a ponta:** o Cruze da planilha devolve 34 eventos em ordem, com os 21
gastos reais, as duas propostas recusadas, a venda e as 20 fotos como um evento só; julho
traz o Cruze e agosto traz nenhum no filtro por período; e a tela administrativa desenterrou
13 documentos que estavam pagos e inalcançáveis no bucket desde o M6.

---

### M11 — FIPE e a tela Mercado — **concluído**

Plano completo em `docs/plans/m11-fipe.md`; decisões em
`docs/architecture/decisions/ADR-0005-consulta-da-tabela-fipe.md`.

- Consulta automática pelo `FipeCode` guardado desde o M6: com ele, o preço vem em uma
  chamada, sem navegar marca, modelo e ano. Para o carro sem código, três escolhas — marca,
  modelo e ano — dão o código, e a partir daí a consulta é direta.
- A FIPE não publica API; o acesso é por espelho de terceiros. A consulta entra atrás de uma
  porta no domínio, como o armazenamento de arquivos, para trocar de fonte sem tocar no resto.
- O mês de referência é sempre fixado nas consultas de preço: duas chamadas à mesma fonte, no
  mesmo minuto, chegaram a devolver meses diferentes. As listas de nomes vão sem fixar, porque
  nome não é dinheiro.
- **A tabela sugere; o preço é da pessoa.** A consulta escreve valor, mês, modelo e origem, e
  jamais um campo de preço.
- Cotações guardadas por modelo e mês: dez carros do mesmo modelo custam uma consulta, e a
  cotação de mês fechado vira o histórico que a tela Mercado lê.
- O pátio se atualiza sozinho uma vez por mês, respeitando o valor digitado à mão. Valor velho
  aparece marcado na ficha e na listagem.
- **Tela Mercado**: compra, venda, pedido e propostas contra a tabela do mês de cada negócio,
  e a perda de referência de quem está parado.
- A FIPE jamais bloqueia a operação: fonte fora do ar mantém o último valor, marcado como
  desatualizado.

**Pronto:** o Cruze aparece na tela Mercado vendido por R$ 60.000 quando a tabela do mês dizia
R$ 56.530 — 6,14% acima.

---
### M12 — Matriz perfil × endpoint e isolamento entre empresas — **concluído**

Plano completo em `docs/plans/m12-matriz-perfil-endpoint.md`. Fecha o **A6**, que estava
pendente desde o marco de acesso.

- A API sobe de verdade no teste, contra um MariaDB descartável em contêiner. `dotnet test`
  segue sendo um comando só.
- Matriz de 63 endpoints × 5 perfis, com a expectativa derivada das telas de cada perfil, mais
  o anônimo levando 401 em tudo.
- Lista curta e escrita à mão do que cada perfil **jamais** alcança — porque a matriz derivada
  prova que a fechadura combina com a etiqueta, e não que a etiqueta é a certa.
- Isolamento entre empresas com duas revendas: leitura e escrita cruzadas respondem 404.

**Encontrou oito handlers** que liam pelo código público sem filtrar a empresa, um deles
excluindo usuário de outra revenda com 204. Consertado na raiz: `GetByCodeAsync` passou a pedir
a empresa.

---

### M13 — Faxina — **concluído**

Plano completo em `docs/plans/m13-faxina.md`.

- Saiu o `next-auth`, que estava no `package.json` com zero referências no código.
- Saíram seis chaves do `appsettings.json` que não mapeavam para propriedade nenhuma desde a
  ADR-0003; o que ficou tem o nome certo e é lido de verdade.
- As divergências 6 e 7 desta lista foram fechadas dizendo como se resolveram.
- O carro vendido parou de contar dias de pátio no dia em que saiu — o número crescia toda
  manhã na listagem e na ficha.
- **ADR-0006** registra a decisão de isolamento por cliente.

---

### M14 — Pátios, e o relatório de cada lugar onde o carro está — **concluído**

Plano completo em `docs/plans/m14-patios.md`.

- **`Yard`**: um cadastro só, com o tipo dentro — pátio da revenda ou loja de terceiro. Tela
  própria, permissão própria, e o repasse combinado em percentual **ou** em valor. Pátio da casa
  jamais carrega repasse.
- **O carro mora num pátio**: uma coluna no veículo, escolha no cadastro e na ficha, e um botão
  próprio para mover.
- **A mudança de pátio virou evento** na linha do tempo do M10, com o de onde, o para onde, o
  motivo, a hora e quem fez. A passagem tem tabela própria, e as chaves para o pátio são
  `restrict`: excluir um pátio jamais apaga a história de quem passou por ele.
- **O painel ganhou o bloco Por pátio** — carros, capital parado e tempo médio em cada lugar —
  **sem trocar o total pelo pedaço**. A listagem de veículos ganhou o filtro por pátio, aplicado
  no banco.
- **A venda sugere o repasse** combinado no cadastro do pátio quando o carro está na loja de um
  parceiro. O cálculo do M8 não mudou: os campos continuam editáveis.

Segurança: um código de pátio que a empresa desconhece responde lista vazia no filtro e 404 na
mudança de pátio — o pátio é procurado por código **e** por empresa, juntos.

---

## 3. Ordem e dependências

```text
M0 -> M1 -> M2 -> M3 -> M4 -> M5 -> M6 -> M8 -> M9 -> M10 -> M11 -> M12 -> M13 -> M14
                              (fim da Fase 1: Acesso)
```

M3 pode começar em paralelo a M2 assim que o contrato do token estiver definido.
O M7 deixou de existir: custo entrou no M6. O M8 depende do M6.

## 4. Riscos abertos

O apanhado do que ficou em aberto quando o desenvolvimento parou para o MVP está em
`docs/PENDENCIAS.md`.

- **Fonte da FIPE:** resolvido no M11 quanto ao desenho, e aberto quanto ao fornecedor — o
  espelho é de terceiros e pode sumir ou passar a cobrar. As três saídas estão prontas: a porta
  no domínio, o interruptor de configuração e o valor digitado à mão.
- **Backup:** durabilidade de bucket não é backup. É o V1 e o V2 do M9.
- **Deploy:** hospedagem, domínio e conta no R2 são as decisões do V0 do M9.
- **Multiempresa:** resolvido no M0 — toda tabela de operação carrega `IdTenant` —, e
  **provado no M12**, com duas revendas e teste de leitura e escrita cruzadas. A decisão de
  isolamento por cliente está escrita na **ADR-0006**: pilha própria por cliente, com o
  `IdTenant` por baixo.
- **Acesso do parceiro ao próprio pátio:** anotado, e deixado para depois — de novo no M14, que
  criou o cadastro do pátio mas não a porta de entrada dele. O dono da loja onde o carro está
  poderia entrar e ver só os carros que estão com ele; é uma fronteira de segurança nova dentro
  da mesma empresa, e por isso vira marco próprio.
