# Manual do Revenda Pro

Para quem vai usar o sistema no dia a dia. Sem jargão: cada tela explicada pelo que ela
responde, e o caminho completo de um carro, do leilão até a venda.

Atualizado em 8 de setembro de 2026.

> Versão em página, para ler e compartilhar: https://claude.ai/code/artifact/dbee6bf8-ae49-49be-8549-ff05c1f77f95

---

## 1. Entrar

Abra o endereço do sistema e informe **e-mail e senha**. A sessão fica no navegador; fechar a
aba mantém você conectado, e **Sair**, no rodapé do menu, encerra.

O menu à esquerda mostra **apenas as telas que o seu perfil alcança**. Se um colega vê um item
que você não vê, é o perfil que difere — e quem ajusta isso é o Administrador, em *Perfis*.

No topo da tela ficam o botão de **tema claro ou escuro** e o seu nome.

---

## 2. Painel

A primeira leitura do dia. Ele responde: quanto dinheiro está parado no pátio, quanto já
voltou, e quais carros merecem atenção.

| Indicador | O que ele diz |
|---|---|
| **No pátio** | Quantos carros ainda sem venda |
| **Capital parado** | Compra mais gastos dos carros sem venda — o dinheiro que está lá fora |
| **Lucro projetado** | O que sobra se cada carro sair pelo preço desejado |
| **Lucro realizado** | O que sobrou de verdade nas vendas do período |
| **Vendas** | Quantas vendas no período |
| **Dias para vender** | Média entre a compra e a venda |

O bloco **Por pátio** responde a mesma pergunta lugar por lugar: quantos carros, quanto de
capital parado e há quantos dias em média, com uma linha para cada pátio cadastrado e uma
**Sem pátio** para os carros que ainda não têm lugar. Os números do topo continuam somando
tudo — o painel responde *"quanto tenho parado na Loja do Joãozinho"* sem deixar de responder
*"quanto tenho parado no total"*.

O bloco **Fornecedores no período** responde *"com quem eu mais gastei"*: o pago, o previsto e
quantos fornecedores receberam; o **ranking** de quem mais recebeu; a **rosca por ramo**; e as
**colunas mês a mês**, com o previsto hachurado por cima do pago. Ele segue os mesmos **De** e
**Até** das vendas. O painel completo, com o gasto por tipo e a ficha de cada um, fica na tela
**Fornecedores**.

Embaixo, três listas curtas: **Mais dinheiro parado**, **Maior sobra prometida** e **Mais tempo
parado**.

Os campos **De** e **Até** limitam **só o que é realizado** — vendas, lucro realizado, dias
médios. O pátio é sempre o de agora, porque um carro comprado no ano passado continua segurando
dinheiro hoje.

---

## 3. Veículos

A tela do estoque, com **duas formas de olhar** — o botão fica acima da lista, à direita, do
lado da contagem de carros:

- **Mosaico** — cada carro é um cartão com a foto grande, a situação, o custo real e, quando há
  teto de orçamento, a barra do quanto ainda cabe. É a forma de **conhecer o pátio**: a foto
  grande é o que faz reconhecer o carro de relance.
- **Lista** — cada carro é uma linha: miniatura, placa, nome, ano, quilometragem, cor, tempo
  parado e, encostados na direita, o **custo** e o **quero receber**. É a forma de **procurar um
  carro entre muitos**: os valores caem na mesma margem em toda linha, e comparar vinte carros
  vira descer o olho.

A escolha **fica guardada** neste navegador: da próxima vez a tela abre do jeito que você
deixou. Ela vale só para você — a forma que o vendedor escolhe jamais muda a tela de outra
pessoa.

As mesmas duas formas existem em **todos os cadastros** — Usuários, Perfis, Tipos de gasto,
Pátios e Fornecedores —, com o mesmo botão no mesmo lugar, e a escolha guardada tela por tela.

Na lista, a barra de teto do cartão dá lugar a um **selo**, e ele só aparece quando há o que
avisar: *Passou do teto* ou *O previsto estoura*. O selo de *FIPE atrasada* continua nas duas
formas.

**Filtros**, todos combináveis, e valem igual nas duas formas:

- **Buscar** — placa, marca, modelo, versão ou chassi;
- **Situação** e **Origem**;
- **Pátio** — o lugar onde o carro está. Vazio traz todos;
- **Comprado de** / **Até** — o período em que o carro entrou no pátio. Vazio traz tudo.

