# M25 — O logotipo da revenda no papel

Entregue em 9 de setembro de 2026. Plano em `docs/plans/m25-logotipo.md`.

## O que este marco resolve

O quinto e último item da sequência combinada com o stakeholder em 8 de setembro. Desde o M19 a
ficha para venda e a proposta saem com o nome, o CNPJ, o telefone e o endereço da revenda em
cima. Faltava a marca — o que faz o papel parecer **da loja**, e não de um sistema.

> O Rodrigo abre **Dados da revenda**, escolhe o arquivo do logotipo, arrasta e aproxima até
> caber na moldura, e salva. A ficha para venda sai com o logotipo no alto, ao lado do nome. A
> proposta também. A revenda que ainda não subiu logotipo tira os dois papéis exatamente como
> antes.

## As regras

### PNG sem perda, com transparência, e um tamanho só

600 pixels no lado maior.

**Por que é essa:** o processador de fotos do M6 gera WebP a 80, e a 80 a compressão com perda é
invisível numa foto de carro — e **borra a borda de uma letra**. Um logotipo é traço e texto. E o
papel precisa da transparência, senão o logotipo chega com um retângulo branco em volta. Um
tamanho só porque ele ocupa dois centímetros no papel e um quadrado pequeno na tela: dois
centímetros a 300 dpi são 240 pixels, e 600 sobra. Três tamanhos existiriam para uma galeria de
vinte fotos, e aqui seriam três arquivos para manter em dia sem ninguém pedindo.

### O recorte é do navegador, com a proporção do papel

Uma moldura 2:1, a pessoa arrasta e aproxima, e o `canvas` recorta antes de subir.

**Por que é essa:** a moldura tem **a mesma proporção da caixa no timbre** — 40 por 20
milímetros —, então o que a pessoa vê enquanto ajusta é exatamente o que sai impresso. O
servidor recebe uma imagem que já está certa, em vez de ter de adivinhar onde cortar uma imagem
que ele nunca viu inteira. Sem biblioteca: é uma escala e um deslocamento. O fundo fica
transparente, para o logotipo quadrado que sobra ar dos lados chegar ao papel sem retângulo.

### Uma porta própria, e não a da galeria

`ICompanyLogoStorage`, separada de `IImageProcessor`.

**Por que é essa:** a galeria tem regras certas para foto — três tamanhos, WebP, EXIF fora — e
erradas para logotipo. Forçar o logotipo por ela seria trocar as regras da galeria para servir a
um arquivo, ou aceitar um logotipo borrado. Duas portas, cada uma dizendo o que exige.

### Os bytes são servidos pela API, e jamais por endereço assinado

**Por que é essa:** como a foto do usuário. Os dois consumidores são a tela e o PDF, e nenhum
ganha com um endereço que expira: o PDF é desenhado no servidor, que já tem os bytes na mão, e a
tela pede uma imagem pequena, uma vez.

### O nome do arquivo muda a cada troca

`logo-<código>.png`, e a tela pede a imagem com a versão na URL.

**Por que é essa:** um nome fixo faria o navegador continuar mostrando o logotipo antigo depois
da troca, e a pessoa acharia que o sistema ignorou o que ela fez. Com o nome mudando, o cache
pode ser longo sem nunca mentir.

### Ler pede sessão; trocar pede a tela

**Por que é essa:** o logotipo é a identidade da loja, e quem está dentro dela já a conhece.
Trocar e remover são decisões da revenda, e ficam atrás de **Dados da revenda** como o resto dos
dados dela. O endpoint de leitura entrou na lista declarada do `ApiGuardTests` com o motivo — a
mesma lista onde a foto do usuário está.

### Trocar apaga o anterior; e o novo existe antes de o antigo sumir

**Por que é essa:** identidade visual tem versão nenhuma a guardar — quem troca quer o novo. É a
regra da foto, do carro e da pessoa, e o contrário do documento do veículo, que fica para sempre
por requisito do negócio. A ordem — gravar o novo, apontar a linha, apagar o antigo — é o que
garante que uma falha no meio deixa a revenda com o logotipo que tinha, e jamais sem nenhum.

### A revenda sem logotipo sai como sempre saiu

**Por que é essa:** o timbre é um helper só para os dois papéis, e ele desenha a imagem **quando
existe uma**. Sem logotipo a caixa não existe, e o nome ocupa o lugar inteiro, como no M19. Um
marco de aparência jamais pode piorar o papel de quem escolheu não usar a novidade.

### O logotipo é da empresa, e a empresa é lida pelo tenant de quem pede

**Por que é essa:** como todo dado de `Tenant` desde o M0. A chave do objeto começa pelo
`IdTenant`, e o endpoint lê a empresa da sessão, e nunca de um parâmetro. A outra revenda recebe
404.

## Como usar

*Administração → Dados da revenda → Logotipo → Enviar logotipo*. Escolha o arquivo, arraste a
imagem dentro da moldura, aproxime até caber, e **Usar este enquadramento**. A prévia mostra como
ele vai sair. **Trocar** repete o caminho; **Remover** devolve os papéis ao formato anterior.

## O que mudou por baixo

- `Tenant.Logo` e `ChangeLogo`; a migration sem `UPDATE`.
- `ICompanyLogoStorage` e `BucketCompanyLogoStorage`, com o PNG pelo SkiaSharp.
- `HasLogo` e `LogoVersion` no `CompanyDto`; os três endpoints em `api/company/logo`.
- `LogoCropper.tsx` e a seção Logotipo na tela **Dados da revenda**.
- `Letterhead` com a sobrecarga do logotipo; `Logo` nos dois DTOs de relatório.

## O que foi conferido

Com a API no ar: subir vira PNG com a assinatura certa; um JPG de 2400 pixels sai PNG de 600;
trocar muda a versão; remover devolve 404; texto com extensão de imagem é recusado **pelo
conteúdo**; e a outra revenda enxerga logotipo nenhum. Os dois PDFs com logotipo são maiores que
os sem. A ficha real, tirada da pilha local, traz duas imagens e uma `SMask` — a transparência do
PNG entrou no papel —, e lida de volta mostra o logotipo à esquerda do nome, no tamanho certo.

No navegador: o recorte arrastando e aproximando, a prévia, e a troca.

Suíte: **820 verdes**.

## O que ficou de fora, e por quê

- **O logotipo no menu lateral e na tela de entrada**: muda a identidade do sistema inteiro, e
  não só do papel. O pedido era o papel.
- **Cores da marca** no PDF: um logotipo é uma imagem; um tema é uma decisão de design.
- **Marca d'água** no fundo da página.
- **Logotipo por pátio**, para a loja parceira: o papel é da revenda que vende.
- **SVG**: resolveria a nitidez em qualquer tamanho, e traz junto a superfície de um formato que é
  código executável. Um PNG de 600 pixels imprime bem em dois centímetros.

## Pendente

A publicação no servidor da rede continua aberta, junto com a de M19 a M24.
