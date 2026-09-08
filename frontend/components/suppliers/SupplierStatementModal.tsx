"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Modal } from "@/components/common/Modal";
import { apiGet } from "@/lib/api";
import { formatDate, formatMoney } from "@/lib/masks";
import type { Supplier, SupplierStatement } from "@/lib/types";

/**
 * A ficha de um fornecedor: quanto já foi para ele, em que carros, e em quê.
 *
 * É o espaço exclusivo que o stakeholder pediu para "essa questão do gasto". O total conta só
 * o que foi pago; o previsto vem numa linha própria, porque saber que há dois mil reais orçados
 * com a funilaria também interessa — e misturar os dois faria o total subir por uma cotação que
 * talvez nunca vire serviço.
 */
export function SupplierStatementModal({
  supplier,
  from,
  to,
  onClose,
}: {
  supplier: Supplier;
  /** Primeiro dia do período, ou vazio para desde o início. */
  from: string;
  /** Último dia do período, ou vazio para até hoje. */
  to: string;
  onClose: () => void;
}) {
  const [statement, setStatement] = useState<SupplierStatement | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    let alive = true;

    const query = new URLSearchParams();
    if (from) query.set("from", from);
    if (to) query.set("to", to);

    apiGet<SupplierStatement>(
      `suppliers/${supplier.code}/expenses${query.size > 0 ? `?${query}` : ""}`,
      "Falha ao carregar a ficha do fornecedor.",
    ).then((result) => {
      if (!alive) return;

      if (result.ok) setStatement(result.data);
      else setError(result.error);
    });

    return () => {
      alive = false;
    };
  }, [supplier.code, from, to]);

  const mostInAType = Math.max(1, ...(statement?.byType ?? []).map((row) => row.paidTotal));

  return (
    <Modal
      title={supplier.name}
      onClose={onClose}
      error={error}
      width="max-w-3xl"
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
        {supplier.segmentName}
        {from || to ? " · no período escolhido" : " · desde o início"}
      </p>

      {statement === null ? (
        <p className="text-sm text-[var(--text-secondary)]">Carregando...</p>
      ) : (
        <div className="space-y-5">
          <div className="grid gap-3 sm:grid-cols-3">
            <Figure label="Pago" value={formatMoney(statement.paidTotal)} strong />
            <Figure label="Previsto" value={formatMoney(statement.plannedTotal)} />
            <Figure
              label="Gastos"
              value={
                statement.expenses.length === 1 ? "1 gasto" : `${statement.expenses.length} gastos`
              }
            />
          </div>

          {statement.byType.length > 0 && (
            <section>
              <p className="font-display text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
                Em quê
              </p>
              <ul className="mt-3 space-y-2">
                {statement.byType.map((row) => (
                  <li
                    key={row.expenseTypeName}
                    className="grid grid-cols-[140px_minmax(0,1fr)_auto] items-center gap-3 text-sm"
                  >
                    <span className="truncate text-[var(--text-secondary)]">
                      {row.expenseTypeName}
                    </span>
                    <span className="h-2 overflow-hidden rounded-full bg-[var(--surface-2)]">
                      <span
                        className="block h-full rounded-full bg-[var(--signal)]"
                        style={{ width: `${(row.paidTotal / mostInAType) * 100}%` }}
                      />
                    </span>
                    <span className="num text-right">
                      <span className="font-semibold">{formatMoney(row.paidTotal)}</span>
                      <span className="ml-2 text-xs text-[var(--text-muted)]">
                        {row.expenseCount === 1 ? "1 gasto" : `${row.expenseCount} gastos`}
                      </span>
                    </span>
                  </li>
                ))}
              </ul>
            </section>
          )}

          <section>
            <p className="font-display text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
              Em que carros
            </p>

            {statement.expenses.length === 0 ? (
              <p className="mt-3 text-sm text-[var(--text-secondary)]">
                Nenhum gasto com este fornecedor no período.
              </p>
            ) : (
              <div className="mt-3 overflow-x-auto rounded-xl border border-[var(--border)]">
                <table className="w-full text-left text-sm">
                  <thead className="border-b border-[var(--border)] bg-[var(--surface-2)]">
                    <tr>
                      <th className="px-4 py-2.5 font-semibold">Gasto</th>
                      <th className="px-4 py-2.5 font-semibold">Carro</th>
                      <th className="hidden px-4 py-2.5 font-semibold md:table-cell">Data</th>
                      <th className="px-4 py-2.5 text-right font-semibold">Valor</th>
                    </tr>
                  </thead>
                  <tbody>
                    {statement.expenses.map((expense) => (
                      <tr
                        key={expense.code}
                        className="border-b border-[var(--border)] last:border-0"
                      >
                        <td className="px-4 py-2.5">
                          <span className="font-medium">{expense.description}</span>
                          {!expense.isPaid && (
                            <span className="ml-2 rounded-full bg-[color-mix(in_srgb,var(--flare)_20%,transparent)] px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-[var(--warning)]">
                              Previsto
                            </span>
                          )}
                          <span className="mt-0.5 block text-xs text-[var(--text-muted)]">
                            {expense.expenseTypeName}
                            <span className="md:hidden"> · {formatDate(expense.date)}</span>
                          </span>
                        </td>
                        <td className="px-4 py-2.5">
                          <Link
                            href={`/vehicles/${expense.vehicleCode}`}
                            className="font-mono text-xs font-semibold text-[var(--primary)] hover:underline"
                          >
                            {expense.plate}
                          </Link>
                          <span className="mt-0.5 block text-xs text-[var(--text-muted)]">
                            {expense.vehicleName}
                          </span>
                        </td>
                        <td className="num hidden px-4 py-2.5 text-[var(--text-secondary)] md:table-cell">
                          {formatDate(expense.date)}
                        </td>
                        <td className="num px-4 py-2.5 text-right font-semibold">
                          {formatMoney(expense.amount)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      )}
    </Modal>
  );
}

function Figure({ label, value, strong = false }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className="rounded-xl border border-[var(--border)] bg-[var(--surface-2)] px-4 py-3">
      <p className="text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">{label}</p>
      <p className={`num mt-1 text-lg font-bold ${strong ? "text-[var(--signal)]" : ""}`}>{value}</p>
    </div>
  );
}