### Cadastrar um carro

Botão **Novo veículo**. O que o sistema exige: placa, chassi, marca, modelo, os dois anos e o
valor de compra. O resto ajuda, e pode entrar depois.

Vale preencher desde o começo:

- **Data da compra** — é ela que conta os dias em estoque e responde ao filtro por período;
- **De quem comprou** e **Forma de pagamento**;
- **Teto de orçamento** — o máximo que aquele carro deveria custar. É o que faz o sistema
  avisar **antes** de o gasto estourar;
- **Quero receber** e **Mínimo aceito** — limpos, para a revenda. Quando o carro sai por
  terceiro, o repasse da loja entra por cima desse valor;
- **Código FIPE** — guarde. É ele que vai deixar a consulta automática barata quando ela
  existir.

Placa e chassi são **únicos na revenda**: cadastrar o mesmo carro duas vezes é recusado, com o
número repetido na mensagem.

---

## 4. A ficha do carro

Clique no cartão. À esquerda fica o **custo real**, que é a pergunta que mais aparece:

- **Compra**, **Gastos pagos**, **Previsto ainda por pagar** e **Custo se tudo for pago**;
- **Ainda cabe** — quanto falta para o teto, com aviso quando o previsto estoura;
- **Quero receber**, **Sobra**, **Mínimo aceito**, **Tabela FIPE** e **Custo final vs FIPE** —
  quanto do valor de tabela o carro já consumiu em custo.

**Esses números são somados na hora, toda vez.** Nada de total digitado: um total escrito à mão
fica certo até o próximo gasto, e errado a partir dali sem avisar ninguém.

À direita, as abas.

### Ficha para venda

O botão **Ficha para venda**, no alto da ficha do carro, gera um PDF de uma página: a foto de
capa grande, as outras fotos em fila, marca, modelo, versão, ano, quilometragem, cor, combustível,
câmbio, placa, o valor da tabela FIPE e o **preço anunciado** (ou *Consulte*, quando ele ainda
está em branco). Em cima vai o nome da revenda e o contato, vindos de **Dados da revenda**.

É um papel para o comprador: custo, compra, lucro, pátio e fornecedor **ficam de fora**.

Ao lado, **Mandar pelo WhatsApp** leva a ficha a quem perguntou do carro: no celular, a folha
de compartilhamento abre com o PDF anexado e você escolhe o contato; no computador, o PDF é
baixado e o WhatsApp abre perguntando para quem. Veja o capítulo *No celular*.

### Gastos

**Lançar gasto** pede descrição, tipo, valor e data. Duas coisas facilitam a vida:

- ao digitar a descrição, o sistema **sugere** o que a revenda já usou, e escolher uma sugestão
  já preenche o tipo;
- o gasto pode entrar como **previsto** e virar **pago** depois, no ✓ da linha.
- o gasto pode dizer **de quem** foi comprado: o campo **Fornecedor** lista quem está cadastrado
  em Administração › Fornecedores. IPVA, multa e taxa ficam sem, e isso é normal. É esse campo
  que responde, depois, quanto já foi para cada oficina.

O previsto conta no "custo se tudo for pago", e nunca no custo de hoje. É o que permite ver o
estouro chegando.

### Propostas

Registre toda oferta, inclusive as recusadas: **quem ofereceu**, telefone, **valor oferecido**,
como paga, canal e, quando for por loja parceira, o **repasse** dela.

**Quem ofereceu** busca enquanto você digita: quem já comprou ou ofereceu aparece com o
telefone, e escolher preenche o resto. Um nome novo vira **cliente** ao salvar, sem passar por
cadastro nenhum. O nome no card abre a ficha da pessoa em *Clientes*.

Cada proposta tem o botão **Proposta em PDF**: a revenda em cima, o cliente, o carro com a foto,
o valor, a forma de pagamento, a **validade de sete dias**, as observações como condições e duas
linhas para assinar. Quem quer mandar uma proposta a um cliente registra a proposta e imprime,
ou usa **Mandar pelo WhatsApp**: no celular, o WhatsApp abre com o **PDF já anexado** e a
mensagem — carro, valor, validade — copiada para colar; no computador, o PDF é baixado e a
conversa do cliente abre com a mensagem pronta, e você arrasta o arquivo. Sem telefone na
proposta, o WhatsApp pergunta para quem.

