# ADR-0004: Armazenamento de arquivos — porta no domínio, S3 na infraestrutura

Data: 2026-09-01
Estado: aceito
Relacionado: ADR-0003 (camadas e dependências)

## Contexto

O marco M6 traz o cadastro de veículos, e com ele o acervo de fotos e documentos. A escala
muda de patamar: hoje existe uma imagem pequena por pessoa; passam a existir de quinze a
trinta fotos por veículo, mais documentos como CRLV e nota fiscal.

O `DiskPhotoStorageService` guarda a foto de perfil num volume do Docker. Ele resolve o avatar
e falha no acervo por três motivos:

1. **A API vira servidor de imagem.** Cada foto passa pelo processo da aplicação. Uma página
   com trinta miniaturas são trinta requisições atendidas pelo servidor de aplicação.
2. **O volume some com o contêiner.** O acervo de fotos é o ativo mais visível do produto, e
   é o último que se pode perder.
3. **Uma segunda réplica quebra.** Duas instâncias da API têm dois discos diferentes.

Nesta fase o sistema é de **uso interno**, sem página pública de anúncio. Isso decide a
visibilidade dos arquivos e adia a discussão de custo de tráfego: o acervo inteiro de um ano e
meio de operação de uma revenda fica abaixo de 1 GB, e guardar isso custa centavos em qualquer
fornecedor. O egresso volta a pesar no dia em que existir anúncio público.

## Decisão

### 1. A porta mora no domínio, e fala de endereço

```csharp
// Domain/Interfaces/Storage/IFileStorage.cs
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, StorageRequest request, CancellationToken ct = default);

    /// <summary>Endereço que o navegador acessa direto. Para arquivo privado, expira.</summary>
    Uri GetUrl(string key, FileVisibility visibility, TimeSpan? expiresIn = null);

    Task DeleteAsync(string key, FileVisibility visibility, CancellationToken ct = default);

    Task<int> DeleteByPrefixAsync(string prefix, FileVisibility visibility, CancellationToken ct = default);
}
```

`GetUrl` existe por um motivo preciso. Uma porta que só soubesse devolver bytes deixaria o
código independente do fornecedor **no papel**, com todas as imagens continuando a passar pela
aplicação. Seria o custo da abstração sem o benefício dela: a API voltaria a ser servidora de
imagem, que é justamente o que esta decisão evita.

`StorageRequest` carrega a visibilidade:

| Visibilidade | Uso hoje | Como é servido |
|---|---|---|
| `Private` | **tudo**: foto e documento | URL assinada, de vida curta |
| `Public` | nenhum uso ainda | endereço estável, atrás do CDN |

**Nesta fase o sistema é de uso interno, e nada nasce público.** A entrevista com o
stakeholder mostrou por que isso vale inclusive para as fotos: as fotos do sinistro são
**enviadas ao comprador** para vencer a objeção do carro de leilão — *"eu mando as fotos da
batida do carro; 'Pô, mas era só isso?'"*. Ou seja, existe saída de arquivo, e ela é
**dirigida a uma pessoa**, e nunca um endereço permanente que qualquer um alcança.

URL assinada atende esse envio e expira. Link público permanente jamais volta a ser privado
depois de compartilhado.

A RNF-04 fecha a questão: dados, **fotos** e documentos de uma empresa ficam fora do alcance
de outra. Bucket público com chave imprevisível esconde, e não autentica: uma URL que vaza por
histórico, print ou encaminhamento passa a valer para qualquer pessoa.

`Public` continua existindo no modelo porque a distinção é real e vai fazer falta: quando
houver página pública de anúncio, ela passa a valer para exatamente as fotos destinadas à
propaganda — decisão por arquivo, e nunca global.

Essa distinção é **regra de negócio**, e por isso mora no domínio. Deixá-la como configuração
de bucket na infraestrutura é como um documento acaba público por engano.

### 2. Um adaptador só, contra a API do S3

`Infrastructure/Storage/S3FileStorage.cs` é o **único** arquivo do repositório que referencia
o `AWSSDK.S3`. Nada acima dele sabe que S3 existe.

