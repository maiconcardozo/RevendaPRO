# Plano — M21: Clientes — quem ofereceu, quem comprou, e quem volta

Fonte: a conversa com o stakeholder em 8 de setembro de 2026, ao fechar o M20.

> *"Não seria fornecedor, seria comprador. Vamos ver isso em outro momento."* (ao fechar o M18)
> *"Vamos fazer 1, 2, 3, 4, 5, mas na sequência. Vamos planejar o 1 agora."*

## O que a entrega precisa provar

> O Eduardo registra a proposta do Marcos pelo Argo. Ao digitar "Mar", o sistema mostra o
> Marcos que comprou uma Hilux em março, com o telefone; o Eduardo toca nele e o resto já está
> preenchido. A proposta em PDF sai com o **CPF** para assinar. Fecha a venda, e o Marcos
> aparece na tela **Clientes** com dois carros comprados, a proposta recusada de junho, e o
> botão do WhatsApp. Um ano depois chega um Corolla; o Eduardo abre o Marcos e manda a ficha
> direto de lá.

## O terreno

| Peça | Como está hoje |
|---|---|
| Quem ofereceu | `Proposal.ProspectName` e `ProspectPhone`: **texto solto**, digitado a cada proposta. O mesmo cliente que ofereceu por três carros existe três vezes, ou nenhuma |
| Quem comprou | `Sale.BuyerName`, `BuyerDocument`, `BuyerPhone`: texto solto de novo, digitado na hora da venda. O `SaleModal` copia nome e telefone da proposta, e o CPF é pedido ali |
| O cliente na tela | Aparece como nome na lista de vendas, no card da proposta e na tela Mercado. Nenhuma tela lista pessoas |
| Nos documentos | A proposta em PDF imprime nome e telefone; a linha de assinatura do cliente sai **sem documento** |
| O WhatsApp | O M20 manda a proposta para o telefone da proposta. A ficha vai sem número, porque quem perguntou do carro é ninguém no sistema |
| O modelo pronto | **Fornecedores** (M18): entidade por revenda, cadastro com mosaico e lista, ficha com extrato, planilha, e o pátio de demonstração povoado. É a forma que este marco copia |
| Validação | `isValidCpfOrCnpj` já existe no frontend; o `Tenant` e o `Supplier` guardam documento só em dígitos, com o tamanho conferido na entidade |

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | as decisões abaixo estão tomadas por escrito | — |
| **V1** | O cliente existe | Entidade `Customer` por revenda (nome, documento, telefone, e-mail, endereço, observações), repositório, consultas, migration com `IdCustomer` em `Proposal` e em `Sale`, e o **aproveitamento** dos clientes que já estão nas propostas e nas vendas, casados por documento, depois por telefone; o pátio de demonstração com clientes | sobe com o banco de hoje e cada proposta e venda antiga aponta para um cliente, sem digitar nada | — |
| **V2** | A API e as regras | Casos de uso de cliente (listar com busca, ficha com histórico, criar, editar, excluir), a proposta e a venda aceitando **um cliente existente ou um novo em linha** (nome e telefone viram cadastro na hora), a tela `customers` no catálogo, a planilha de clientes em Excel e CSV | um teste registra duas propostas com o mesmo telefone e o cliente é um só; outra revenda enxerga nada | V1 |
| **V3** | A tela Clientes | Lista em mosaico e lista com busca por nome, telefone ou documento; formulário; ficha com o histórico (propostas, compras) e o botão do WhatsApp; a proposta com o **seletor de cliente** que busca enquanto digita e oferece "cadastrar novo"; a venda já ligada ao cliente da proposta; a lista de vendas com o cliente clicável | o Eduardo digita "Mar", escolhe o Marcos, e a proposta sai com o telefone certo sem digitar | V2 |
| **V4** | O cliente no papel e no WhatsApp | A proposta em PDF com CPF ou CNPJ embaixo do nome e a linha de assinatura com o documento; **Mandar ficha** de dentro da ficha do cliente, escolhendo um carro do pátio, com o WhatsApp abrindo já no número dele | o PDF do Marcos sai com o CPF, e a ficha do Corolla vai para o número do Marcos num toque | V3 |
| **V5** | Fechamento | Suíte verde, `MARCOS.md`, `ROADMAP.md`, `endpoints.md`, `mappings.md`, o manual, publicação no servidor quando houver rede | `dotnet test`, `npm run build` e `docker compose up --build` passam | V1–V4 |

## Decisões (V0)

**1. A tela se chama Clientes, e a entidade, `Customer`.**

O stakeholder disse "comprador", e comprador é o papel de quem já fechou. A pessoa que
ofereceu e foi recusada, e que volta em seis meses, é cliente antes de ser comprador. Uma
tela chamada Compradores esconderia metade das pessoas que a loja conhece. Fica **Clientes**,
no grupo Operação, ao lado de Vendas: é o vendedor que a usa, e não o administrador.

**2. A proposta e a venda apontam para o cliente, e a venda guarda a cópia.**

`Proposal.IdCustomer` e `Sale.IdCustomer`, obrigatórios para o que nasce daqui em diante. Os
campos de texto da venda (`BuyerName`, `BuyerDocument`, `BuyerPhone`) **ficam**, como a cópia
do que estava no papel no dia: um contrato diz quem assinou, e a pessoa pode trocar de telefone
ou corrigir o nome depois sem reescrever a história. Os da proposta (`ProspectName`,
`ProspectPhone`) também ficam, pelo mesmo motivo, e a tela passa a ler do cliente.

**3. Ninguém redigita o que já está no banco.**