Ao digitar o valor, o sistema mostra **quanto sobraria** se essa proposta fosse aceita — já
descontando repasse, comissão e o custo do carro. É o número da decisão, e ele aparece antes de
qualquer coisa ser gravada.

### Fotos

JPG, PNG ou WebP. A **primeira foto vira a capa**, e qualquer outra pode assumir depois. Cada
foto pode ser marcada pelo que ela mostra: avaria, reparo, pronto ou outra.

Excluir foto apaga o arquivo.

### Documentos

PDF, JPG ou PNG, classificados por tipo — nota fiscal, recibo, documento de leilão, vistoria,
documento pessoal e assim por diante.

Os links abrem por **endereço assinado, de vida curta**: nenhum documento fica público na
internet. Se a página ficou aberta por muito tempo e um link parar de abrir, use **Atualizar os
links**.

Excluir um documento tira ele da ficha e **mantém o arquivo guardado**. Ele pode voltar — veja
*Documentos excluídos*.

### Ficha

Todos os dados do carro, para conferir e editar. É aqui que fica o bloco **Tabela FIPE**.

#### Tabela FIPE

A tabela é **referência**, e jamais o preço. Ela aparece ao lado do custo real para ajudar a
decidir — e quem decide preço é você. A consulta nunca mexe em *Quero receber*, *Mínimo
aceito* nem *Anunciado*.

Três botões:

- **Achar o modelo** — para o carro que ainda não tem código da tabela. Escolha **marca**,
  **modelo** e **ano**, e pronto: o carro guarda o código, e da próxima vez a consulta é
  direta. Quando o mesmo ano existe como flex e como gasolina, as duas opções aparecem e
  você escolhe. Se o carro já tem um modelo apontado, o botão vira **Trocar modelo**.
- **Consultar agora** — busca o valor da tabela deste mês, e **funciona mesmo no carro que
  ainda está sem modelo apontado**: nesse caso ele procura o modelo antes e **mostra o que
  achou**, seja um candidato ou vinte. A ficha mostra quanto a tabela diz, de que mês veio e,
  quando o valor mudou, quanto ele andou.
- **Desvincular** — aparece quando o carro já tem consulta, e desfaz ela inteira.

**Quem escolhe o modelo é você, sempre.** Mesmo quando a busca chega a um candidato só, a lista
abre para você ver o nome, o preço e o código antes de gravar. O sistema estreita de cem para
poucos; a última palavra é de quem conhece o carro.

Cada linha da lista traz uma **barra de acurácia**. Ela mede o quanto daquele cadastro o nome da
tabela confere — versão, ano, câmbio e combustível:

- o carro cadastrado como `Corolla 2.0 XEi` chega perto de 100%, e o melhor candidato aparece
  marcado como **Recomendado**;
- o carro cadastrado só como `Gol` fica em 50% na lista inteira, porque metade dele segue por
  conferir. Aí **recomendado nenhum aparece** — é o sinal de que a escolha depende de você, e
  de que preencher a **versão** na ficha faz a próxima busca chegar muito mais perto.

Onde a busca empata, ela pergunta em vez de apontar. E o destaque muda apenas o que você lê
primeiro: gravar continua sendo o botão **Usar este modelo**.

**Desvincular** apaga o **valor**, a **referência**, o **código** e a **origem** de uma vez. Os
quatro vieram do mesmo modelo, e um preço sem o modelo que o explica seguiria entrando no painel
de custo. Depois de desvincular, o carro volta a ser um carro sem tabela: a atualização mensal
deixa de alcançá-lo, e o *Consultar agora* volta a procurar o modelo. Seus preços — *Quero
receber*, *Mínimo aceito* e *Anunciado* — continuam exatamente onde estavam.

A linha **Origem** diz de onde veio o número: *consulta automática* ou *informada à mão*. As
duas são legítimas — carro raro, importado ou fora da tabela é precificado por quem conhece
aquele mercado, e o sistema respeita isso.

**O pátio se atualiza sozinho.** Uma vez por mês o sistema percorre os carros sem venda e
traz a tabela nova, sem ninguém pedir. Ele **jamais sobrescreve um valor digitado à mão** —
para trocar um desses, use o botão. Enquanto a referência estiver atrasada, a ficha e o
cartão na listagem mostram *FIPE de 2 meses atrás*.