**Nenhuma classe leva o nome de um fornecedor.** Nada de `R2FileStorage` ou
`CloudflareStorage`: MinIO, Cloudflare R2 e AWS S3 falam a mesma API, e o que muda entre eles
são valores de configuração.

### 3. O fornecedor é configuração, e nada além disso

`Shared/Settings/StorageSettings.cs`, no mesmo formato do `JwtSettings`:

| Configuração | MinIO local | Cloudflare R2 | AWS S3 |
|---|---|---|---|
| `ServiceUrl` | `http://minio:9000` | `https://<conta>.r2.cloudflarestorage.com` | vazio, o SDK resolve |
| `PublicUrl` | `http://localhost:9100` | domínio do CDN | domínio do bucket |
| `Region` | `us-east-1` | `auto` | a região real |
| `ForcePathStyle` | `true` | `true` | `false` |
| `PublicBucket` | `revendapro-public` | idem | idem |
| `PrivateBucket` | `revendapro-private` | idem | idem |
| `AccessKey` / `SecretKey` | do compose | do `.env` | do `.env` |

**Migrar para o S3 no futuro é trocar esses valores.** Sem recompilar, sem tocar em classe
alguma.

`ServiceUrl` e `PublicUrl` são separados de propósito. Com o MinIO em contêiner, a API fala com
ele em `minio:9000`, e o navegador precisa de `localhost:9100`. Uma URL assinada gerada com o
endereço interno simplesmente quebra no browser — é o primeiro erro que aparece quando os dois
viram um só.

E **a URL tem que ser assinada já com o endereço público**, e não assinada com o interno e
reescrita depois. A assinatura versão 4 inclui o host: a query sai com
`X-Amz-SignedHeaders=host`, e trocar o host de uma URL já assinada devolve
`SignatureDoesNotMatch`. Por isso o `S3FileStorage` mantém dois clientes — um que envia e
apaga pelo `ServiceUrl`, outro que só assina, pelo `PublicUrl`. O segundo jamais abre conexão:
assinar é cálculo local. Quando os dois endereços são iguais — Cloudflare R2, AWS S3 — existe
um cliente só.

### 4. Formato da chave, portátil por construção

```
{idTenant}/vehicles/{vehicleCode}/{photoCode}-{size}.webp
{idTenant}/vehicles/{vehicleCode}/documents/{documentCode}.pdf
```

O nome do arquivo enviado **jamais** vira chave: ele carrega acentuação, espaço e, com
frequência, o que o cliente quiser mandar. A chave é derivada de `Code`, que é UUID v7.

O tenant vem primeiro para que apagar tudo de uma empresa, ou aplicar uma regra de ciclo de
vida por empresa, seja uma operação de prefixo.

Nenhum trecho da chave menciona fornecedor, o que mantém a cópia entre eles um `rclone sync`.

### 5. Três tamanhos, em WebP

A imagem é convertida na entrada e gravada em miniatura, cartão e cheia. A tela pede o menor
que couber.

Isso rende mais que a escolha de fornecedor: entregar um JPEG de 4 MB para preencher um
quadrado de 200 pixels desperdiça a maior parte dos bytes, e o corte de tráfego fica entre
cinco e dez vezes. Vale em qualquer fornecedor, inclusive no dia de trocar.

### 6. O arquivo passa pela API

O navegador **jamais** envia direto para o bucket com URL assinada. O arquivo entra pela API
porque três coisas precisam acontecer antes de ele existir:

- conferir os **bytes mágicos**, e nunca a extensão ou o `Content-Type` informados;
- **remover o EXIF**, porque foto de celular carrega coordenada de GPS — enviar o original ao
  comprador revela onde a foto foi tirada, que costuma ser o pátio ou a casa de alguém;
- **reprocessar** para os três tamanhos.

Envio direto economiza banda e entrega ao cliente a decisão do que entra no bucket. No volume
de uma revenda, a economia é irrelevante.


### 7. Tipos aceitos e limite configurável

A RNF-09 pede **PDF, JPG, JPEG e PNG**, com limite de tamanho **configurável por arquivo**.

