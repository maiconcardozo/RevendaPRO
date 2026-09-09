# M19 — Relatórios: a ficha do carro, a proposta para o cliente, e as planilhas

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md`, do plano
`docs/plans/m19-relatorios.md` e da ADR-0007.

## O que este marco resolve

> *"Preciso tirar um resumo do carro para venda em forma de relatório PDF. Também preciso tirar
> algo que eu possa imprimir ou mandar uma proposta de venda para um cliente. Ele precisa ser CSV
> ou Excel. No PortalCliente.Global tem as bibliotecas de relatório que já funcionam."*

## As regras

### As mesmas bibliotecas da casa, e só na API

QuestPDF (licença Community) e ClosedXML, nas versões do PortalCliente.Global, referenciadas
apenas pela API: o handler entrega o DTO, a classe em `Api/Reports` o desenha. Um teste de
arquitetura garante que nenhuma das duas entra em `Application`, `Domain` ou `Infrastructure`.

**Por que é essa:** o desenho do papel é detalhe de apresentação. Uma referência a QuestPDF no
domínio faria a regra de negócio depender de como o PDF é montado — e trocar a biblioteca viraria
uma reescrita, em vez de uma classe nova.

### CSV escrito à mão, com ponto e vírgula e BOM

**Por que é essa:** é o que o Excel em português abre certo, no primeiro clique. Uma biblioteca
genérica devolveria vírgula e UTF-8 sem BOM — tecnicamente correto, e ilegível para quem vai
usar.

### Toda formatação passa por `pt-BR` explícito

**Por que é essa:** o contêiner sobe em cultura invariante. Sem dizer, o papel sairia com ponto
decimal e data ao contrário — errado justamente no documento que vai para o cliente.

### A revenda no papel

O `Tenant` ganhou CNPJ, telefone, e-mail e endereço, e a tela **Dados da revenda**.

**Por que é essa:** um papel que sai da loja precisa dizer de quem é. Sem isso, a proposta é uma
folha anônima com um número.

### A ficha para venda mostra o que vende, e esconde o que é da casa

Foto de capa e mais seis, dados, tabela FIPE e preço anunciado — ou *Consulte*. Custo, compra,
lucro, pátio e fornecedor ficam de fora, **por decisão do handler**.

**Por que é essa:** é um papel para o comprador. E a decisão é do handler, e não da tela: um
campo escondido só no desenho volta a aparecer no primeiro relatório novo que reaproveitar o DTO.

### A proposta sai da proposta registrada

Revenda, cliente, carro com a foto, valor, forma de pagamento, validade de sete dias, observações
como condições e duas linhas para assinar.

**Por que é essa:** o papel tem de dizer o mesmo que o sistema. Um formulário que se preenche à
parte cria uma segunda verdade, que ninguém consegue conciliar depois.

### As planilhas levam o que a tela mostra, com os filtros da tela

Veículos, gastos, vendas e fornecedores, em Excel ou CSV, com número e data guardados **como**
número e data.

**Por que é essa:** exportar o banco inteiro obrigaria a pessoa a refazer no Excel o filtro que
ela acabou de fazer na tela. E número guardado como texto é uma planilha que não soma.

## Como usar

Na ficha do carro, *Ficha para venda* (PDF). Na proposta, *Proposta em PDF* — com o WhatsApp como
atalho, a mensagem pronta e o PDF anexado pela pessoa. Em cada listagem, o botão de exportar.

## O que mudou por baixo

- `Api/Reports` com os desenhos; o teste de arquitetura que prende as bibliotecas ali.
- Os campos da revenda e a tela **Dados da revenda**.
- O rodapé numera as páginas e usa o horário de Brasília.

## O que foi conferido

Gerado de dentro do Docker, com foto, e renderizado — a ficha e a proposta. Cada formato foi
aberto de volta em teste: a assinatura `%PDF-`, a célula de decimal no Excel, o BOM e as aspas no
CSV.

## O que ficou de fora, e por quê

- **O envio pelo WhatsApp direto do celular**, que veio no M20 — aqui o atalho existia, mas só no
  computador.
- **A publicação no servidor da rede** ficou para uma sessão em rede.