Quando a fonte está fora do ar, a consulta avisa e **o valor que estava na ficha continua**
lá. Nenhuma operação do sistema depende da FIPE: salvar veículo, lançar gasto e registrar
venda funcionam com ela fora do ar.

### Linha do tempo

A história inteira do carro em ordem: compra, mudanças de situação, gastos, fotos e documentos,
propostas e a venda — com o **nome de quem fez cada coisa**. Os botões no topo filtram por
assunto: **Tudo**, **Negócio**, **Gastos**, **Anexos**, **Esteira** e **Pátios**.

É a aba que responde *"o que aconteceu com esse carro?"* sem depender da memória de ninguém.

### Mudar a situação

O botão **Mudar situação** move o carro na esteira, com motivo opcional que fica no histórico:

> Em análise → Comprado → Em reparo → Pronto para venda → Anunciado → Em negociação → Vendido

O sistema recusa salto de etapa, e **permite voltar** onde a operação volta — um carro retorna
para a oficina quando aparece algo depois de pronto.

**"Vendido" tem uma porta só: registrar a venda.** A mudança de situação recusa esse destino de
propósito, para nunca existir carro marcado como vendido sem venda por trás.

### Mudar de pátio

O botão **Mudar de pátio** leva o carro para outro lugar — o pátio da revenda, a loja de um
parceiro, ou nenhum. O motivo é opcional e fica no histórico.

Cada mudança vira um evento na linha do tempo, com o de onde, o para onde, o dia, a hora e quem
fez. É essa história que responde depois *"esse carro ficou dois meses na Loja do Joãozinho e
voltou sem vender"* — a informação que decide se vale deixar carro lá de novo.

Quem não tem a tela **Pátios** continua vendo onde o carro está, logo abaixo do nome dele na
ficha. O que ele não faz é mover: ler é informação, mover é decisão.

---

## 5. Registrar a venda

Na ficha de um carro pronto, o botão verde **Vender** abre o registro. Partindo de uma
proposta, use **Aceitar e vender** na aba Propostas: os dados dela já vêm preenchidos.

- **Data da venda** e **Valor fechado**;
- **Como pagou** e **Canal** — venda direta ou loja parceira. Sendo loja, informe qual e o
  repasse: ele entra **por cima** do que você quer receber. **Quando o carro já está na loja de
  um parceiro, os três vêm preenchidos** com o que foi combinado no cadastro do pátio — e os
  três continuam editáveis, porque o combinado de hoje pode não ser o do próximo carro;
- **Comissão** e **para quem** — para quem trouxe o comprador. Zero quando ninguém trouxe;
- **Comprador**: nome, CPF ou CNPJ e telefone. Partindo de uma proposta, o comprador já é o
  cliente dela; o nome também busca quem a loja conhece, e um nome novo vira cliente ao
  registrar. O CPF digitado aqui completa o cadastro dele;
- **Troca**, quando parte do pagamento vem em carro: os dados do carro que entra e **quanto ele
  vale no negócio**. O sistema **cadastra esse carro no pátio** com origem *Troca* e o valor
  acordado como compra;
- **Observações** ficam só aqui.

Ao gravar: o carro vai para **Vendido**, a faixa verde da venda aparece na ficha com valor,
custo e sobra, e as demais propostas daquele carro são marcadas como recusadas.

**Cancelar venda** desfaz tudo isso e devolve o carro para a esteira, caso o negócio caia.

---

## 6. Vendas

Cada carro que saiu, no período escolhido, com o que ele deixou: valor, custo, líquido, margem
e dias entre a compra e a venda. Os campos **De** e **Até** filtram pela **data da venda**, e
começam no primeiro dia do mês. O nome do comprador abre a ficha dele em *Clientes*.

---

## 7. Clientes

Quem ofereceu, quem comprou, quem volta. Toda proposta e toda venda apontam para alguém daqui,
e ninguém precisa cadastrar antes: a pessoa nasce na primeira proposta, com nome e telefone.
Quem já estava no sistema antes desta tela foi aproveitado das vendas e propostas antigas.

- **Buscar** por nome, telefone ou CPF. A lista mostra o total comprado e o que a pessoa já
  fez: carros comprados, propostas.
- **Ficha**: os dados, o botão do **WhatsApp** no número, e a história — as compras e as
  propostas, cada uma com o carro, a data e a situação. Clicar no carro abre a ficha dele.
