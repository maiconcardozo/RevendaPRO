# ADR-0007: Documentos gerados — PDF, Excel e CSV montados na camada da API

Data: 2026-09-08
Estado: aceito
Relacionado: ADR-0003 (camadas e dependências), `docs/plans/m19-relatorios.md`

## Contexto

O M19 põe o sistema a gerar papel: a ficha do carro para venda, a proposta para o cliente, e as
planilhas das listagens. Duas perguntas precisavam de resposta antes da primeira linha: **com
qual biblioteca**, e **em qual camada**.

A referência da casa é o `PortalCliente.Global`, levantado inteiro em 8 de setembro de 2026.
Ele gera PDF com **QuestPDF 2024.12.3** e Excel com **ClosedXML 0.104.2**, declarados só no
projeto Web; cada documento é uma classe estática com `ContentType` e `Gerar(...)`; os handlers
do MediatR respondem DTO, e o endpoint transforma o DTO em bytes com `Results.File`. O legado
que ele substituiu usava DevExpress e FreeSpire, e os dois foram descartados de propósito.

O PortalCliente não tem CSV, não numera páginas e mistura a cultura do servidor com `pt-BR`
explícito.

## Decisão

### 1. QuestPDF e ClosedXML, na versão que a referência usa

QuestPDF sob a licença **Community**, declarada como a primeira instrução do `Program.cs`:

```csharp
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
```

O `DocumentTheme` a declara de novo no construtor estático, para um teste ou uma ferramenta que
gere um documento sem subir a API funcionar do mesmo jeito.

A licença Community vale para organizações que faturam menos de um milhão de dólares por ano,
e este sistema está muito abaixo disso; a mesma justificativa está escrita na referência. Se um
dia a revenda passar desse teto, a licença Professional é uma linha e um pagamento, e nenhuma
mudança de código.

ClosedXML é MIT e dispensa declaração.

### 2. CSV escrito à mão

Ponto e vírgula como separador, porque o Excel em português usa a vírgula como decimal e abre
um CSV com vírgula como uma coluna só; BOM UTF-8 na frente, porque sem ele o Excel lê os acentos
errado; aspas dobradas onde o valor tem separador, aspas ou quebra de linha. São vinte linhas em
`TableCsv`, e uma dependência para isso seria peso sem retorno.

### 3. Os documentos vivem em `RevendaPro.Api/Reports/`

A camada da API pode olhar para `Application` e para `Domain`; `Application` jamais olha para
`Infrastructure` nem para bibliotecas de apresentação. Um PDF é apresentação: a mesma pergunta
que a tela responde em HTML, respondida em papel. Então:

- o handler em `Application/Reports/` responde o **DTO** do documento (a ficha, a proposta, as
  linhas da planilha), com toda a regra de negócio — o que entra, o que fica de fora, o preço
  que aparece;
- a classe em `Api/Reports/` transforma o DTO em bytes, e só isso;
- o controller devolve `File(bytes, contentType, fileName)`.

`QuestPDF` e `ClosedXML` são referências **apenas** do projeto `RevendaPro.Api`. O teste de
arquitetura garante.

### 4. Uma classe por documento oficial, e três genéricos para tabela

`SaleSheetPdf` e `ProposalPdf` são desenhados à mão. `TablePdf`, `TableExcel` e `TableCsv`
recebem `(Título, Func<T, object?>)` por coluna e resolvem toda planilha: o valor chega tipado,
e a célula do Excel guarda número e data de verdade.

`DocumentTheme` concentra o que todo PDF repete: a cultura `pt-BR`, o cabeçalho com a revenda
(nome, CNPJ, telefone, endereço), o rodapé com *"gerado pelo Revenda Pro em <data>"* e
*"página N de M"*, as fontes e os tamanhos.

### 5. Cultura explícita em toda formatação

O contêiner Linux sobe com cultura invariante. Toda data e todo valor passam por
`DocumentTheme.Culture` (`pt-BR`): `R$ 1.234,56`, `8 de setembro de 2026`. Formatar sem cultura
é o erro que a referência comete, e que aqui é proibido por convenção.

## Consequências

- **Positivas:** as bibliotecas já provaram funcionar num sistema da casa; a camada de
  aplicação continua testável sem gerar um byte de PDF; a planilha nasce com tipos certos.
- **Negativas:** QuestPDF traz binário nativo (Skia); a imagem Docker pode precisar de
  `libfontconfig1`, e o fechamento do marco confere isso gerando um PDF de dentro do contêiner.
- **Abertas:** logotipo da revenda no documento (marco próprio) e envio direto por e-mail (o
  PortalCliente tem SMTP genérico; aqui fica para quando houver credencial configurada).
