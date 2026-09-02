# Plano — M6: Veículo e custo

Fontes: `Revenda PRO requisitos.docx` e `docs/TRANSCRICOES_ENTREVISTA_REVENDAPRO.md`.
Armazenamento em `ADR-0004`. Padrão de código em `ADR-0003` e `PADRAO-GLOBAL.md`.

A tela `vehicles` já existe no catálogo e já é liberada por perfil. Falta o que ela mostra.

## O que a entrega precisa provar

O próprio documento de requisitos define o alvo da primeira versão:

> O usuário consegue cadastrar um veículo, lançar tudo que gastou, consultar seu custo e
> decidir se uma proposta vale a pena.

**Isso atravessa o que eu havia separado em M6, M7 e parte do M8.** Cadastrar veículo sem
lançar gasto entrega meia funcionalidade: o teto de orçamento, que é o número que o usuário
consulta o dia inteiro, só existe se houver despesa. Por isso este marco passa a ser
**veículo e custo**, e o M7 deixa de existir como marco separado.

O decisor, nas palavras do stakeholder:

> *"Quero 58, o carro me custa 40, o cara manda 55 no dinheiro. Ganhar 15 mil? Já dou-lhe
> fogo."*

Três números e uma decisão de segundos. Tudo aqui existe para que esses três números estejam
certos e à mão.

## Modelo

PK é `Id`; chave estrangeira leva `Id` na frente.

### Vehicle

| Coluna | Tipo | Requisito | Notas |
|---|---|---|---|
| IdTenant | int | RNF-04 | FK Tenant |
| Plate | varchar(7) | RF-03 | sem hífen, única por tenant entre as ativas |
| Brand, Model, Version | varchar | RF-03 | |
| ModelYear, ManufactureYear | smallint | RF-03 | |
| Color | varchar(30) | | sete Dusters na pasta dele se distinguem por cor |
| Mileage | int | RF-03 | |
| FuelType, Transmission | int | | enum |
| Chassis | varchar(17) | RF-03 | VIN, único por tenant entre os ativos |
| Renavam | varchar(11) | | |
| Origin | int | RF-04 | leilão, particular, loja, **troca**, outro |
| HasDamage | tinyint(1) | RF-05 | |
| DamageDescription | varchar(500) | RF-05 | |
| Status | int | RF-06 | abaixo |
| PurchasePrice | decimal(12,2) | RF-07 | |
| PurchaseDate | date | RF-07 | |
| SupplierName | varchar(160) | RF-07 | fornecedor ou leilão |
| PurchasePaymentMethod | int | RF-07 | |
| **BudgetCeiling** | decimal(12,2) | **novo** | teto de custo total |
| FipeValue | decimal(12,2) | RF-14 | informado à mão |
| FipeReferenceDate | date | RF-14 | a tabela muda todo mês |
| **FipeCode** | varchar(10) | **novo** | código do modelo na FIPE. Vazio enquanto for manual |
| DesiredNetPrice | decimal(12,2) | RF-16 | **quanto ele quer receber** |
| MinimumNetPrice | decimal(12,2) | RF-16 | mínimo aceito |
| AdvertisedPrice | decimal(12,2) | RF-16 | anunciado, com a margem da loja por cima |
| MarketNotes | varchar(500) | **novo** | pesquisa de mercado |
| Notes | varchar(1000) | RF-03 | |
| IdCoverPhoto | int | | FK VehiclePhoto |

Status, na sequência que ele descreveu (RF-06):

```
EmAnalise -> Comprado -> EmReparo -> ProntoParaVenda -> Anunciado -> Negociando -> Vendido
```

### VehicleExpense

| Coluna | Tipo | Requisito | Notas |
|---|---|---|---|
| IdVehicle | int | RF-08 | FK Vehicle, cascade |
| Description | varchar(160) | RF-08 | |
| Category | int | RF-09 | abaixo |
| Amount | decimal(12,2) | RF-08 | |
| Date | date | RF-08 | |
| IsPaid | tinyint(1) | RF-08, RF-11 | falso = despesa prevista |

Categorias (RF-09, mais o que apareceu no `GASTOS.docx` real): frete, documentação,
despachante, peças, mecânica, **elétrica**, pintura, funilaria, mão de obra, pneus,
**alinhamento**, **polimento**, taxas, outros.

### VehiclePhoto

| Coluna | Tipo | Requisito | Notas |
|---|---|---|---|
| IdVehicle | int | RF-12 | FK Vehicle, cascade |
| Kind | int | RF-12 | **dano, reparo, finalizado**, outro |
| StorageKey | varchar(200) | | prefixo comum aos três tamanhos |
| ContentType, SizeInBytes, Width, Height | | | |
| Order | int | | ordem, arrastável |

`Kind` existe porque a foto do dano tem função própria: ela é **enviada ao comprador** para
explicar o histórico do carro de leilão.

### VehicleDocument

| Coluna | Tipo | Requisito |
|---|---|---|
| IdVehicle | int | RF-13 |
| Kind | int | RF-13 |
| StorageKey | varchar(200) | bucket privado |
| FileName | varchar(160) | nome original, só para exibir |
| ContentType, SizeInBytes | | |

Tipos observados no acervo real: nota de venda, comprovante de pagamento, documento de leilão
(gate pass), termo, vistoria, despachante, comprovante de residência, documento pessoal, outro.

### VehicleStatusHistory

`IdVehicle`, `FromStatus`, `ToStatus`, `Reason`. Sem ela, o tempo em cada etapa se perde a cada
mudança — e a RF-24 pede tempo em estoque.

## Números calculados, e jamais guardados

