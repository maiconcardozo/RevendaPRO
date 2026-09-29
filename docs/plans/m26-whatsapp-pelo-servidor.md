# Plano — M26: A proposta pelo WhatsApp da revenda, num clique

Fonte: a conversa com o stakeholder em 28 de setembro de 2026, depois de ver o M20 no servidor.

> *"Hoje quando eu clico em enviar pro WhatsApp ele baixa o arquivo e abre o WhatsApp
> apontando pra quem eu quero mandar. Eu queria que já mandasse direto pelo WhatsApp o
> orçamento."*

## O que a entrega precisa provar

> O Eduardo fecha o valor com o cliente, no computador da loja ou no celular. Abre a proposta,
> toca em **Enviar pelo WhatsApp**, confere o número na confirmação e toca em **Enviar**. Em
> poucos segundos o cliente recebe, no WhatsApp, uma mensagem da revenda com o **PDF da
> proposta anexado**. Nada é baixado, nenhum aplicativo abre, e nada precisa ser arrastado. A
> proposta passa a mostrar **"Enviada pelo WhatsApp em 28/09, 14:32, por Eduardo"**. Quando o
> cliente responde, a resposta chega no WhatsApp da revenda, no celular de sempre.

## O terreno

| Peça | Como está hoje |
|---|---|
| O botão do WhatsApp (M20) | `lib/share.ts`: no celular, abre a tela de compartilhar do aparelho com o PDF anexado, e a pessoa escolhe o WhatsApp e o contato; no computador, baixa o PDF e abre o `wa.me` com a conversa e a mensagem. O arquivo sai sempre do aparelho da pessoa |
| O PDF | Gerado na API: `ProposalPdf.Render` e `SaleSheetPdf.Render`, entregues por `ReportsController`. Ninguém guarda o arquivo, que é gerado a cada pedido |
| O telefone do cliente | `Proposal.ProspectPhone`, texto livre, e o `Customer` do M21 ligado por `IdCustomer` |
| Saída para a internet | A API já chama a internet: a consulta da FIPE (`AddHttpClient`, respostas gravadas nos testes) |
| Entrada vinda da internet | Nenhuma. O servidor da rede (`192.168.1.7`) fica fora da internet, e a produção com domínio (M9) ainda depende de VPS |
| Registro de envio | Nenhum. A ADR-0008 diz: "o sistema não sabe se a mensagem foi enviada" |

## A restrição que decide o desenho

**Um navegador jamais anexa arquivo numa conversa do WhatsApp.** O `wa.me` só leva texto, e o
WhatsApp Desktop recebe nada de uma página. O limite do M20 é do navegador, e trocar de
navegador ou de biblioteca resolve nada.

O único caminho em que o sistema entrega o PDF sozinho é a **WhatsApp Business Platform
(Cloud API)** da Meta: a API da revenda envia o arquivo para a Meta, e a Meta o entrega ao
número do cliente. É o caminho que a ADR-0008 deixou aberto.

Ela traz três regras que moldam o marco inteiro:

1. **Quem inicia a conversa é a empresa, então a mensagem precisa de um modelo aprovado.** Fora
   da janela de 24 horas depois da última mensagem do cliente, a Meta só aceita *templates*
   aprovados de antemão. O PDF vai no **cabeçalho do modelo** (cabeçalho do tipo documento), e
   o texto tem campos: nome do cliente, carro, valor.
2. **O número que envia fica registrado na Meta.** Há dois jeitos:
   - **Coexistência**: o número do WhatsApp Business que a loja já usa é conectado à Cloud
     API e continua funcionando no aplicativo do celular. As respostas do cliente chegam no
     aplicativo como sempre;
   - **Número novo, só da API**: nenhum aplicativo lê esse número. Ler as respostas pediria um
     *webhook* e uma caixa de entrada dentro do sistema, e o *webhook* pede o sistema na
     internet.
