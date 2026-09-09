"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { ArrowDownRight, ArrowUpRight, Building2, Car, Check, Wallet } from "lucide-react";
import { ExportButtons } from "@/components/common/ExportButtons";
import { Field } from "@/components/common/Field";
import { StoreExpensesView } from "@/components/cashflow/StoreExpensesView";
import { Empty, PageError } from "@/components/vehicles/VehicleUi";
import { apiGet, apiSend } from "@/lib/api";
import { formatDate, formatMoney } from "@/lib/masks";
import {
  CASHFLOW_KIND,
  type Cashflow,
  type CashflowLine,
  type ExpenseType,
  type StoreExpense,
  type Supplier,
} from "@/lib/types";

type Tab = "summary" | "store";

const today = () => new Date().toISOString().slice(0, 10);
const firstOfMonth = () => `${today().slice(0, 7)}-01`;

/**
 * O caixa (M22): o que vence, o que entrou, o que atrasou.
 *
 * Três origens numa tela só — o gasto do carro, a despesa da loja e o que falta receber de cada
 * venda. O que se deve e o que se tem a receber aparecem inteiros, porque "quanto eu devo" é a
 * pergunta de hoje; o período delimita só o que já se moveu.
 */
export function CashflowView({
  initialCashflow,
  initialExpenses,
  initialTypes,
  initialSuppliers,
}: {
  initialCashflow: Cashflow;
  initialExpenses: StoreExpense[];
  initialTypes: ExpenseType[];
  initialSuppliers: Supplier[];
}) {
  const [tab, setTab] = useState<Tab>("summary");
  const [cashflow, setCashflow] = useState(initialCashflow);
  const [from, setFrom] = useState(firstOfMonth());
  const [to, setTo] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  const reload = useCallback(async () => {
    const query = new URLSearchParams();
    if (from) query.set("from", from);
    if (to) query.set("to", to);

    const result = await apiGet<Cashflow>(
      `cashflow${query.size > 0 ? `?${query}` : ""}`,
      "Falha ao carregar o caixa.",
    );

    if (result.ok) setCashflow(result.data);
    else setError(result.error);
  }, [from, to]);

  // Um respiro antes de ir ao banco: quem digita a data escreve dia, mês e ano.
  useEffect(() => {
    const timer = setTimeout(reload, 250);

    return () => clearTimeout(timer);
  }, [reload]);

  async function settle(line: CashflowLine, isPaid: boolean) {
    setBusy(true);
    setError("");

    const result = await apiSend("PATCH", "cashflow/payables", "Falha ao dar baixa.", {
      kind: line.kind,
      code: line.code,
      isPaid,
      paidDate: null,
    });

    setBusy(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    await reload();
  }

  const periodQuery = () => {
    const query = new URLSearchParams();
    if (from) query.set("from", from);
    if (to) query.set("to", to);
    return query.size > 0 ? `?${query}` : "";
  };

  const balance = cashflow.receivedInPeriod - cashflow.paidInPeriod;

  return (
    <div className="dash-anim">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
            Operação
          </p>
          <h1 className="hero-title text-3xl font-bold">Caixa</h1>
          <p className="mt-1 max-w-2xl text-sm text-[var(--text-secondary)]">
            O que vence, o que entrou, o que atrasou. O gasto do carro, a despesa da loja e o que
            falta receber de cada venda, numa lista só — e a baixa em um clique.
          </p>
        </div>

        <div className="flex flex-wrap items-end gap-3">
          <div className="grid max-w-sm grid-cols-2 gap-3">
            <Field label="De" type="date" value={from} onChange={setFrom} />
            <Field label="Até" type="date" value={to} onChange={setTo} placeholder="Hoje" />
          </div>
          <div className="pb-1">
            <ExportButtons path={`exports/cashflow${periodQuery()}`} name="Caixa" onError={setError} />
          </div>
        </div>
      </div>

      <PageError message={error} />

      <div className="mb-6 flex gap-1 overflow-x-auto border-b border-[var(--border)] [scrollbar-width:none]">
        {(
          [
            ["summary", "Resumo", Wallet],
            ["store", "Despesas da loja", Building2],
          ] as const
        ).map(([key, label, Icon]) => (
          <button
            key={key}
            type="button"
            onClick={() => setTab(key)}
            aria-current={tab === key ? "page" : undefined}
            className={[
              "inline-flex shrink-0 items-center gap-2 whitespace-nowrap border-b-2 px-3.5 py-2.5 text-sm font-semibold transition",
              tab === key
                ? "border-[var(--primary)] text-[var(--primary)]"
                : "border-transparent text-[var(--text-secondary)] hover:text-[var(--text-primary)]",
            ].join(" ")}
          >
            <Icon size={15} />
            {label}
          </button>
        ))}
      </div>

      {tab === "store" ? (
        <StoreExpensesView
          initialExpenses={initialExpenses}
          initialTypes={initialTypes}
          initialSuppliers={initialSuppliers}
          from={from}
          to={to}
          onChanged={reload}
        />
      ) : (
        <>
          <div className="mb-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <Card
              label="A pagar"
              value={formatMoney(cashflow.payableOpen)}
              hint={
                cashflow.payableDueSoon > 0
                  ? `${formatMoney(cashflow.payableDueSoon)} vence nos próximos 7 dias`
                  : "Nada vencendo nesta semana"
              }
              tone="var(--warning)"
            />
            <Card
              label="Vencido"
              value={formatMoney(cashflow.payableOverdue)}
              hint={cashflow.payableOverdue > 0 ? "Passou do prazo e continua em aberto" : "Tudo em dia"}
              tone={cashflow.payableOverdue > 0 ? "var(--critical)" : undefined}
            />
            <Card
              label="A receber"
              value={formatMoney(cashflow.receivableOpen)}
              hint={
                cashflow.receivableOverdue > 0
                  ? `${formatMoney(cashflow.receivableOverdue)} passou do prazo`
                  : "Das vendas com saldo"
              }
              tone="var(--success)"
            />
            <Card
              label="No período"
              value={formatMoney(balance)}
              hint={`Entrou ${formatMoney(cashflow.receivedInPeriod)} · saiu ${formatMoney(cashflow.paidInPeriod)}`}
              tone={balance >= 0 ? "var(--success)" : "var(--critical)"}
            />
          </div>

          <Section
            title="O que a revenda deve"
            icon={<ArrowUpRight size={14} />}
            empty="Conta nenhuma em aberto. O caixa está limpo."
            lines={cashflow.payables}
            action={(line) => (
              <button
                type="button"
                onClick={() => settle(line, true)}
                disabled={busy}
                aria-label={`Marcar ${line.description} como paga`}
                title="Marcar como paga"
                className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--success)] disabled:opacity-40"
              >
                <Check size={15} />
              </button>
            )}
          />

          <Section
            title="O que a revenda tem a receber"
            icon={<ArrowDownRight size={14} />}
            empty="Venda nenhuma com saldo. Tudo recebido."
            lines={cashflow.receivables}
            action={(line) =>
              line.vehicleCode ? (
                <Link
                  href={`/vehicles/${line.vehicleCode}`}
                  title="Abrir o carro para registrar a entrada"
                  className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--primary)]"
                >
                  <Car size={15} />
                </Link>
              ) : null
            }
          />

          {cashflow.paidInPeriod > 0 && (
            <p className="mt-4 text-xs text-[var(--text-muted)]">
              Para desfazer uma baixa, abra a despesa em <span className="font-semibold">Despesas da loja</span>,
              ou o gasto na ficha do carro.
            </p>
          )}
        </>
      )}
    </div>
  );
}

