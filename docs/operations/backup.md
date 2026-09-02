# Backup e restauração

A RNF-11 pede backup periódico do banco **e dos arquivos**. São duas proteções diferentes,
porque os dois riscos são diferentes.

## O que protege o quê

| Risco | O que cobre |
|---|---|
| Disco do banco falha | O dump diário no bucket |
| `DELETE` errado no banco, migration que deu errado | O dump diário: restaura o de ontem, ou o do mês |
| Disco do bucket falha | A durabilidade do próprio bucket (o MinIO em RAID, o R2 por conta) |
| Alguém apaga um arquivo do bucket por engano | **O versionamento**: o objeto apagado vira versão anterior, e volta |

Durabilidade **não é** backup. Ela protege contra o disco; um `DELETE` errado apaga em todas as
réplicas ao mesmo tempo. Por isso as duas coisas existem separadas.

## O banco: `ops/backup`

Um container a mais no compose (`backup`), construído sobre a imagem do MariaDB — para o
`mariadb-dump` ser da mesma versão do servidor — com o `mc` (cliente S3 do MinIO) por cima.
Ele roda uma vez ao subir, e depois **todo dia às 06:00 UTC** (03:00 em Brasília).

O dump vai para o bucket `revendapro-backup`:

```
db/daily/AAAA-MM-DD.sql.gz     guardado por 30 dias
db/monthly/AAAA-MM.sql.gz      o dump do dia 1, guardado por 365 dias
```

Um dump com menos de 1 KB é recusado — é o sintoma de banco vazio ou de credencial errada, e
guardar isso por cima do de ontem seria pior que falhar.

Acompanhar:

```bash
docker compose logs -f backup
```

Forçar uma rodada agora:

```bash
docker compose exec backup backup.sh
```

### Restaurar

Num banco **à parte**, para conferir sem tocar na operação:

```bash
docker compose run --rm backup restore.sh latest daily revendapro_conferencia
```

Por cima do banco de produção — o script pede a confirmação, porque isto substitui cada tabela:

```bash
docker compose run --rm -e RESTORE_FORCE=1 backup restore.sh 2026-09-02
```

Depois de restaurar por cima, reinicie a API: `docker compose restart api`. As migrations
que o dump já continha ficam registradas em `__EFMigrationsHistory`, então a API sobe sem
tentar aplicar nada de novo.

Formas do primeiro argumento: `latest`, uma data `AAAA-MM-DD` (diário) ou um mês `AAAA-MM`
com `monthly` no segundo argumento.

### Quando publicar no Cloudflare R2

Nada muda no código nem no script. No compose de produção, o serviço `backup` recebe:

```
STORAGE_SERVICE_URL=https://<conta>.r2.cloudflarestorage.com
STORAGE_ACCESS_KEY=<token R2>
STORAGE_SECRET_KEY=<segredo R2>
STORAGE_BACKUP_BUCKET=revendapro-backup
```

O bucket de backup é um terceiro bucket, separado do público e do privado de propósito: um
token que só a rotina de backup usa, e que ninguém mais tem.

## Os arquivos: versionamento do bucket privado

A API liga o versionamento do bucket privado ao subir (`Storage:KeepFileVersions`, ligado por
padrão). É idempotente, e funciona no MinIO e no R2 — o R2 tem versionamento em
disponibilidade geral desde 2026. Se o token não tiver a permissão, a API avisa no log e sobe
mesmo assim.

Com versionamento, apagar um objeto cria um *delete marker* e a versão anterior continua lá.
Recuperar pelo `mc`:

```bash
mc ls --versions store/revendapro-private/1/vehicles/<codigo>/
mc cp --version-id <id> store/revendapro-private/<chave> ./recuperado.webp
```

Versões antigas ocupam espaço. Regra de ciclo de vida recomendada: descartar versões
**não correntes** depois de 90 dias. No MinIO:

```bash
mc ilm rule add store/revendapro-private --noncurrent-expire-days 90
```

No R2, a mesma regra se configura no painel do bucket, em *Object lifecycle rules*.

O bucket público **não** é versionado: hoje ele está vazio, e o que for para lá será derivado
do privado.

## O que ainda falta

- **Alarme.** Hoje a falha de uma rodada aparece só no log. Quando houver deploy, o `/health`
  da API e o log do backup precisam de alguém olhando — é parte do V5 do M9.
- **Teste de restauração periódico.** O V6 do M9 documenta o roteiro; a rotina de repetir
  isso a cada tantos meses é disciplina, não código.
