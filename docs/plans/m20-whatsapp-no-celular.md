# Plano — M20: A proposta pelo WhatsApp, do computador e do celular

Fonte: a conversa com o stakeholder em 8 de setembro de 2026, ao fechar o M19.

> *"Vamos fazer o plano para isso, muito interessante essa funcionalidade do WhatsApp,
> lembrando que também precisa que no celular também vá."*

## O que a entrega precisa provar

> O Eduardo fecha o valor com o cliente no pátio, pelo celular. Abre o carro, abre a proposta,
> toca em **Mandar pelo WhatsApp**. O WhatsApp abre com o **PDF já anexado** e a mensagem
> pronta; ele escolhe o contato e manda. No computador da loja, o mesmo botão abre o WhatsApp
> com a conversa do cliente e a mensagem, e o PDF cai na pasta de downloads para arrastar.
> A **ficha para venda** vai pelo mesmo caminho, para quem perguntou do carro. E o sistema fica
> na **tela inicial do celular**, com ícone, como um aplicativo.

## O terreno

| Peça | Como está hoje |
|---|---|
| O botão do WhatsApp | Abre `wa.me/55<telefone>?text=...` com a mensagem pronta. O PDF **a pessoa anexa à mão**, depois de baixar. No celular são quatro passos: baixar, abrir o WhatsApp, achar o arquivo, anexar |
| A ficha para venda | Só baixa. Sem atalho de WhatsApp |
| O download | `downloadFile` busca o arquivo por `fetch` e dispara um `<a download>`. No celular o arquivo some na pasta de downloads |
| O layout no celular | O painel já é responsivo: menu recolhe em hambúrguer abaixo de `lg`. O caminho **carro → proposta → botões** jamais foi conferido numa tela de 390 px |
| HTTPS na rede | O servidor da LAN atende em **HTTP puro** (`Caddyfile.lan`). O cookie da sessão sai sem `Secure` |
| Tela inicial | Sem `manifest`, sem ícone. O celular só salva um atalho de navegador |

## A restrição que decide o desenho

O navegador do celular sabe mandar um arquivo para outro aplicativo: é a **Web Share API**
(`navigator.share` com `files`). Ela abre a folha de compartilhamento do aparelho, a pessoa
toca no WhatsApp, escolhe o contato, e o PDF vai anexado. Funciona no Chrome do Android e no
Safari do iPhone, que são os aparelhos de uma revenda.

Ela tem uma condição: **só existe em página segura (HTTPS)**. Em `http://192.168.1.24` o
navegador esconde a função. Por isso o marco tem uma peça de infraestrutura no meio: o Caddy da
rede passa a servir HTTPS com o certificado interno dele, e cada celular instala a raiz uma vez.
Em produção, com domínio, isso vem de graça pelo Let's Encrypt.

No computador, o Chrome do Windows até abre a folha de compartilhamento do sistema, mas o
WhatsApp Desktop raramente está lá. O caminho do computador continua sendo o `wa.me` com a
conversa certa e o PDF baixado, só que os dois num toque só.

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento e a ADR-0008 | as decisões abaixo estão tomadas por escrito | — |
| **V1** | Mandar pelo aparelho | `lib/share.ts`: `shareDocument(caminho, nome, mensagem)` busca o PDF, e **onde o aparelho compartilha arquivo**, abre a folha com o PDF anexado; **onde compartilha nada**, baixa o PDF e abre o `wa.me` com a conversa e a mensagem. O botão **Mandar pelo WhatsApp** na proposta e na ficha do carro | no Android, tocar no botão abre a folha com o PDF; no Windows, baixa e abre a conversa do cliente | — |
| **V2** | HTTPS na rede | `Caddyfile.lan` com `tls internal`, `:80` redirecionando para `:443`, cookie `Secure` ligado na LAN, e o roteiro para instalar a raiz do Caddy no Android e no iPhone em `docs/ops/` | `https://192.168.1.24` abre sem aviso num celular com a raiz instalada, e o `navigator.share` aparece | — |
| **V3** | O caminho no celular | Carro, galeria, proposta e os botões conferidos a 390 px: botões em largura cheia, valores legíveis, nada cortado, telefone do cliente com teclado numérico. Capturas com o Playwright emulando iPhone e Pixel, guardadas em `docs/screens/celular/` | um vendedor faz a proposta e manda pelo WhatsApp sem girar a tela nem dar zoom | V1 |
| **V4** | Na tela inicial | `manifest.webmanifest`, ícones em 192 e 512, `apple-touch-icon`, `theme-color` acompanhando o tema. Sem *service worker*: o sistema segue precisando de rede | "Adicionar à tela inicial" põe o ícone da Revenda Pro e abre sem a barra do navegador | V2 |
| **V5** | Fechamento | Suíte verde, `MARCOS.md`, `ROADMAP.md`, o manual com o passo a passo no celular, publicado no servidor da rede com o HTTPS ligado | `dotnet test`, `npm run build` e `docker compose up --build` passam; o celular do stakeholder manda uma proposta | V1–V4 |