- **Mandar ficha**: escolha um carro à venda, e o WhatsApp abre no número do cliente com a
  ficha em PDF, do mesmo jeito da proposta (no celular, anexada; no computador, baixada e a
  conversa aberta). É como avisar quem já comprou que chegou um carro novo.
- **Editar**: nome, CPF ou CNPJ, telefone, e-mail, endereço e anotações. O CPF vai na proposta
  em PDF, embaixo do nome e na assinatura.

**CPF igual** ao de outro cliente é recusado: dois documentos iguais são a mesma pessoa, e o
sistema diz de quem é. **Telefone igual** é um aviso — o telefone da loja parceira pode estar em
mais de um comprador — e **Salvar mesmo assim** confirma que é outra pessoa.

**Excluir** só sai para quem está só cadastrado. Quem tem proposta ou compra fica, e a tela diz
quantas são.

---

## 8. Caixa

O dinheiro no tempo: o que vence, o que entrou, o que atrasou. É a tela da segunda de manhã.

**No resumo**, quatro números e duas listas. *A pagar* é tudo o que a revenda deve, e embaixo
dele quanto vence nos próximos sete dias; *Vencido* é o que passou do prazo, e é o único número
que aparece em vermelho; *A receber* é o que falta entrar das vendas; e *No período* é quanto
entrou menos quanto saiu, nas datas escolhidas em cima.

A lista **O que a revenda deve** junta o gasto do carro e a despesa da loja, ordenados pelo
vencimento — o que vence primeiro é o que você vê primeiro. O ícone à esquerda diz de onde a
linha veio: o carro, ou a loja. O **visto** à direita dá baixa: a conta some da lista e o total
muda na hora.

A lista **O que a revenda tem a receber** traz cada venda com saldo. O botão do carro abre a
ficha dele, que é onde a entrada de dinheiro é registrada — uma venda pode receber em partes, e
cada entrada tem valor, data e forma próprios.

**Despesas da loja**, na segunda aba, é onde entra o que jamais pertence a um carro: aluguel,
energia, salário, imposto, contador. Lance com **Vence em** para ela aparecer no caixa; sem
prazo, ela vence na própria data. O visto dá baixa, e a seta desfaz.

**Nos gastos do carro** e **na venda**, o mesmo dinheiro aparece de perto: o gasto previsto
mostra *Vencido* em vermelho quando passa do prazo, e a venda mostra quanto foi esperado, quanto
entrou e quanto falta.

**O período** vale para as duas abas, e delimita só o que já se moveu: "quanto eu devo" é a
pergunta de hoje, e mudar as datas jamais a esconde.

---

## 9. Mercado

A revenda contra a tabela FIPE. Cada valor é comparado com a tabela **do mês em que aquele
negócio aconteceu** — comparar uma venda de agosto com a tabela de hoje mediria a passagem
do tempo e chamaria isso de resultado.

Os três cartões de cima respondem:

- **Compramos** — o preço de compra contra a tabela do mês da compra. É a vantagem do leilão,
  medida em vez de suposta.
- **Vendemos** — o preço fechado contra a tabela do mês da venda.
- **Estamos pedindo** — o *Quero receber* dos carros parados contra a tabela de agora.

Embaixo deles, **quanto o pátio perdeu de referência**: no mês, e desde o dia em que cada
carro entrou. Carro parado perde valor de tabela todo mês, e este é o custo de segurá-lo.

As listas mostram, carro a carro: **No pátio**, **Propostas na mesa** e **Vendidos**. A seta
verde é bom, a vermelha é ruim — e o lado bom muda: comprar abaixo da tabela é vitória,
vender abaixo dela é aperto.

Onde faltar a cotação daquele mês, a tela escreve **Sem comparação** em vez de inventar um
número, e o carro fica **fora das médias**. O sistema guarda cotações desde o M11, então
negócio anterior a isso aparece assim.

---

## 10. Administração

Telas que costumam ficar com o Administrador e o Gestor.

### Usuários

Cadastro de quem usa o sistema: nome, e-mail, senha (**mínimo de 8 caracteres**), CPF ou CNPJ,
telefone, foto e os perfis.

Três detalhes que evitam susto:

- **inativar** e **excluir** são coisas diferentes: o inativo continua na lista, sem entrar; o
  excluído sai da lista;
- ninguém exclui a própria conta — outro administrador faz isso;
- o e-mail é único na revenda.

