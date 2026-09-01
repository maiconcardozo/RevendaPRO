# Plano — Melhorias de frontend

Revisão de `frontend/components/layout/PanelShell.tsx`, `frontend/app/globals.css`,
`frontend/app/login/page.tsx`, `dashboard`, `usuarios` e `perfis`.
Plano apenas. Nada implementado.

---

## 1. O que já está bom — e por que não vou mexer

A direção visual atual **não é um template**. A paleta tem vocabulário próprio (`signal`,
`gain`, `drop`, `flare` — linguagem de instrumentação), o fundo `instrument-grid` de 44px
sustenta a metáfora de painel, o azul-marinho `#0b1e3f` com ciano `#0090c4` é uma escolha, e
o modo escuro é um conjunto de tokens redefinidos, não um filtro.

Isso se preserva. As propostas abaixo **aprofundam** essa direção; nenhuma a substitui.

---

## 2. O problema real: a tipografia não existe

```css
.font-display { font-family: Arial, "Segoe UI", sans-serif; }
.hero-title   { font-family: Arial, "Segoe UI", sans-serif; }
body          { font-family: Arial, "Segoe UI", sans-serif; }
.num          { font-family: ui-monospace, monospace; }
```

Há três classes com nomes de papéis tipográficos distintos e **todas resolvem para Arial**.
A estrutura de um sistema tipográfico está lá; a personalidade, não. Em Windows — que é o
ambiente do usuário piloto — isso entrega a fonte mais anônima disponível.

É a mudança de maior impacto e a mais barata do plano.

### Proposta

| Papel | Fonte | Onde |
|---|---|---|
| Display | **Archivo** (700/800) e **Archivo Expanded** (700) | títulos, `hero-title`, eyebrows em caixa alta |
| Corpo | **Inter** (400/500/600) | texto, tabelas, formulários, botões |
| Identificador | **JetBrains Mono** (500) | placa, chassi, renavam, número de NF |

Por quê:

- **Archivo** é uma grotesca desenhada para interface de alta densidade, com uma versão
  Expanded que faz os rótulos em caixa alta com `tracking-[.18em]` — que o layout **já usa**
  — finalmente funcionarem como device tipográfico em vez de Arial esticado.
- **Inter** em corpo com `font-variant-numeric: tabular-nums` resolve o alinhamento de todos
  os valores em real, sem recorrer a mono para dinheiro.
- **JetBrains Mono** fica reservado para strings que realmente são códigos: placa e chassi.
  Isso dá função ao mono em vez de usá-lo como enfeite.

Custo: um `next/font/google` no `layout.tsx` e três linhas em `globals.css`. Sem mudar markup.

---

## 3. Consequências da decisão de arquitetura no layout

A decisão de eliminar Admin Master / Cliente já invalida três coisas na tela:

1. **Bloco "Ambiente / Admin Master" na topbar** (com o ponto pulsante `animate-ping`).
   O conceito deixou de existir. Remover.
   No lugar, ocupar o espaço com **o nome da tela atual** — informação real, e resolve a
   pergunta "onde eu estou" quando a sidebar está recolhida.

2. **"Admin Master" no rodapé da sidebar.** Passa a exibir o **perfil** do usuário, que agora
   é o que determina o menu. Vira contexto útil: "por isso eu vejo estes itens".

3. **"Admin Master" no dropdown do usuário.** Remover a linha.

---

## 4. Remover o que não faz nada

O painel tem controles que parecem funcionais e não são:

- **Sino de notificações** — não notifica nada. Remover até existir a funcionalidade.
- **"Minha conta"** e **"Admin Master"** no dropdown são `<div>`, não botões. Um usuário de
  teclado nem alcança; um de mouse clica e nada acontece. Remover ou implementar.
- **Identidade duplicada:** o usuário aparece no rodapé da sidebar *e* no avatar da topbar.

  Proposta — cada elemento com um papel só:
  - rodapé da sidebar = **quem você é e qual seu perfil** (informação, não clicável);
  - avatar da topbar = **o controle** (dropdown com sair).

---

## 5. Duas assinaturas vindas do próprio assunto

O `instrument-grid` é um fundo bonito, mas é papel milimetrado genérico — poderia estar em
qualquer painel. As duas propostas abaixo vêm do mundo de quem vai usar o sistema.

### 5.1 A placa como identificador — a aposta

Um revendedor não identifica um carro por UUID. Identifica pela **placa**. Ela é a chave
natural do domínio inteiro: as fotos, os documentos, os orçamentos e a lista de gastos que ele
hoje organiza no papel são organizados por placa.

Proposta: um componente `<Placa>` no padrão Mercosul — tarja azul superior, caractere
monoespaçado em caixa alta, proporção correta — usado **em todo lugar onde um veículo é
identificado**: linha da listagem, card, cabeçalho do wizard, resultado de busca, anexo.

```text
┌──────────────┐
│▓▓▓▓ BRASIL ▓▓│
│  ABC 1D23    │   ← JetBrains Mono, caixa alta, tabular
└──────────────┘
```

