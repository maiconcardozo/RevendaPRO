# Plano — M19: Relatórios — a ficha do carro, a proposta para o cliente, e as planilhas

Fonte: a conversa com o stakeholder em 8 de setembro de 2026, ao fechar os padrões de lista.

> *"Preciso que você planeje algo para relatórios. Preciso tirar um resumo do carro para venda
> em forma de relatório PDF. Também preciso tirar algo que eu possa imprimir ou mandar uma
> proposta de venda para um cliente. Ele precisa ser CSV ou Excel. No PortalCliente.Global tem
> as bibliotecas de relatório que já funcionam, então veja todo o sistema."*

## O que a entrega precisa provar

> Na ficha do Civic, um botão gera a **ficha para venda** em PDF — foto, dados, preço, tabela —,
> pronta para imprimir ou mandar pelo WhatsApp. Na proposta que o Eduardo fez, outro botão gera a
> **proposta em PDF**, com o nome da revenda em cima e o valor combinado, para ele assinar ou
> guardar. E nas listagens — veículos, gastos, vendas, fornecedores — um botão baixa **a planilha
> em Excel ou CSV** do que está na tela, com os mesmos filtros.

## O terreno

| Peça | Como está hoje |
|---|---|
| Geração de PDF ou planilha | **Não existe.** Nada no sistema sai da tela |
| A referência | `PortalCliente.Global` gera PDF com **QuestPDF** (licença Community) e Excel com **ClosedXML**, um construtor estático por documento na camada da API, com `ContentType` e `Gerar(...)` em cada um. CSV **não existe** lá |
| A revenda no papel | O `Tenant` tem só o **nome**. Uma proposta para o cliente sem telefone e endereço da loja é um papel sem remetente |
| As fotos | Três versões em WebP no MinIO ou R2, lidas pela API por `IFileStorage.OpenReadAsync` |
| Download no frontend | O proxy `/api/backend` já repassa `Content-Disposition` e `Content-Type`; um link com o cookie da sessão baixa qualquer arquivo |
| Filtros das listagens | Veículos, gastos por fornecedor e vendas já têm período e filtros na API; a planilha é a mesma consulta, em outro formato |

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento e a ADR-0007 | as decisões abaixo estão tomadas por escrito | — |
| **V1** | A revenda no papel | `Tenant` ganha documento, telefone, e-mail e endereço; a tela **Dados da revenda** em Administração | o administrador preenche o CNPJ, o telefone e o endereço da loja, e eles aparecem em todo documento gerado | — |
| **V2** | A base dos documentos | QuestPDF e ClosedXML na API, o construtor genérico de tabela em PDF, Excel e CSV, o cabeçalho e rodapé padrão com a revenda e a numeração de página, e o helper de download no frontend | um teste gera cada formato e confere os bytes; o Docker gera PDF | V1 |
| **V3** | A ficha do carro para venda | `GET api/vehicles/{code}/reports/sale-sheet`: foto de capa e galeria, dados, preço anunciado, tabela de referência, e o contato da revenda. Botão na ficha do carro | o Civic sai em uma página, com foto, e a pessoa imprime ou manda | V2 |
| **V4** | A proposta para o cliente | `GET api/vehicles/{code}/proposals/{proposalCode}/document`: a proposta registrada vira o documento com a revenda em cima, o cliente, o carro, o valor, a forma de pagamento, a validade e as assinaturas. Botão na proposta, e o atalho do WhatsApp quando há telefone | a proposta do Eduardo sai em PDF com o valor e a data, pronta para assinar | V2 |
| **V5** | As planilhas | Exportação em **Excel e CSV** de veículos (com os filtros da tela), gastos com fornecedor (por período), vendas (por período) e gasto por fornecedor. Botões nas telas | baixar a planilha de veículos filtrada por pátio traz só aquele pátio, com os tipos certos nas células | V2 |
| **V6** | Fechamento | Suíte verde, `MARCOS.md`, `ROADMAP.md`, `endpoints.md`, `mappings.md` e o manual atualizados, publicado no servidor | `dotnet test`, `npm run build` e `docker compose up --build` passam, e o PDF sai do contêiner | V1–V5 |

## Decisões (V0)

**1. As mesmas bibliotecas do PortalCliente: QuestPDF para PDF, ClosedXML para Excel.**

