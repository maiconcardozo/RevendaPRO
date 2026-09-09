# M18 — Fornecedores, e quanto já foi para cada um

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md` e do plano
`docs/plans/m18-fornecedores.md`.

## O que este marco resolve

> *"Preciso fazer a implementação de fornecedor e quanto você já gastou em cada fornecedor. Vai
> ser oficina, pintura, autopeças, essas coisas. E no dash preciso de um painel onde eu vou ver
> quais são os fornecedores que eu mais gastei, e ter um espaço exclusivo também."*

## As regras

### Fornecedor diz **de quem**; tipo de gasto diz **o quê**

O gasto aponta para os dois.

**Por que é essa:** são perguntas diferentes, e a mesma oficina responde a várias da segunda —
cobra Mecânica num carro e Peças no outro. Juntar as duas coisas obrigaria a escolher entre saber
o que se gastou e saber com quem.

### O fornecedor é opcional

**Por que é essa:** IPVA, multa e taxa de leilão vêm sem fornecedor. Obrigar a escolher criaria o
fornecedor "Governo" em toda revenda — um cadastro inventado para satisfazer o formulário.

### O ramo é cadastro, e a revenda nasce com 25

O plano propunha um enum; o stakeholder pediu cadastro no mesmo dia — *"já coloque bastante para
não precisar ficar cadastrando"*.

**Por que é essa:** é a razão do tipo de gasto, do M6: a estofaria e o chaveiro só aparecem no
uso, e uma lista fixa os mandaria para "Outros", que é onde a análise para de valer. O ramo se
administra de dentro da tela Fornecedores, porque uma tela "Ramos" seria uma permissão a mais
para uma coisa só.

### "Quanto gastei" é o que foi **pago**

O previsto aparece à parte.

**Por que é essa:** um orçamento com a funilaria interessa, e ainda assim jamais é dinheiro que
saiu — a mesma regra do custo do carro, no M6.

### A soma é do banco

`GROUP BY`, e nunca a lista inteira de gastos carregada para somar no servidor.

**Por que é essa:** é o que a listagem recusa desde o M6. Somar na aplicação funciona com vinte
carros e para de funcionar com dois mil, em silêncio e de madrugada.

### Duas leituras, para duas perguntas

O dashboard mostra o painel de fornecedores **no período das vendas** — a leitura do mês. A tela
Fornecedores abre com ele inteiro, *desde o início*.

**Por que é essa:** "com quem gastei este mês" e "com quem gastei desde sempre" são decisões
diferentes: a primeira é a conversa do mês, a segunda é com quem a loja tem relação.

### Quem registra gasto lê a lista; quem administra, lê valores

`GET api/suppliers` é guardado por `vehicles` e vem **sem dinheiro**; o ranking e a ficha exigem
`suppliers`.

**Por que é essa:** o mecânico da oficina precisa escolher o fornecedor no lançamento, e jamais
precisa saber quanto a loja já pagou a ele.

### Fornecedor com gasto no nome recusa exclusão, e diz quantos

**Por que é essa:** apagá-lo apagaria a resposta para a pergunta que o cadastro existe para
responder.

## Como usar

*Administração → Fornecedores*: cadastro, ramo e a ficha de cada um — pago, previsto, em quê, e
em que carros, com a placa levando ao veículo. No painel, o bloco de fornecedores no período das
vendas. No lançamento do gasto, o campo de fornecedor, opcional.

## O que mudou por baixo

- Entidade de fornecedor e de ramo, o vínculo opcional no gasto, e as somas com `GROUP BY`.
- Os gráficos em SVG — ranking em barras, rosca por ramo, colunas mês a mês com o previsto
  hachurado por cima do pago, e o gasto por tipo —, com a paleta validada nos dois temas.
- O pátio de demonstração ganhou nove fornecedores, com a Mecânica dividida entre duas oficinas
  para o ranking ter disputa; quem já tinha os vinte carros no banco recebeu o fornecedor nos
  gastos antigos **sem apagar nada**.

## O que foi conferido

Provado contra o MariaDB real: a soma, o período, a ficha, o painel — e a outra revenda
enxergando nada.

## O que ficou de fora, e por quê

- **Contas a pagar do fornecedor**: aqui o gasto é do carro. O dinheiro no tempo, com vencimento
  e atraso, veio no M22.
