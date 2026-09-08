"use client";

import { useCallback, useEffect, useState } from "react";
import { Check, Pencil, Plus, Trash2, Undo2 } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { Field } from "@/components/common/Field";
import { Modal } from "@/components/common/Modal";
import { Select } from "@/components/common/Select";
import { TextArea } from "@/components/common/TextArea";
import { Empty, PageError } from "@/components/vehicles/VehicleUi";
import { apiGet, apiSend } from "@/lib/api";
import { formatDate, formatMoney, maskMoney, moneyValue } from "@/lib/masks";
import type { ExpenseType, StoreExpense, Supplier } from "@/lib/types";

type Draft = {
  code: string | null;
  description: string;
  expenseTypeCode: string;
  supplierCode: string;
  amount: string;
  date: string;
  dueDate: string;
  isPaid: boolean;
  notes: string;
};

const today = () => new Date().toISOString().slice(0, 10);

/** O primeiro dia do mês, que é onde o período começa. */
const firstOfMonth = () => `${today().slice(0, 7)}-01`;

/**
 * O que a loja paga e que jamais pertence a um carro: aluguel, energia, salário, imposto (M22).
 *
 * O gasto do carro entra na ficha dele, e o custo daquele carro cresce. Isto aqui é a outra
 * metade da conta — a que existe mesmo no mês em que a revenda vende carro nenhum.
 */
