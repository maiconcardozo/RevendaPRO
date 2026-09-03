"use client";

import { useState } from "react";
import Link from "next/link";
import { ArchiveRestore, ExternalLink } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { apiGet, apiSend } from "@/lib/api";
import { formatBytes, formatMoment } from "@/lib/masks";
import { VEHICLE_DOCUMENT_KIND_LABEL, type DeletedDocument } from "@/lib/types";
import { Empty, PageError } from "./VehicleUi";

/**
 * The documents somebody deleted, and the way back.
 *
 * Deleting a document takes it out of the file of the vehicle and keeps the object in the
 * bucket, by requirement: a dealership answers for what it sold years after selling it. That
 * left files paid for and unreachable — they were there, and nobody could ask for them.
 *
 * There is no button here to erase one for good, and the absence is the design.
 */
export function DeletedDocumentsView({
  initialDocuments,
}: {
  initialDocuments: DeletedDocument[];
}) {
  const [documents, setDocuments] = useState(initialDocuments);
  const [toRestore, setToRestore] = useState<DeletedDocument | null>(null);
  const [error, setError] = useState("");
  const [restoreError, setRestoreError] = useState("");
  const [busy, setBusy] = useState(false);

  async function reload() {
    const result = await apiGet<DeletedDocument[]>(
      "deleted-documents",
      "Falha ao carregar os documentos excluídos.",
    );

    if (result.ok) {
      setDocuments(result.data);
      setError("");
    } else {
      setError(result.error);
    }
  }

  async function restore() {
    if (!toRestore) return;

    setBusy(true);
    setRestoreError("");

    const result = await apiSend(
      "POST",
      `deleted-documents/${toRestore.code}/restore`,
      "Falha ao devolver o documento.",
    );

    setBusy(false);

    if (result.ok) {
      setToRestore(null);
      await reload();
    } else {
      setRestoreError(result.error);
    }
  }

  return (
    <div className="dash-anim">
      <div className="mb-6">
        <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
          Administração
        </p>
        <h1 className="hero-title text-3xl font-bold">Documentos excluídos</h1>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          Todo documento excluído continua guardado. Abra para conferir, e devolva à ficha do
          veículo quando a exclusão tiver sido engano.
        </p>
      </div>

      <PageError message={error} />

      {documents.length === 0 ? (
        <Empty title="Todo documento da revenda está na ficha do seu veículo." />
      ) : (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow)]">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[var(--border)] text-left text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
                <th className="px-4 py-3">Documento</th>
                <th className="px-4 py-3">Veículo</th>
                <th className="px-4 py-3">Excluído em</th>
                <th className="px-4 py-3">Por</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>

            <tbody>
              {documents.map((document) => (
                <tr
                  key={document.code}
                  className="border-b border-[var(--border)] last:border-0"
                >
                  <td className="px-4 py-3">
                    <p className="font-semibold">{document.fileName}</p>
                    <p className="num text-xs text-[var(--text-muted)]">
                      {VEHICLE_DOCUMENT_KIND_LABEL[document.kind]} ·{" "}
                      {formatBytes(document.sizeInBytes)}
                    </p>
                  </td>

                  <td className="px-4 py-3">
                    <Link
                      href={`/vehicles/${document.vehicleCode}`}
                      className="font-semibold text-[var(--primary)] hover:underline"
                    >
                      {document.plate}
                    </Link>
                    <p className="text-xs text-[var(--text-muted)]">
                      {document.brand} {document.model}
                    </p>
                  </td>

                  <td className="num px-4 py-3 text-[var(--text-secondary)]">
                    {formatMoment(document.deletedAt)}
                  </td>

                  <td className="px-4 py-3 text-[var(--text-secondary)]">
                    {document.deletedBy ?? "—"}
                  </td>

                  <td className="px-4 py-3">
                    <div className="flex items-center justify-end gap-2">
                      <a
                        href={document.url}
                        target="_blank"
                        rel="noreferrer"
                        title="Abrir o arquivo"
                        className="inline-flex items-center gap-1.5 rounded-md border border-[var(--border)] px-2.5 py-1.5 text-xs font-semibold text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)]"
                      >
                        <ExternalLink size={14} />
                        Abrir
                      </a>

                      <button
                        type="button"
                        onClick={() => {
                          setRestoreError("");
                          setToRestore(document);
                        }}
                        className="inline-flex items-center gap-1.5 rounded-md bg-[var(--primary)] px-2.5 py-1.5 text-xs font-semibold text-white transition hover:bg-[var(--primary-strong)]"
                      >
                        <ArchiveRestore size={14} />
                        Devolver
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {toRestore && (
        <Confirmation
          title="Devolver o documento"
          message={
            <>
              <span className="font-semibold">{toRestore.fileName}</span> volta para a ficha do{" "}
              {toRestore.brand} {toRestore.model}, placa {toRestore.plate}.
            </>
          }
          confirmLabel="Devolver"
          onConfirm={restore}
          onCancel={() => setToRestore(null)}
          busy={busy}
          error={restoreError}
        />
      )}
    </div>
  );
}
