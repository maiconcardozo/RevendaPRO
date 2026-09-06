#!/usr/bin/env bash
#
# Poe uma foto em cada carro do patio de demonstracao.
#
# POR QUE ISTO E UM SCRIPT, E JAMAIS PARTE DO SEMEADOR
#
# O semeador (DbInitializer) roda na subida da API, e precisa funcionar com a maquina offline e
# sem depender de nenhum servico de terceiro. Buscar foto na internet ali dentro colocaria a
# subida do sistema na mao de um site que ninguem controla. Guardar vinte fotos no repositorio
# resolveria isso e criaria outro problema: megabytes de binario no historico, para sempre.
#
# Entao a foto e um passo separado, opcional, e rodado por quem quer ver a galeria e a
# miniatura da listagem com imagem de verdade.
#
# A FONTE
#
# loremflickr.com, que existe para isto: devolve foto do Flickr por palavra-chave, e e o que
# projetos usam como imagem de exemplo. O `lock` deixa a escolha estavel - rodar de novo traz a
# mesma foto para o mesmo carro, em vez de embaralhar o patio a cada execucao.
#
# COMO USAR
#
#   ./ops/demo-photos.sh                 # usa http://localhost:5100 e o .env da raiz
#   API=http://outra-maquina:5100 ./ops/demo-photos.sh
#
# Ele envia pela MESMA porta que a tela usa - POST /api/vehicles/{code}/photos, multipart -,
# entao a foto passa pela conversao em WebP, pelos tres tamanhos e pela capa automatica, igual
# a uma foto enviada por uma pessoa.

set -euo pipefail

API="${API:-http://localhost:5100}"
RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEMP="$(mktemp -d)"

trap 'rm -rf "$TEMP"' EXIT

if [ -f "$RAIZ/.env" ]; then
  set -a
  # shellcheck disable=SC1091
  . "$RAIZ/.env"
  set +a
fi

: "${REVENDAPRO_ADMIN_EMAIL:?informe REVENDAPRO_ADMIN_EMAIL, ou tenha um .env na raiz}"
: "${REVENDAPRO_ADMIN_PASSWORD:?informe REVENDAPRO_ADMIN_PASSWORD, ou tenha um .env na raiz}"

echo "[fotos] entrando em $API"

token=$(curl -s -X POST "$API/api/auth/login" \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"$REVENDAPRO_ADMIN_EMAIL\",\"password\":\"$REVENDAPRO_ADMIN_PASSWORD\"}" \
  | sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')

if [ -z "$token" ]; then
  echo "[fotos] o login falhou. Confira o .env e se a API esta no ar." >&2
  exit 1
fi

# Placa, codigo, marca: e a placa DEM que separa o carro de demonstracao do carro de verdade.
curl -s -H "Authorization: Bearer $token" "$API/api/vehicles?search=DEM1A" \
  | tr '{' '\n' \
  | grep -o '"code":"[^"]*","plate":"DEM1A[0-9]*","chassis":"[^"]*","brand":"[^"]*"' \
  | sed 's/"code":"\([^"]*\)","plate":"\([^"]*\)","chassis":"[^"]*","brand":"\([^"]*\)"/\1 \2 \3/' \
  > "$TEMP/carros.txt"

total=$(wc -l < "$TEMP/carros.txt" | tr -d ' ')

if [ "$total" = "0" ]; then
  echo "[fotos] nenhum carro de demonstracao neste banco."
  echo "[fotos] suba a pilha com RevendaPro__SeedDemoVehicles=true e tente de novo."
  exit 0
fi

echo "[fotos] $total carros de demonstracao"

enviadas=0
puladas=0

while read -r code plate brand; do
  # Carro que ja tem foto fica como esta: rodar de novo jamais empilha copia.
  fotos=$(curl -s -H "Authorization: Bearer $token" "$API/api/vehicles/$code/photos" \
    | grep -o '"code":"' | wc -l | tr -d ' ')

  if [ "$fotos" != "0" ]; then
    puladas=$((puladas + 1))
    continue
  fi

  lock=$(echo "$plate" | tr -dc '0-9')
  arquivo="$TEMP/$plate.jpg"

  curl -sL --max-time 30 -o "$arquivo" "https://loremflickr.com/800/600/car,$brand?lock=$lock" || true

  tamanho=$(wc -c < "$arquivo" 2>/dev/null | tr -d ' ' || echo 0)

  # Marca que a fonte desconhece devolve resposta curta demais para ser foto. Cai para "car".
  if [ "${tamanho:-0}" -lt 5000 ]; then
    curl -sL --max-time 30 -o "$arquivo" "https://loremflickr.com/800/600/car?lock=$lock" || true
    tamanho=$(wc -c < "$arquivo" 2>/dev/null | tr -d ' ' || echo 0)
  fi

  if [ "${tamanho:-0}" -lt 5000 ]; then
    echo "[fotos] $plate: a fonte de imagens ficou muda, seguindo"
    continue
  fi

  # kind=3 e "Finalizado": a foto do carro pronto, que e a que serve de capa na listagem.
  codigo_http=$(curl -s -o /dev/null -w "%{http_code}" -X POST \
    -H "Authorization: Bearer $token" \
    -F "file=@$arquivo;type=image/jpeg" \
    -F "kind=3" \
    "$API/api/vehicles/$code/photos")

  if [ "$codigo_http" = "200" ]; then
    enviadas=$((enviadas + 1))
  else
    echo "[fotos] $plate: a API respondeu $codigo_http"
  fi
done < "$TEMP/carros.txt"

echo "[fotos] $enviadas enviadas, $puladas ja tinham foto"
