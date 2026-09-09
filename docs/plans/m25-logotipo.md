# Plano — M25: O logotipo da revenda no papel

Fonte: a sequência combinada com o stakeholder em 8 de setembro de 2026 — o quinto e último item
da lista. É o que faltava para o papel que sai da loja parecer da loja.

## O que a entrega precisa provar

> O Rodrigo abre **Dados da revenda**, escolhe o arquivo do logotipo, ajusta o enquadramento
> arrastando a imagem dentro da moldura, e salva. Ele gera a **ficha para venda** de um carro: o
> logotipo está no alto, ao lado do nome da revenda, no tamanho certo e sem esticar. Gera a
> **proposta**: está lá também. Uma revenda que ainda não subiu logotipo continua tirando os dois
> papéis exatamente como antes.

## O terreno

| Peça | Como está hoje |
|---|---|
| O timbre | `DocumentTheme.Letterhead`, **um helper só**, usado pela ficha para venda e pela proposta (M19) |
| Os dados da revenda | `Tenant` com nome, CNPJ, telefone, e-mail e endereço, e a tela **Dados da revenda** (M19) |
| Arquivo de imagem | Vai para o bucket, jamais para o disco (M9). O processador do M6 recusa pelo conteúdo, apaga o EXIF e gera três tamanhos **em WebP a 80** |
| A foto do usuário | O precedente mais próximo: guardada no bucket, e **servida pela própria API**, porque ela aparece em toda página |

## As perguntas que decidem o desenho

**Em que formato o logotipo é guardado?**

Em **PNG, sem perda** — e jamais no WebP a 80 da galeria. O processador do M6 é feito para foto:
a 80 a compressão com perda é invisível numa foto de carro, e borra a borda de uma letra. Um
logotipo é traço e texto. E o papel timbrado precisa de **transparência**, para o logotipo não
chegar com um retângulo branco em volta.

**Quem serve os bytes?**

A **própria API**, como a foto do usuário. Ela aparece na tela de dados e dentro de um PDF que o
servidor desenha — em nenhum dos dois casos o navegador ganharia com um endereço assinado, e o
caminho fica um só.

**Onde o recorte acontece?**

No **navegador**, antes de subir. A moldura tem a proporção do espaço que ele vai ocupar no papel,
e a pessoa arrasta e aproxima até caber. Assim o que ela vê enquanto ajusta é exatamente o que
sai impresso — e o servidor recebe uma imagem que já está certa, em vez de adivinhar onde cortar.

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | as decisões abaixo estão tomadas por escrito | — |
| **V1** | O logotipo entra | `Tenant.Logo`, o processamento em PNG, os três endpoints, e a tela **Dados da revenda** com a moldura de recorte e a prévia | subir uma imagem e vê-la na tela, trocar e remover | — |
| **V2** | O logotipo no papel | O timbre desenhando o logotipo na ficha para venda e na proposta, com a revenda sem logotipo saindo igual a antes | os dois PDFs saem com o logotipo no alto, e sem ele quando não há | V1 |
| **V3** | Fechamento | Documento de entrega, `MARCOS.md`, `ROADMAP.md`, `endpoints.md`, o manual, e o merge na `main` por pull request | `dotnet test`, `npm run build` e `docker compose up --build` passam, e a skill `fechar-marco` foi seguida | V2 |

## Decisões (V0)

**1. PNG sem perda, com transparência, e um tamanho só.**

O logotipo entra e sai PNG. Não há três renditions: ele ocupa dois centímetros no papel e um
quadrado pequeno na tela, então um arquivo de até 600 pixels no lado maior serve os dois. Três
tamanhos existiriam para a galeria de vinte fotos, e aqui seriam três arquivos para manter em dia
sem ninguém pedindo.

**2. O recorte é do navegador, com a proporção do papel.**

A moldura tem a mesma proporção do espaço no timbre. A pessoa arrasta a imagem e usa o controle
de aproximação; o recorte é feito num `canvas` e o que sobe já é o resultado. **O que ela vê é o
que sai impresso** — e o servidor deixa de precisar adivinhar onde cortar uma imagem que ele
nunca viu inteira.

**3. O arquivo é servido pela API, e jamais por endereço assinado.**

Como a foto do usuário. A tela e o PDF são os dois consumidores, e nenhum dos dois ganha com um
endereço que expira: o PDF é desenhado no servidor, que já tem os bytes na mão, e a tela pede uma
imagem pequena, uma vez.

**4. O nome do arquivo muda a cada troca.**

`logo-<código>.png`. É o mesmo motivo da foto do usuário: um nome fixo faria o navegador continuar
mostrando o logotipo antigo depois da troca, e a pessoa acharia que o sistema ignorou o que ela
fez.

**5. Ler o logotipo pede sessão; trocar pede a tela.**

`GET` exige apenas estar dentro do sistema — o logotipo é a identidade da loja, e quem está dentro
dela já a conhece. `POST` e `DELETE` exigem a tela **Dados da revenda**, como o resto dos dados da
empresa.

**6. A revenda sem logotipo sai como sempre saiu.**

O timbre é o mesmo helper para os dois papéis, e ele desenha a imagem **quando existe uma**. Sem
logotipo, o nome da revenda ocupa o lugar inteiro, exatamente como no M19: um marco de aparência
jamais pode piorar o papel de quem escolheu não usar a novidade.

**7. O logotipo é da empresa, e a empresa é lida pelo tenant de quem pede.**

Como todo dado de `Tenant` desde o M0. A revenda A jamais alcança o arquivo da B — a chave do
objeto começa pelo `IdTenant`, e o endpoint lê a empresa da sessão, e nunca de um parâmetro.

**8. Remover o logotipo apaga o objeto.**

Diferente do documento do veículo, que fica no bucket para sempre por requisito do negócio. Um
logotipo é identidade visual: quem o troca quer o novo, e guardar o antigo seria pagar
armazenamento por uma versão que ninguém vai pedir. É a mesma regra da foto — do carro e da
pessoa.

## O que muda em cada camada

| Camada | Muda |
|---|---|
| **Domain** | `Tenant.Logo` e `ChangeLogo`; uma porta para processar o logotipo, separada da de foto |
| **Infrastructure** | Migration com a coluna; o processador PNG com SkiaSharp; a consulta de `Tenant` lendo a coluna nova |
| **Application** | `HasLogo` no DTO da empresa; os comandos de salvar e remover; os bytes chegando a quem desenha o papel |
| **Api** | Três endpoints em `CompanyController`; o timbre desenhando a imagem |
| **Frontend** | A moldura de recorte e a prévia na tela **Dados da revenda** |
| **Testes** | Unidade: o processador recusando o que não é imagem e devolvendo PNG; o timbre com e sem logotipo. Integração: subir, ler, trocar, remover — e a outra revenda enxergando nada |
| **Docs** | O documento de entrega, `MARCOS.md`, `ROADMAP.md`, `endpoints.md` e o manual |

## O que fica de fora deste marco

- **O logotipo no menu lateral e na tela de entrada.** Ele muda a identidade do sistema inteiro, e
  não só do papel — é outra conversa, e o pedido era o papel.
- **Cores da marca** no PDF: um logotipo é uma imagem; um tema é uma decisão de design.
- **Marca d'água** no fundo da página.
- **Logotipo por pátio**, para a loja parceira: o papel é da revenda que vende.
- **SVG.** Ele resolveria a nitidez em qualquer tamanho, e traz junto a superfície de um formato
  que é código executável. Um PNG de 600 pixels imprime bem em dois centímetros.
