# O celular na rede da loja

Desde o M20 o servidor da rede local atende em **HTTPS**, com um certificado emitido pelo
próprio Caddy. É o que permite mandar a proposta pelo WhatsApp com o PDF já anexado: o
navegador só oferece a folha de compartilhamento em página segura (ADR-0008).

Como o certificado é da casa, cada celular precisa **instalar a raiz uma vez**. São dois
minutos por aparelho, e depois disso o endereço abre sem aviso, como qualquer site.

Nos exemplos abaixo, `192.168.1.7` é o IP da máquina do servidor e `3200` é a porta do site
(`LAN_PORT` do `.env`). Troque pelos da sua.

## 1. Baixar a raiz

No navegador do celular, abra:

```text
http://192.168.1.7/raiz-revendapro.crt
```

É o único endereço que o servidor entrega em HTTP. O arquivo `raiz-revendapro.crt` cai na
pasta de downloads (Android) ou vira um perfil para instalar (iPhone).

## 2. Instalar

### Android

1. **Configurações → Segurança e privacidade → Mais configurações de segurança → Criptografia
   e credenciais → Instalar um certificado → Certificado de CA.** Em alguns aparelhos o
   caminho é *Configurações → Segurança → Credenciais → Instalar do armazenamento*.
2. O Android avisa que o certificado permite a quem o emitiu ler o tráfego. Toque em
   **Instalar mesmo assim**: a raiz é a da sua loja, e só vale para o servidor de vocês.
3. Escolha o arquivo `raiz-revendapro.crt` na pasta de downloads.
4. Abra `https://192.168.1.7:3200` no **Chrome**. A página abre com o cadeado.

O Firefox do Android ignora a raiz instalada no aparelho; use o Chrome.

### iPhone

1. Ao baixar, o Safari pergunta *"Este site está tentando baixar um perfil de configuração"*.
   Toque em **Permitir**.
2. **Ajustes → Perfil baixado → Instalar**, e a senha do aparelho.
3. **Ajustes → Geral → Sobre → Ajustes de confiança de certificados** e ligue a chave da
   **Caddy Local Authority**. Sem este passo o Safari continua avisando.
4. Abra `https://192.168.1.7:3200` no **Safari**.

## 3. Pôr na tela inicial

Com o endereço aberto:

- **Android (Chrome)**: menu **⋮ → Adicionar à tela inicial** (ou **Instalar aplicativo**).
- **iPhone (Safari)**: botão de compartilhar **→ Adicionar à Tela de Início**.

O ícone da Revenda Pro aparece entre os aplicativos e abre sem a barra do navegador. Ele
continua precisando da rede da loja: sem Wi-Fi, a tela avisa que o servidor está fora.

## 4. Mandar uma proposta

Abra o carro, a aba **Propostas**, e toque em **Mandar pelo WhatsApp**. A folha do aparelho
abre com o PDF; toque no WhatsApp, escolha o contato e mande. A mensagem já está copiada:
cole no campo de texto se o WhatsApp deixar só o arquivo.

## Quando algo sai do lugar

| O que aparece | O que é |
|---|---|
| "Sua conexão não é particular" ou "Este site não é seguro" | a raiz ainda está fora deste aparelho: volte ao passo 2 |
| O botão baixa o PDF em vez de abrir a folha | a página abriu em `http://`, ou o navegador é o Firefox: abra em `https://` no Chrome ou no Safari |
| As fotos aparecem quebradas | o `LAN_HOST` do `.env` está diferente do IP que o celular usa: confira em `docs/operations/deploy.md` |
| A raiz mudou de nome ou o aviso voltou depois de meses | a pasta `revendapro_caddy_data` do Docker foi apagada e o Caddy criou outra raiz; instale a nova em cada aparelho |

## Para quem administra

A raiz vive no volume `revendapro_caddy_data`, em `/data/caddy/pki/authorities/local/root.crt`.
Ela dura dez anos; o certificado do IP, o Caddy renova sozinho. Para tirar uma cópia:

```powershell
docker cp revenda-pro-caddy:/data/caddy/pki/authorities/local/root.crt .\raiz-revendapro.crt
```

A porta do site é `LAN_PORT` no `.env` — 443 por padrão, e 3200 no servidor da loja, para
deixar a 443 livre. Ela entra no endereço, no redirecionamento vindo da porta 80 e no
CORS — o certificado não muda por causa dela.

Se o IP da máquina mudar, muda `LAN_HOST` no `.env` e sobe de novo. O Caddy emite o
certificado para o IP novo com a mesma raiz, e os celulares continuam confiando.
