#!/usr/bin/env bash
# Restaura um dump do bucket num banco.
#
#   restore.sh latest                  ultimo dump diario, no banco DB_NAME
#   restore.sh 2026-09-02              o dump daquele dia
#   restore.sh 2026-08 monthly         o dump mensal de agosto
#   restore.sh latest daily outro_db   no banco "outro_db" (criado se nao existir)
#
# O banco de destino e criado quando nao existe. Quando existe, o dump substitui cada tabela
# que ele contem (DROP TABLE IF EXISTS vem dentro do dump). Restaurar POR CIMA do banco de
# producao e uma decisao, nao um acidente: o script pede confirmacao a menos que RESTORE_FORCE=1.
set -euo pipefail

which="${1:-latest}"
kind="${2:-daily}"
target="${3:-${DB_NAME:?DB_NAME e obrigatorio}}"

: "${DB_HOST:=database}"
: "${DB_USER:=root}"
: "${DB_PASSWORD:?DB_PASSWORD e obrigatorio}"
: "${STORAGE_SERVICE_URL:?STORAGE_SERVICE_URL e obrigatorio}"
: "${STORAGE_ACCESS_KEY:?STORAGE_ACCESS_KEY e obrigatorio}"
: "${STORAGE_SECRET_KEY:?STORAGE_SECRET_KEY e obrigatorio}"
: "${STORAGE_BACKUP_BUCKET:=revendapro-backup}"

mc alias set store "$STORAGE_SERVICE_URL" "$STORAGE_ACCESS_KEY" "$STORAGE_SECRET_KEY" --api S3v4 > /dev/null

folder="store/$STORAGE_BACKUP_BUCKET/db/$kind/"

if [ "$which" = "latest" ]; then
    which="$(mc ls "$folder" | awk '{print $NF}' | sed 's/\.sql\.gz$//' | sort | tail -n 1)"
    [ -n "$which" ] || { echo "[restore] nenhum dump em $folder" >&2; exit 1; }
fi

object="$folder$which.sql.gz"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "[restore] baixando $object"
mc cp --quiet "$object" "$work/dump.sql.gz" > /dev/null

if [ "${RESTORE_FORCE:-0}" != "1" ] && [ "$target" = "${DB_NAME:-}" ]; then
    echo "[restore] isto vai substituir as tabelas de '$target' em $DB_HOST. Confirme com RESTORE_FORCE=1." >&2
    exit 2
fi

mariadb --host="$DB_HOST" --user="$DB_USER" --password="$DB_PASSWORD" \
    -e "CREATE DATABASE IF NOT EXISTS \`$target\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"

gunzip -c "$work/dump.sql.gz" | mariadb --host="$DB_HOST" --user="$DB_USER" --password="$DB_PASSWORD" "$target"

tables="$(mariadb --host="$DB_HOST" --user="$DB_USER" --password="$DB_PASSWORD" -N \
    -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '$target';")"

echo "[restore] '$target' restaurado de $which ($tables tabelas)"
