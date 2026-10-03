#!/usr/bin/env bash
# O destino "store:" do rclone, montado so com variaveis de ambiente - sem arquivo de
# configuracao para esquecer dentro da imagem. Incluido pelo backup.sh e pelo restore.sh.
#
# "Other" e o S3 generico: serve para o MinIO local e para o Cloudflare R2. O endereco no
# estilo caminho (endpoint/bucket) e o padrao desse provedor, e e o que o MinIO espera.
: "${STORAGE_SERVICE_URL:?STORAGE_SERVICE_URL e obrigatorio}"
: "${STORAGE_ACCESS_KEY:?STORAGE_ACCESS_KEY e obrigatorio}"
: "${STORAGE_SECRET_KEY:?STORAGE_SECRET_KEY e obrigatorio}"

export RCLONE_CONFIG_STORE_TYPE=s3
export RCLONE_CONFIG_STORE_PROVIDER=Other
export RCLONE_CONFIG_STORE_ENDPOINT="$STORAGE_SERVICE_URL"
export RCLONE_CONFIG_STORE_ACCESS_KEY_ID="$STORAGE_ACCESS_KEY"
export RCLONE_CONFIG_STORE_SECRET_ACCESS_KEY="$STORAGE_SECRET_KEY"

# Configuracao toda no ambiente: sem isto o rclone avisa a cada chamada que o arquivo dele
# esta faltando, e o aviso suja o log do backup.
export RCLONE_CONFIG=/dev/null