Na primeira subida, o sistema cria os clientes a partir das vendas e das propostas que já
existem, por revenda: casa por **documento** quando há, depois por **telefone**, e por último
por **nome igual** sem telefone. Quem caiu em nenhum caso vira um cliente próprio. É a mesma
decisão do M18, quando os gastos antigos receberam fornecedor sem apagar nada. A rotina é
idempotente: roda em quem está sem `IdCustomer`, e só.

**4. Cadastrar sem sair da proposta.**

O campo "Quem ofereceu" vira um seletor que **busca enquanto digita** por nome, telefone ou
documento, e oferece *Cadastrar "Marcos"* quando acha ninguém. Nome e telefone bastam para
nascer; documento e o resto vêm depois, na ficha, ou na hora da venda. Um cadastro na frente
da proposta é uma proposta a menos registrada.

**5. Duplicado se evita pelo telefone e pelo documento, e nunca pelo nome.**

Dois clientes com o mesmo documento é recusa: "Este CPF já é do Marcos Silva". Telefone igual é
**aviso** com a opção de usar o existente, porque um telefone da loja parceira pode aparecer em
mais de um comprador. Nome igual passa: há muitos Joões.

**6. A ficha do cliente é o histórico, e o histórico é leitura.**

Propostas (com carro, valor, situação), compras (com carro, valor, data) e o total comprado.
Nada de "lead", "funil" ou "etapa": o sistema registra o que aconteceu, e o vendedor sabe o que
fazer com isso. Editar o cliente muda os dados dele; o histórico muda por si, pelas propostas e
vendas que apontam para ele.

**7. Excluir é soft delete, e só sem história.**

Um cliente com proposta ou venda é recusado: "O Marcos tem 2 propostas e 1 compra". Sem
história, sai com `IsActive = 0`, como todo cadastro daqui.

**8. O cliente vai para o papel e para o WhatsApp.**

A proposta em PDF imprime o documento embaixo do nome e na linha de assinatura, que é o que
falta para o papel valer como aceite. Da ficha do cliente, **Mandar ficha** lista os carros do
pátio, a pessoa escolhe um, e o `shareDocument` do M20 abre o WhatsApp no número do cliente.
É o carro novo chegando a quem já comprou, sem procurar o contato.

## O que muda em cada camada

| Camada | Muda |
|---|---|
| **Domain** | `Customer` (TenantEntity): `Name`, `Document`, `Phone`, `Email`, `Address`, `Notes`; `Create`, `Rename`, `SetContact`; documento e telefone só em dígitos, tamanho conferido como no `Tenant`. `Proposal.IdCustomer` e `Sale.IdCustomer` com `AssignCustomer`. `ICustomerRepository` (busca, ficha, contagem de história, achar por documento e por telefone) |
| **Infrastructure** | `CustomerMap`, migration `Customers` (tabela, índices por revenda em documento e telefone, FK em proposta e venda), `Queries/Customers`, `CustomerRepository`, `DbInitializer.EnsureCustomersAsync` (o aproveitamento da decisão 3), `DemoYard` com clientes nas propostas e vendas |
| **Application** | `Customers/`: `ListCustomersQuery(search)`, `GetCustomerQuery` (com histórico), `Create/Update/DeleteCustomerCommand`, validadores. `RegisterProposalCommand` e `RegisterSaleCommand` ganham `CustomerCode?` e mantêm nome e telefone: com código, usa; sem código, acha por telefone ou cria. DTOs de proposta e venda com o cliente |
| **Api** | `CustomersController` (`api/customers`, busca, `{code}`, escrita, `{code}/history`), guardado pela tela `customers`; a busca também aberta a `vehicles`, porque o seletor da proposta é do vendedor. `ExportsController` com `customers`. `ProposalPdf` com o documento |
| **Frontend** | `app/(panel)/customers/page.tsx`, `components/customers/CustomersView.tsx` (ListBar, ViewSwitch, cards `h-full`), `CustomerForm`, `CustomerDetail` (histórico, WhatsApp, Mandar ficha), `CustomerPicker` (busca enquanto digita, "cadastrar novo"), `ProposalsPanel` e `SaleModal` usando o picker, `SalesView` com o link. Ícone `Contact` no `PanelShell` |
| **Testes** | Unidade: entidade (documento, telefone), o aproveitamento (casa por documento, por telefone, por nome), a recusa de duplicado, a proposta criando cliente em linha. Integração: duas propostas com o mesmo telefone geram um cliente; outra revenda enxerga nada; excluir com história é recusado; planilha. `SoftDeleteTests` e `ApiGuardTests` cobrem os novos automaticamente |
| **Docs** | `endpoints.md`, `mappings.md`, `MARCOS.md`, `ROADMAP.md`, manual (seção Clientes e a proposta com o seletor) |

## O que fica de fora deste marco

- **Funil de vendas**, etapas e lembretes de retorno: é outro produto em cima deste cadastro.
- **Aniversário, mensagens em massa** e qualquer envio automático: pede a WhatsApp Business
  Platform, que o stakeholder quer avaliar antes.
- **Cliente do carro de troca** como vendedor para a loja: quem entrega o carro usado na troca
  é o mesmo cliente; registrar de quem a loja compra fica com o "fornecedor da compra do carro",
  adiado desde o M18.
- **Importar planilha de clientes**: a loja que tem uma lista traz para o V1 do próximo marco,
  se pedir.
- **Endereço estruturado** (CEP, cidade, UF): uma linha basta para o papel; estruturar é para
  quando houver uso, como filtrar por cidade.