Regras de contenção, para não virar piada:
- tamanho de chip, nunca de gráfico — altura equivalente a um badge;
- só na identificação do veículo, nunca decorativa;
- versão só-texto para densidade alta (tabelas com muitas linhas);
- fallback quando não há placa (veículo de leilão sem documentação): chassi em mono.

**Este é o risco do projeto.** Justificativa: é literalmente como o público-alvo nomeia o
objeto central do sistema. Se não convencer na primeira tela, o fallback é um chip mono
simples com borda — mesma função, sem a tarja.

### 5.2 O trilho de status

RF-05 define 8 status que **são uma sequência real**: em análise → comprado → em transporte →
em reparo → pronto para venda → anunciado → vendido. Marcadores sequenciais só se justificam
quando a ordem carrega informação — aqui carrega, e é a informação mais importante da tela:
onde o carro está e há quanto tempo.

Proposta: um único componente "trilho", em três densidades:

```text
compacto (linha de tabela)
●━━●━━●━━○──○──○──○     em reparo · 12 dias

completo (detalhe do veículo)
●─────●─────●─────◉─────○─────○─────○
comprado  transp.  reparo   ← aqui   pronto  anunc.  vendido
14/03     18/03    21/03      12d
```

É aqui que a metáfora de "instrumento" da paleta atual finalmente paga: o trilho é o
mostrador. Um componente, reusado em três lugares — não três desenhos diferentes.

---

## 6. Disciplina de cor para dinheiro

`--gain` e `--drop` já existem com nome de resultado financeiro. Mas nada impede que amanhã um
toast de sucesso use verde e dilua o significado.

Proposta: **`--gain` e `--drop` ficam reservados exclusivamente para margem e lucro.** Feedback
de interface ganha tokens próprios (`--ok`, `--erro`), visualmente distintos. Em um sistema
cujo propósito é dizer se o carro deu lucro, verde precisa significar uma coisa só.

---

## 7. Estados que o menu dinâmico obriga a criar

Com o menu vindo de `/api/auth/me`, o shell renderiza antes do menu chegar. Sem tratamento,
o usuário vê a sidebar vazia e os itens "pipocando".

- **Carregando:** skeleton com o número e a altura aproximados dos itens reais. Sem spinner,
  sem deslocamento de layout.
- **Perfil sem telas:** não cair em painel vazio. Tela dedicada: "Seu perfil ainda não tem
  telas liberadas. Fale com o administrador da revenda."
- **403 por URL direta:** dentro do shell, com o menu ainda visível, e um caminho de volta.
- **Listas vazias:** convite à ação, não lamento. "Nenhum veículo no estoque." + botão
  "Cadastrar veículo". Vale para usuários, perfis, custos e vendas.

---

## 8. Piso de acessibilidade

Itens verificados no código atual que faltam:

- **Skip link** para o `<main>` — sem ele, teclado percorre o menu inteiro a cada página.
- **Drawer mobile sem armadilha de foco.** Abre e o foco fica no conteúdo atrás; fecha e o
  foco não volta para o botão que abriu.
- **Dropdown do usuário** não fecha com `Escape` nem com clique fora, e o botão não tem
  `aria-expanded`.
- **Sidebar recolhida** identifica os itens só por `title`, que leitor de tela não anuncia de
  forma confiável. Precisa de `aria-label` no link.
- **Botão de recolher** sem `aria-expanded` / `aria-controls`.

`:focus-visible` e `prefers-reduced-motion` já estão corretos no `globals.css` — manter.

---

## 9. Dívidas pontuais

- `frontend/app/login/page.tsx` tem `http://localhost:5100` **hardcoded** e usa
  `bg-[#0b1e3f]` / `text-white` cru em vez dos tokens. Quebra em qualquer ambiente que não
  seja a máquina local. Resolver junto com o marco A4.
- `PanelShell.tsx`, `globals.css` e as páginas estão em uma linha por bloco. Legível pela
  máquina, caro para o humano manter. Reformatar (Prettier) antes de crescer o módulo de
  veículos.
- `next-auth` está instalado e não é usado — remover ou adotar (decisão do ADR-0002).
- A leitura de tema em `useEffect` + `requestAnimationFrame` provoca flash de tema claro no
  primeiro paint para quem usa escuro. Resolver com script inline no `<head>` antes da
  hidratação.

---

## 10. Ordem sugerida

| # | Item | Esforço | Impacto |
|---|---|---|---|
| 1 | Tipografia (Archivo + Inter + JetBrains Mono) | baixo | **alto** |
| 2 | Remover Ambiente/Admin Master, sino e controles mortos | baixo | alto |
| 3 | Reservar gain/drop para dinheiro; criar ok/erro | baixo | médio |
| 4 | Piso de acessibilidade | médio | alto |
| 5 | Estados de carregando / vazio / 403 | médio | alto — obrigatório para A4 |
| 6 | Flash de tema e URL da API hardcoded | baixo | médio |
| 7 | Componente `<Placa>` | médio | alto — a assinatura |
| 8 | Componente trilho de status | médio | alto — junto com o marco M6 |

Os itens 1 a 6 podem entrar junto com o marco **A4**. Os itens 7 e 8 pertencem ao módulo de
veículos (**M6**) — antes disso não há o que identificar nem status para trilhar.