function Card({
  label,
  value,
  hint,
  tone,
}: {
  label: string;
  value: string;
  hint: string;
  tone?: string;
}) {
  return (
    <section className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]">
      <p className="text-[11px] font-semibold uppercase tracking-wide text-[var(--text-muted)]">{label}</p>
      <p className="num mt-1 text-2xl font-bold" style={{ color: tone }}>
        {value}
      </p>
      <p className="mt-1 text-xs text-[var(--text-secondary)]">{hint}</p>
    </section>
  );
}

function Section({
  title,
  icon,
  empty,
  lines,
  action,
}: {
  title: string;
  icon: React.ReactNode;
  empty: string;
  lines: CashflowLine[];
  action: (line: CashflowLine) => React.ReactNode;
}) {
  return (
    <section className="mb-6">
      <h2 className="font-display mb-2 flex items-center gap-2 text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
        {icon}
        {title}
        <span className="text-[var(--text-muted)]">
          {lines.length === 1 ? "1 conta" : `${lines.length} contas`}
        </span>
      </h2>

      {lines.length === 0 ? (
        <Empty title={empty} />
      ) : (
        <ul className="divide-y divide-[var(--border)] rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow)]">
          {lines.map((line) => (
            <li key={`${line.kind}-${line.code}`} className="flex items-center gap-3 px-4 py-3 text-sm">
              <span
                className="grid h-8 w-8 shrink-0 place-items-center rounded-md bg-[var(--surface-2)]"
                style={{ color: line.isOverdue ? "var(--critical)" : "var(--signal)" }}
                title={line.kind === CASHFLOW_KIND.storeExpense ? "Despesa da loja" : "Do carro"}
              >
                {line.kind === CASHFLOW_KIND.storeExpense ? <Building2 size={15} /> : <Car size={15} />}
              </span>

              <span className="min-w-0 flex-1">
                <span className="block truncate font-medium">
                  {line.description}
                  {line.isOverdue && (
                    <span className="ml-2 rounded-full bg-[color-mix(in_srgb,var(--critical)_16%,transparent)] px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-[var(--critical)]">
                      Vencido
                    </span>
                  )}
                </span>
                <span className="mt-0.5 block truncate text-xs text-[var(--text-muted)]">
                  {[line.party, line.category, line.plate].filter(Boolean).join(" · ") || "Sem detalhe"}
                </span>
              </span>

              <span className="num shrink-0 text-right">
                <span className="block font-semibold">{formatMoney(line.amount)}</span>
                <span
                  className="block text-xs"
                  style={{ color: line.isOverdue ? "var(--critical)" : "var(--text-muted)" }}
                >
                  {line.dueDate ? `vence ${formatDate(line.dueDate)}` : "sem prazo"}
                </span>
              </span>

              <span className="shrink-0">{action(line)}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