| Número | Fórmula | Requisito |
|---|---|---|
| Custo total | `PurchasePrice` + despesas **pagas** | RF-10 |
| Custo previsto | custo total + despesas previstas | RF-11 |
| Orçamento consumido | custo total ÷ `BudgetCeiling` | **novo** |
| Sobra do orçamento | `BudgetCeiling` − custo total | **novo** |
| Percentual sobre FIPE | custo total ÷ `FipeValue` | RF-15 |
| Lucro projetado | preço − custo total | RF-17 |
| Dias em estoque | hoje − `PurchaseDate` | RF-24 |
| Capital parado | soma do custo dos veículos ainda sem venda | RF-23 |

Guardar total calculado é exatamente o defeito do `GASTOS.docx`: o total foi digitado uma vez,
três despesas entraram depois, e o documento passou a mostrar **R$ 350 a menos** — verificado
somando linha por linha o arquivo real. Quem soma é o sistema, sempre, a cada leitura.

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Armazenamento | `IFileStorage`, `S3FileStorage`, MinIO no compose | **concluído** | — |
| **V1** | Imagem | WebP em três tamanhos, EXIF removido, bytes conferidos | **concluído** | V0 |
| **V2** | Domínio | Entidades, enums, máquina de status e os cálculos | Transição inválida lança regra de negócio; o custo bate com o `GASTOS.docx` real | — |
| **V3** | Persistência | Mapeamentos, migration, query objects, repositories | Migration aplica, layout de coluna no padrão | V2 |
| **V4** | Veículo | Cadastrar, editar, listar com filtro, mudar status | Placa e chassi repetidos no mesmo tenant são recusados | V3 |
| **V5** | Custo | Lançar despesa paga e prevista, custo total, teto e alerta | O total muda sozinho ao lançar despesa | V4 |
| **V6** | Fotos e documentos | Enviar, ordenar, definir capa, classificar; documento em bucket privado | Vinte fotos sobem; o documento exige URL assinada | V1, V4 |
| **V7** | Api | `/api/vehicles`, tudo guardado por `RequireScreen("vehicles")` | `ApiGuardTests` passa sem exceção nova | V4, V5, V6 |
| **V8** | Frontend | Listagem, galeria, formulário, lançamento rápido de despesa | Build do Next; a listagem carrega a miniatura, e nunca a cheia | V7 |
| **V9** | Testes | Status, unicidade, cálculo de custo, recusa de arquivo | Suíte verde | V8 |

## Decisões deste plano

**Teto sobre o custo total.** Confirmado pelo stakeholder: os R$ 37.994 do Cruze são compra
mais tudo que foi gasto para deixá-lo apto à venda, e o teto dele era R$ 40 mil. A tela mostra
o percentual consumido e **quanto ainda cabe** — que é a pergunta real de quem está comprando
peça.

**Preço primário é o que ele recebe.** `DesiredNetPrice` é *"quero 58 para mim"*. Quando a
venda é por terceiro, existe um **percentual de repasse**, e o anunciado sobe por cima. Modelar
como "preço de venda menos comissão" descreveria errado a cabeça dele.

**Percentual sobre FIPE é informação secundária.** *"Não tem muito esse negócio de vender
tantos por cento."* Ele compara com anúncios da cidade dele, e por isso existe `MarketNotes`.


**O `FipeCode` existe hoje para a integração de amanhã sair barata.** Guardando só o valor, a
consulta automática precisaria reencontrar cada carro por texto — casar "Cruze", "Hatch" e
"2014" contra o catálogo da FIPE. Isso falha justamente nos modelos com muitas versões, que são
quase todos: um Cruze 2014 tem LT, LTZ e Sport6, manual e automático, cada um com preço
próprio. O código identifica o modelo exato. Uma coluna anulável agora evita um de-para
adivinhado depois.

A FIPE também é **base de preço**: a tela oferece preencher o valor desejado a partir dela, que
é como o stakeholder pensa — *"é 66 de FIPE, quero 58"*.
**Lançar despesa precisa ser rápido.** Ele hoje digita uma linha no Word. Se o formulário
exigir data e situação de pagamento em cada lançamento, fica **mais lento que o Word**, e a
RNF-02 cai. Data já vem com hoje, situação já vem como paga: o caminho comum é descrição e
valor.

**Placa** aceita o formato antigo e o Mercosul, guardada sem hífen, única por tenant entre as
ativas.

**Preço** em `decimal(12,2)`, jamais `double` — RNF-12.

**Chassi** com dezessete caracteres, sem `I`, `O` e `Q`, que a norma exclui para evitar
confusão com `1` e `0`.

**Capa** por `IdCoverPhoto` no `Vehicle`, e não `IsCover` no `VehiclePhoto`: assim o banco
garante uma capa só.

**Quilometragem** só aumenta na edição, salvo correção registrada em `Notes`. Ele fotografa o
hodômetro, então existe prova.

## Fora deste marco

**Venda e proposta**, que ficam no marco seguinte, e trazem junto três coisas desta entrevista:

- **Troca como forma de pagamento.** *"Pode ser tbm troca que gera uma entrada, ou um carro e
  um dinheiro."* Uma venda pode gerar **um veículo novo no estoque**, com o valor acordado como
  preço de compra. E o lucro realizado deixa de ser só dinheiro: parte vira estoque.
- **Percentual de repasse** quando a venda sai por terceiro.
- **Proposta** com lucro líquido projetado na hora, que é a RF-19.

Consulta automática à FIPE fica fora do MVP por decisão do próprio documento.

## Ainda em aberto

- O percentual de repasse incide sobre o preço anunciado, ou é somado ao que ele quer receber?
  As duas leituras aparecem na entrevista. A conta muda.
- A forma de pagamento muda o preço aceito — em quanto, e isso é regra ou caso a caso?