3. **O resultado da entrega chega por webhook.** A Meta responde na hora que *aceitou* a
   mensagem, mas "entregue", "lida" e "falhou" (por exemplo, número sem WhatsApp) chegam depois,
   num endereço público. Na rede local, o sistema consegue saber **que enviou** e fica sem saber
   **se chegou**.

## Pré-requisitos do lado da revenda (antes do V1)

Estes passos dependem do stakeholder, e são eles que destravam o marco. O código dos V1 a V3
pode ser escrito e testado antes, contra respostas gravadas.

| # | Passo | Onde | Observação |
|---|---|---|---|
| P1 | Conta no **Meta Business** (Gerenciador de Negócios) com a empresa **verificada** | business.facebook.com | Pede CNPJ e um documento da empresa. A verificação leva de dias a semanas |
| P2 | **Conta do WhatsApp Business Platform** (WABA) ligada ao Business | developers.facebook.com, app do tipo *Business*, produto WhatsApp | A Meta dá um número de teste para desenvolver antes do número real |
| P3 | **O número**: conectar o WhatsApp Business atual por **coexistência** (recomendado) ou registrar um número novo | Configuração do WhatsApp no app | Conferir no dia se o número atual está apto à coexistência |
| P4 | **Forma de pagamento** na WABA | Gerenciador de Negócios, pagamentos | A Meta cobra por modelo entregue |
| P5 | **Usuário do sistema** com token permanente, com as permissões `whatsapp_business_messaging` e `whatsapp_business_management` | Gerenciador de Negócios, usuários do sistema | O token vai só no `.env` do servidor, jamais no Git |
| P6 | Submeter os **dois modelos** abaixo e esperar a aprovação | Gerenciador do WhatsApp, modelos de mensagem | Aprovação costuma sair em minutos ou horas |

### Os modelos a submeter

Idioma `pt_BR`, categoria **Utilidade**. A Meta pode reclassificar para Marketing, que custa
mais; o V0 registra a categoria que ela aprovou.

**`proposta_veiculo`**, cabeçalho **documento**:

> Olá, {{1}}! Segue a proposta do {{2}} que conversamos, no valor de {{3}}.
> O PDF com os detalhes está anexado. Qualquer dúvida, é só responder esta mensagem.
> {{4}}

Exemplo: `{{1}}` Carlos, `{{2}}` Onix LTZ 2021, placa ABC1D23, `{{3}}` R$ 72.900,00, `{{4}}` Revenda Piloto.

**`ficha_veiculo`**, cabeçalho **documento**:

