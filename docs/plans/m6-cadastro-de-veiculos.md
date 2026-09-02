# Plano — M6: Cadastro de veículos

Decisão de armazenamento em `docs/architecture/decisions/ADR-0004-armazenamento-de-arquivos.md`.
Padrão de código em `ADR-0003` e `docs/architecture/PADRAO-GLOBAL.md`.

A tela `vehicles` já existe no `ScreenCatalog` e já é liberada por perfil. O que falta é o que
ela mostra.

## Modelo

Nomes de coluna seguem a convenção: PK é `Id`, chave estrangeira leva `Id` na frente.

### Vehicle

| Coluna | Tipo | Notas |
|---|---|---|
| IdTenant | int | FK Tenant |
| Plate | varchar(7) | placa, sem hífen. Única por tenant, entre as ativas |
| Brand | varchar(60) | |
| Model | varchar(80) | |
| Version | varchar(80) | |
| ModelYear | smallint | |
| ManufactureYear | smallint | |
| Color | varchar(30) | |
| Mileage | int | quilometragem |
| FuelType | int | enum |
| Transmission | int | enum |
| Chassis | varchar(17) | VIN. Único por tenant, entre os ativos |
| Renavam | varchar(11) | |
| Status | int | enum, abaixo |
| PurchasePrice | decimal(12,2) | |
| ListPrice | decimal(12,2) | |
| Notes | varchar(1000) | |
| IdCoverPhoto | int | FK VehiclePhoto, nulo até ter foto |

Status, que é o que dá sentido ao produto — a revenda compra, recupera e revende:

```
Purchased -> InRepair -> Available -> Reserved -> Sold
                 \-> WriteOff
```

### VehiclePhoto

| Coluna | Tipo | Notas |
|---|---|---|
| IdVehicle | int | FK Vehicle, cascade |
| StorageKey | varchar(200) | prefixo comum aos três tamanhos |
| ContentType | varchar(40) | sempre `image/webp` após o reprocessamento |
| SizeInBytes | int | soma dos três tamanhos |
| Width | smallint | da imagem cheia |
| Height | smallint | da imagem cheia |
| Order | int | ordem no anúncio, arrastável na tela |

### VehicleDocument

| Coluna | Tipo | Notas |
|---|---|---|
| IdVehicle | int | FK Vehicle, cascade |
| StorageKey | varchar(200) | **bucket privado** |
| Kind | int | enum: CRLV, nota fiscal, laudo, outro |
| FileName | varchar(160) | nome original, só para exibir |
| ContentType | varchar(80) | |
| SizeInBytes | int | |

### VehicleStatusHistory

| Coluna | Tipo | Notas |
|---|---|---|
| IdVehicle | int | FK Vehicle, cascade |
| FromStatus | int | nulo na entrada |
| ToStatus | int | |
| Reason | varchar(240) | |

Uma revenda precisa saber quanto tempo o carro ficou em cada etapa. Sem esta tabela, essa
resposta se perde a cada mudança.

## Marcos

| # | Marco | Entrega | Pronto quando | Depende |
|---|---|---|---|---|
| **V0** | Armazenamento | `IFileStorage` no domínio, `S3FileStorage` na infraestrutura, `StorageSettings`, MinIO no compose, criação dos dois buckets na inicialização | Sobe arquivo pelo MinIO e o navegador abre pela URL pública | — |
| **V1** | Imagem | Conversão para WebP em três tamanhos, remoção de EXIF, conferência dos bytes mágicos | Um JPEG de 4 MB vira três WebP; um `.exe` renomeado para `.jpg` é recusado | V0 |
| **V2** | Domínio | `Vehicle`, `VehiclePhoto`, `VehicleDocument`, `VehicleStatusHistory`, enums e a máquina de status | Transição inválida lança regra de negócio | — |
| **V3** | Persistência | Mapeamentos, migration, query objects e repositories | Migration aplica e o layout de coluna segue o padrão | V2 |
| **V4** | Casos de uso | Cadastrar, editar, listar com filtro e paginação, mudar status, excluir | Validação recusa placa e chassi repetidos no mesmo tenant | V3 |
| **V5** | Fotos e documentos | Enviar, ordenar, definir capa, remover. Documento em bucket privado com URL de vida curta | Vinte fotos sobem, a capa aparece na listagem, o documento exige URL assinada | V1, V4 |
| **V6** | Api | `/api/vehicles`, todas as ações guardadas por `RequireScreen("vehicles")` | O `ApiGuardTests` passa sem exceção nova | V4, V5 |
| **V7** | Frontend | Listagem com filtro e galeria, formulário com máscara de placa, envio múltiplo de fotos com arrastar para ordenar | Build do Next; a listagem carrega a miniatura, e nunca a imagem cheia | V6 |
| **V8** | Testes | Máquina de status, unicidade de placa e chassi, recusa de arquivo, exclusão lógica nas consultas novas | Suíte verde | V7 |

```
V0 ─> V1 ─┐
          ├─> V5 ─> V6 ─> V7 ─> V8
V2 ─> V3 ─┴─> V4 ─┘
```

## Decisões que este plano assume

**Placa.** Aceita o formato antigo (`ABC1234`) e o Mercosul (`ABC1D23`), guardada sem hífen, com
a máscara vivendo na tela. Unicidade por tenant, entre os registros ativos — uma placa pode
voltar a existir depois que o veículo anterior for excluído.

**Preço.** `decimal(12,2)`, e jamais `double`. Dinheiro em ponto flutuante acumula erro de
arredondamento.

**Chassi.** Dezessete caracteres, sem `I`, `O` e `Q`, que a norma exclui para evitar confusão
com `1` e `0`. A validação recusa esses três.

**Capa.** `IdCoverPhoto` sai do `Vehicle`, e não uma coluna `IsCover` no `VehiclePhoto`. Assim o
banco garante que existe uma capa só, em vez de a aplicação ter de garantir.

**Quilometragem.** Aceita apenas aumento na edição, salvo correção explícita registrada em
`Notes`. Quilometragem que cai sozinha é o sinal clássico de adulteração.

## Fora deste marco

Consulta à FIPE fica no M8, e por isso `ListPrice` entra à mão por enquanto. Custos de
recuperação são o M7, e o `VehicleExpense` nasce lá.
