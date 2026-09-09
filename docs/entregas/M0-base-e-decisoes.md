# M0 — A base, e as decisões que sustentam tudo

Marco de fundação. Documento de entrega escrito em 8 de setembro de 2026, a partir de
`docs/MARCOS.md` e das ADR em `docs/architecture/decisions/`.

## O que este marco resolve

Antes de qualquer funcionalidade, quatro decisões foram escritas como ADR. Elas explicam quase
todo o código que veio depois — e é por isso que este documento existe: quem for mexer no
sistema daqui a um ano vai esbarrar nelas, e merece saber o motivo em vez de descobrir a regra
pelo atrito.

## As regras

### Permissão é tela (ADR-0002)

Cada tela é uma permissão, e a chave da tela **é** a permissão. Declarar uma linha no
`ScreenCatalog` cria a permissão, concede ao Administrador e coloca o item no menu — sem
migration e sem SQL na mão. O menu de cada pessoa é exatamente o que ela pode abrir.

**Por que é essa:** a alternativa era chave de permissão em texto livre (`vehicle.edit`,
`vehicle.delete`), e ela envelhece em silêncio — a chave escrita errada não concede nada e
ninguém percebe até alguém reclamar. Amarrando a permissão à tela, o sistema tem uma lista
fechada, e a tela nova nasce protegida.

### O padrão Global (ADR-0003)

Código e comentário em **inglês**; texto de tela em **português**. Chave primária `Id` inteira,
com um `Code` (UUID v7) público. Entity Framework **só** para schema e mapeamento; leitura e
escrita com **Dapper**. Envelope `SuccessDetails<T>` em toda resposta.

**Por que é essa:** o `Id` inteiro é o que o banco usa bem em índice e junção; o `Code` é o que
sai para o mundo, porque um inteiro sequencial na URL conta quantos clientes a empresa tem. E o
Dapper existe porque o SQL desta casa é escrito à mão, de propósito: a consulta que soma o custo
de vinte carros é uma, e não vinte.

### Nenhum arquivo no banco, nenhum arquivo em disco (ADR-0004)

Tudo em bucket S3, com endereço assinado de vida curta. MinIO no desenvolvimento, Cloudflare R2
na produção — a diferença é configuração, e nada mais.

**Por que é essa:** arquivo em disco prende o sistema à máquina onde ele roda, e arquivo no
banco transforma todo backup num problema de tamanho. O endereço assinado é o que permite o
navegador buscar a foto direto do bucket, sem que ela passe pela API duas vezes.

### Toda leitura filtra a exclusão lógica, e um teste confere

O Entity Framework esconde a linha excluída sozinho; o Dapper, não. Por isso existe um teste que
**inspeciona cada SELECT escrito à mão** e exige o filtro. Hoje só quatro consultas leem linha
excluída de propósito, e cada uma tem o motivo escrito no próprio teste.

**Por que é essa:** a escolha do Dapper trouxe esse risco junto, e um risco conhecido sem guarda
vira defeito na primeira consulta escrita com pressa. O teste é a guarda, e a lista de exceções
é curta o bastante para ser lida.

## O que mudou por baixo

- As quatro ADR, em `docs/architecture/decisions/`. A **ADR-0001** ficou substituída pela
  ADR-0002.
- `ScreenCatalog` e o sincronizador de telas.
- O teste de exclusão lógica que percorre os objetos de consulta.

## O que foi conferido

A base subiu com `dotnet test`, `npm run build` e `docker compose up --build` passando — o
critério que todo marco deste projeto repete desde aqui.

## O que ficou de fora, e por quê

- **Chave de permissão granular** por ação: ver a primeira regra.
- **ORM para leitura**: o SQL à mão é a decisão, e o teste de exclusão lógica é o preço dela,
  pago à vista.
