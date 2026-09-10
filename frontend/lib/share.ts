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
 * **A ordem importa, e é o motivo de este arquivo ter duas metades.** Abrir a folha do aparelho
 * e abrir uma aba nova só funcionam **dentro do toque** — e um `await` para buscar o PDF gasta o
 * toque: no iPhone a folha recusa (`NotAllowedError`), e no computador o download consome a
 * ativação e o navegador bloqueia a aba do WhatsApp como pop-up. Foi exatamente o que aconteceu
 * no teste da loja: "cliquei e ele não mandou". Por isso `prepareDocument` busca o arquivo
 * **antes**, e `sendDocument` faz só o que precisa do toque, sem esperar nada.
 *
 * Um botão só. A decisão é do código, por `navigator.canShare`, e jamais da pessoa.
 */
export type PreparedDocument = { file: File; name: string };

export type PrepareResult = { ok: true; document: PreparedDocument } | { ok: false; error: string };

export type ShareResult =
  /** A folha do aparelho abriu com o PDF; a mensagem está na área de transferência. */
  | { ok: true; how: "shared" }
  /** O PDF foi baixado e a conversa abriu; falta arrastar o arquivo. */
  | { ok: true; how: "downloaded" }
  /** O PDF foi baixado, mas o navegador bloqueou a aba do WhatsApp. */
  | { ok: true; how: "blocked" }
  /** A pessoa fechou a folha sem escolher. Nada a dizer. */
  | { ok: true; how: "cancelled" }
  /** O PDF ainda estava sendo buscado; a tela pede um segundo toque. */
  | { ok: true; how: "preparing" }
  | { ok: false; error: string };

/** O que a tela diz depois, para quem precisa de um passo a mais. */
export const SHARE_NOTICE: Record<Extract<ShareResult, { ok: true }>["how"], string> = {
  shared: "A mensagem foi copiada. Cole no WhatsApp, junto do PDF.",
  downloaded: "O PDF foi baixado. Anexe na conversa que abriu.",
  blocked: "O PDF foi baixado. O navegador bloqueou a aba do WhatsApp: libere pop-ups para este endereço e tente de novo.",
  cancelled: "",
  preparing: "Preparando o PDF… toque de novo para mandar.",
};

/** Busca o PDF pelo proxy. Pode esperar o quanto precisar: ainda não gastou o toque de ninguém. */
export async function prepareDocument(path: string, fallbackName: string): Promise<PrepareResult> {
  const file = await fetchFile(path, fallbackName);

  if (!file.ok) return file;

  return {
    ok: true,
    document: {
      file: new File([file.blob], file.name, { type: file.blob.type || "application/pdf" }),
      name: file.name,
    },
  };
}

/**
 * Manda um documento já buscado. **Chame dentro do clique, sem `await` antes**: é o que
 * mantém a ativação do toque viva para a folha do aparelho ou para a aba do WhatsApp.
 */
export function sendDocument(
  document: PreparedDocument,
  message: string,
  phone?: string | null,
): Promise<ShareResult> {
  if (isHandheld() && canShareFiles(document.file)) {
    // Sem esperar: a cópia é assíncrona, e a folha precisa ser pedida ainda dentro do toque.
    void copyQuietly(message);

    return navigator
      .share({ files: [document.file], text: message, title: document.name })
      .then((): ShareResult => ({ ok: true, how: "shared" }))
      .catch((error: unknown): ShareResult => {
        if (error instanceof DOMException && error.name === "AbortError") {
          return { ok: true, how: "cancelled" };
        }

        // A folha recusou o arquivo. O toque já foi gasto, então a conversa abre na própria
        // aba — navegar não precisa de ativação — e o PDF fica para a pessoa anexar.
        saveFile(document.file, document.name);
        window.location.assign(whatsappUrl(phone, message));

        return { ok: true, how: "downloaded" };
      });
  }

  return Promise.resolve(sendFromDesktop(document, message, phone));
}

/**
 * O caminho do computador: a aba do WhatsApp **primeiro**, porque é ela que o navegador só
 * deixa abrir dentro do toque; o download vem depois, e esse funciona sem ativação.
 */
function sendFromDesktop(document: PreparedDocument, message: string, phone?: string | null): ShareResult {
  const popup = window.open(whatsappUrl(phone, message), "_blank", "noopener");

  saveFile(document.file, document.name);

  return { ok: true, how: popup === null ? "blocked" : "downloaded" };
}

/**
 * Abre a aba do WhatsApp em branco, ainda dentro do toque, para ser apontada depois — o jeito
 * de manter um toque só no computador quando o PDF ainda não foi buscado.
 */
export function openPendingWindow(): Window | null {
  return isHandheld() ? null : window.open("about:blank", "_blank");
}

/** Aponta a aba aberta antes para a conversa, e baixa o PDF. */
export function sendThroughPendingWindow(
  popup: Window | null,
  document: PreparedDocument,
  message: string,
  phone?: string | null,
): ShareResult {
  saveFile(document.file, document.name);

  if (popup === null || popup.closed) {
    return { ok: true, how: "blocked" };
  }

  popup.location.href = whatsappUrl(phone, message);

  return { ok: true, how: "downloaded" };
}

/** A conversa do WhatsApp, com ou sem número. */
export function whatsappUrl(phone: string | null | undefined, message: string): string {
  const digits = (phone ?? "").replace(/\D/g, "");
  const target = digits ? `https://wa.me/55${digits}` : "https://wa.me/";

  return `${target}?text=${encodeURIComponent(message)}`;
}

/** Celular ou tablet: onde a folha de compartilhamento tem o WhatsApp de verdade. */
export function isHandheld(): boolean {
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