Elas já funcionam num sistema da casa, com a licença resolvida: QuestPDF **Community** vale para
quem fatura menos de um milhão de dólares por ano, e é declarada na primeira linha do
`Program.cs`, como lá. ClosedXML é MIT e dispensa qualquer declaração. Nada de DevExpress, iText
ou navegador headless: cada um traz custo de licença, de imagem Docker ou de memória que este
sistema não tem por que pagar.

**2. CSV é escrito à mão, com ponto e vírgula e BOM.**

O PortalCliente não tem CSV, então aqui a decisão é nova. O Excel brasileiro abre um CSV com
vírgula como uma coluna só, porque a vírgula é o separador decimal; **ponto e vírgula** é o que
ele espera. O **BOM** UTF-8 na frente é o que faz o Excel ler os acentos. Uma biblioteca para
isso seria dependência para vinte linhas.

**3. O documento é montado na camada da API, e a aplicação entrega só dados.**

É como o PortalCliente faz, e é o que a arquitetura da casa permite: `Api → Application → Domain`.
O handler responde o DTO; a classe em `Api/Reports/` transforma o DTO em bytes. QuestPDF e
ClosedXML jamais entram em `Application` ou `Domain` — uma regra de negócio não sabe o que é uma
página A4. Fica registrado na **ADR-0007**.

**4. Um construtor estático por documento, e dois genéricos para tabela.**

`SaleSheetPdf`, `ProposalPdf` — cada documento oficial tem a sua classe, com `ContentType` e
`Render(...)`. Para as planilhas, dois genéricos resolvem tudo: `TableExcel.Render<T>(nome,
itens, colunas)` e `TableCsv.Render<T>(...)`, em que a coluna é `(Título, Func<T, object?>)`. O
valor vai tipado — `decimal`, `DateOnly` —, e a planilha guarda número e data de verdade, que é o
que permite somar e ordenar no Excel.

**5. A revenda ganha documento, telefone, e-mail e endereço — e uma tela.**

Um papel que sai da loja precisa dizer de quem é. O `Tenant` ganha os quatro campos, editados na
tela **Dados da revenda** (`company`), em Administração, só para o Administrador nascer com ela.
Logotipo fica de fora deste marco: o nome da revenda em destaque cumpre o papel, e upload de
imagem com recorte é um marco próprio.

**6. A ficha para venda mostra o que vende, e esconde o que é da casa.**

Foto de capa grande, até seis fotos menores, marca, modelo, versão, ano, quilometragem, cor,
combustível, câmbio, a **tabela de referência** (valor e mês, quando consultada) e o **preço
anunciado**. O que **jamais** sai: custo, compra, lucro, pátio, fornecedor. É um documento para o
comprador, e o que a revenda pagou no carro não é assunto dele. Sem preço anunciado, a ficha sai
com *"consulte"*: é melhor que inventar um número.

**7. A proposta para o cliente sai da proposta registrada.**

O sistema já registra a proposta — quem ofereceu, telefone, valor, forma de pagamento, canal. O
documento é ela em papel timbrado: a revenda em cima, o cliente, o carro (com a foto), o valor,
a forma de pagamento, a data, a **validade de sete dias**, as condições em texto livre (as
observações da proposta) e duas linhas para assinar. Nada de entidade nova: quem quer mandar
uma proposta registra a proposta, e imprime.

O **WhatsApp** entra como atalho: quando a proposta tem telefone, um botão abre o `wa.me` com a
mensagem pronta — carro, valor, validade. O PDF vai anexado pela pessoa, porque o WhatsApp não
aceita anexo por link; o atalho poupa a digitação, que é o que dá para poupar.

**8. As planilhas exportam o que a tela mostra, com os filtros da tela — e tudo, sem página.**

A planilha de veículos leva os mesmos parâmetros da listagem; a de gastos e a de vendas, o mesmo
período. Nada de paginação: planilha é para levar tudo, e as listagens deste sistema cabem em
memória com folga (a maior é o pátio inteiro, algumas dezenas de carros). A ordem das colunas
segue a ordem da tela.

**9. Página numerada e cultura fixa.**

O PortalCliente não numera páginas e mistura culturas; aqui o rodapé traz *"página 2 de 3"* e
toda formatação usa `pt-BR` explicitamente — servidor em contêiner Linux nasce com cultura
invariante, e um preço em `1,234.56` no papel da revenda é um erro que ninguém vê antes do
cliente.

## O que muda em cada camada

