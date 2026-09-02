#!/usr/bin/env bash
# Roda o backup uma vez por dia, na hora configurada (UTC), e fica esperando o proximo.
#
# Sem cron de proposito: um cron dentro de container precisa de um processo a mais, de
# arquivo de agenda e de ambiente repassado a mao. Um laco que dorme ate a hora certa faz o
# mesmo com dez linhas, e o "docker compose logs backup" mostra cada rodada.
set -euo pipefail

# "docker compose run --rm backup restore.sh latest" chega aqui com argumentos: executa o que
# foi pedido e sai, em vez de entrar no laco diario.
if [ "$#" -gt 0 ]; then
    exec "$@"
fi

: "${BACKUP_HOUR_UTC:=06}"
: "${BACKUP_ON_START:=1}"

if [ "$BACKUP_ON_START" = "1" ]; then
    backup.sh || echo "[backup] a rodada inicial falhou; a proxima e as ${BACKUP_HOUR_UTC}:00 UTC" >&2
fi

while true; do
    now="$(date -u +%s)"
    next="$(date -u -d "today ${BACKUP_HOUR_UTC}:00" +%s)"

    if [ "$next" -le "$now" ]; then
        next="$(date -u -d "tomorrow ${BACKUP_HOUR_UTC}:00" +%s)"
    fi

    echo "[backup] proxima rodada em $(date -u -d "@$next" +%FT%TZ)"
    sleep $(( next - now ))

    backup.sh || echo "[backup] a rodada falhou; tenta de novo amanha" >&2
done
