# Decisões pendentes

Perguntas que **o negócio responde**, e não o código. Cada uma nasceu de uma dúvida real ao usar
o sistema, e todas mudam número na tela — por isso estão escritas aqui em vez de resolvidas por
palpite.

Escritas para serem lidas em voz alta numa conversa: o caso concreto, as opções, o que cada uma
muda, e uma recomendação. Respondida a pergunta, ela vira uma linha em `docs/MARCOS.md` ou uma
ADR, e sai daqui.

Estado em **4 de setembro de 2026**, com o MVP no ar em Docker local.

Os números abaixo são dos dois carros que existem no sistema hoje:

| | Chevrolet Cruze (vendido) | Fiat Argo (pronto para venda) |
|---|---|---|
| Compra | R$ 29.450,00 | R$ 20.000,00 |
| Gastos pagos | R$ 8.544,00 | R$ 0,00 |
| Gastos previstos | R$ 2.500,00 | — |
| **Custo real** | **R$ 37.994,00** | **R$ 20.000,00** |
| Tabela FIPE (set/2026) | R$ 56.530,00 | R$ 51.757,00 |
| Quero receber | R$ 58.000,00 | R$ 53.500,00 |
| Mínimo aceito | R$ 55.000,00 | R$ 50.000,00 |
| Onde está | Loja do Joãozinho | Loja do Joãozinho (8%) |

---

## 1. "Custo final vs FIPE" usa o custo de hoje ou o custo se tudo for pago?

### O caso

O Cruze tem **R$ 2.500 de retrovisor previsto e ainda por pagar**. A linha na ficha mostra hoje:

> **Custo final vs FIPE — 67,21%**

Esse 67,21% é `37.994 ÷ 56.530` — o custo **já pago**. O previsto fica de fora.

### A dúvida

O rótulo diz **"final"**, e "final" lê como *"depois de tudo pago"*. Só que o número é o de hoje.
Quem lê 67,21% e paga o retrovisor amanhã vê o número saltar para 71,63% sem nada ter mudado no
carro.

### As opções

| | O que muda | Cruze |
|---|---|---|
| **A. Continuar com o custo de hoje** | Nada muda no sistema. A linha mede o dinheiro que já saiu | **67,21%** |
| **B. Passar a usar o custo se tudo for pago** | "Final" fica literal. O número já conta o que ainda vai ser pago, e ele para de saltar | **71,63%** |
| **C. Mostrar os dois** | Duas linhas em vez de uma, só quando existe gasto previsto | 67,21% hoje · 71,63% se tudo for pago |

### Recomendação

**B.** O painel inteiro já tem essa separação — "Gastos pagos", "Previsto ainda por pagar",
"Custo se tudo for pago" —, e o alerta de teto de orçamento **já decide pelo projetado**: ele
avisa *"o gasto de hoje cabe no teto, e o que está previsto passa dele"*. Um número chamado
"final" que ignora o previsto discorda do alerta que está três linhas acima.

**Se a resposta for A**, o rótulo devia deixar de dizer "final".

---

## 2. O repasse da loja parceira entra no custo do carro?

### O caso

O Argo está na **Loja do Joãozinho**, cadastrada com **8% de repasse**. O painel de custo mostra:

```
Custo real                 R$ 20.000,00
Quero receber              R$ 53.500,00
Sobra              R$ 33.500,00 · 62,62%
Mínimo aceito              R$ 50.000,00
────────────────────────────────────────
Pela Loja do Joãozinho · 8%
Anúncio sai por            R$ 58.152,17
A loja fica com            R$  4.652,17
────────────────────────────────────────
Tabela FIPE set/2026       R$ 51.757,00
Custo final vs FIPE              38,64%
```

Os 8% aparecem **ao lado do preço**, e ficam **fora do custo real**.

### A dúvida

A pergunta que apareceu foi: *"o Argo está na loja com 8%, e eu não vejo isso no custo real"*.

### O que sustenta o desenho de hoje

