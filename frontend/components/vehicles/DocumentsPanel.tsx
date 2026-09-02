"use client";

import { useCallback, useEffect, useState } from "react";
import { Download, FileText, FileUp, RefreshCw, Trash2 } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { apiGet, apiSend } from "@/lib/api";
import { apiUploadMany } from "@/lib/upload";
import { formatBytes, formatMoment } from "@/lib/masks";
import { VEHICLE_DOCUMENT_KIND_LABEL, type VehicleDocument } from "@/lib/types";
import { Empty, PageError } from "./VehicleUi";

/**
 * The documents of the vehicle: invoice, registration certificate, auction paper, receipt.
 *
 * All in the private bucket, addressed by a signed URL that expires in fifteen minutes —
 * RNF-06 forbids a permanent public link, and personal buyer documents live here.
 *
 * **Deleting takes it out of the listing and leaves the file in the store, forever.** It is
 * the only deletion in the system that behaves this way, and the confirmation says so: a
 * document is fiscal and legal evidence, and can be demanded years later.
 */
export function DocumentsPanel({
  vehicleCode,
  maxUploadSize,
}: {
  vehicleCode: string;
  /** Largest accepted file. Refusing here saves the whole upload. */
  maxUploadSize: number;
}) {
  const [documents, setDocuments] = useState<VehicleDocument[] | null>(null);
  const [error, setError] = useState("");
  const [kind, setKind] = useState("1");
  const [uploading, setUploading] = useState<{ done: number; total: number } | null>(null);
  const [busy, setBusy] = useState(false);
  const [toDelete, setToDelete] = useState<VehicleDocument | null>(null);

  const load = useCallback(async () => {
    const result = await apiGet<VehicleDocument[]>(
      `vehicles/${vehicleCode}/documents`,
      "Falha ao carregar os documentos.",
    );

    if (result.ok) {
      setDocuments(result.data);
      setError("");
    } else {
      setDocuments([]);
      setError(result.error);
    }
  }, [vehicleCode]);

  useEffect(() => {
    load();
  }, [load]);

  async function upload(files: FileList | null) {
    if (!files || files.length === 0) return;

    setError("");

    const outcome = await apiUploadMany(
      `vehicles/${vehicleCode}/documents`,
      [...files],
      { kind },
      "Falha ao enviar o documento.",
      (done, total) => setUploading({ done, total }),
      maxUploadSize,
    );

    setUploading(null);

    if (outcome.failures.length > 0) {
      setError(outcome.failures[0].error);
    }

    await load();
  }

  async function reclassify(document: VehicleDocument, newKind: string) {
    const result = await apiSend(
      "PATCH",
      `vehicles/${vehicleCode}/documents/${document.code}/kind`,
      "Falha ao classificar o documento.",
      { kind: Number(newKind) },
    );

    if (!result.ok) {
      setError(result.error);
      return;
    }

    await load();
  }

  async function remove(document: VehicleDocument) {
    setBusy(true);

    const result = await apiSend(
      "DELETE",
      `vehicles/${vehicleCode}/documents/${document.code}`,
      "Falha ao excluir o documento.",
    );

    setBusy(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    setToDelete(null);
    await load();
  }

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm text-[var(--text-secondary)]">
            {documents === null
              ? "Carregando…"
              : `${documents.length} ${documents.length === 1 ? "documento" : "documentos"}`}
          </p>
          <p className="mt-0.5 text-xs text-[var(--text-muted)]">
            PDF, JPG ou PNG. Cada link vale quinze minutos, e depois disso é preciso atualizar
            a lista.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={load}
            aria-label="Atualizar os links"
            title="Atualizar os links"
            className="grid h-9 w-9 place-items-center rounded-md border border-[var(--border)] text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)]"
          >
            <RefreshCw size={15} />
          </button>

          <select
            value={kind}
            onChange={(event) => setKind(event.target.value)}
            aria-label="Que documento é"
            className="rounded-md border border-[var(--border)] bg-[var(--canvas)] px-3 py-2 text-sm"
          >
            {Object.entries(VEHICLE_DOCUMENT_KIND_LABEL).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>

          <label className="inline-flex cursor-pointer items-center gap-2 rounded-md bg-[var(--primary)] px-3.5 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]">
            <FileUp size={16} />
            {uploading ? `Enviando ${uploading.done}/${uploading.total}…` : "Enviar documento"}
            <input
              type="file"
              multiple
              accept="application/pdf,image/jpeg,image/png"
              className="sr-only"
              disabled={uploading !== null}
              onChange={(event) => {
                upload(event.target.files);
                event.target.value = "";
              }}
            />
          </label>
        </div>
      </div>

      <PageError message={error} />

      {documents !== null && documents.length === 0 ? (
        <Empty title="Nenhum documento guardado neste carro." />
      ) : (
        <div className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow)]">
          <ul>
            {(documents ?? []).map((document) => (
              <li
                key={document.code}
                className="flex flex-wrap items-center gap-3 border-b border-[var(--border)] px-4 py-3 last:border-0"
              >
                <FileText size={18} className="shrink-0 text-[var(--text-muted)]" />

                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium">{document.fileName}</p>
                  <p className="num mt-0.5 text-xs text-[var(--text-secondary)]">
                    {formatBytes(document.sizeInBytes)}
                    <span className="font-sans"> · enviado em </span>
                    {formatMoment(document.uploadedAt)}
                  </p>
                </div>

                <select
                  value={String(document.kind)}
                  onChange={(event) => reclassify(document, event.target.value)}
                  aria-label={`Classificação de ${document.fileName}`}
                  className="rounded-md border border-[var(--border)] bg-[var(--canvas)] px-2 py-1.5 text-xs"
                >
                  {Object.entries(VEHICLE_DOCUMENT_KIND_LABEL).map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>

                <div className="flex items-center gap-1">
                  <a
                    href={document.url}
                    target="_blank"
                    rel="noopener noreferrer"
                    aria-label={`Abrir ${document.fileName}`}
                    title="Abrir"
                    className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] transition hover:bg-[var(--surface-2)] hover:text-[var(--primary)]"
                  >
                    <Download size={15} />
                  </a>

                  <button
                    type="button"
                    onClick={() => setToDelete(document)}
                    aria-label={`Excluir ${document.fileName}`}
                    className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] transition hover:bg-[var(--surface-2)] hover:text-[var(--critical)]"
                  >
                    <Trash2 size={15} />
                  </button>
                </div>
              </li>
            ))}
          </ul>
        </div>
      )}

      {toDelete && (
        <Confirmation
          title="Excluir documento"
          message={
            <>
              <strong>{toDelete.fileName}</strong> sai desta lista. O arquivo continua guardado
              no armazenamento, porque documento é prova e pode ser cobrado anos depois — um
              administrador consegue trazer a linha de volta.
            </>
          }
          confirmLabel="Excluir"
          danger
          busy={busy}
          onConfirm={() => remove(toDelete)}
          onCancel={() => setToDelete(null)}
        />
      )}
    </div>
  );
}
