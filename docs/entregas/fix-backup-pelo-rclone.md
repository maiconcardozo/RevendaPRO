# Correção — O backup pelo rclone

Entregue em 2 de outubro de 2026, fora de marco. Branch `fix-backup-sem-minio-mc`, mesclada na
`main` em `686ca33`.

## O que esta correção resolve

O backup diário do banco era montado com o `mc`, o cliente de armazenamento da MinIO. Em 2026 a
MinIO arquivou o projeto aberto e tirou do ar as imagens e os binários: o Docker Hub recusa o
download, o quay.io também, e o site de downloads responde *410 Gone*. A partir de 28 de setembro,
**toda reconstrução completa do sistema falhava** na hora de montar o backup, na máquina de
desenvolvimento e no servidor da rede.

Nada tinha parado: o contêiner de backup que já estava no ar continuava gravando o dump todo dia.
O risco era o dia de montar de novo, quando o backup simplesmente deixaria de existir.

## As regras

### O cliente do backup é o rclone

O `rclone` 1.71.1, copiado da imagem oficial do mesmo jeito que o `mc` era: um binário só, por
cima da imagem do MariaDB.

**Por que é essa:** é mantido, é publicado no Docker Hub, e fala S3 com qualquer fornecedor — o
MinIO de hoje e o Cloudflare R2 da produção (M9). Ele dispensa `apt-get`, que o Dockerfile evita
de propósito: uma rede que bloqueia o espelho do Ubuntu deixaria o build sem backup nenhum.

### O destino é configurado só pelo ambiente

O `ops/backup/storage.sh` monta o destino `store:` a partir de `STORAGE_SERVICE_URL`,
`STORAGE_ACCESS_KEY` e `STORAGE_SECRET_KEY`, e é incluído pelo `backup.sh` e pelo `restore.sh`.

**Por que é essa:** um arquivo de configuração do rclone dentro da imagem guardaria as chaves num
lugar a mais, e ficaria desatualizado na primeira troca de chave. As variáveis são as mesmas que o
`mc` já usava, então o `.env` e o compose ficaram como estavam.

### O comportamento do backup é o mesmo

Mesmo dump, mesmo bucket (`revendapro-backup`), mesmas pastas (`db/daily/` e `db/monthly/`), mesma
retenção (30 dias para o diário, 365 para o mensal), e o mesmo `restore.sh` com os mesmos
argumentos.

**Por que é essa:** os backups gravados pelo `mc` continuam sendo lidos e restaurados pelo rclone.
Uma troca de ferramenta que mudasse o formato do que já está guardado seria uma migração, e esta
é só a troca da ferramenta.

## Como usar

Nada muda para quem usa o sistema. Para quem opera o servidor:

```bash
# backup na hora
docker compose -f docker-compose.lan.yml --env-file .env exec -T backup backup.sh

# restaurar o último diário num banco à parte, para conferir
docker compose -f docker-compose.lan.yml --env-file .env exec -T backup restore.sh latest daily banco_de_conferencia
```

O passo a passo completo, inclusive como recuperar uma foto apagada pelas versões do bucket, está
em `docs/operations/backup.md`.

## O que mudou por baixo

- `ops/backup/Dockerfile`: `FROM rclone/rclone:1.71.1` no lugar de `minio/mc`.
- `ops/backup/storage.sh`, novo: o destino `store:` pelo ambiente.
- `ops/backup/backup.sh` e `restore.sh`: `rclone mkdir`, `copyto`, `lsf` e `delete --min-age` no
  lugar de `mc mb`, `cp`, `ls` e `rm --older-than`.
- `docs/operations/backup.md`: os comandos de recuperação pelo rclone.
- `docs/PENDENCIAS.md`: o item do `mc` saiu, e entrou o risco da imagem do próprio MinIO (§2.2).

## O que foi conferido

- **Na pilha local:** o backup gravou `db/daily/2026-10-03.sql.gz`; a restauração num banco
  descartável trouxe 25 tabelas e 33 veículos, e o banco foi apagado em seguida; a poda por idade,
  em modo simulado, escolheu os dias anteriores e poupou o de hoje.
- **No servidor da rede (`192.168.1.7`):** a reconstrução completa original do `deploy.ps1` voltou
  a passar (`BUILD_EXIT=0`, `UP_EXIT=0`), e um backup de verdade gravou o dump do dia com o
  `rclone v1.71.1`. O site respondeu 200 depois da troca.

## O que ficou de fora, e por quê

- **A imagem do próprio servidor de arquivos MinIO** (`minio/minio`), que saiu do ar do mesmo
  jeito. Ela continua na máquina de desenvolvimento e no servidor, e os contêineres seguem
  rodando; trocar o armazenamento é uma decisão — outro S3 local ou ir direto para o R2 do M9 — e
  está em `docs/PENDENCIAS.md` §2.2. Até lá, `docker image prune` jamais roda no servidor.

## Feito no mesmo dia, sem código

- **O M27 foi publicado no servidor da rede**, junto com esta correção. Ver
  `docs/entregas/M27-custo-e-piso.md`.
- **A tela Dados da revenda do servidor foi preenchida com dados fictícios**, a pedido, por ser
  ambiente de teste: *Revenda Piloto Veículos*, CNPJ 11.111.111/0001-11, (47) 99999-0000,
  `contato@revendapiloto.test`, *Rua dos Testes, 123 - Centro, Joinville/SC*, e um logotipo de
  teste. A ficha para venda saiu com o timbre completo. **Trocar pelos dados reais** antes de a
  loja usar o sistema de verdade.