## Decisões (V0)

**1. O PDF vai anexado pela Web Share API, e o texto vai junto.**

`navigator.share({ files: [pdf], text: mensagem })`. É a API do navegador, sem biblioteca, sem
conta em serviço nenhum, e o arquivo sai do aparelho da pessoa direto para o WhatsApp: o
sistema jamais fala com a Meta. O WhatsApp do Android costuma **descartar o texto** quando há
arquivo; por isso a mensagem também vai para a **área de transferência**, e a tela avisa: *"A
mensagem foi copiada. Cole no WhatsApp, junto do PDF."* Um toque a mais, e nunca uma mensagem
perdida.

**2. Um botão só, que decide pelo aparelho.**

A pessoa vê **Mandar pelo WhatsApp**; a decisão entre folha de compartilhamento e `wa.me` é do
código, por `navigator.canShare({ files })`. Dois botões, "no celular" e "no computador",
seriam pedir para a pessoa saber o que o navegador dela faz. O botão **Proposta em PDF**
continua, para quem quer imprimir ou guardar.

**3. A ficha para venda também vai pelo WhatsApp, sem telefone.**

A ficha é o que se manda para quem *perguntou* do carro, e essa pessoa ainda é ninguém no
sistema. No celular, a folha de compartilhamento deixa escolher o contato. No computador, o
`wa.me` abre **sem número**, e o WhatsApp Web pede o contato. Cadastrar interessado para poder
mandar a ficha seria pôr um cadastro na frente da venda.

**4. HTTPS na rede é pré-requisito, e vem do próprio Caddy.**

`tls internal` faz o Caddy emitir certificado para o IP e manter uma raiz própria. Instalar
essa raiz num celular é um passo de dois minutos, feito uma vez por aparelho, e fica escrito em
`docs/ops/celular-na-rede.md`. Alternativas descartadas: **mkcert** (a mesma raiz, com uma
ferramenta a mais) e **um domínio público apontando para o IP da LAN** (funciona, mas prende a
loja a um DNS de fora para abrir um sistema de dentro). Em produção o Caddyfile de produção já
faz Let's Encrypt e nada muda.

**5. Nada de API oficial do WhatsApp neste marco.**

A **WhatsApp Business Platform** mandaria a proposta do servidor, sem celular na mão. Ela pede
empresa verificada na Meta, número dedicado, modelos de mensagem aprovados, e cobra por
conversa. Para uma revenda que manda dez propostas por semana, a folha de compartilhamento
resolve, e o número que o cliente vê é o do vendedor que ele já conhece. Fica registrado como
caminho futuro na ADR-0008.

**6. Sem link público da proposta, por enquanto.**

Um link `https://.../p/<token>` mandado no lugar do PDF permitiria saber quando o cliente abriu.
Pede o sistema exposto na internet, que o servidor da rede está longe de ser, e uma entidade
nova com expiração. Volta quando houver produção com domínio.

**7. Tela inicial sem *offline*.**

O `manifest` dá ícone, nome e tela cheia. Um *service worker* daria abrir sem rede, e com ele
viria o cache que mostra um preço velho. Uma revenda que vende pelo preço da tela precisa da
tela viva.

## O que muda em cada camada

| Camada | Muda |
|---|---|
| **Domain, Application, Infrastructure** | Nada. O marco é todo de frontend e operação |
| **Api** | Nada nos endpoints. Talvez `Cache-Control: private, no-store` explícito nos PDFs, para o celular jamais reaproveitar uma proposta velha |
| **Frontend** | `lib/share.ts` (compartilhar, com o download como caminho de volta); `ProposalsPanel` e `VehicleDetail` com **Mandar pelo WhatsApp**; ajustes de largura nas telas do caminho; `app/manifest.ts`, ícones em `public/`, `theme-color` no layout; `COOKIE_SECURE` respeitando a LAN |
| **Ops** | `Caddyfile.lan` com HTTPS interno; `docker-compose.lan.yml` expondo `443`; `docs/ops/celular-na-rede.md` |
| **Testes** | Frontend sem *runner* hoje: o compartilhamento é conferido em Playwright com `navigator.share` simulado (a folha do aparelho jamais abre em automação), e a jornada é fotografada em dois aparelhos emulados. A API muda quase nada; a suíte continua igual |
| **Docs** | ADR-0008, `MARCOS.md`, `ROADMAP.md`, manual (capítulo *No celular*) |

## O que fica de fora deste marco

- **A API oficial do WhatsApp** e o envio pelo servidor (decisão 5).
- **O link público da proposta**, com "visualizada em" (decisão 6).
- **Funcionar sem rede** (decisão 7).
- **Notificação no celular** quando uma proposta vence: pede *push*, que pede *service worker*.
- **O envio por e-mail** direto do sistema, que já ficou de fora do M19.