export function StoreExpensesView({
  initialExpenses,
  initialTypes,
  initialSuppliers,
}: {
  initialExpenses: StoreExpense[];
  /** Só os tipos que servem para a loja: Funilaria jamais aparece no aluguel. */
  initialTypes: ExpenseType[];
  initialSuppliers: Supplier[];
}) {
  const [expenses, setExpenses] = useState(initialExpenses);
  const [types] = useState(initialTypes);
  const [suppliers] = useState(initialSuppliers);
  const [from, setFrom] = useState(firstOfMonth());
  const [to, setTo] = useState("");
  const [draft, setDraft] = useState<Draft | null>(null);
  const [toDelete, setToDelete] = useState<StoreExpense | null>(null);
  const [error, setError] = useState("");
  const [formError, setFormError] = useState("");
  const [deleteError, setDeleteError] = useState("");
  const [saving, setSaving] = useState(false);
  const [busy, setBusy] = useState(false);

  const reload = useCallback(async () => {
    const query = new URLSearchParams();
    if (from) query.set("from", from);
    if (to) query.set("to", to);

    const result = await apiGet<StoreExpense[]>(
      `store-expenses${query.size > 0 ? `?${query}` : ""}`,
      "Falha ao carregar as despesas da loja.",
    );

    if (result.ok) setExpenses(result.data);
    else setError(result.error);
  }, [from, to]);

  useEffect(() => {
    reload();
  }, [reload]);

  async function save() {
    if (!draft) return;

    if (!draft.description.trim()) {
      setFormError("Descreva a despesa.");
      return;
    }

    if (!draft.expenseTypeCode) {
      setFormError("Escolha o tipo da despesa.");
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
      isNew ? "store-expenses" : `store-expenses/${draft.code}`,
      "Falha ao salvar a despesa.",
      {
        description: draft.description.trim(),
        expenseTypeCode: draft.expenseTypeCode,
        supplierCode: draft.supplierCode || null,
        amount: moneyValue(draft.amount),
        date: draft.date || today(),
        dueDate: draft.dueDate || null,
        isPaid: draft.isPaid,
        paidDate: null,
        notes: draft.notes.trim() || null,
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

  async function pay(expense: StoreExpense, isPaid: boolean) {
    setBusy(true);

    const result = await apiSend(
      "PATCH",
      `store-expenses/${expense.code}/payment`,
      "Falha ao dar baixa na despesa.",
      { isPaid, paidDate: null },
    );

    setBusy(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    await reload();
  }

  async function remove(expense: StoreExpense) {
    setBusy(true);
    setDeleteError("");

    const result = await apiSend(
      "DELETE",
      `store-expenses/${expense.code}`,
      "Falha ao excluir a despesa.",
    );

    setBusy(false);

    if (!result.ok) {
      setDeleteError(result.error);
      return;
    }

    setToDelete(null);
    await reload();
  }

  function openNew() {
    setFormError("");
    setDraft({
      code: null,
      description: "",
      expenseTypeCode: types[0]?.code ?? "",
      supplierCode: "",
      amount: "",
      date: today(),
      dueDate: "",
      isPaid: false,
      notes: "",
    });
  }

  function edit(expense: StoreExpense) {
    setFormError("");
    setDraft({
      code: expense.code,
      description: expense.description,
      expenseTypeCode: expense.expenseTypeCode,
      supplierCode: expense.supplierCode ?? "",
      amount: maskMoney(String(Math.round(expense.amount * 100))),
      date: expense.date.slice(0, 10),
      dueDate: expense.dueDate.slice(0, 10),
      isPaid: expense.isPaid,
      notes: expense.notes ?? "",
    });
  }

  const paid = expenses.filter((e) => e.isPaid).reduce((total, e) => total + e.amount, 0);
  const planned = expenses.filter((e) => !e.isPaid).reduce((total, e) => total + e.amount, 0);
  const overdue = expenses.filter((e) => e.isOverdue).reduce((total, e) => total + e.amount, 0);

  return (
    <div className="dash-anim">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
            Operação
          </p>
          <h1 className="hero-title text-3xl font-bold">Caixa</h1>
          <p className="mt-1 max-w-2xl text-sm text-[var(--text-secondary)]">
            O que a loja paga e que jamais pertence a um carro: aluguel, energia, salário,
            imposto. O gasto do carro entra na ficha dele; isto aqui é a outra metade da conta,
            a que existe mesmo no mês em que a revenda vende carro nenhum.
          </p>
        </div>

        <button
          type="button"
          onClick={openNew}
          className="inline-flex items-center gap-2 rounded-md bg-[var(--primary)] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]"
        >
          <Plus size={17} />
          Lançar despesa
        </button>
      </div>

      <PageError message={error} />

      <div className="mb-5 flex flex-wrap items-end gap-4">
        <div className="grid max-w-sm grid-cols-2 gap-3">
          <Field label="De" type="date" value={from} onChange={setFrom} />
          <Field label="Até" type="date" value={to} onChange={setTo} placeholder="Hoje" />
        </div>

        <div className="flex flex-wrap gap-3">
          <Total label="Pago" value={formatMoney(paid)} />
          <Total label="A pagar" value={formatMoney(planned)} tone="var(--warning)" />
          {overdue > 0 && <Total label="Vencido" value={formatMoney(overdue)} tone="var(--critical)" />}
        </div>
      </div>

      {expenses.length === 0 ? (
        <Empty title="Nenhuma despesa da loja no período. Lance a primeira." />
      ) : (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow)]">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[var(--border)] text-left text-xs uppercase tracking-wide text-[var(--text-muted)]">
                <th className="px-4 py-3 font-semibold">Despesa</th>
                <th className="hidden px-4 py-3 font-semibold sm:table-cell">Tipo</th>
                <th className="hidden px-4 py-3 font-semibold md:table-cell">Vencimento</th>
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
                      <span
                        className={[
                          "ml-2 rounded-full px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide",
                          expense.isOverdue
                            ? "bg-[color-mix(in_srgb,var(--critical)_16%,transparent)] text-[var(--critical)]"
                            : "bg-[color-mix(in_srgb,var(--flare)_20%,transparent)] text-[var(--warning)]",
                        ].join(" ")}
                      >
                        {expense.isOverdue ? "Vencido" : "A pagar"}
                      </span>
                    )}
                    {expense.notes && (
                      <span className="mt-0.5 block text-xs text-[var(--text-muted)]">{expense.notes}</span>
                    )}
                    <span className="mt-0.5 block text-xs text-[var(--text-secondary)] sm:hidden">
                      {expense.expenseTypeName}
                      {expense.supplierName && ` · ${expense.supplierName}`} · vence{" "}
                      {formatDate(expense.dueDate)}
                    </span>
                  </td>
                  <td className="hidden px-4 py-3 text-[var(--text-secondary)] sm:table-cell">
                    {expense.expenseTypeName}
                    {expense.supplierName && (
                      <span className="mt-0.5 block text-xs text-[var(--text-muted)]">
                        {expense.supplierName}
                      </span>
                    )}
                  </td>
                  <td className="num hidden px-4 py-3 md:table-cell">
                    <span style={{ color: expense.isOverdue ? "var(--critical)" : undefined }}>
                      {formatDate(expense.dueDate)}
                    </span>
                    {expense.paidDate && (
                      <span className="mt-0.5 block text-xs text-[var(--text-muted)]">
                        pago {formatDate(expense.paidDate)}
                      </span>
                    )}
                  </td>
                  <td className="num px-4 py-3 text-right font-semibold">{formatMoney(expense.amount)}</td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end gap-1">
                      <button
                        type="button"
                        onClick={() => pay(expense, !expense.isPaid)}
                        disabled={busy}
                        aria-label={
                          expense.isPaid
                            ? `Desfazer a baixa de ${expense.description}`
                            : `Marcar ${expense.description} como paga`
                        }
                        title={expense.isPaid ? "Desfazer a baixa" : "Marcar como paga"}
                        className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--success)] disabled:opacity-40"
                      >
                        {expense.isPaid ? <Undo2 size={15} /> : <Check size={15} />}
                      </button>
                      <button
                        type="button"
                        onClick={() => edit(expense)}
                        aria-label={`Editar ${expense.description}`}
                        className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--primary)]"
                      >
                        <Pencil size={15} />
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setDeleteError("");
                          setToDelete(expense);
                        }}
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
          title={draft.code ? "Editar despesa" : "Lançar despesa da loja"}
          onClose={() => setDraft(null)}
          error={formError}
          width="max-w-xl"
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
              label="Descrição"
              required
              value={draft.description}
              onChange={(description) => setDraft({ ...draft, description })}
              placeholder="Aluguel de outubro"
              maxLength={160}
            />

            <div className="grid gap-4 sm:grid-cols-2">
              <Select
                label="Tipo"
                required
                value={draft.expenseTypeCode}
                onChange={(expenseTypeCode) => setDraft({ ...draft, expenseTypeCode })}
                options={types.map((type) => ({ value: type.code, label: type.name }))}
                placeholder="Escolha o tipo"
                hint="A lista traz os tipos da loja. Ajuste em Tipos de gasto."
              />

              <Field
                label="Valor"
                required
                inputMode="decimal"
                mask={maskMoney}
                value={draft.amount}
                onChange={(amount) => setDraft({ ...draft, amount })}
                placeholder="2.400,00"
                aside={<span className="text-xs text-[var(--text-muted)]">R$</span>}
              />
            </div>

            <Select
              label="A quem se paga"
              value={draft.supplierCode}
              onChange={(supplierCode) => setDraft({ ...draft, supplierCode })}
              options={suppliers.map((supplier) => ({ value: supplier.code, label: supplier.name }))}
              placeholder="Sem fornecedor"
              hint="Imposto e taxa ficam sem."
            />

            <div className="grid gap-4 sm:grid-cols-2">
              <Field
                label="Data"
                type="date"
                value={draft.date}
                onChange={(date) => setDraft({ ...draft, date })}
                hint="A que mês a despesa pertence."
              />

              <label className="flex items-center gap-2.5 sm:mt-7">
                <input
                  type="checkbox"
                  checked={draft.isPaid}
                  onChange={(event) => setDraft({ ...draft, isPaid: event.target.checked })}
                />
                <span className="text-sm">Já paguei</span>
              </label>
            </div>

            {!draft.isPaid && (
              <Field
                label="Vence em"
                type="date"
                value={draft.dueDate}
                onChange={(dueDate) => setDraft({ ...draft, dueDate })}
                hint="Em branco, vence na data da despesa."
              />
            )}

            <TextArea
              label="Complemento"
              rows={2}
              value={draft.notes}
              onChange={(notes) => setDraft({ ...draft, notes })}
              placeholder="Referente ao mês de outubro, boleto 3 de 12."
            />
          </div>
        </Modal>
      )}

      {toDelete && (
        <Confirmation
          title={`Excluir ${toDelete.description}?`}
          message="A despesa sai da lista e continua guardada."
          confirmLabel="Excluir"
          busy={busy}
          error={deleteError}
          onCancel={() => setToDelete(null)}
          onConfirm={() => remove(toDelete)}
        />
      )}
    </div>
  );
}

function Total({ label, value, tone }: { label: string; value: string; tone?: string }) {
  return (
    <div className="rounded-lg border border-[var(--border)] bg-[var(--surface)] px-4 py-2">
      <p className="text-[10px] font-semibold uppercase tracking-wide text-[var(--text-muted)]">{label}</p>
      <p className="num text-base font-bold" style={{ color: tone }}>
        {value}
      </p>
    </div>
  );
}
