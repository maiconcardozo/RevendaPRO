# M21 — Clientes: quem ofereceu, quem comprou, e quem volta

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md` e do plano
`docs/plans/m21-clientes.md`.

## O que este marco resolve

> *"Não seria fornecedor, seria comprador."*

O M18 deu à loja o cadastro de quem ela **paga**. Faltava o cadastro de quem ela **atende** — a
pessoa que ofereceu, a que comprou, e a que volta em seis meses.

## As regras

### Cliente antes de comprador

A tela se chama **Clientes** e fica no grupo Operação, entre os carros e as vendas.

**Por que é essa:** a pessoa que ofereceu e foi recusada existe desde a primeira proposta, e é
ela que volta. Uma tela chamada Compradores esconderia metade das pessoas que a loja conhece — e
o lugar é Operação porque a tela é do vendedor, e não da administração.

### A proposta e a venda apontam para o cliente, **e** guardam a cópia do papel

`IdCustomer` nas duas; os textos de sempre ficam como o que estava escrito no dia. A tela lê o
nome do cliente de hoje.

**Por que é essa:** um contrato diz quem assinou mesmo que a pessoa troque de telefone. Apontar
só para o cadastro faria a correção de um nome reescrever documentos antigos; guardar só o texto
faria a loja perder o vínculo. As duas coisas juntas respondem as duas perguntas.

### Ninguém redigitou nada

Na primeira subida, cada revenda ganhou os clientes das vendas e propostas que já tinha: casados
por documento, depois por telefone, depois por nome igual quando um dos lados estava sem
telefone.

**Por que é essa:** um cadastro que nasce vazio é um cadastro que ninguém preenche. E a ordem do
casamento vai do sinal mais forte para o mais fraco — dois Eduardos com telefones diferentes
continuaram **duas pessoas**, porque juntar por engano é o erro caro: ele mistura o histórico de
compra de gente que não se conhece.

### Cadastrar sem sair da proposta

O seletor busca enquanto digita, por nome, telefone ou documento; escolher preenche telefone e
CPF, digitar um nome novo cadastra ao salvar, e o campo diz qual dos dois está acontecendo.

**Por que é essa:** obrigar a abrir outra tela no meio de uma negociação é o que faz a pessoa
digitar qualquer coisa para seguir em frente.

### Documento igual é recusa; telefone igual é aviso

A segunda tentativa com o mesmo telefone confirma e passa.

**Por que é essa:** o documento **é** a pessoa — dois cadastros com o mesmo CPF são um erro,
sempre. O telefone é da casa: marido e mulher compram no mesmo número, e recusar por ele
impediria um cadastro legítimo.

### A ficha é a história

Propostas e compras com o carro de cada uma, o total comprado, o WhatsApp no número — e **Mandar
ficha**, que escolhe um carro do pátio e abre o WhatsApp do cliente com a ficha em PDF, pelo
caminho do M20.

**Por que é essa:** é o carro novo chegando a quem já comprou. Sem isso o cadastro seria uma
lista de nomes, e não uma ferramenta de venda.

### O documento vai ao papel, e à linha de assinatura

A proposta em PDF imprime o CPF ou CNPJ embaixo do nome, revenda e cliente.

**Por que é essa:** era o que faltava para o papel valer como aceite.

## Como usar

*Operação → Clientes* lista, cadastra e abre a ficha. Na proposta e na venda, o seletor de
cliente. Na ficha, *Mandar ficha* para oferecer um carro do pátio.

## O que mudou por baixo

- A entidade `Customer` — nome, documento conferido de verdade, telefone, e-mail, endereço e
  anotação —, o `IdCustomer` na proposta e na venda, e o aproveitamento na subida.
- A venda que fecha uma proposta é do cliente dela, e completa o CPF que a proposta deixou em
  branco.
- O guarda de colunas do M18 passou a cobrir `Proposal`, `Sale` e `Customer`.

## O que foi conferido

No MariaDB real: duas propostas com o mesmo telefone e uma venda com CPF viram **um** cliente, e
rodar o aproveitamento de novo muda nada; a outra revenda enxerga nada; a planilha sai. No banco
local, 9 vendas e 11 propostas viraram **18 clientes**. Suíte: **708 verdes**.

## O que ficou de fora, e por quê

- **Funil de vendas e etapas**, **envios em massa**, **importar planilha** de clientes e
  **endereço estruturado** — nenhum deles foi pedido, e cada um é um marco.

## Pendente

A publicação no servidor da rede fica para a próxima sessão em rede.
