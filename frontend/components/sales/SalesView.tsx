"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ArrowLeftRight, HandCoins, TrendingUp, Wallet } from "lucide-react";
import { Field } from "@/components/common/Field";
import { Empty, PageError, Stat } from "@/components/vehicles/VehicleUi";
import { apiGet } from "@/lib/api";
import { formatDate, formatMoney, formatPercent } from "@/lib/masks";
import { PAYMENT_METHOD_LABEL, SALE_CHANNEL, type SaleListing } from "@/lib/types";

/** First day of the current month, so the screen opens on what is happening now. */
function monthStart(): string {
  const now = new Date();

  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`;
}

/**
 * What was sold, and what each sale left (RF-21, RF-23).
 *
 * The period is the only filter: the question this screen answers is "how did the month go",
 * and the answer is the sum at the top with the lines that produced it underneath.
 */
export function SalesView({ initialSales }: { initialSales: SaleListing[] }) {
  const [sales, setSales] = useState(initialSales);
  const [from, setFrom] = useState(monthStart());
  const [to, setTo] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const reload = useCallback(async () => {
    setLoading(true);

    const query = new URLSearchParams();

    if (from) query.set("from", from);
    if (to) query.set("to", to);

    const result = await apiGet<SaleListing[]>(
      `sales${query.size > 0 ? `?${query}` : ""}`,
      "Falha ao carregar as vendas.",
    );

    setLoading(false);

    if (result.ok) {
      setSales(result.data);
      setError("");
    } else {
      setError(result.error);
    }
  }, [from, to]);

  useEffect(() => {
    reload();
  }, [reload]);

  const sold = sales.reduce((t, s) => t + s.amount, 0);
  const profit = sales.reduce((t, s) => t + s.netProfit, 0);

  return (
    <div className="dash-anim">
      <div className="mb-6">
        <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
          Operação
        </p>
        <h1 className="hero-title text-3xl font-bold">Vendas</h1>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          Cada carro que saiu, e quanto sobrou dele.
        </p>
      </div>

      <div className="mb-6 grid gap-4 sm:grid-cols-3">
        <Stat
          label="Vendas no período"
          value={String(sales.length)}
          icon={<HandCoins size={17} className="text-[var(--signal)]" />}
        />
        <Stat
          label="Vendido"
          value={formatMoney(sold)}
          hint="Preço fechado, carro de troca incluído"
          icon={<Wallet size={17} className="text-[var(--signal)]" />}
        />
        <Stat
          label="Sobrou"
          value={formatMoney(profit)}
          hint="Depois da loja, da comissão e do custo"
          icon={<TrendingUp size={17} className="text-[var(--signal)]" />}
        />
      </div>

      <PageError message={error} />

      <div className="mb-4 grid gap-3 sm:grid-cols-[180px_180px_minmax(0,1fr)] sm:items-start">
        <Field label="De" type="date" value={from} onChange={setFrom} />
        <Field label="Até" type="date" value={to} onChange={setTo} placeholder="Hoje" />
        {loading && <p className="pt-7 text-xs text-[var(--text-muted)]">Carregando…</p>}
      </div>

      {sales.length === 0 ? (
        <Empty title="Nenhuma venda neste período." />
      ) : (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow)]">
          <table className="w-full min-w-[40rem] text-left text-sm">
            <thead className="border-b border-[var(--border)] bg-[var(--surface-2)]">
              <tr>
                <th className="px-4 py-3 font-semibold">Data</th>
                <th className="px-4 py-3 font-semibold">Carro</th>
                <th className="hidden px-4 py-3 font-semibold md:table-cell">Comprador</th>
                <th className="px-4 py-3 text-right font-semibold">Vendido</th>
                <th className="hidden px-4 py-3 text-right font-semibold sm:table-cell">Custou</th>
                <th className="px-4 py-3 text-right font-semibold">Sobrou</th>
                <th className="hidden px-4 py-3 text-right font-semibold lg:table-cell">Dias</th>
              </tr>
            </thead>
            <tbody>
              {sales.map((sale) => (
                <tr key={sale.code} className="border-b border-[var(--border)] last:border-0">
                  <td className="num px-4 py-3 text-[var(--text-secondary)]">{formatDate(sale.date)}</td>
                  <td className="px-4 py-3">
                    <Link
                      href={`/vehicles/${sale.vehicleCode}`}
                      className="font-medium hover:text-[var(--primary)] hover:underline"
                    >
                      {sale.name}
                    </Link>
                    <span className="num mt-0.5 block text-xs text-[var(--text-secondary)]">
                      {sale.plate}
                      <span className="font-sans">
                        {" · "}
                        {PAYMENT_METHOD_LABEL[sale.paymentMethod]}
                        {sale.channel === SALE_CHANNEL.partnerStore && ` · ${sale.partnerStoreName}`}
                      </span>
                      {sale.hadTradeIn && (
                        <ArrowLeftRight
                          size={12}
                          className="ml-1.5 inline text-[var(--text-muted)]"
                          aria-label="Com troca"
                        />
                      )}
                    </span>
                  </td>
                  <td className="hidden px-4 py-3 text-[var(--text-secondary)] md:table-cell">
                    {sale.buyerName}
                  </td>
                  <td className="num px-4 py-3 text-right font-semibold">{formatMoney(sale.amount)}</td>
                  <td className="num hidden px-4 py-3 text-right text-[var(--text-secondary)] sm:table-cell">
                    {formatMoney(sale.cost)}
                  </td>
                  <td
                    className="num px-4 py-3 text-right font-semibold"
                    style={{ color: sale.netProfit >= 0 ? "var(--success)" : "var(--critical)" }}
                  >
                    {formatMoney(sale.netProfit)}
                    {sale.margin !== null && (
                      <span className="block text-[11px] font-normal text-[var(--text-muted)]">
                        {formatPercent(sale.margin)}
                      </span>
                    )}
                  </td>
                  <td className="num hidden px-4 py-3 text-right text-[var(--text-secondary)] lg:table-cell">
                    {sale.daysInStock ?? "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
