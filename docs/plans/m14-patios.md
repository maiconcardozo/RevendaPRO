# Plano — M14: Pátios, e o relatório de cada lugar onde o carro está

Fontes: a conversa com o stakeholder em 4 de setembro de 2026, e o exemplo que ele deu:

> *"O Rodrigo tem o pátio particular dele, que anuncia, e ele deixa outros carros em outras
> revendas. Ele precisa tirar relatório de cada pátio ou revenda, e um todo junto, mas sempre
> agrupado."*

E, quando perguntei se pátio próprio e loja de terceiro eram cadastros diferentes:

> *"Tudo seria pátio. Ele vai cadastrar um pátio particular ou um pátio Loja do Joãozinho. São
> os mesmos carros com as mesmas configurações — pagar ou não comissão. É o mesmo registro, só
> discriminado por pátio."*

## O que a entrega precisa provar

> O Rodrigo cadastra o pátio dele e a Loja do Joãozinho. Cada carro fica **em um pátio**, e
> mudar de pátio deixa rastro na linha do tempo. O painel responde *"quanto tenho parado na
> Loja do Joãozinho"*, *"quanto no meu pátio"* e **tudo somado** — sem escolher entre uma
> coisa e outra.

## O terreno

| Peça | Como está hoje |
|---|---|
| Onde o carro está | **Não existe.** O veículo tem situação na esteira, e nada sobre lugar |
| Loja de terceiro | Existe como `PartnerStoreName`: **um texto digitado à mão em cada venda**, junto do repasse. Redigitado a cada negócio, e impossível de agrupar |
| Cadastro da revenda | `ExpenseType` é o molde pronto: entidade por empresa, tela própria, e a ADR-0002 fazendo tela virar permissão |
| Linha do tempo | Pronta desde o M10, e recebe um tipo de evento novo sem obra |
| Painel e listagem | Somam o pátio inteiro, sem nenhum agrupamento |

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Plano | Este documento | as cinco decisões abaixo estão tomadas por escrito | — |
| **V1** | O cadastro | Entidade `Yard`, migration, repositório, casos de uso, endpoints e a tela **Pátios** com a permissão própria | a revenda cadastra "Pátio Centro" e "Loja do Joãozinho", diz se cada um paga comissão e quanto, e o pátio em uso recusa exclusão | — |
| **V2** | O carro mora num pátio | Coluna no veículo, escolha no cadastro e na ficha, e a mudança de pátio virando evento na linha do tempo | o Cruze sai do Pátio Centro para a Loja do Joãozinho, e a linha do tempo conta isso com quem fez e quando | V1 |
| **V3** | O relatório por pátio | Filtro na listagem de veículos e um agrupamento no painel: carros, capital parado e tempo médio por pátio, **e o total** | o painel responde "quanto tenho parado na Loja do Joãozinho" sem deixar de responder "quanto tenho parado no total" | V2 |
| **V4** | O repasse vem do cadastro | A venda por loja parceira sugere o repasse combinado do pátio, em vez de a pessoa digitar toda vez | registrar a venda de um carro que está na Loja do Joãozinho já chega com o repasse dela preenchido | V2 |
| **V5** | Fechamento | Suíte verde, `MARCOS.md`, `ROADMAP.md`, `mappings.md` e o manual atualizados | `dotnet test`, `npm run build` e `docker compose up --build` passam | V1–V4 |

## Decisões (V0)

**1. Um cadastro só, com um tipo dentro.**

Pátio próprio e loja de terceiro **não** viram duas tabelas. É um cadastro com um campo dizendo
qual é qual — foi o que o stakeholder descreveu, e é o que mantém a soma possível: dois
cadastros exigiriam somar duas coisas diferentes em todo relatório, e alguém acabaria somando
só uma.

O tipo importa porque muda o comportamento: pátio próprio jamais paga comissão, e loja de
terceiro quase sempre paga.

**2. O carro está em um lugar por vez.**

Uma coluna no veículo, e não uma tabela de ligação. O carro não fica em dois pátios ao mesmo
tempo, e uma tabela de ligação abriria essa porta para um estado que a operação não tem.

**3. A mudança de pátio é evento, e não substituição silenciosa.**

Ela entra na linha do tempo do M10 como um tipo novo. É o que responde *"esse carro ficou dois
meses na Loja do Joãozinho e voltou sem vender"* — a informação que decide se vale deixar carro
lá de novo. Sem isso, o sistema esquece a passagem no instante em que o carro muda.

**4. O relatório agrupa, e jamais troca o total pelo pedaço.**

O painel ganha um bloco **por pátio**, e mantém os números do topo somando tudo. Trocar o total
por um filtro obrigaria a pessoa a escolher entre a parte e o todo — e a frase dele foi
explícita: *"de cada um e um todo junto"*.

**5. O repasse é sugerido, e continua sendo decidido por quem vende.**

O cadastro guarda o que foi combinado com o pátio, e a tela de venda **preenche** com isso. O
cálculo do negócio, que é do M8, não muda em nada: quem fecha a venda pode alterar o valor,
porque o combinado de hoje pode não ser o do próximo carro.

É o mesmo raciocínio da FIPE no M11: o sistema sugere pela presença, e quem decide dinheiro é a
pessoa.

## O que fica de fora deste marco

- **Acesso do parceiro ao próprio pátio.** O dono da Loja do Joãozinho entrar e ver só os carros
  que estão com ele é uma fronteira de segurança nova **dentro** da mesma empresa. Anotado no
  M12, e marco próprio.
- **Carro de terceiro.** O stakeholder foi claro: *"o carro ainda é dele, só está em pátio
  diferente"*. Custo e lucro seguem como estão.
- **Endereço e mapa do pátio.** Contato basta para o que a operação faz hoje.
- **Transferência em lote.** Mover dez carros de uma vez entra quando alguém precisar mover dez
  carros de uma vez.
