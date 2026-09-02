"use client";

import { useState } from "react";
import { ArrowDown, ArrowUp, Pencil, Plus, Trash2 } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { Field } from "@/components/common/Field";
import { Modal } from "@/components/common/Modal";
import { TextArea } from "@/components/common/TextArea";
import { apiGet, apiSend } from "@/lib/api";
import type { ExpenseType } from "@/lib/types";
import { Empty, PageError } from "./VehicleUi";

type Draft = { code: string | null; name: string; keywords: string; position: number };

/**
 * The kinds of expense the dealership maintains.
 *
 * A table, and never a fixed list in the code: every operation names things its own way, and
 * whoever finds out they need "Despachante" finds out on a Saturday night, with nobody around
 * to ship a new version.
 *
 * The keywords are what keeps the entry fast. "balanceamento" was never typed by anybody and
 * still lands on Alinhamento, because the word sits on the row of the type — including on the
 * types the dealership creates itself.
 */
export function ExpenseTypesView({ initialTypes }: { initialTypes: ExpenseType[] }) {
  const [types, setTypes] = useState(initialTypes);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [toDelete, setToDelete] = useState<ExpenseType | null>(null);
  const [error, setError] = useState("");
  const [formError, setFormError] = useState("");
  const [deleteError, setDeleteError] = useState("");
  const [saving, setSaving] = useState(false);
  const [busy, setBusy] = useState(false);

  async function reload() {
    const result = await apiGet<ExpenseType[]>(
      "expense-types",
      "Falha ao carregar os tipos de gasto.",
    );

    if (result.ok) setTypes(result.data);
    else setError(result.error);
  }

  async function save() {
    if (!draft) return;

    if (!draft.name.trim()) {
      setFormError("Informe o nome do tipo.");
      return;
    }

    setSaving(true);
    setFormError("");

    const isNew = draft.code === null;

    const result = await apiSend(
      isNew ? "POST" : "PUT",
      isNew ? "expense-types" : `expense-types/${draft.code}`,
      "Falha ao salvar o tipo de gasto.",
      {
        name: draft.name.trim(),
        keywords: draft.keywords.trim() || null,
        position: draft.position,
      },
    );

    setSaving(false);

    if (!result.ok) {
      setFormError(result.error);
      return;
    }

    setDraft(null);
    await reload();
  }

  /** Swaps places with the neighbour, saving both positions. */
  async function swap(index: number, direction: -1 | 1) {
    const other = index + direction;

    if (other < 0 || other >= types.length) return;

    setBusy(true);
    setError("");

    const a = types[index];
    const b = types[other];

    const first = await apiSend(
      "PUT",
      `expense-types/${a.code}`,
      "Falha ao reordenar.",
      { name: a.name, keywords: a.keywords, position: b.position },
    );

    const second = first.ok
      ? await apiSend("PUT", `expense-types/${b.code}`, "Falha ao reordenar.", {
          name: b.name,
          keywords: b.keywords,
          position: a.position,
        })
      : first;

    setBusy(false);

    if (!second.ok) {
      setError(second.error);
    }

    await reload();
  }

  async function remove(type: ExpenseType) {
    setBusy(true);
    setDeleteError("");

    const result = await apiSend(
      "DELETE",
      `expense-types/${type.code}`,
      "Falha ao excluir o tipo de gasto.",
    );

    setBusy(false);

    if (!result.ok) {
      setDeleteError(result.error);
      return;
    }

    setToDelete(null);
    await reload();
  }

  return (
    <div className="dash-anim">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
            Administração
          </p>
          <h1 className="hero-title text-3xl font-bold">Tipos de gasto</h1>
          <p className="mt-1 max-w-2xl text-sm text-[var(--text-secondary)]">
            A lista que aparece ao lançar um gasto. As palavras-chave sugerem o tipo a partir do
            que a pessoa digitou, então vale escrever como a equipe fala.
          </p>
        </div>

        <button
          type="button"
          onClick={() => {
            setFormError("");
            setDraft({
              code: null,
              name: "",
              keywords: "",
              position: (types.at(-1)?.position ?? 0) + 1,
            });
          }}
          className="inline-flex items-center gap-2 rounded-md bg-[var(--primary)] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]"
        >
          <Plus size={17} />
          Novo tipo
        </button>
      </div>

      <PageError message={error} />

      {types.length === 0 ? (
        <Empty title="Nenhum tipo cadastrado." />
      ) : (
        <div className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow)]">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-[var(--border)] bg-[var(--surface-2)]">
              <tr>
                <th className="px-5 py-3 font-semibold">Tipo</th>
                <th className="hidden px-5 py-3 font-semibold md:table-cell">Palavras-chave</th>
                <th className="px-5 py-3 font-semibold">Em uso</th>
                <th className="px-5 py-3" />
              </tr>
            </thead>
            <tbody>
              {types.map((type, index) => (
                <tr key={type.code} className="border-b border-[var(--border)] last:border-0">
                  <td className="px-5 py-3.5 font-medium">{type.name}</td>
                  <td className="hidden px-5 py-3.5 text-[var(--text-secondary)] md:table-cell">
                    {type.keywords || "—"}
                  </td>
                  <td className="num px-5 py-3.5 text-[var(--text-secondary)]">
                    {type.expenseCount}
                  </td>
                  <td className="px-5 py-3.5">
                    <div className="flex justify-end gap-1">
                      <button
                        type="button"
                        onClick={() => swap(index, -1)}
                        disabled={busy || index === 0}
                        aria-label={`Subir ${type.name}`}
                        className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] disabled:opacity-30"
                      >
                        <ArrowUp size={15} />
                      </button>
                      <button
                        type="button"
                        onClick={() => swap(index, 1)}
                        disabled={busy || index === types.length - 1}
                        aria-label={`Descer ${type.name}`}
                        className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] disabled:opacity-30"
                      >
                        <ArrowDown size={15} />
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setFormError("");
                          setDraft({
                            code: type.code,
                            name: type.name,
                            keywords: type.keywords ?? "",
                            position: type.position,
                          });
                        }}
                        aria-label={`Editar ${type.name}`}
                        className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--primary)]"
                      >
                        <Pencil size={15} />
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setDeleteError("");
                          setToDelete(type);
                        }}
                        disabled={type.expenseCount > 0}
                        title={
                          type.expenseCount > 0
                            ? "Troque o tipo destes lançamentos para poder excluir"
                            : undefined
                        }
                        aria-label={`Excluir ${type.name}`}
                        className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--critical)] disabled:cursor-not-allowed disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-[var(--text-secondary)]"
                      >
                        <Trash2 size={15} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {draft && (
        <Modal
          title={draft.code ? "Editar tipo de gasto" : "Novo tipo de gasto"}
          onClose={() => setDraft(null)}
          error={formError}
          width="max-w-md"
          footer={
            <>
              <button
                type="button"
                onClick={() => setDraft(null)}
                className="rounded-md border border-[var(--border)] px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--surface-2)]"
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={save}
                disabled={saving}
                className="rounded-md bg-[var(--primary)] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)] disabled:opacity-50"
              >
                {saving ? "Salvando..." : "Salvar"}
              </button>
            </>
          }
        >
          <div className="space-y-4">
            <Field
              label="Nome"
              required
              value={draft.name}
              onChange={(name) => setDraft({ ...draft, name })}
              placeholder="Funilaria e pintura"
            />

            <TextArea
              label="Palavras-chave"
              rows={2}
              value={draft.keywords}
              onChange={(keywords) => setDraft({ ...draft, keywords })}
              placeholder="lataria, pintura, amassado, polir"
              hint="Separadas por vírgula. Quando alguém digitar uma delas na descrição, este tipo é sugerido."
            />
          </div>
        </Modal>
      )}

      {toDelete && (
        <Confirmation
          title="Excluir tipo de gasto"
          message={
            <>
              Excluir <strong>{toDelete.name}</strong>? Ele sai da lista de escolha ao lançar um
              gasto.
            </>
          }
          confirmLabel="Excluir"
          danger
          busy={busy}
          error={deleteError}
          onConfirm={() => remove(toDelete)}
          onCancel={() => {
            setDeleteError("");
            setToDelete(null);
          }}
        />
      )}
    </div>
  );
}