- O repasse é **custo do negócio**, e não do carro. Ele some no dia em que o carro volta para o
  pátio da casa, e o custo de um carro não devia mudar por causa de onde ele está parado.
- É a regra que veio do próprio stakeholder no M8: *"eu quero 58 para mim, a loja põe a dela em
  cima"*. Repasse por cima do preço, e não por dentro do custo.
- Somá-lo ao custo contaminaria o **"Custo final vs FIPE"**: ele passaria a medir custo do carro
  **mais** condição comercial, e deixaria de responder "comprei bem?".

### As opções

| | O que muda |
|---|---|
| **A. Como está** | Custo real é o custo do carro. O repasse aparece ao lado do preço, na projeção |
| **B. Somar o repasse ao custo real** | O Argo passaria a "custar" R$ 24.652,17 enquanto estiver na loja, e voltaria a R$ 20.000 ao sair dela. O percentual contra a FIPE iria de 38,64% para 47,63% |
| **C. Uma terceira linha, "custo com o repasse"** | Custo real fica intacto, e o total do negócio aparece separado |

### Recomendação

**A**, que é o que está no ar. **B** faz o custo de um carro parado mudar sozinho, e é o tipo de
número que ninguém consegue explicar seis meses depois.

---

## 3. A projeção pela loja parceira usa o "quero receber" ou o "mínimo aceito"?

### O caso

Hoje ela projeta em cima do **quero receber**:

```
Quero receber R$ 53.500  →  anúncio R$ 58.152,17  ·  loja fica com R$ 4.652,17
```

Pelo **mínimo aceito**, o mesmo carro daria:

```
Mínimo aceito R$ 50.000  →  anúncio R$ 54.347,83  ·  loja fica com R$ 4.347,83
```

### A dúvida

Na hora de anunciar, o número que vai para o anúncio é o do desejado. Mas na hora de **negociar**,
o que importa é até onde dá para ceder — e esse piso, pela loja, é R$ 54.347,83, e não os
R$ 50.000 que estão na tela.

### As opções

| | O que muda |
|---|---|
| **A. Só o desejado** | Como está. Uma linha, e a mais usada |
| **B. Os dois** | O bloco ganha uma segunda linha com o piso pela loja — que é o número da negociação |

### Recomendação

**B.** É uma linha a mais, e ela responde a pergunta que aparece no telefone: *"consigo fechar por
54?"*. Hoje quem responde isso faz a conta de cabeça, e a conta é uma divisão.

---

## 4. A projeção deve considerar a comissão?

### O caso

A projeção de hoje ignora a **comissão** — o valor pago a quem trouxe o comprador. Ela entra
depois, na tela de venda.

Com R$ 1.000 de comissão, o Argo precisaria sair por **R$ 59.239,13** para a revenda ainda
receber os R$ 53.500 limpos.

### A dúvida

A comissão é **por negócio**, e não por pátio: ela depende de quem trouxe o comprador, e muitas
vendas não têm nenhuma. Projetar com um valor que ainda não existe inventaria um preço de anúncio
alto demais.

### As opções

| | O que muda |
|---|---|
| **A. Ignorar, como está** | A projeção responde "pela loja". A comissão entra na venda, onde ela é decidida |
| **B. Um campo de comissão padrão no cadastro do pátio** | Cadastro novo, e ele passaria a valer para todo carro daquele pátio |

### Recomendação

**A.** A comissão não pertence ao lugar onde o carro está. Se aparecer uma comissão que se repete
sempre, ela é do **vendedor**, e o lugar dela seria o cadastro de usuário — outro assunto.

---

## 5. Onde fica o corte do "apertado" no custo contra a FIPE?

### O caso

A linha ganhou cor, em três faixas:

| Faixa | Cor | Leitura |
|---|---|---|
| até 90% | 🟢 verde | sobra espaço entre o custo e o mercado |
| 90% a 100% | 🟡 âmbar | custou menos que a tabela, e apertado |
| 100% em diante | 🔴 vermelho | custou mais do que a tabela |

O Argo está em 38,64% (verde) e o Cruze em 67,21% (verde).

### A dúvida