### Perfis

Um perfil é um **conjunto de telas**. Marque as telas e todo mundo com aquele perfil passa a
ver — e a alcançar — exatamente aquilo.

Cinco perfis nascem com o sistema: **Administrador**, **Gestor**, **Financeiro**, **Vendedor** e
**Oficina**. Eles podem ganhar e perder telas, e jamais ser excluídos. Perfis novos podem ser
criados à vontade.

### Tipos de gasto

A lista que aparece ao lançar um gasto, mantida por você — nenhuma revenda nomeia as coisas do
mesmo jeito. Cada tipo aceita **palavras-chave**: quem escreve "balanceamento" cai em
Alinhamento porque a palavra está no tipo, sem ter digitado o tipo.

### Pátios

Os lugares onde os carros ficam: o pátio da revenda, e as lojas de terceiros onde ela deixou
carro para vender. **É um cadastro só**, com um campo dizendo qual é qual.

Cada pátio guarda:

- **Nome** — único dentro da revenda;
- **Tipo** — *Pátio da revenda* ou *Loja de terceiro*;
- **Contato** — nome e telefone de quem responde por lá;
- **Repasse combinado** — em percentual **ou** em valor, nunca nos dois. Só para loja de
  terceiro: pátio da casa jamais cobra da casa, e mudar o tipo para *Pátio da revenda* limpa o
  repasse.

É esse repasse que a tela de venda sugere quando o carro está lá.

**Pátio com carro dentro recusa exclusão**, e diz quantos carros são — mova os carros primeiro.

### Dados da revenda

O nome, o CNPJ, o telefone, o e-mail e o endereço da loja. É o que sai impresso em cima de
cada documento gerado — a ficha para venda e a proposta para o cliente. Preencha uma vez; todo
papel que sair depois já vem com o remetente.

### Fornecedores

De quem você compra serviço e peça: a oficina, a funilaria, a loja de autopeças, o despachante.
**Fornecedor diz de quem; tipo de gasto diz o quê.** A mesma oficina pode cobrar Mecânica num
carro e Peças no outro, e é por isso que cada gasto aponta para os dois.

Cada fornecedor guarda:

- **Nome** — único dentro da revenda;
- **Ramo** — o que ele faz. A lista já vem pronta com 25 ramos, e o botão **Ramos** deixa
  renomear, acrescentar ou excluir;
- **Falar com** e **Telefone**;
- **CPF ou CNPJ** — opcional;
- **Anotações**.

A tela abre com o **painel**: pago, previsto, fornecedores e carros atendidos; o ranking de quem
mais recebeu (clicar no nome abre a ficha); a rosca por ramo; as colunas mês a mês; e o gasto por
tipo. Embaixo vem o cadastro: cada card mostra o total **pago**, quantos gastos e o último
deles, do maior para o menor. **Ver gastos** abre
a ficha: pago, previsto, a quebra por tipo de gasto, e cada gasto com a **placa** do carro, que
leva à ficha do veículo. Os campos **De** e **Até** limitam o período; sem eles a tela mostra
desde o início.

O previsto aparece à parte de propósito: um orçamento com a funilaria interessa, e ainda assim
jamais entra no "quanto gastei" até ser pago.

**Fornecedor com gasto no nome recusa exclusão**, e diz quantos gastos são. **Ramo com
fornecedor dentro** também. Quem registra gasto vê a lista de fornecedores para escolher, mesmo
sem a tela de Fornecedores; cadastrar e ver valores exige a tela.

### Documentos excluídos

Todo documento excluído continua guardado. Esta tela mostra qual era o arquivo, de qual carro,
quando saiu e por quem — permite **abrir** para conferir e **devolver** à ficha do veículo.

Exclusão definitiva não é oferecida, e isso é de propósito: uma revenda responde pelo que vendeu
anos depois.

---

## 11. Planilhas

Ao lado do seletor de mosaico e lista, em **Veículos**, e ao lado do período em **Vendas**, em
**Fornecedores**, em **Clientes** e no **Caixa**, os botões **Excel** e **CSV** baixam o que a
tela mostra, com os mesmos filtros
— e tudo, sem página. Em Fornecedores há duas planilhas: o **gasto por fornecedor** e **todos os
gastos** do período, cada um com o carro, o tipo e o fornecedor.

