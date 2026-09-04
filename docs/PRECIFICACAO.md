# Precificação

Como cobrar pelo Revenda Pro, e por quê. Escrito em **4 de setembro de 2026**, com o MVP pronto e
rodando, e ainda **sem nenhum cliente pagante** — então quase tudo aqui é decisão a validar, e não
resultado observado.

Cada afirmação vem marcada com o que ela é:

- **Medido** — número que saiu do sistema ou da arquitetura deste repositório.
- **Suposto** — estimativa minha, para ser trocada pelo número real assim que houver um.
- **A decidir** — pergunta que precisa de uma resposta de negócio.

O que falta **construir** está em `docs/PENDENCIAS.md`; o que falta **decidir no produto**, em
`docs/DECISOES-PENDENTES.md`. Este documento é sobre o que falta decidir no **negócio**.

---

## 1. Para quem é

Duas pessoas diferentes, e elas pagam diferente.

| | **O comprador de leilão** | **A revenda pequena** |
|---|---|---|
| Quem é | compra, recupera e revende por conta própria | loja com equipe, 20 a 80 carros |
| Quantos carros | 5 a 20 por ano | 20 a 80 parados a qualquer momento |
| Quem usa o sistema | ele mesmo | 3 a 6 pessoas, com papéis diferentes |
| O que dói | *"esse carro deu lucro mesmo?"* | isso, mais *"quem lançou esse gasto?"* e *"cadê o documento?"* |
| Disposição a pagar | baixa | média, e previsível |

**Precifique para a revenda.** Metade do que o sistema faz — perfis, matriz de permissão, linha do
tempo com autor, pátios, documentos excluídos — só tem sentido com mais de uma pessoa mexendo. O
comprador solo entra por um plano de entrada honestamente limitado, e não por um plano grátis.

---

## 2. O fato da arquitetura que limita o preço

**Medido, na ADR-0006:** cada cliente ganha uma **pilha própria** — mesmo código, outro
`docker compose -p`, outro banco. E a conta de memória está escrita lá:

> Cada pilha pede ~1 GB: banco ~400 MB, API ~200 MB, frontend ~200 MB. Um VPS de 4 GB segura duas
> ou três com folga.

Três consequências que amarram a precificação:

1. **Plano gratuito permanente está fora.** Cliente que não paga consome a mesma memória do que
   paga. Teste com prazo, sim; grátis para sempre, não.
2. **O preço mínimo tem piso**, e o piso é infraestrutura de verdade — não é um registro a mais
   numa tabela compartilhada.
3. **A própria ADR manda revisar por volta de dez clientes.** A precificação precisa financiar
   essa migração, ou o décimo cliente é onde a margem quebra.

---

## 3. A escolha que mais importa: por que se cobra

**Decidido: assinatura por revenda, com teto de carros. Jamais por usuário.**

| Métrica | Por que sim | Por que não |
|---|---|---|
| **Por usuário** | fácil de explicar | revenda pequena tem 3 pessoas e vai **compartilhar login**. Aí morre a linha do tempo com autor, o perfil de Oficina, a matriz de permissão — ou seja, você cobra para o cliente sabotar o próprio produto |
| **Por carro no pátio** ✅ | é o número que ele já usa para pensar no negócio, e cresce junto com o valor entregue | cria incentivo para excluir carro — resolvido abaixo |
| **Por carro vendido** | alinhamento perfeito com o valor | receita irregular, e ele tem motivo para esconder venda |
| **Percentual da venda** | — | ele esconde venda, e a sensação é a mesma da loja parceira ficando com um pedaço. Péssima química num produto que existe para mostrar quanto sobra |

⚠️ **O incentivo perverso, e como fechá-lo.** A exclusão aqui é lógica (RNF-08): o carro some da
tela e a linha continua no banco. Conte pelo que **entrou no mês**, e não pelo que está visível
hoje — assim excluir carro não reduz fatura, e o número fica auditável dos dois lados.

---

## 4. Os planos

**A decidir: os valores.** Os tetos abaixo são proposta; os preços ficam em branco de propósito,
porque a seção 5 diz como chegar neles.

| Plano | Para quem | Teto | O que entra |
|---|---|---|---|
| **Entrada** | quem compra em leilão por conta própria | 10 carros/mês, 2 usuários | veículo, custo real, gastos, fotos, documentos, venda, FIPE |
| **Revenda** | a loja pequena com equipe | 40 carros/mês, usuários à vontade | tudo do Entrada, mais perfis, propostas, painel, mercado |
| **Pátio** | quem deixa carro em loja de terceiro | 120 carros/mês | tudo, mais multi-pátio e relatório por lugar |

**Por que o multi-pátio fica no plano de cima:** ele é do M14, e resolve uma dor que só existe em
quem já tem escala — *"quanto tenho parado na Loja do Joãozinho"*. Quem tem essa dor tem dinheiro.

---

## 5. Como chegar no número

### O piso: quanto custa atender um cliente

**A calcular com as contas reais.** A estrutura da conta é esta, e os itens saíram do
`docker-compose.prod.yml` e do `docs/operations/deploy.md`:

| Item | Como calcular | Fonte |
|---|---|---|
| **VPS** | preço do VPS ÷ quantas pilhas cabem nele | ~1 GB por pilha (**medido**, ADR-0006) |
| **Bucket (R2)** | GB armazenados × preço/GB | 20 fotos por carro, em 3 tamanhos, mais documentos |
| **Backup** | GB do dump diário × retenção | `docs/operations/backup.md` |
| **Domínio** | anual ÷ 12 | um por cliente, ou subdomínio do seu |
| **Tabela FIPE** | plano com token ÷ clientes | ver o alerta abaixo |
| **Seu tempo** | horas/mês de suporte × sua hora | é o item que a maioria esquece, e o maior |

