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

: "${DB_HOST:=database}"
: "${DB_NAME:?DB_NAME e obrigatorio}"
: "${DB_USER:=root}"
: "${DB_PASSWORD:?DB_PASSWORD e obrigatorio}"
: "${BACKUP_HOUR_UTC:=06}"
: "${BACKUP_ON_START:=1}"
: "${BACKUP_WAIT_SCHEMA_SECONDS:=300}"

# Na primeira subida de uma maquina nova o banco nasce vazio: quem cria as tabelas e a API,
# alguns segundos depois. O backup so depende do banco estar de pe, entao chegaria aqui antes
# e guardaria um dump sem tabela nenhuma. Esperar o schema aparecer e mais honesto do que
# deixar a trava de tamanho reprovar a rodada inicial de todo deploy novo.
wait_for_schema() {
    local deadline=$(( $(date -u +%s) + BACKUP_WAIT_SCHEMA_SECONDS ))
    local tables

    while [ "$(date -u +%s)" -lt "$deadline" ]; do
        tables="$(mariadb --host="$DB_HOST" --user="$DB_USER" --password="$DB_PASSWORD" \
            --skip-column-names --batch \
            --execute="SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '$DB_NAME'" \
            2> /dev/null || echo 0)"

        if [ "${tables:-0}" -gt 0 ]; then
            return 0
        fi

        echo "[backup] banco ainda sem tabelas; a API esta subindo o schema"
        sleep 10
    done

    return 1
}

if [ "$BACKUP_ON_START" = "1" ]; then
    if wait_for_schema; then
        backup.sh || echo "[backup] a rodada inicial falhou; a proxima e as ${BACKUP_HOUR_UTC}:00 UTC" >&2
    else
        echo "[backup] o schema demorou mais de ${BACKUP_WAIT_SCHEMA_SECONDS}s para aparecer;" \
             "a rodada inicial fica para as ${BACKUP_HOUR_UTC}:00 UTC" >&2
    fi
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