| | Tipos | Passa pelo processamento |
|---|---|---|
| Foto de veículo | JPEG, PNG, WebP | sim: orientação, três tamanhos, EXIF removido |
| Documento | PDF, JPEG, PNG | falso: o arquivo é guardado como veio |

WebP entra na lista de fotos por ser um superconjunto sem custo — quem manda um WebP manda uma
imagem válida. PDF jamais entra como foto: ele vai para o bucket como está, porque converter
documento em imagem perde texto selecionável e assinatura.

O limite vive em `StorageSettings`, e não em constante de código, porque a RNF-09 pede que ele
seja ajustável — a foto de um celular novo pesa muito mais que a de um antigo, e esse número
muda com o tempo sem que nada mais mude.

## Requisitos que sustentam esta decisão

| Requisito | O que ele determina aqui |
|---|---|
| RNF-04 | Foto e documento de uma empresa ficam fora do alcance de outra |
| RNF-06 | Acesso autenticado, e link público permanente jamais |
| RNF-09 | PDF, JPG, JPEG e PNG, com limite configurável por arquivo |
| RNF-11 | Backup periódico **do banco e dos arquivos** — ver pendência abaixo |
| RNF-13 | LGPD: o acervo real contém RG e comprovante de residência de terceiros |
## Ambientes

| Ambiente | Fornecedor | Por quê |
|---|---|---|
| Desenvolvimento e homologação | **MinIO**, no `docker-compose` | custo zero, conta nenhuma, teste offline |
| Produção | **Cloudflare R2** | saída sem cobrança, CDN incluído, API do S3 |

O MinIO é **AGPL**. Para uso local, onde se usa sem distribuir, isso é indiferente. Promovê-lo
a produção exige olhar a licença com cuidado.

## Consequências

O que fica melhor:

- Trocar de fornecedor é configuração.
- A API deixa de servir imagem: ela devolve endereços, e o navegador busca os bytes.
- O acervo sobrevive ao contêiner.
- Um teste de arquitetura garante que o SDK da AWS jamais apareça fora da infraestrutura, do
  mesmo modo que já garante para o EF Core e o Dapper.

O que custa:

- Mais um serviço no `docker-compose`.
- Uma dependência de processamento de imagem. **O ImageSharp mudou para licença comercial
  acima de um limite de faturamento**; a escolha recai sobre o **SkiaSharp**, que é MIT. Mesma
  cautela já registrada para FluentAssertions e MediatR em `PADRAO-GLOBAL.md`.
- Trocar de fornecedor é barato no código e **não é instantâneo nos dados**: mover terabytes
  entre buckets é um trabalho à parte, com `rclone` ou equivalente. A decisão fácil de reverter
  é a do código, e nunca a do acervo já acumulado.


## Pendência registrada: backup

A RNF-11 pede backup periódico do banco **e dos arquivos anexados**, e nada disso existe hoje.

Vale separar duas coisas que costumam ser confundidas: um bucket é **durável**, e durabilidade
protege contra o disco falhar. Ela protege nada contra um `DELETE` errado, que apaga em todas
as réplicas ao mesmo tempo. Backup é outra coisa.

O caminho é versionamento no bucket, ou uma cópia periódica para um segundo destino. Fica fora
do M6 e precisa existir antes da primeira revenda de verdade entrar.
## A foto de perfil

No M6 ela ficou em disco, num volume do Docker, porque migrar naquele momento seria retrabalho.
No M9 ela passou a usar a mesma porta (`BucketUserPhotoStorage`): entra reduzida a uma
rendição WebP de 320 pixels, sem metadados, na chave `{idTenant}/users/{userCode}/{nome}.webp`
do bucket privado. A API continua servindo os bytes em `GET /api/users/{code}/photo` — é a
única leitura que passa pelo processo, porque o avatar é desenhado em toda página para quem
está logado e o sidebar não tem como pedir endereço assinado a cada carregamento.

Com isso **nenhum arquivo do sistema vive fora do bucket**, e o volume `revendapro_files`
deixou de existir.
