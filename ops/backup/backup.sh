#!/usr/bin/env bash
# Faz um dump do banco e guarda no bucket de backup, com retencao.
#
# Layout no bucket:
#   db/daily/AAAA-MM-DD.sql.gz    guardado por BACKUP_KEEP_DAILY_DAYS (30 por padrao)
#   db/monthly/AAAA-MM.sql.gz     o dump do dia 1, guardado por BACKUP_KEEP_MONTHLY_DAYS (365)
#
# Um dump e um arquivo unico, consistente (--single-transaction), com as instrucoes de criacao
# das tabelas. Restaurar e rodar o restore.sh - ver docs/operations/backup.md.
set -euo pipefail

: "${DB_HOST:=database}"
: "${DB_NAME:?DB_NAME e obrigatorio}"
: "${DB_USER:=root}"
: "${DB_PASSWORD:?DB_PASSWORD e obrigatorio}"
: "${STORAGE_SERVICE_URL:?STORAGE_SERVICE_URL e obrigatorio}"
: "${STORAGE_ACCESS_KEY:?STORAGE_ACCESS_KEY e obrigatorio}"
: "${STORAGE_SECRET_KEY:?STORAGE_SECRET_KEY e obrigatorio}"
: "${STORAGE_BACKUP_BUCKET:=revendapro-backup}"
: "${BACKUP_KEEP_DAILY_DAYS:=30}"
: "${BACKUP_KEEP_MONTHLY_DAYS:=365}"

today="$(date -u +%F)"
month="$(date -u +%Y-%m)"
day_of_month="$(date -u +%d)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "[backup] $(date -u +%FT%TZ) dump de $DB_NAME em $DB_HOST"

# --single-transaction: le uma foto consistente do InnoDB sem travar as tabelas; a operacao
# continua gravando enquanto o dump roda.
mariadb-dump \
    --host="$DB_HOST" --user="$DB_USER" --password="$DB_PASSWORD" \
    --single-transaction --quick --routines --events --triggers \
    --default-character-set=utf8mb4 \
    "$DB_NAME" | gzip -9 > "$work/$today.sql.gz"

size="$(stat -c %s "$work/$today.sql.gz")"

if [ "$size" -lt 1024 ]; then
    echo "[backup] ERRO: dump com $size bytes - pequeno demais para ser um banco de verdade" >&2
    exit 1
fi

# O alias fala com qualquer S3: MinIO hoje, R2 quando publicar.
mc alias set store "$STORAGE_SERVICE_URL" "$STORAGE_ACCESS_KEY" "$STORAGE_SECRET_KEY" --api S3v4 > /dev/null
mc mb --ignore-existing "store/$STORAGE_BACKUP_BUCKET" > /dev/null

mc cp --quiet "$work/$today.sql.gz" "store/$STORAGE_BACKUP_BUCKET/db/daily/$today.sql.gz"
echo "[backup] guardado db/daily/$today.sql.gz ($size bytes)"

if [ "$day_of_month" = "01" ]; then
    mc cp --quiet "$work/$today.sql.gz" "store/$STORAGE_BACKUP_BUCKET/db/monthly/$month.sql.gz"
    echo "[backup] guardado db/monthly/$month.sql.gz"
fi

# Retencao. O mc entende "older than", entao a poda e uma linha por pasta.
mc rm --quiet --recursive --force --older-than "${BACKUP_KEEP_DAILY_DAYS}d" \
    "store/$STORAGE_BACKUP_BUCKET/db/daily/" 2> /dev/null || true
mc rm --quiet --recursive --force --older-than "${BACKUP_KEEP_MONTHLY_DAYS}d" \
    "store/$STORAGE_BACKUP_BUCKET/db/monthly/" 2> /dev/null || true

echo "[backup] concluido"
