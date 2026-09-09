# M8 — Proposta, venda, troca e painel

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md` e do plano
`docs/plans/m8-venda-e-proposta.md`.

## O que este marco resolve

O carro entra, gasta, e uma hora alguém oferece dinheiro por ele. A pergunta do vendedor, no
instante em que o cliente fala um número, é uma só: **quanto sobra se eu aceitar?** Este marco
responde isso antes de qualquer coisa ser gravada, e fecha a venda quando o número serve.

## As regras

### Quanto sobra é calculado na hora, e jamais guardado

Ao digitar o valor da proposta, a tela mostra o que sobraria — já descontando repasse, comissão
e o custo do carro somado agora.

**Por que é essa:** é o número da decisão, e ele precisa existir **antes** de existir uma
proposta gravada. E vale a regra do M6: o custo do carro se move até ele sair do pátio, então
guardar a sobra a congelaria numa conta que envelhece.

### O repasse da loja parceira entra por cima

O vendedor diz quanto quer receber; o repasse da loja soma em cima disso.

**Por que é essa:** foi exatamente como o stakeholder descreveu — *"eu quero 58 para mim, a loja
põe a dela em cima"*. Descontar por dentro daria um número menor no bolso dele, e a conversa com
a loja parceira acontece na outra ordem.

### "Vendido" tem uma porta só: registrar a venda

A mudança de situação recusa esse destino. Um carro só fica vendido pela venda.

**Por que é essa:** um carro marcado como vendido sem venda por trás é um carro sem comprador,
sem preço e sem lucro — e some do estoque levando a informação junto. A porta única é o que
impede o sistema de ter um estado que a operação não tem.

### A troca cria um carro no estoque

Quando o comprador entrega um carro, ele nasce no pátio com origem *Troca* e o valor acordado
como preço de compra.

**Por que é essa:** o carro existe de verdade, e vai precisar de gastos, fotos e uma venda. Ele
entra pela porta da frente, como qualquer outro — e cancelar a venda **jamais** o apaga, pelo
mesmo motivo.

### O painel soma o estoque inteiro

Capital parado, contagem por situação, lucro projetado e realizado, e os cinco carros de maior
investimento, maior sobra prometida e mais tempo parado.

**Por que é essa:** é a leitura de quem abre o sistema de manhã. Cada número é somado no momento
da chamada, em poucas consultas — e jamais uma por carro.

## Como usar

Na ficha do carro, aba **Propostas** → *Registrar proposta*: quem ofereceu, quanto, como paga e
por qual canal. A sobra aparece enquanto se digita. Para fechar, *Aceitar e vender* na proposta,
ou o botão verde **Vender** no alto da ficha.

## O que mudou por baixo

- `Proposal`, `Sale`, e o objeto de valor `DealResult`, que faz a conta e nada guarda.
- Origem *Troca* no veículo, e o vínculo entre a venda e o carro que entrou.
- O painel (`api/dashboard`) e a listagem de vendas.

## O que foi conferido

Ponta a ponta, com os números reais do stakeholder: o Cruze que custou **R$ 37.994**, vendido
por 55 com 20 em carro, deixa os mesmos **R$ 17.006** que a proposta prometia — e o carro da
troca nasce no pátio a 20 mil.

## O que ficou de fora, e por quê

- **A consulta automática da FIPE.** O único acesso gratuito é um espelho comunitário sem
  contrato, e a decisão foi esperar. O código FIPE já era guardado desde o M6, de propósito, e é
  ele que deixou a integração barata no M11.
- **O recebimento da venda.** Aqui a venda registrada era dinheiro no bolso; o dinheiro no tempo
  só chegou no M22.