| Camada | Novo | Alterado |
|---|---|---|
| Domínio | — | `Tenant` ganha `Document`, `Phone`, `Email`, `Address`; `ITenantRepository` ganha `GetByIdAsync` |
| Aplicação | `Company/` (ler e salvar os dados da revenda), `Reports/` (as consultas que juntam o DTO de cada documento: ficha, proposta, planilhas) | — |
| Infra | migration `CompanyDetails`, consulta e repositório do tenant, linha no `ScreenCatalog` | `AccessMaps` |
| API | `Reports/` com `DocumentTheme`, `TablePdf`, `TableExcel`, `TableCsv`, `SaleSheetPdf`, `ProposalPdf`; `ReportsController` e as ações de exportação; licença no `Program.cs`; `libfontconfig1` no Dockerfile se o contêiner pedir | `CompanyController` |
| Frontend | `lib/download.ts`, tela **Dados da revenda**, botões nas telas | `VehicleDetail`, `ProposalsPanel`, `VehiclesView`, `SalesView`, `SuppliersView`, ícone no `PanelShell` |
| Testes | `Unit/ReportRenderingTests.cs` (cada formato gera bytes com a assinatura certa; a ficha esconde o custo), `Unit/CompanyTests.cs`; integração: os endpoints respondem o `Content-Type` certo e a outra revenda leva 404 | matriz de permissão por descoberta |
| Docs | este plano, ADR-0007 | `endpoints.md`, `mappings.md`, manual, `MARCOS.md`, `ROADMAP.md` |

## O que a implementação acrescentou ao plano

- **A licença do QuestPDF vive num inicializador de módulo**, e não só no `Program.cs`: o
  QuestPDF a confere antes de desenhar a primeira página, e um construtor estático só rodaria
  quando alguém tocasse na classe — o teste de renderização caiu exatamente nisso.
- **O horário de Brasília no rodapé.** O contêiner roda em UTC, e "gerado às 06:20" num papel
  impresso às três da tarde faz o cliente desconfiar do resto. `DocumentTheme.Now()` converte, e
  cai no relógio da máquina se o fuso faltar.
- **As fotos entram em WebP direto.** O plano previa converter; o QuestPDF decodifica WebP, e o
  teste prova com uma foto gerada na hora. As renderizações vêm no tamanho de card: grande o
  bastante para a página, leve o bastante para o PDF ficar em cento e poucos KB com foto.
- **A planilha de gastos ganhou a sua consulta** (`ListExpensesForExportQuery`): um SELECT com o
  carro de cada gasto, nomeando tipo e fornecedor na aplicação. Nada de listar carro por carro.
- **As fotos são reamostradas a 110 ppp e comprimidas em JPEG médio.** No padrão do QuestPDF a
  ficha do Renegade saía com 1,2 MB; com o ajuste, 95 KB, e a foto continua nítida na página. É o
  tamanho que o WhatsApp manda sem reclamar.
- **Um controller só para as planilhas** (`api/exports/*`), cada ação guardada pela tela da
  lista que ela exporta, e o formato decidido ali: `csv` sai CSV, o resto sai Excel — a pessoa
  pediu a planilha, e a planilha é o que ela leva.
- **O card da proposta ganhou dois botões**, e não um: o PDF e, quando há telefone, o WhatsApp
  com a mensagem pronta. O nome do carro chega ao card pela ficha, para a mensagem dizer qual é.
- **Sem publicação no servidor da rede neste fechamento**, a pedido do stakeholder, que estava
  fora da rede: o marco fecha na `main`, no GitHub e na pilha local. A publicação fica para a
  próxima vez que a máquina estiver ao alcance, pelo procedimento de sempre.

## O que fica de fora deste marco

- **Logotipo da revenda.** Upload, recorte e o lugar dele em cada documento é marco próprio. O
  nome em destaque cumpre o papel até lá.
- **Enviar o PDF por e-mail ou WhatsApp direto do sistema.** O PortalCliente tem SMTP genérico e
  nenhum uso para relatório; aqui o atalho do WhatsApp com a mensagem pronta é o que cabe sem
  credencial de e-mail configurada.
- **Relatório de fechamento do mês em PDF** (o painel inteiro em papel). As planilhas respondem
  a pergunta do contador; o painel em PDF é um documento a desenhar quando alguém pedir.
- **Modelo de proposta editável** (texto padrão de condições por revenda). As observações da
  proposta cobrem o caso; um modelo por revenda é cadastro novo.
