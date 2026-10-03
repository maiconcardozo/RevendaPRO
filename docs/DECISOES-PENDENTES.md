# Decisões pendentes

Perguntas que **o negócio responde**, e não o código. Cada uma nasceu de uma dúvida real ao usar
o sistema, e todas mudam número na tela — por isso estão escritas aqui em vez de resolvidas por
palpite.

Escritas para serem lidas em voz alta numa conversa: o caso concreto, as opções, o que cada uma
muda, e uma recomendação. Respondida a pergunta, ela vira uma linha em `docs/MARCOS.md` ou uma
ADR, e sai daqui.

Estado em **2 de outubro de 2026**, com o sistema no ar no servidor da rede (`192.168.1.7`). As quatro perguntas sobre custo e projeção foram respondidas nesse dia, e estão na última seção.

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

## 1. Onde fica o corte do "apertado" no custo contra a FIPE?

### O caso

A linha ganhou cor, em três faixas:

| Faixa | Cor | Leitura |
|---|---|---|
| até 90% | 🟢 verde | sobra espaço entre o custo e o mercado |
| 90% a 100% | 🟡 âmbar | custou menos que a tabela, e apertado |
| 100% em diante | 🔴 vermelho | custou mais do que a tabela |

O Argo está em 38,64% (verde) e o Cruze em 71,63% (verde), já pelo custo se tudo for pago (M27).

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

## 2. Consulta por placa: vale contratar um provedor?

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

Respondidas com o stakeholder em **2 de outubro de 2026**:

- **"Custo final vs FIPE" passa a usar o custo se tudo for pago.** "Final" fica literal, e a
  linha concorda com o alerta do teto de orçamento, que já decide pelo projetado. No Cruze, vai de
  67,21% para 71,63%. **A construir.**
- **O repasse da loja parceira fica fora do custo real**, como está. Ele é condição comercial do
  lugar onde o carro está, e aparece ao lado do preço, na projeção.
- **A projeção pela loja parceira mostra também o piso**, calculado pelo mínimo aceito, além do
  anúncio pelo quero receber. No Argo, o piso pela Loja do Joãozinho é R$ 54.347,83. **A
  construir.**
- **A projeção continua ignorando a comissão.** Ela é de cada negócio, e entra na tela de venda,
  onde é decidida.

---

O que ainda falta **construir** está em `docs/PENDENCIAS.md`, e o que falta decidir sobre **cobrar**
está em `docs/PRECIFICACAO.md`. Este documento é sobre o que falta decidir **no produto**.
