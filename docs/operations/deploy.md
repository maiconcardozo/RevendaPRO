# Deploy

Como o sistema sobe numa máquina de produção, e como volta de um desastre. Escrito para
alguém que não acompanhou o desenvolvimento: se um passo depende de algo que só está na
cabeça de quem construiu, o documento está errado, e não a pessoa.

## O que é preciso ter antes

| Item | Para quê | Onde se resolve |
|---|---|---|
| Uma máquina Linux com Docker e Docker Compose | rodar tudo | VPS de 2 GB de RAM basta para uso interno |
| Um domínio apontando para o IP da máquina (registro A) | HTTPS e o endereço que a equipe decora | no registrador do domínio |
| Portas 80 e 443 abertas na máquina | o Caddy emitir o certificado e atender | firewall da VPS |
| Conta no Cloudflare R2 com três buckets | fotos, documentos e backup | painel do Cloudflare |
| Um token de API do R2 com leitura e escrita nesses buckets | a API e o backup gravarem | R2 > Manage R2 API Tokens |

Os buckets: `revendapro-public`, `revendapro-private` e `revendapro-backup`. Ligue o
**versionamento** no `revendapro-private` (a API tenta ligar sozinha ao subir; se o token não
tiver a permissão, ela avisa no log e segue). Ver `docs/operations/backup.md`.

## Subir pela primeira vez

```bash
git clone https://github.com/maiconcardozo/RevendaPRO.git
cd RevendaPRO
cp .env.production.example .env
# edite o .env: DOMAIN, senhas, chave JWT e as três variáveis STORAGE_*
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

O que acontece sozinho na primeira subida:

1. O banco nasce vazio e a API aplica as migrations (`SchemaMigrator`).
2. A API sincroniza o catálogo de telas e cria a empresa piloto, o perfil Administrador e o
   usuário admin do `.env`.
3. O backup faz a primeira rodada e passa a rodar todo dia às 06:00 UTC.
4. O Caddy pede o certificado ao Let's Encrypt para o domínio e passa a atender em HTTPS.

Conferir:

```bash
docker compose -f docker-compose.prod.yml ps          # todos "Up", api "healthy"
docker compose -f docker-compose.prod.yml logs api    # "Database ready", "Versioning enabled"
docker compose -f docker-compose.prod.yml logs caddy  # "certificate obtained successfully"
docker compose -f docker-compose.prod.yml logs backup # "[backup] guardado db/daily/..."
```

Depois: abrir `https://<DOMAIN>` no celular, entrar com o admin do `.env`, cadastrar um
veículo, subir uma foto. Se a foto aparece na listagem, o R2 está certo dos dois lados
(gravação pela API, leitura pelo navegador com endereço assinado).

## O que fica diferente do ambiente local

| | Local (`docker-compose.yml`) | Produção (`docker-compose.prod.yml`) |
|---|---|---|
| Armazenamento | MinIO no compose | Cloudflare R2, por variável |
| HTTPS | nenhum, `localhost:3100` | Caddy, certificado automático |
| Portas expostas | banco, MinIO, API e front, em `127.0.0.1` | só 80 e 443 |
| Usuários de demonstração | criados | desligados |
| Buckets | a API cria ao subir | provisionados no painel |
| Log | padrão do Docker | arquivo com rotação: 5 × 20 MB por serviço |

O código é o mesmo. Toda diferença acima é configuração, e está no compose ou no `.env`.

## Atualizar para uma versão nova

```bash
git pull
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

A API aplica as migrations pendentes ao subir. O backup da noite anterior existe; para uma
mudança grande de schema, force um antes:

```bash
docker compose -f docker-compose.prod.yml exec backup backup.sh
```

## Voltar de um desastre

**O banco foi corrompido ou alguém apagou o que não devia.** Restaure o dump de ontem por cima
(o script exige a confirmação):

```bash
docker compose -f docker-compose.prod.yml run --rm -e RESTORE_FORCE=1 backup restore.sh latest
docker compose -f docker-compose.prod.yml restart api
```

Para um dia específico, troque `latest` pela data (`2026-09-02`), ou pelo mês com `monthly`.

**Um arquivo foi apagado do bucket.** O bucket privado é versionado: o objeto continua lá como
versão anterior. Procedimento em `docs/operations/backup.md`.

**A máquina inteira morreu.** Numa máquina nova: os passos de "subir pela primeira vez" com o
mesmo `.env`, depois o restore do último dump. Os arquivos não precisam de nada — estão no R2,
que não morre com a máquina. É por isso que nenhum arquivo do sistema vive em disco.

## O que olhar de vez em quando

- `docker compose -f docker-compose.prod.yml ps` — a coluna de saúde da API.
- O log do backup: uma rodada por dia. Dois dias sem "guardado" é problema.
- O espaço em disco da máquina (`df -h`). O log tem rotação; o banco cresce devagar.
- Uma vez por trimestre: restaurar o último dump num banco à parte e abrir, para ter certeza
  de que o backup restaura — um backup nunca testado é uma esperança.

```bash
docker compose -f docker-compose.prod.yml run --rm backup restore.sh latest daily conferencia
```