> Olá, {{1}}! Segue a ficha do {{2}}, com fotos e dados do carro.
> Qualquer dúvida, é só responder esta mensagem.
> {{3}}

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano e decisões | Este documento e a **ADR-0009**, que complementa a ADR-0008. Os pré-requisitos P1 a P6 conferidos com o stakeholder | as decisões abaixo estão aceitas, e os modelos submetidos | — |
| **V1** | O cliente da Cloud API | `IWhatsAppClient` na Application, e na Infrastructure um `WhatsAppCloudClient` por `HttpClient` tipado: **enviar o arquivo** (`POST /{phone-number-id}/media`, devolve o id da mídia) e **enviar o modelo** com o documento no cabeçalho (`POST /{phone-number-id}/messages`, devolve o `wamid`). `WhatsAppSettings` com versão da Graph API, `PhoneNumberId`, token e nomes dos modelos. Os erros da Meta viram resultado tratado (token vencido, modelo pausado, número inválido, limite de envio) e jamais exceção solta | os testes passam contra respostas **gravadas** da Meta, sem tocar a rede, como a FIPE | — |
| **V2** | Registro do envio | Entidade `WhatsAppMessage` (tenant, tipo: proposta ou ficha, código do que foi enviado, telefone E.164, `wamid`, estado, erro, quem enviou, quando), migration, repositório com exclusão lógica. Telefone normalizado para `55` + DDD + número, com o número validado antes de gastar uma chamada | a proposta responde "enviada em … por …" pela API | V1 |
| **V3** | Os endpoints | `POST /api/proposals/{code}/whatsapp` e `POST /api/vehicles/{code}/sale-sheet/whatsapp`: montam o PDF com o mesmo `ProposalPdf.Render` e `SaleSheetPdf.Render`, enviam, registram, e devolvem o estado. Protegidos **pela mesma tela** que já protege a proposta e a ficha. Quando o WhatsApp está sem configuração, respondem que o envio pela revenda está desligado, e o frontend usa o caminho do M20 | a matriz perfil × endpoint cobre os dois; o isolamento entre empresas também; um envio repetido em sequência é recusado (clique duplo) | V2 |
| **V4** | A tela | **Enviar pelo WhatsApp** abre uma confirmação com nome, número e prévia do texto; **Enviar** chama a API e mostra "Proposta enviada para (47) 99999-0000". A proposta e a ficha mostram o último envio. O caminho do M20 continua como link secundário, **Enviar do meu WhatsApp**, e vira o botão principal quando o envio pela revenda está desligado. Textos pela skill `texto-afirmativo` | no computador, um clique entrega o PDF no número de teste da Meta | V3 |
| **V5** | Número real e servidor | `.env` do servidor com o token e o `PhoneNumberId` reais, publicado no `192.168.1.7`, e uma proposta de verdade mandada para o celular do stakeholder | o stakeholder recebe a proposta no próprio celular, vinda do número da revenda, e responde por ali | V4, P1–P6 |
| **V6** | Fechamento | Suíte verde, `MARCOS.md`, `ROADMAP.md`, manual (*Enviar pelo WhatsApp*), `docs/operations/whatsapp.md` com o passo a passo da Meta e a troca do token, documento de entrega, PR | `dotnet test`, `npm run build` e `docker compose up --build` passam; a skill `fechar-marco` cumprida | V5 |

**Fica para depois da produção com domínio (M9):** o *webhook* de estados, com "entregue",
"lida" e "falhou" na proposta. Descrito na decisão 5.

## Decisões (V0)

**1. Envio pelo servidor, com o M20 como caminho de volta.**

Com o WhatsApp configurado, o botão principal envia pela API. Sem configuração, ou com a Meta
recusando (token vencido, modelo pausado), a tela oferece o caminho do M20 na hora, com o aviso
do motivo. Uma revenda sem conta na Meta continua usando o sistema como hoje.

Isso contraria o "um botão só" do M20, e de propósito: os dois caminhos saem de **números
diferentes**. O da revenda é o principal; **Enviar do meu WhatsApp** fica como link para quem
quer mandar do próprio número.

**2. Configuração no `.env`, uma revenda por servidor, por enquanto.**

`WhatsApp__AccessToken`, `WhatsApp__PhoneNumberId`, `WhatsApp__GraphVersion`,
`WhatsApp__ProposalTemplate`, `WhatsApp__SaleSheetTemplate` e `WhatsApp__TemplateLanguage`,
lidos por `IOptions<WhatsAppSettings>`. O token é segredo e fica fora do banco. Quando houver
mais de uma revenda no mesmo servidor, a configuração passa para o tenant, com o token
criptografado; a interface `IWhatsAppClient` recebe a configuração por parâmetro para essa
troca custar só a origem do dado.

**3. O PDF sobe como mídia e fica fora de qualquer link público.**

A API envia o arquivo à Meta (`/media`) e usa o id no cabeçalho do modelo. A alternativa, um
`link` para o PDF, pediria o sistema na internet. A mídia enviada expira na Meta sozinha, e o
sistema guarda o arquivo em lugar nenhum: o PDF continua gerado a cada pedido.

**4. Consentimento: quem envia confirma que o cliente pediu.**

