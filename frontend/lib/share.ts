import { fetchFile, saveFile } from "./download";

/**
 * Manda um documento gerado pela API pelo WhatsApp, do jeito que o aparelho permite (M20).
 *
 * No celular, o navegador sabe entregar um arquivo a outro aplicativo: a Web Share API abre a
 * folha de compartilhamento com o PDF anexado, a pessoa toca no WhatsApp e escolhe o contato.
 * A mensagem vai junto e também para a área de transferência, porque o WhatsApp do Android
 * costuma descartar o texto quando há arquivo.
 *
 * Onde o aparelho compartilha arquivo nenhum — o computador da loja, ou uma página sem HTTPS —,
 * o PDF cai na pasta de downloads e o `wa.me` abre a conversa com a mensagem pronta; a pessoa
 * arrasta o arquivo. Sem número, o WhatsApp pergunta para quem.
 *
 * A folha só entra em aparelho de mão. O Chrome do Windows também sabe compartilhar arquivo,
 * mas abre a folha do sistema, onde o WhatsApp raramente está; no computador, o download e a
 * conversa aberta são o caminho mais curto até o cliente.
 *
 * Um botão só. A decisão é do código, por `navigator.canShare`, e jamais da pessoa.
 */
export type ShareRequest = {
  /** Caminho na API, depois de `/api/backend/`. */
  path: string;
  /** Nome do arquivo quando a API deixa de dizer. */
  fallbackName: string;
  /** O texto que abre a conversa. */
  message: string;
  /** O telefone do destinatário, só dígitos, quando há um. */
  phone?: string | null;
};

export type ShareResult =
  /** A folha do aparelho abriu com o PDF; a mensagem está na área de transferência. */
  | { ok: true; how: "shared" }
  /** O PDF foi baixado e a conversa abriu; falta arrastar o arquivo. */
  | { ok: true; how: "downloaded" }
  /** A pessoa fechou a folha sem escolher. Nada a dizer. */
  | { ok: true; how: "cancelled" }
  | { ok: false; error: string };

/** O que a tela diz depois, para quem precisa de um passo a mais. */
export const SHARE_NOTICE: Record<Extract<ShareResult, { ok: true }>["how"], string> = {
  shared: "A mensagem foi copiada. Cole no WhatsApp, junto do PDF.",
  downloaded: "O PDF foi baixado. Anexe na conversa que abriu.",
  cancelled: "",
};

export async function shareDocument(request: ShareRequest): Promise<ShareResult> {
  const file = await fetchFile(request.path, request.fallbackName);

  if (!file.ok) return file;

  const pdf = new File([file.blob], file.name, { type: file.blob.type || "application/pdf" });

  if (isHandheld() && canShareFiles(pdf)) {
    await copyQuietly(request.message);

    try {
      await navigator.share({ files: [pdf], text: request.message, title: file.name });
      return { ok: true, how: "shared" };
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") {
        return { ok: true, how: "cancelled" };
      }
      // A folha recusou o arquivo: o caminho do computador serve para o celular também.
    }
  }

  saveFile(pdf, file.name);
  window.open(whatsappUrl(request.phone, request.message), "_blank", "noopener");

  return { ok: true, how: "downloaded" };
}

/** A conversa do WhatsApp, com ou sem número. */
export function whatsappUrl(phone: string | null | undefined, message: string): string {
  const digits = (phone ?? "").replace(/\D/g, "");
  const target = digits ? `https://wa.me/55${digits}` : "https://wa.me/";

  return `${target}?text=${encodeURIComponent(message)}`;
}

/** Celular ou tablet: onde a folha de compartilhamento tem o WhatsApp de verdade. */
function isHandheld(): boolean {
  const hints = (navigator as Navigator & { userAgentData?: { mobile?: boolean } }).userAgentData;

  if (hints && typeof hints.mobile === "boolean") return hints.mobile;

  // O iPad se apresenta como Macintosh; o toque o entrega.
  return (
    /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent) ||
    (/Macintosh/.test(navigator.userAgent) && navigator.maxTouchPoints > 1)
  );
}

function canShareFiles(file: File): boolean {
  try {
    return typeof navigator.canShare === "function" && navigator.canShare({ files: [file] });
  } catch {
    return false;
  }
}

async function copyQuietly(text: string): Promise<void> {
  try {
    await navigator.clipboard?.writeText(text);
  } catch {
    // Sem área de transferência: o texto ainda vai na folha, e a pessoa digita se precisar.
  }
}
