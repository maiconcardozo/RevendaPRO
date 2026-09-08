"use client";

import { useState } from "react";
import { Check, Pencil, Plus, Trash2, X } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { Modal } from "@/components/common/Modal";
import { apiSend } from "@/lib/api";
import type { SupplierSegment } from "@/lib/types";

/**
 * O cadastro de ramos, dentro da tela de fornecedores.
 *
 * Mora aqui, e não numa tela própria, porque quem mexe em ramo é quem mexe em fornecedor — uma
 * tela "Ramos" seria uma permissão a mais para uma coisa só. A revenda nasce com a lista farta,
 * então o uso comum deste modal é renomear um ramo ou acrescentar o que faltou.
 */
export function SegmentsModal({
  segments,
  onClose,
  onChanged,
}: {
  segments: SupplierSegment[];
  onClose: () => void;
  /** Chamado depois de cada gravação, para a tela recarregar a lista. */
  onChanged: () => Promise<void>;
}) {
  const [editing, setEditing] = useState<{ code: string | null; name: string } | null>(null);
  const [toDelete, setToDelete] = useState<SupplierSegment | null>(null);
  const [error, setError] = useState("");
  const [deleteError, setDeleteError] = useState("");
  const [busy, setBusy] = useState(false);

  async function save() {
    if (!editing) return;

    if (!editing.name.trim()) {
      setError("Informe o nome do ramo.");
      return;
    }

    setBusy(true);
    setError("");

    const isNew = editing.code === null;
    const position = isNew
      ? (segments.at(-1)?.position ?? 0) + 1
      : (segments.find((segment) => segment.code === editing.code)?.position ?? 0);

    const result = await apiSend(
      isNew ? "POST" : "PUT",
      isNew ? "supplier-segments" : `supplier-segments/${editing.code}`,
      "Falha ao salvar o ramo.",
      { name: editing.name.trim(), position },
    );

    setBusy(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    setEditing(null);
    await onChanged();
  }

  async function remove(segment: SupplierSegment) {
    setBusy(true);
    setDeleteError("");

    const result = await apiSend(
      "DELETE",
      `supplier-segments/${segment.code}`,
      "Falha ao excluir o ramo.",
    );

    setBusy(false);

    if (!result.ok) {
      setDeleteError(result.error);
      return;
    }

    setToDelete(null);
    await onChanged();
  }

  return (
    <>
      <Modal
        title="Ramos de fornecedor"
        onClose={onClose}
        error={error}
        width="max-w-xl"
        footer={
          <button
            type="button"
            onClick={onClose}
            className="rounded-md border border-[var(--border)] px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--surface-2)]"
          >
            Fechar
          </button>
        }
      >
        <p className="mb-4 text-sm text-[var(--text-secondary)]">
          O ramo diz o que o fornecedor faz. A lista já vem pronta, e você renomeia ou acrescenta o
          que faltar. Um ramo com fornecedor dentro fica no cadastro.
        </p>

        <ul className="divide-y divide-[var(--border)] rounded-xl border border-[var(--border)]">
          {segments.map((segment) => (
            <li key={segment.code} className="flex items-center gap-2 px-4 py-2.5 text-sm">
              {editing?.code === segment.code ? (
                <InlineEditor
                  value={editing.name}
                  busy={busy}
                  onChange={(name) => setEditing({ ...editing, name })}
                  onSave={save}
                  onCancel={() => setEditing(null)}
                />
              ) : (
                <>
                  <span className="min-w-0 flex-1 truncate font-medium">{segment.name}</span>
                  <span className="num shrink-0 text-xs text-[var(--text-secondary)]">
                    {segment.supplierCount === 1
                      ? "1 fornecedor"
                      : `${segment.supplierCount} fornecedores`}
                  </span>
                  <button
                    type="button"
                    onClick={() => {
                      setError("");
                      setEditing({ code: segment.code, name: segment.name });
                    }}
                    aria-label={`Renomear ${segment.name}`}
                    className="grid h-8 w-8 shrink-0 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--primary)]"
                  >
                    <Pencil size={15} />
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setDeleteError("");
                      setToDelete(segment);
                    }}
                    disabled={segment.supplierCount > 0}
                    title={
                      segment.supplierCount > 0
                        ? "Mude o ramo destes fornecedores para poder excluir"
                        : undefined
                    }
                    aria-label={`Excluir ${segment.name}`}
                    className="grid h-8 w-8 shrink-0 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--critical)] disabled:cursor-not-allowed disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-[var(--text-secondary)]"
                  >
                    <Trash2 size={15} />
                  </button>
                </>
              )}
            </li>
          ))}

          <li className="px-4 py-2.5 text-sm">
            {editing?.code === null ? (
              <InlineEditor
                value={editing.name}
                busy={busy}
                placeholder="Nome do ramo"
                onChange={(name) => setEditing({ code: null, name })}
                onSave={save}
                onCancel={() => setEditing(null)}
              />
            ) : (
              <button
                type="button"
                onClick={() => {
                  setError("");
                  setEditing({ code: null, name: "" });
                }}
                className="inline-flex items-center gap-2 text-sm font-semibold text-[var(--primary)] hover:underline"
              >
                <Plus size={15} />
                Novo ramo
              </button>
            )}
          </li>
        </ul>
      </Modal>

      {toDelete && (
        <Confirmation
          title={`Excluir ${toDelete.name}?`}
          message="O ramo sai da lista e continua guardado. Um ramo do catálogo inicial volta na próxima subida do sistema."
          confirmLabel="Excluir"
          busy={busy}
          error={deleteError}
          onCancel={() => setToDelete(null)}
          onConfirm={() => remove(toDelete)}
        />
      )}
    </>
  );
}

function InlineEditor({
  value,
  busy,
  placeholder,
  onChange,
  onSave,
  onCancel,
}: {
  value: string;
  busy: boolean;
  placeholder?: string;
  onChange: (value: string) => void;
  onSave: () => void;
  onCancel: () => void;
}) {
  return (
    <div className="flex w-full items-center gap-2">
      <input
        autoFocus
        value={value}
        placeholder={placeholder}
        maxLength={80}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === "Enter") onSave();
          if (event.key === "Escape") onCancel();
        }}
        className="control min-w-0 flex-1"
      />
      <button
        type="button"
        onClick={onSave}
        disabled={busy}
        aria-label="Salvar"
        className="grid h-8 w-8 shrink-0 place-items-center rounded-md bg-[var(--primary)] text-white disabled:opacity-50"
      >
        <Check size={15} />
      </button>
      <button
        type="button"
        onClick={onCancel}
        aria-label="Cancelar"
        className="grid h-8 w-8 shrink-0 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)]"
      >
        <X size={15} />
      </button>
    </div>
  );
}
