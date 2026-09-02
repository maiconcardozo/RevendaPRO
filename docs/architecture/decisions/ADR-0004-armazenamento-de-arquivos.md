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

O tráfego de saída domina a conta de um site de anúncios: guardar 28 GB custa centavos em
qualquer fornecedor, e servir esses mesmos bytes para o público custa a partir de dezenas de
dólares por mês, crescendo justamente quando o produto dá certo.

## Decisão

### 1. A porta mora no domínio, e fala de endereço

```csharp
// Domain/Interfaces/Storage/IFileStorage.cs
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, StorageRequest request, CancellationToken ct = default);

    /// <summary>Endereço que o navegador acessa direto. Para arquivo privado, expira.</summary>
    Uri GetUrl(string key, TimeSpan? expiresIn = null);

    Task DeleteAsync(string key, CancellationToken ct = default);
}
```

`GetUrl` existe por um motivo preciso. Uma porta que só soubesse devolver bytes deixaria o
código independente do fornecedor **no papel**, com todas as imagens continuando a passar pela
aplicação. Seria o custo da abstração sem o benefício dela, e o CDN — metade da razão de
escolher o fornecedor — ficaria inútil.

`StorageRequest` carrega a visibilidade:

| Visibilidade | Quem usa | Como é servido |
|---|---|---|
| `Public` | foto de veículo, que nasce para ir no anúncio | endereço estável, atrás do CDN |
| `Private` | documento, que carrega dado pessoal | URL assinada, de vida curta |

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
- **remover o EXIF**, porque foto de celular carrega coordenada de GPS — publicar o original
  no anúncio revela onde a foto foi tirada;
- **reprocessar** para os três tamanhos.

Envio direto economiza banda e entrega ao cliente a decisão do que entra no bucket. No volume
de uma revenda, a economia é irrelevante.

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
- A API deixa de servir imagem, e o CDN passa a atender a maior parte das requisições.
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

## O que fica fora

A foto de perfil continua em disco. É uma imagem pequena por pessoa, já funciona, e migrar
agora seria retrabalho sem ganho. Quando fizer sentido, ela passa a usar a mesma porta.
