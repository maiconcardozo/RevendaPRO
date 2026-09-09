# M20 — A proposta pelo WhatsApp, do computador e do celular

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md`, do plano
`docs/plans/m20-whatsapp-no-celular.md` e da ADR-0008.

## O que este marco resolve

> *"Muito interessante essa funcionalidade do WhatsApp, lembrando que também precisa que no
> celular também vá."*

O M19 deixou a proposta em PDF e o atalho do WhatsApp — no computador. No celular, que é onde o
vendedor está, o caminho não existia.

## As regras

### Um botão só, que decide pelo aparelho

*Mandar pelo WhatsApp*, na proposta e na ficha do carro. No celular, a Web Share API abre a folha
de compartilhamento com o **PDF já anexado**; no computador, o PDF cai na pasta de downloads e o
`wa.me` abre a conversa com a mensagem pronta.

**Por que é essa:** dois botões obrigariam a pessoa a saber em que aparelho ela está, e a acertar
— num sistema que já sabe.

### A mensagem também vai para a área de transferência

**Por que é essa:** o WhatsApp do Android **descarta o texto** quando há arquivo. Sem a cópia, o
cliente receberia um PDF sem uma palavra junto.

### O sistema jamais fala com a Meta

O arquivo sai do aparelho de quem vende, do número que o cliente já conhece.

**Por que é essa:** é o que dispensa cadastro, aprovação de modelo de mensagem e custo por
conversa — e o cliente recebe do número do vendedor, e não de um remetente comercial que ele não
reconhece.

### HTTPS na rede, sem domínio

O Caddy da LAN emite o certificado para o IP com a raiz interna dele, entregue em
`http://<IP>/raiz-revendapro.crt` — o único endereço em HTTP —, e cada celular a instala uma vez.

**Por que é essa:** a Web Share API **só existe em página segura**, e o servidor da rede atendia
em HTTP. Sem domínio próprio não há certificado público, então a raiz interna é o caminho — e ela
precisa ser baixável por HTTP, senão ninguém consegue instalar o que falta para o HTTPS
funcionar.

### As fotos saem pela porta 9100 do mesmo Caddy

**Por que é essa:** uma página segura recusa foto em HTTP. Com o mesmo Caddy na frente, o cookie
de sessão voltou a nascer `Secure`, como deve.

### Sem *service worker*

Manifesto, ícones em 192 e 512, o ícone do iPhone e a barra do sistema na cor do tema — e cache
offline nenhum.

**Por que é essa:** uma revenda que vende pelo preço da tela precisa da **tela viva**. Um preço
guardado em cache é uma proposta feita com o número de ontem.

## Como usar

Na proposta ou na ficha do carro, *Mandar pelo WhatsApp*. No celular, instale a raiz uma vez
seguindo `docs/operations/celular-na-rede.md`, e adicione o sistema à tela inicial.

## O que mudou por baixo

- `frontend/lib/share.ts` e a divisão do download em buscar, salvar e baixar.
- O `Caddyfile` da LAN, com a raiz interna e a porta das fotos.
- O manifesto e os ícones — e o guarda de sessão deixando passar manifesto e ícones, senão o
  celular salvava um atalho de navegador em vez do aplicativo.
- O caminho no celular, ajustado: as abas rolam de lado, os botões viram uma grade de dois por
  linha com *Mandar pelo WhatsApp* na linha inteira, e no card da proposta *Aceitar e vender*
  fecha embaixo na largura toda.

## O que foi conferido

Num Pixel e num iPhone emulados, com capturas em `docs/screens/celular/`. Dois detalhes que só o
teste mostrou: o redirecionamento automático do Caddy passava na frente da rota da raiz, e quem
abre um IP manda SNI nenhum — enquanto dentro do Docker o endereço local é o do contêiner;
`default_sni` resolve. A API não mudou uma linha: a suíte continua a mesma.

## O que ficou de fora, e por quê

- A **WhatsApp Business Platform**, que o stakeholder quer avaliar depois.
- O **link público da proposta** com "visualizada em", que pede o sistema na internet.

## Pendente

A publicação no servidor da rede, junto da instalação da raiz nos celulares, fica para a próxima
sessão em rede.
