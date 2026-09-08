/**
 * Baixa um arquivo gerado pela API — PDF, Excel ou CSV — pelo mesmo proxy que serve o JSON.
 *
 * Vai por `fetch`, e não por `window.open`: o proxy injeta o token a partir do cookie, e uma
 * resposta de erro (403 sem a tela, 422 com a razão) chega como JSON legível em vez de uma aba
 * em branco. O nome vem do `Content-Disposition` que a API manda, e cai no `fallback` quando o
 * cabeçalho falta.
 *
 * A busca e a gravação são peças separadas (M20): quem compartilha pelo aparelho busca do mesmo
 * jeito e só troca o que faz com os bytes.
 */
export type DownloadResult = { ok: true } | { ok: false; error: string };

/** O arquivo já buscado, com o nome que a API deu. */
export type FetchedFile = { ok: true; blob: Blob; name: string } | { ok: false; error: string };

/** Busca o arquivo pelo proxy, com o erro já legível quando a API recusa. */
export async function fetchFile(path: string, fallbackName: string): Promise<FetchedFile> {
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

  return { ok: true, blob, name };
}

/** Manda o arquivo para a pasta de downloads, pelo `<a download>` de sempre. */
export function saveFile(blob: Blob, name: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = name;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

export async function downloadFile(path: string, fallbackName: string): Promise<DownloadResult> {
  const file = await fetchFile(path, fallbackName);

  if (!file.ok) return file;

  saveFile(file.blob, file.name);

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
