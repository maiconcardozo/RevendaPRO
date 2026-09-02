"use client";

import { useEffect, useRef, useState } from "react";
import { Check, Pencil, Plus, Trash2 } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { Field } from "@/components/common/Field";
import { Modal } from "@/components/common/Modal";
import { Select } from "@/components/common/Select";
import { TextArea } from "@/components/common/TextArea";
import { apiGet, apiSend } from "@/lib/api";
import { formatDate, formatMoney, maskMoney, moneyValue } from "@/lib/masks";
import type { ExpenseSuggestion, ExpenseType, VehicleExpense } from "@/lib/types";
import { Empty, PageError } from "./VehicleUi";

type Draft = {
  code: string | null;
  expenseTypeCode: string;
  description: string;
  amount: string;
  date: string;
  notes: string;
  isPaid: boolean;
};

const today = () => new Date().toISOString().slice(0, 10);

/**
 * The expenses of the vehicle, and the entry that has to beat a Word document.
 *
 * Today he types one line into a document. If the form demands a date and a payment state on
 * every entry it becomes **slower than Word** and nobody uses it — that is where RNF-02 dies.
 * The date already says today, the state already says paid: the common path is a description
 * and an amount, and the type rides along with the suggestion.
 */
export function ExpensesPanel({
  vehicleCode,
  types,
  initialExpenses,
  onChanged,
}: {
  vehicleCode: string;
  types: ExpenseType[];
  initialExpenses: VehicleExpense[];
  /** The cost changes with every entry, so the whole sheet reloads. */
  onChanged: () => void;
}) {
  const [expenses, setExpenses] = useState(initialExpenses);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [toDelete, setToDelete] = useState<VehicleExpense | null>(null);
  const [error, setError] = useState("");
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => setExpenses(initialExpenses), [initialExpenses]);

  async function reload() {
    const result = await apiGet<VehicleExpense[]>(
      `vehicles/${vehicleCode}/expenses`,
      "Falha ao carregar os gastos.",
    );

    if (result.ok) setExpenses(result.data);
    else setError(result.error);
  }

  async function save() {
    if (!draft) return;

    if (!draft.expenseTypeCode) {
      setFormError("Escolha o tipo de gasto.");
      return;
    }

    if (!draft.description.trim()) {
      setFormError("Informe a descrição do gasto.");
      return;
    }

    if (moneyValue(draft.amount) <= 0) {
      setFormError("Informe um valor maior que zero.");
      return;
    }

    setSaving(true);
    setFormError("");

    const isNew = draft.code === null;

    const result = await apiSend(
      isNew ? "POST" : "PUT",
      isNew
        ? `vehicles/${vehicleCode}/expenses`
        : `vehicles/${vehicleCode}/expenses/${draft.code}`,
      "Falha ao salvar o gasto.",
      {
        vehicleCode,
        expenseTypeCode: draft.expenseTypeCode,
        description: draft.description.trim(),
        amount: moneyValue(draft.amount),
        date: draft.date || today(),
        notes: draft.notes.trim() || null,
        isPaid: draft.isPaid,
      },
    );

    setSaving(false);

    if (!result.ok) {
      setFormError(result.error);
      return;
    }

    setDraft(null);
    await reload();
    onChanged();
  }

  async function confirmPayment(expense: VehicleExpense) {
    setBusy(true);

    const result = await apiSend(
      "PATCH",
      `vehicles/${vehicleCode}/expenses/${expense.code}/payment`,
      "Falha ao confirmar o pagamento.",
    );

    setBusy(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    await reload();
    onChanged();
  }

  async function remove(expense: VehicleExpense) {
    setBusy(true);

    const result = await apiSend(
      "DELETE",
      `vehicles/${vehicleCode}/expenses/${expense.code}`,
      "Falha ao excluir o gasto.",
    );

    setBusy(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    setToDelete(null);
    await reload();
    onChanged();
  }

  const paid = expenses.filter((e) => e.isPaid).reduce((t, e) => t + e.amount, 0);
  const planned = expenses.filter((e) => !e.isPaid).reduce((t, e) => t + e.amount, 0);

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-[var(--text-secondary)]">
          <span className="num font-semibold text-[var(--text-primary)]">
            {expenses.length}
          </span>{" "}
          {expenses.length === 1 ? "lançamento" : "lançamentos"} ·{" "}
          <span className="num font-semibold text-[var(--text-primary)]">
            {formatMoney(paid)}
          </span>{" "}
          pagos
          {planned > 0 && (
            <>
              {" · "}
              <span className="num font-semibold text-[var(--warning)]">
                {formatMoney(planned)}
              </span>{" "}
              previstos
            </>
          )}
        </p>

        <button
          type="button"
          onClick={() => {
            setFormError("");
            setDraft({
              code: null,
              expenseTypeCode: "",
              description: "",
              amount: "",
              date: today(),
              notes: "",
              isPaid: true,
            });
          }}
          className="inline-flex items-center gap-2 rounded-md bg-[var(--primary)] px-3.5 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]"
        >
          <Plus size={16} />
          Lançar gasto
        </button>
      </div>

      <PageError message={error} />

      {expenses.length === 0 ? (
        <Empty title="Nenhum gasto lançado neste carro." />
      ) : (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow)]">
          <table className="w-full min-w-[30rem] text-left text-sm">
            <thead className="border-b border-[var(--border)] bg-[var(--surface-2)]">
              <tr>
                <th className="px-4 py-3 font-semibold">Descrição</th>
                <th className="hidden px-4 py-3 font-semibold sm:table-cell">Tipo</th>
                <th className="hidden px-4 py-3 font-semibold md:table-cell">Data</th>
                <th className="px-4 py-3 text-right font-semibold">Valor</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody>
              {expenses.map((expense) => (
                <tr key={expense.code} className="border-b border-[var(--border)] last:border-0">
                  <td className="px-4 py-3">
                    <span className="font-medium">{expense.description}</span>
                    {!expense.isPaid && (
                      <span className="ml-2 rounded-full bg-[color-mix(in_srgb,var(--flare)_20%,transparent)] px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-[var(--warning)]">
                        Previsto
                      </span>
                    )}
                    {expense.notes && (
                      <span className="mt-0.5 block text-xs text-[var(--text-muted)]">
                        {expense.notes}
                      </span>
                    )}
                    <span className="mt-0.5 block text-xs text-[var(--text-secondary)] sm:hidden">
                      {expense.expenseTypeName} · {formatDate(expense.date)}
                    </span>
                  </td>
                  <td className="hidden px-4 py-3 text-[var(--text-secondary)] sm:table-cell">
                    {expense.expenseTypeName}
                  </td>
                  <td className="num hidden px-4 py-3 text-[var(--text-secondary)] md:table-cell">
                    {formatDate(expense.date)}
                  </td>
                  <td className="num px-4 py-3 text-right font-semibold">
                    {formatMoney(expense.amount)}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end gap-1">
                      {!expense.isPaid && (
                        <button
                          type="button"
                          onClick={() => confirmPayment(expense)}
                          disabled={busy}
                          aria-label={`Marcar ${expense.description} como pago`}
                          title="Marcar como pago"
                          className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--success)] disabled:opacity-40"
                        >
                          <Check size={15} />
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={() => {
                          setFormError("");
                          setDraft({
                            code: expense.code,
                            expenseTypeCode: expense.expenseTypeCode,
                            description: expense.description,
                            amount: maskMoney(String(Math.round(expense.amount * 100))),
                            date: expense.date.slice(0, 10),
                            notes: expense.notes ?? "",
                            isPaid: expense.isPaid,
                          });
                        }}
                        aria-label={`Editar ${expense.description}`}
                        className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--primary)]"
                      >
                        <Pencil size={15} />
                      </button>
                      <button
                        type="button"
                        onClick={() => setToDelete(expense)}
                        aria-label={`Excluir ${expense.description}`}
                        className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--critical)]"
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
          title={draft.code ? "Editar gasto" : "Lançar gasto"}
          onClose={() => setDraft(null)}
          error={formError}
          width="max-w-lg"
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
                {saving ? "Salvando..." : "Salvar gasto"}
              </button>
            </>
          }
        >
          <div className="space-y-4">
            <DescriptionField
              value={draft.description}
              onChange={(description) => setDraft({ ...draft, description })}
              onPick={(suggestion) =>
                setDraft({
                  ...draft,
                  description: suggestion.description,

                  // The suggestion brings the type along: that is what makes the second
                  // entry cost two fields instead of four.
                  expenseTypeCode: suggestion.expenseTypeCode,
                })
              }
            />

            <div className="grid gap-4 sm:grid-cols-2">
              <Select
                label="Tipo"
                required
                value={draft.expenseTypeCode}
                onChange={(expenseTypeCode) => setDraft({ ...draft, expenseTypeCode })}
                options={types.map((t) => ({ value: t.code, label: t.name }))}
                placeholder="Escolha"
              />

              <Field
                label="Valor"
                required
                inputMode="decimal"
                mask={maskMoney}
                value={draft.amount}
                onChange={(amount) => setDraft({ ...draft, amount })}
                placeholder="480,00"
                aside={<span className="text-xs text-[var(--text-muted)]">R$</span>}
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <Field
                label="Data"
                type="date"
                value={draft.date}
                onChange={(date) => setDraft({ ...draft, date })}
              />

              <label className="flex items-center gap-2.5 sm:mt-7">
                <input
                  type="checkbox"
                  checked={!draft.isPaid}
                  onChange={(e) => setDraft({ ...draft, isPaid: !e.target.checked })}
                />
                <span className="text-sm">Ainda vou pagar</span>
              </label>
            </div>

            <TextArea
              label="Complemento"
              rows={2}
              value={draft.notes}
              onChange={(notes) => setDraft({ ...draft, notes })}
              placeholder="Comprado na autopeças joãozinho"
            />
          </div>
        </Modal>
      )}

      {toDelete && (
        <Confirmation
          title="Excluir gasto"
          message={
            <>
              Excluir <strong>{toDelete.description}</strong>, de{" "}
              <strong>{formatMoney(toDelete.amount)}</strong>? O custo do veículo cai na hora.
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

/**
 * The description, with the list of what this dealership already wrote.
 *
 * The suggestion comes from the company history and from the keywords on each type, so "lamp"
 * finds "Lampada" and "balanceamento" lands on Alinhamento having never been typed.
 */
function DescriptionField({
  value,
  onChange,
  onPick,
}: {
  value: string;
  onChange: (value: string) => void;
  onPick: (suggestion: ExpenseSuggestion) => void;
}) {
  const [suggestions, setSuggestions] = useState<ExpenseSuggestion[]>([]);
  const [open, setOpen] = useState(false);
  const picked = useRef(false);

  useEffect(() => {
    if (picked.current) {
      picked.current = false;
      return;
    }

    if (value.trim().length < 2) {
      setSuggestions([]);
      return;
    }

    const timer = setTimeout(async () => {
      const result = await apiGet<ExpenseSuggestion[]>(
        `vehicles/expense-suggestions?term=${encodeURIComponent(value.trim())}`,
        "",
      );

      if (result.ok) {
        setSuggestions(result.data);
        setOpen(result.data.length > 0);
      }
    }, 250);

    return () => clearTimeout(timer);
  }, [value]);

  return (
    <div className="relative">
      <Field
        label="Descrição"
        required
        value={value}
        onChange={(next) => {
          onChange(next);
          setOpen(true);
        }}
        placeholder="Parachoque"
        hint="Escolher uma sugestão já preenche o tipo."
      />

      {open && suggestions.length > 0 && (
        <ul className="absolute z-10 mt-1 w-full overflow-hidden rounded-md border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow-lg)]">
          {suggestions.slice(0, 6).map((suggestion) => (
            <li key={`${suggestion.description}-${suggestion.expenseTypeCode}`}>
              <button
                type="button"
                onMouseDown={(event) => {
                  event.preventDefault();
                  picked.current = true;
                  onPick(suggestion);
                  setOpen(false);
                }}
                className="flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm hover:bg-[var(--surface-2)]"
              >
                <span>{suggestion.description}</span>
                <span className="shrink-0 text-xs text-[var(--text-muted)]">
                  {suggestion.expenseTypeName}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