A política da Meta exige que o cliente tenha aceitado receber mensagens da empresa. Numa
revenda, a proposta nasce de uma conversa em que o cliente pediu o valor. A confirmação do
envio mostra o número e o nome, e o registro guarda quem enviou. Um campo "aceita WhatsApp" no
cliente fica para quando houver envio em massa, que está fora deste marco.

**5. Sem webhook na rede local.**

O estado que a tela mostra é **"Enviada"**, com o `wamid` guardado. "Entregue", "lida" e
"falhou" pedem um endpoint público assinado (`X-Hub-Signature-256`), que só faz sentido com o
sistema em domínio. A entidade do V2 já nasce com os estados todos, para o webhook só
preenchê-los. Até lá, um número sem WhatsApp aparece como "Enviada", e o manual diz isso.

**6. Coexistência como recomendação para o número.**

Com o número atual da loja em coexistência, as respostas chegam no aplicativo de sempre e o
cliente reconhece o número. Com um número novo só de API, as respostas ficam sem leitor até
existir o webhook e uma caixa de entrada. Se a coexistência for recusada para o número da
loja, o V5 para e a decisão volta ao stakeholder.

**7. Custo.**

A Meta cobra **por modelo entregue**. A cobrança por conversa acabou em julho de 2025. Em
utilidade, no Brasil, são centavos por mensagem; em marketing, algumas vezes mais. **Conferir a
tabela da Meta no dia do V0** e registrar o valor aqui. Com dez propostas por semana, o custo
mensal é baixo em qualquer das duas categorias.

## O que muda em cada camada

| Camada | Muda |
|---|---|
| **Domain** | `WhatsAppMessage` e o enum de estados; normalização e validação do telefone em E.164 |
| **Application** | `IWhatsAppClient`; comandos `SendProposalByWhatsApp` e `SendSaleSheetByWhatsApp`; consulta do último envio |
| **Infrastructure** | `WhatsAppCloudClient` por `HttpClient` tipado, com `WhatsAppSettings`; mapeamento e migration de `WhatsAppMessage` |
| **Api** | Os dois endpoints do V3; os geradores de PDF chamados de dentro do comando, sem passar por `ReportsController` |
| **Frontend** | Confirmação de envio; `ProposalsPanel` e `VehicleDetail` com o último envio; `lib/share.ts` rebaixado a caminho secundário |
| **Ops** | Variáveis `WhatsApp__*` no `docker-compose.lan.yml` e no `.env.example`; `docs/operations/whatsapp.md` |
| **Testes** | Cliente contra respostas gravadas da Meta (sucesso, token inválido, modelo pausado, número inválido, limite); integração dos endpoints com o cliente simulado; matriz perfil × endpoint; isolamento entre empresas; nenhum teste toca a rede |
| **Docs** | ADR-0009, `MARCOS.md`, `ROADMAP.md`, manual, documento de entrega |

## Riscos

- **Verificação da empresa na Meta** é o que mais pode atrasar, porque o prazo é da Meta. Por
  isso P1 começa junto do V0, e o código avança em paralelo com o número de teste.
- **Modelo reclassificado ou pausado.** A Meta pode mover o modelo para Marketing ou pausá-lo
  por qualidade (clientes bloqueando o número). O cliente trata os dois casos e a tela cai para
  o caminho do M20.
- **Coexistência indisponível** para o número da loja. Tratado na decisão 6.
- **Token vencido.** O token de usuário do sistema é permanente, mas pode ser revogado. O erro
  vira aviso claro para o administrador, e `docs/operations/whatsapp.md` ensina a trocar.

## O que fica de fora deste marco

- **Webhook** de entrega e leitura, e a **caixa de entrada** dentro do sistema: depois do M9.
- **Envio em massa** e campanhas (aviso de carro novo no pátio para a lista de clientes).
- **Lembrete automático** de proposta sem resposta.
- **Uma configuração de WhatsApp por revenda** no mesmo servidor (decisão 2).
- **O link público da proposta** com "visualizada em", que segue com a decisão 6 do M20.
