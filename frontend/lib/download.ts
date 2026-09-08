/**
 * Baixa um arquivo gerado pela API — PDF, Excel ou CSV — pelo mesmo proxy que serve o JSON.
 *
 * Vai por `fetch`, e não por `window.open`: o proxy injeta o token a partir do cookie, e uma
 * resposta de erro (403 sem a tela, 422 com a razão) chega como JSON legível em vez de uma aba
 * em branco. O nome vem do `Content-Disposition` que a API manda, e cai no `fallback` quando o
 * cabeçalho falta.
 */
export type DownloadResult = { ok: true } | { ok: false; error: string };

export async function downloadFile(path: string, fallbackName: string): Promise<DownloadResult> {
  let response: Response;

  try {
    response = await fetch(`/api/backend/${path}`, { credentials: "same-origin" });
  } catch {
    return { ok: false, error: "Servidor indisponível. Tente novamente." };
  }

  if (!response.ok) {
    let detail = "Falha ao gerar o arquivo.";

    try {
      const body = (await response.json()) as { detail?: string };
      if (body.detail) detail = body.detail;
    } catch {
      // Sem corpo legível: a mensagem padrão já diz o que houve.
    }

    return { ok: false, error: detail };
  }

  const blob = await response.blob();
  const name = fileNameOf(response.headers.get("content-disposition")) ?? fallbackName;

  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = name;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);

  return { ok: true };
}

/** O nome do arquivo no `Content-Disposition`, com preferência pela forma UTF-8. */
function fileNameOf(header: string | null): string | null {
  if (!header) return null;

  const utf8 = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (utf8) return decodeURIComponent(utf8[1]);

  const plain = /filename="?([^";]+)"?/i.exec(header);
  return plain ? plain[1] : null;
}
