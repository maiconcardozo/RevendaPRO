"use client";

import { useState } from "react";
import { FileSpreadsheet, FileText } from "lucide-react";
import { downloadFile } from "@/lib/download";

/**
 * Os dois botões de planilha (M19): Excel e CSV do que a tela mostra, com os filtros da tela.
 *
 * O caminho já vem com a query da tela; aqui só entra o formato. Enquanto o arquivo é gerado, o
 * botão diz que está gerando, e o erro — sem a tela, servidor fora — aparece ao lado.
 */
export function ExportButtons({
  path,
  name,
  onError,
}: {
  /** O caminho da API sem o formato, com a query da tela: `exports/vehicles?status=3`. */
  path: string;
  /** O nome que o arquivo leva quando o servidor não manda um. */
  name: string;
  onError?: (message: string) => void;
}) {
  const [busy, setBusy] = useState<"xlsx" | "csv" | null>(null);

  async function download(format: "xlsx" | "csv") {
    setBusy(format);

    const separator = path.includes("?") ? "&" : "?";
    const result = await downloadFile(`${path}${separator}format=${format}`, `${name}.${format}`);

    setBusy(null);

    if (!result.ok) onError?.(result.error);
  }

  const base =
    "inline-flex items-center gap-1.5 rounded-md border border-[var(--border)] px-2.5 py-1.5 text-xs font-semibold text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)] disabled:opacity-50";

  return (
    <div className="inline-flex gap-1.5" role="group" aria-label="Exportar a lista">
      <button
        type="button"
        onClick={() => download("xlsx")}
        disabled={busy !== null}
        title="Baixar em Excel, com os filtros da tela"
        className={base}
      >
        <FileSpreadsheet size={14} />
        {busy === "xlsx" ? "Gerando..." : "Excel"}
      </button>
      <button
        type="button"
        onClick={() => download("csv")}
        disabled={busy !== null}
        title="Baixar em CSV, com os filtros da tela"
        className={base}
      >
        <FileText size={14} />
        {busy === "csv" ? "Gerando..." : "CSV"}
      </button>
    </div>
  );
}