O Excel guarda número e data de verdade, então dá para somar e ordenar. O CSV sai com ponto e
vírgula e acentos certos, do jeito que o Excel em português abre. O arquivo vem com o nome e a
data: `Veiculos08092026.xlsx`.

## 12. No celular

O sistema funciona no celular pelo navegador — **Chrome** no Android, **Safari** no iPhone —, no
mesmo endereço da loja. É onde a proposta acontece de verdade: o valor fechado no pátio, o
PDF mandado na hora.

**Antes da primeira vez**, cada aparelho instala a raiz do certificado da loja, uma vez só. É
um passo de dois minutos, e o roteiro com os toques de cada aparelho está com quem administra
(`docs/operations/celular-na-rede.md`). Sem ele, o navegador avisa que a conexão "não é
particular", e o botão do WhatsApp baixa o PDF em vez de abrir a folha de compartilhamento.

**Na tela inicial.** Com o sistema aberto, *Adicionar à tela inicial* (Chrome: menu ⋮;
Safari: botão de compartilhar). O ícone da Revenda Pro aparece entre os aplicativos e abre em
tela cheia. Ele continua precisando da rede da loja.

**Mandar uma proposta.** Abra o carro, a aba **Propostas**, e toque em **Mandar pelo WhatsApp**.
A folha do aparelho abre com o PDF; toque no WhatsApp, escolha o contato e mande. A mensagem já
está copiada: cole no campo de texto se o WhatsApp deixar só o arquivo. A **ficha para venda**
vai pelo mesmo botão, no alto da ficha do carro.

## 13. Quando algo dá errado

| Situação | O que fazer |
|---|---|
| Apaguei um documento por engano | *Documentos excluídos* → **Devolver**. O arquivo nunca saiu de lá. |
| Apaguei uma foto por engano | Fale com quem administra o sistema: o bucket guarda versões, e a foto é recuperável pelo procedimento de operação. |
| A venda caiu | **Cancelar venda** na ficha. O carro volta para a esteira. |
| O link de um documento parou de abrir | **Atualizar os links**. Eles expiram por segurança. |
| O arquivo foi recusado por tamanho | O limite é de 12 MB por arquivo, e é configurável por quem administra. |
| "Vendido" não aparece ao mudar a situação | É a regra: vendido só registrando a venda. |
| A placa foi recusada | Esse carro já está cadastrado. A mensagem traz a placa repetida. |
| Um item sumiu do meu menu | O menu segue o perfil. Quem ajusta é o Administrador, em *Perfis*. |
| O celular avisa que a conexão "não é particular" | A raiz do certificado ainda está fora desse aparelho. Peça o roteiro a quem administra. |
| O botão do WhatsApp baixou o PDF em vez de abrir a folha | A página abriu em `http://`, ou o navegador é o Firefox. Abra em `https://` no Chrome ou no Safari. |

---

## 14. O caminho completo, num exemplo real

O Cruze que já rodou de ponta a ponta no sistema:

1. **Comprado** em leilão por R$ 29.450, com teto de orçamento de R$ 40.000.
2. **Em reparo**: 21 gastos lançados, de um filtro de R$ 21 a lata e pintura de R$ 800,
   somando R$ 8.544. O custo real vira **R$ 37.994** — somado, e jamais digitado.
3. **Fotos e documentos** anexados: 20 fotos e a nota do leilão.
4. **Pronto para venda**, com "quero receber" de R$ 58.000.
5. **Propostas**: uma de R$ 55.000 do marketplace e outra de R$ 63.000 por loja parceira que
   fica com R$ 5.000. Nas duas, a tela mostra a sobra antes de qualquer decisão.
6. **Vendido** por R$ 60.000 pela loja parceira, com R$ 4.000 de repasse e R$ 1.000 de
   comissão: sobra **R$ 17.006**, margem de 28,34%, 61 dias entre a compra e a venda.
7. **Linha do tempo**: os 34 eventos, em ordem, com o nome de quem fez cada um.
8. **Mercado**: a venda aparece contra a tabela do mês em que ela aconteceu — **R$ 60.000
   quando a FIPE dizia R$ 56.530, 6,14% acima**.
9. **Pátios**: ele saiu do *Pátio Centro* para a *Loja do Joãozinho* e a linha do tempo conta
   isso com o motivo, a hora e quem fez. O painel passa a responder quanto está parado em cada
   lugar, sem deixar de somar o total.
