# M6 — Veículo, custo, gastos, fotos e documentos

Documento de entrega escrito em 8 de setembro de 2026, a partir de `docs/MARCOS.md` e do plano
`docs/plans/m6-cadastro-de-veiculos.md`.

## O que este marco resolve

O coração da operação: o carro entra, gasta, e o dono precisa saber **quanto ele custou de
verdade** a qualquer momento. Até aqui isso vivia numa planilha.

## As regras

### O custo é somado a cada leitura, e jamais guardado

Compra mais gastos pagos, calculado no momento em que alguém pergunta. Coluna `CustoTotal`
nenhuma existe.

**Por que é essa:** foi a decisão mais importante do marco, e ela veio de um defeito na planilha
real do stakeholder. O total tinha sido digitado uma vez; três gastos entraram embaixo dele
depois; e o documento seguia mostrando **R$ 350 a menos** do que o carro tinha custado. Um total
guardado está certo até o próximo gasto, e errado a partir dali — em silêncio, que é o pior
jeito de estar errado.

### Gasto previsto fica fora do custo real

Todo gasto é *pago* ou *previsto*. Só o pago entra no custo.

**Por que é essa:** um orçamento com a funilaria interessa para decidir, e ainda assim jamais é
dinheiro que saiu. Misturar os dois faria o custo do carro subir por causa de um serviço que
talvez nunca aconteça.

### O tipo de gasto é tabela da revenda, com palavras-chave

Mecânica, Elétrica, Funilaria… mantidos pela própria loja, com palavras que sugerem o tipo a
partir do que a pessoa digitou. Quem escreve "balanceamento" cai em Alinhamento sem nunca ter
cadastrado a palavra.

**Por que é essa:** os tipos que faltam só aparecem no uso — retrovisor, vidro elétrico,
ar-condicionado. Com lista fixa, tudo isso cai em "Outros", que é onde a análise de gasto para
de valer.

### O tipo do arquivo é julgado pelos primeiros bytes, e nunca pela extensão

**Por que é essa:** extensão é texto que qualquer um renomeia. Os primeiros bytes são o arquivo.

### Documento excluído continua no bucket; foto excluída sai de verdade

**Por que é essa:** uma revenda responde pelo que vendeu anos depois, e o documento é a prova.
A foto é ilustração — guardá-la para sempre seria pagar armazenamento por nada.

### Placa e chassi são únicos por empresa, conferidos por consulta

**Por que é essa:** a conferência é por consulta, e jamais por índice único, porque a linha
excluída fica na tabela: um índice recusaria uma placa que voltou a estar livre. (Essa mesma
escolha é o que o M23 precisa tratar ao devolver um carro da lixeira.)

## Como usar

*Veículos → Novo veículo*: placa, chassi, marca, modelo, ano, quilometragem, origem e o preço de
compra. Na ficha, a aba **Gastos** lança o que se gasta, com tipo, data e a marca de pago ou
previsto; **Fotos** e **Documentos** anexam arquivos; o bloco de custo mostra o total somado
agora, o teto de orçamento e quanto ainda cabe.

## O que mudou por baixo

- `Vehicle`, `VehicleExpense`, `ExpenseType`, `VehiclePhoto`, `VehicleDocument`,
  `VehicleStatusHistory`.
- Foto convertida para WebP em três tamanhos; limite de 12 MB, configurável.
- `VehicleCost`, o objeto de valor que soma — e que nada guarda.

## O que foi conferido

Contra o `GASTOS.docx` real do stakeholder: o Cruze com os 21 gastos fecha em **R$ 37.994** —
o número que a planilha dele tinha errado.

## O que ficou de fora, e por quê

- **Integração com a tabela FIPE**: o código FIPE já era guardado aqui, de propósito, e a
  consulta automática veio no M11.
- **Recuperar carro e gasto excluídos**: só o documento ganhou porta de volta, porque só o
  arquivo dele ficava pago e inalcançável. O resto veio no M23.