> ⚠️ **A FIPE já custa.** Em 4 de setembro de 2026, o espelho gratuito **recusou consultas por
> cota diária estourada** durante os testes (**medido** — está no log da API e virou a mensagem
> própria do M15 V6). Com clientes de verdade, o plano com token deixa de ser opcional. Isso é
> custo por consulta, e entra na conta antes de o primeiro cliente entrar.

**Regra prática (suposto):** cobre pelo menos **4 a 5 vezes** o custo de infraestrutura por
cliente. SaaS que cobra menos que isso morre no primeiro cliente que dá trabalho — e cliente
pequeno dá trabalho.

### O teto: quanto vale para ele

Aqui os números são **medidos**, e são o argumento de venda inteiro:

- A planilha real do stakeholder mostrava **R$ 350 a menos** do que o carro tinha custado: o total
  foi digitado uma vez, e três gastos entraram embaixo dele depois.
- O Cruze fechou com **R$ 17.006 de sobra, margem de 28,34%, 61 dias entre a compra e a venda** —
  números que ninguém tinha antes.
- A venda saiu **6,14% acima da tabela FIPE** do mês dela.

**Um carro vendido no prejuízo por engano, por ano, paga anos de assinatura.** É essa a frase.

### A referência: quanto cobra quem já está lá

**A fazer.** Duas tardes ligando para concorrente de gestão de revenda, anotando preço, teto e o
que entra. É o número que impede tanto de cobrar barato demais quanto de assustar.

---

## 6. Quatro decisões que vêm junto

**1. Cobre implantação, uma vez.**
Cada cliente é uma pilha nova, mais importar a planilha dele, mais treinar. É trabalho real, e a
taxa faz três coisas: melhora o caixa, afasta curioso, e cria o momento em que **os dados dele
entram no sistema** — que é o que faz ele não sair depois.

**2. Anual com dois meses grátis.**
Revenda pequena adora desconto, e você mata a rotatividade, que é o que mais dói em cliente
pequeno.

**3. Teste com os dados dele, e jamais com dados de demonstração.**
Quatorze dias, e **você** importa cinco carros da planilha dele na primeira reunião. O momento da
virada é ele ver o custo real de um carro **dele** e o número não bater com o que ele achava. Com
dados de demonstração isso nunca acontece.

**4. Consulta por placa e FIPE paga são adicionais, e não plano.**
As duas têm custo por consulta. Cobre por consulta com margem, ou em pacote mensal. Ver a
**pergunta 6** de `docs/DECISOES-PENDENTES.md`, que já lista o que precisa ser decidido — provedor,
custo por consulta, placa ou chassi, e a cláusula de LGPD.

---

## 7. O que **não** fazer

| | Por quê |
|---|---|
| **Cobrar por usuário** | o cliente compartilha login, e o produto perde a parte que o segura |
| **Plano gratuito permanente** | cada cliente é uma pilha de ~1 GB. Grátis custa igual |
| **Cobrar por foto ou por GB** | é invisível para o cliente, e ele para de subir foto — que é o que faz o anúncio vender |
| **Percentual sobre a venda** | ele esconde venda, e a química é péssima num produto que existe para mostrar quanto sobra |
| **Prometer o que está aberto** | subida em produção, acesso do parceiro ao próprio pátio e consulta por placa **ainda não existem**. Ver `docs/PENDENCIAS.md` |

---

## 8. Os dois primeiros

Rodrigo e Thiago não são clientes: são **âncora**. Preço de fundador travado para sempre, em troca
de três coisas concretas:

1. **Depoimento gravado**, com o número real deles.
2. **Permissão de usar esse número** na conversa com o próximo.
3. **Duas apresentações cada**, para colegas do ramo.

Revenda pequena compra de quem conhece. Seu canal é a rede deles, e não anúncio.

---

## 9. O que ainda falta responder

| Pergunta | Quem responde |
|---|---|
| Os três preços | você, depois do piso, do teto e da referência |
| Valor da implantação | você |
| Carro **entrado no mês** ou **parado no pátio** como medida da fatura | você — a recomendação acima é "entrado", pela exclusão lógica |
| Domínio próprio por cliente, ou subdomínio seu | decisão técnica com efeito no custo |
| Plano da FIPE: no preço ou como adicional | depende do preço por consulta |
| A partir de quantos clientes a pilha por cliente vira pilha compartilhada | ADR-0006 diz "por volta de dez"; a precificação precisa financiar essa virada |

---

## O que já está pronto para vender

Isto aqui existe, roda e foi conferido contra dados reais — é o que pode ser demonstrado sem
promessa nenhuma:

- **Custo real** somado a cada leitura, e jamais digitado.
- **Teto de orçamento** que avisa antes de o gasto estourar.
- **Proposta com a sobra calculada** antes de qualquer coisa ser gravada.
- **Venda** com repasse de loja parceira, comissão e troca — e a troca cadastra o carro que entra.
- **Linha do tempo** com o nome de quem fez cada coisa.
- **Tabela FIPE consultada sozinha**, uma vez por mês, e o carro medido contra ela.
- **Pátios**, com o relatório de cada lugar sem perder o total.
- **Permissão por tela**, provada com a API no ar: 63 endpoints × 5 perfis.
- **Isolamento entre empresas**, provado com duas revendas de verdade.
- **529 testes verdes.**
