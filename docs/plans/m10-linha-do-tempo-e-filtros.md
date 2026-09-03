# Plano — M10: Linha do tempo e filtros

Fontes: `docs/ROADMAP.md` (M10, RF-25 e RF-26), o que ficou anotado ao fechar o M6 (documento
excluído continua no bucket, por decisão) e o M8 (cada assunto do veículo vive na sua aba).

O sistema já registra tudo o que acontece com um carro. O que falta é **ler isso em ordem**:
hoje a compra está no cabeçalho, o gasto na aba de custos, a foto na de fotos, a proposta na de
propostas e a mudança de status numa aba chamada Histórico que só mostra status. Quem pergunta
"o que aconteceu com esse Cruze?" precisa abrir cinco abas e juntar as datas de cabeça.

## O que a entrega precisa provar

> Numa tela só, em ordem: o Cruze foi comprado em 15/08 por 32 mil, foi para a oficina em 17/08,
> levou R$ 350 de funilaria em 19/08, ganhou 12 fotos em 22/08, recebeu uma proposta de 53 mil
> em 28/08 que foi recusada, e foi vendido em 30/08 por 55 mil. E o documento que alguém apagou
> por engano na terça volta pela tela de administração, porque ele nunca saiu do bucket.

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | **concluído** — quatro decisões escritas antes de qualquer código | — |
| **V1** | Linha do tempo na API | Uma consulta que reúne compra, gastos, anexos, propostas, mudanças de status e venda de um veículo, em ordem cronológica; `GET /api/vehicles/{code}/timeline` | **concluído** — o Cruze devolve 34 eventos em ordem, numa ida ao banco, cada um com o nome de quem o fez | — |
| **V2** | Linha do tempo na tela | A aba Histórico passa a mostrar a operação inteira, agrupada por dia, com filtro por tipo de evento | **concluído** — a história do Cruze aparece inteira, com filtro por assunto, no computador e no celular; o endpoint `/history`, órfão, saiu junto | V1 |
| **V3** | Filtro por período | `from`/`to` sobre a data de compra na listagem de veículos, API e tela, no mesmo vocabulário da tela de Vendas | **concluído** — julho traz o Cruze, agosto traz nenhum, setembro traz o Argo; o filtro vai ao banco, e tem teste guardando | — |
| **V4** | Documentos excluídos | Tela administrativa que lista o documento excluído, permite baixar e devolver à ficha do veículo | **concluído** — a tela desenterrou 13 arquivos que estavam pagos e inalcançáveis no bucket; devolver traz de volta à ficha, devolver de novo responde 422, e a devolução fica na auditoria | — |
| **V5** | Fechamento | Suíte verde, `docs/api/endpoints.md`, `ROADMAP.md` e o catálogo de telas atualizados | `dotnet test`, `npm run build` e `docker compose up --build` passam | V1–V4 |

## Decisões (V0)

**1. A linha do tempo lê as tabelas do domínio, e jamais a auditoria.**

Existe uma tentação óbvia: `AuditLog` já guarda quem fez o quê, quando, com o antes e o depois
em JSON. Mas ela existe para perícia — responder "quem mexeu nisso" —, e o que a ficha precisa
é significado: *"funilaria, R$ 350, pago por Fulano"*, e não `{"Amount":350.00,...}`. Ler a
auditoria obrigaria a desserializar um JSON diferente por entidade e ainda traria ruído que o
operador ignora (uma correção de observação, um campo mexido duas vezes).

Então a linha do tempo é uma consulta `UNION ALL` sobre as tabelas que já existem — `Vehicle`
(a compra), `VehicleExpense`, `VehiclePhoto`, `VehicleDocument`, `Proposal`,
`VehicleStatusHistory` e `Sale` — projetando todas para a mesma forma: momento, tipo, título,
detalhe, valor opcional e quem fez. Uma ida ao banco, ordenada pelo banco. A auditoria continua
onde está, para o que ela serve.

**2. Vinte fotos de uma vez são um evento, e não vinte.**

Subir as fotos de um carro é um ato só, feito num minuto. Vinte linhas iguais afogariam a
história. Fotos e documentos enviados no mesmo dia entram agrupados — *"12 fotos enviadas"* —,
com o detalhe de quantos. Gasto, proposta, status e venda entram um a um: cada um é uma decisão
diferente, tomada numa hora diferente.

Sem paginação: mesmo um carro trabalhoso fecha em algumas dezenas de eventos depois do
agrupamento, e cortar a história em páginas é justamente perder o que este marco entrega.

**3. O período filtra pela data de compra.**

A pergunta que a listagem de veículos responde é "o que entrou no pátio nesse intervalo" — o
eixo é a compra. Quem quer "o que vendi em agosto" já tem a tela de Vendas, que filtra pela data
da venda desde o M8. Duas datas na mesma tela, com um seletor de qual delas vale, seria uma
opção a mais para o usuário decidir toda vez, para responder a mesma coisa que duas telas já
respondem. O `from`/`to` e o padrão de início do mês copiam a tela de Vendas de propósito: o
mesmo gesto nas duas listagens.

**4. Documento excluído volta; ele nunca é apagado de verdade.**

Desde o M6 o `DELETE` de um documento tira ele da ficha e **mantém o arquivo no bucket** — foi
requisito explícito. Isso deixou um arquivo que ninguém alcança: está lá, pago, invisível. A
tela administrativa fecha esse buraco, e ela só devolve — jamais oferece apagar de vez. Um
botão de exclusão definitiva contrariaria o requisito de guardar documento para sempre, e a
recuperação administrativa da RNF-08.

A tela entra como screen nova (`deleted-documents`, grupo Administração). Pela ADR-0002,
permissão é tela: declarar a linha no `ScreenCatalog` faz o `ScreenSynchronizer` criar a
permissão e concedê-la ao Administrador, sem migration e sem SQL na mão. Os outros perfis de
sistema ficam de fora por padrão.

## O que fica de fora deste marco

- **Recuperação administrativa de veículo e de gasto excluídos.** A RNF-08 vale para tudo, mas o
  roteiro pede a rotina do documento, que é a que tem arquivo pago parado no bucket. As outras
  entram quando alguém precisar.
- **Linha do tempo da revenda inteira** (todos os carros num fluxo só). A pergunta real é sempre
  sobre um carro.
- **Exportar a história em PDF.** Ninguém pediu.
- **M11 — FIPE**, que segue com marco próprio.
