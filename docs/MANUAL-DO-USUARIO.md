# Manual do Revenda Pro

Para quem vai usar o sistema no dia a dia. Sem jargão: cada tela explicada pelo que ela
responde, e o caminho completo de um carro, do leilão até a venda.

Atualizado em 3 de setembro de 2026.

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

Embaixo, três listas curtas: **Mais dinheiro parado**, **Maior sobra prometida** e **Mais tempo
parado**.

Os campos **De** e **Até** limitam **só o que é realizado** — vendas, lucro realizado, dias
médios. O pátio é sempre o de agora, porque um carro comprado no ano passado continua segurando
dinheiro hoje.

---

## 3. Veículos

A tela do estoque. Cada carro é um cartão com a foto de capa, a situação, o custo real e, quando
há teto de orçamento, a barra do quanto ainda cabe.

**Filtros**, todos combináveis:

- **Buscar** — placa, marca, modelo, versão ou chassi;
- **Situação** e **Origem**;
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
- **Quero receber**, **Sobra**, **Mínimo aceito** e **Custo sobre a FIPE**.

**Esses números são somados na hora, toda vez.** Nada de total digitado: um total escrito à mão
fica certo até o próximo gasto, e errado a partir dali sem avisar ninguém.

À direita, as abas.

### Gastos

**Lançar gasto** pede descrição, tipo, valor e data. Duas coisas facilitam a vida:

- ao digitar a descrição, o sistema **sugere** o que a revenda já usou, e escolher uma sugestão
  já preenche o tipo;
- o gasto pode entrar como **previsto** e virar **pago** depois, no ✓ da linha.

O previsto conta no "custo se tudo for pago", e nunca no custo de hoje. É o que permite ver o
estouro chegando.

### Propostas

Registre toda oferta, inclusive as recusadas: **quem ofereceu**, telefone, **valor oferecido**,
como paga, canal e, quando for por loja parceira, o **repasse** dela.

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

Dois botões:

- **Achar o modelo** — para o carro que ainda não tem código da tabela. Escolha **marca**,
  **modelo** e **ano**, e pronto: o carro guarda o código, e da próxima vez a consulta é
  direta. Quando o mesmo ano existe como flex e como gasolina, as duas opções aparecem e
  você escolhe. Se o carro já tem um modelo apontado, o botão vira **Trocar modelo**.
- **Consultar agora** — busca o valor da tabela deste mês. A ficha mostra quanto ela diz,
  de que mês veio e, quando o valor mudou, quanto ele andou desde o que estava lá.

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
assunto: **Tudo**, **Negócio**, **Gastos**, **Anexos** e **Esteira**.

É a aba que responde *"o que aconteceu com esse carro?"* sem depender da memória de ninguém.

### Mudar a situação

O botão **Mudar situação** move o carro na esteira, com motivo opcional que fica no histórico:

> Em análise → Comprado → Em reparo → Pronto para venda → Anunciado → Em negociação → Vendido

O sistema recusa salto de etapa, e **permite voltar** onde a operação volta — um carro retorna
para a oficina quando aparece algo depois de pronto.

**"Vendido" tem uma porta só: registrar a venda.** A mudança de situação recusa esse destino de
propósito, para nunca existir carro marcado como vendido sem venda por trás.

---

## 5. Registrar a venda

Na ficha de um carro pronto, o botão verde **Vender** abre o registro. Partindo de uma
proposta, use **Aceitar e vender** na aba Propostas: os dados dela já vêm preenchidos.

- **Data da venda** e **Valor fechado**;
- **Como pagou** e **Canal** — venda direta ou loja parceira. Sendo loja, informe qual e o
  repasse: ele entra **por cima** do que você quer receber;
- **Comissão** e **para quem** — para quem trouxe o comprador. Zero quando ninguém trouxe;
- **Comprador**: nome, CPF ou CNPJ e telefone;
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
começam no primeiro dia do mês.

---

## 7. Mercado

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

## 8. Administração

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

### Documentos excluídos

Todo documento excluído continua guardado. Esta tela mostra qual era o arquivo, de qual carro,
quando saiu e por quem — permite **abrir** para conferir e **devolver** à ficha do veículo.

Exclusão definitiva não é oferecida, e isso é de propósito: uma revenda responde pelo que vendeu
anos depois.

---

## 9. Quando algo dá errado

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

---

## 10. O caminho completo, num exemplo real

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
