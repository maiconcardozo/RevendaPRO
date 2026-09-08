# ADR-0008: Envio pelo aparelho — o documento vai pelo WhatsApp de quem vende

Data: 2026-09-08
Estado: aceito
Relacionado: ADR-0007 (documentos gerados), `docs/plans/m20-whatsapp-no-celular.md`

## Contexto

O M19 deixou a proposta e a ficha do carro em PDF, e um atalho que abria o WhatsApp com a
mensagem pronta. O PDF ficava por conta da pessoa: baixar, abrir o WhatsApp, achar o arquivo,
anexar. No computador é arrastar; no celular, onde a proposta acontece de verdade, são quatro
passos e um arquivo perdido na pasta de downloads.

Havia três caminhos para o PDF chegar ao cliente:

1. **A folha de compartilhamento do aparelho** (Web Share API): o navegador entrega o arquivo
   a outro aplicativo, e a pessoa escolhe o WhatsApp e o contato.
2. **Um link público** para o PDF, mandado no lugar do arquivo, com o sistema exposto na
   internet e um token com validade.
3. **A WhatsApp Business Platform**: o servidor manda a mensagem com o documento, sem celular
   na mão.

## Decisão

### 1. A folha do aparelho, e o `wa.me` como caminho de volta

`navigator.share({ files: [pdf], text })`, no `lib/share.ts` do frontend. O arquivo sai do
aparelho de quem vende direto para o WhatsApp: **o sistema jamais fala com a Meta**, guarda
número de ninguém além do que a proposta já tem, e o cliente recebe do número do vendedor que
ele conhece.

Onde o aparelho compartilha arquivo nenhum, o mesmo botão baixa o PDF e abre `wa.me` com a
conversa e a mensagem. A decisão é do código, por `navigator.canShare({ files })`; a pessoa vê
**um botão só**.

A mensagem vai na folha **e** na área de transferência: o WhatsApp do Android descarta o texto
quando há arquivo, e a tela avisa que a mensagem está copiada.

### 2. HTTPS é pré-requisito, e na rede local vem do próprio Caddy

A Web Share API só existe em página segura. O `Caddyfile.lan` passa a servir HTTPS com
`tls internal`: o Caddy emite o certificado para o IP e mantém uma raiz própria, instalada uma
vez em cada celular da loja. O cookie da sessão ganha `Secure`. Em produção, com domínio, o
Caddyfile de produção já faz Let's Encrypt e nada muda.

### 3. O que ficou de fora, e por quê

- **O link público** pede o sistema na internet, que o servidor da rede está longe de ser, e
  uma entidade nova com expiração. Volta com a produção em domínio, junto do "visualizada em".
- **A WhatsApp Business Platform** pede empresa verificada na Meta, número dedicado, modelos
  de mensagem aprovados, e cobra por conversa. Para dez propostas por semana, a folha resolve.
  O stakeholder quer olhar esse caminho depois; a porta continua aberta, porque o PDF já nasce
  na API e mandá-lo por outro canal é só outro consumidor do mesmo endpoint.

## Consequências

- O envio depende do navegador do aparelho. Chrome no Android e Safari no iPhone compartilham
  arquivo; o Firefox do Android e todo navegador em HTTP caem no caminho do download. O
  comportamento é o mesmo botão, com o aviso certo embaixo.
- Um celular sem a raiz do Caddy abre `https://192.168.1.24` com aviso de certificado. O roteiro
  em `docs/operations/celular-na-rede.md` é parte da entrega, e é o custo de ter HTTPS sem domínio.
- O sistema não sabe se a mensagem foi enviada. Depois que a folha abre, o resto é entre a
  pessoa e o WhatsApp. Registrar "proposta enviada" pediria o link público ou a API oficial.