**Os 90% são palpite meu, e não do negócio.** O raciocínio foi: vendendo pela tabela cheia
sobrariam 10% brutos, e o repasse da loja parceira e a comissão saem daí. Mas quem sabe onde
aperta de verdade é quem compra em leilão há anos.

### A pergunta, em uma linha

> A partir de quantos por cento da tabela um carro deixa de ser um bom negócio?

### Recomendação

Nenhuma. É a única pergunta desta lista que o código não tem como responder — e trocar o número é
trocar um `90` no código.

---

## 6. Consulta por placa: vale contratar um provedor?

### O caso

> *"O Rodrigo falou que tem um programa que você coloca a placa do veículo e ele já traz a FIPE.
> Você conseguiria replicar isso?"*

### Como esses programas funcionam

Duas etapas, e o sistema já tem uma delas:

1. **Consultam a placa** numa base de dados veiculares e recebem marca, modelo, versão, ano e
   chassi.
2. **Casam isso com a FIPE** — e vários desses serviços já devolvem o **código FIPE pronto** na
   mesma resposta.

A etapa 2 é o **M15**, entregue. A etapa 1 é o que falta, e ela é a decisão: **fonte de dados de
placa é paga, por consulta**. A FIPE tem espelho aberto; a placa não tem. Não existe API pública
e gratuita para isso.

### O que muda na tela

No **Novo veículo**, a placa — que já é o primeiro campo — ganha um botão **Buscar**:

> Digita `RQP8E56` → volta *Jeep Renegade 1.8 Longitude, 2020/2019, chassi 9BW…* → e, quando o
> provedor mandar o código FIPE junto, o valor da tabela já vem preenchido.

O cadastro de um carro deixa de ser doze campos e passa a ser uma placa mais conferir.

### O que precisa ser decidido

| Pergunta | Por que ela é do negócio |
|---|---|
| **Qual provedor, e quanto custa por consulta?** | É cobrança por uso, e o preço varia com o volume. Precisa de orçamento — e ele muda a conta de quantos carros por mês compensam |
| **Placa ou chassi?** | O chassi **já é campo obrigatório** em todo carro cadastrado, e a consulta por chassi costuma custar menos. Se o chassi já vem na nota do leilão, talvez a placa nem seja necessária |
| **A cláusula de LGPD do contrato** | Placa liga a um proprietário, e isso é dado pessoal. Para carro que a revenda está comprando ou já comprou o uso é legítimo, e é o contrato do provedor que sustenta isso |

### O que já está pronto do lado do código

O desenho. Seria uma porta `IVehicleByPlate` no domínio e um adaptador na infraestrutura, com a
chave no `.env` — exatamente o mesmo formato da FIPE (ADR-0005) e do bucket (ADR-0004). Trocar de
provedor depois seria escrever outro adaptador.

### Recomendação

**Vale**, e é o maior ganho de digitação que sobrou no cadastro. Duas ressalvas para levar junto:

- **O M15 continua valendo depois disso.** Nem todo provedor devolve o código FIPE, a placa às
  vezes volta sem a versão, e a consulta pode falhar ou o contrato acabar. O casador já resolve
  cinco de dez sozinho, **sem custo por consulta**.
- **Comece medindo.** Antes de assinar, vale contar quantos carros entram por mês: é esse número
  que diz se a economia de digitação paga a consulta.

---

## O que já está decidido, e sai daqui

- **O repasse é sugerido, e continua sendo decidido por quem vende.** O cadastro do pátio guarda
  o combinado, a tela de venda preenche com ele, e o número segue editável (M14, V4).
- **Custo real jamais é guardado**, e é somado a cada leitura (M6).
- **"Vendido" tem uma porta só**: registrar a venda (M8).
- **Isolamento por cliente**: pilha própria por cliente, com o `IdTenant` por baixo (ADR-0006).

---

O que ainda falta **construir** está em `docs/PENDENCIAS.md`, e o que falta decidir sobre **cobrar**
está em `docs/PRECIFICACAO.md`. Este documento é sobre o que falta decidir **no produto**.
