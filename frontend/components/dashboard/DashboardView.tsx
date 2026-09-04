"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { Car, Clock, HandCoins, MapPin, TrendingUp, Wallet } from "lucide-react";
import { Field } from "@/components/common/Field";
import { Empty, PageError, Stat, StatusPill } from "@/components/vehicles/VehicleUi";
import { apiGet } from "@/lib/api";
import { formatDate, formatDays, formatMoney, formatPercent } from "@/lib/masks";
import {
  VEHICLE_STATUS_LABEL,
  YARD_KIND_LABEL,
  type Dashboard,
  type RankedVehicle,
} from "@/lib/types";

/** First day of the current month, so the realized side opens on what is happening now. */
function monthStart(): string {
  const now = new Date();

  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`;
}

/**
 * The operation in one screen (RF-23, RF-24).
 *
 * Two halves. The yard as it is now: money parked, how many cars in each step, and what they
 * promise. And the period: what was sold and what was left. The period only touches the
 * second half — the yard is always today's.
 */
export function DashboardView({
  firstName,
  initial,
}: {
  firstName: string;
  initial: Dashboard;
}) {
  const [data, setData] = useState(initial);
  const [from, setFrom] = useState(monthStart());
  const [to, setTo] = useState("");
  const [error, setError] = useState("");

  const reload = useCallback(async () => {
    const query = new URLSearchParams();

    if (from) query.set("from", from);
    if (to) query.set("to", to);

    const result = await apiGet<Dashboard>(
      `dashboard${query.size > 0 ? `?${query}` : ""}`,
      "Falha ao carregar o painel.",
    );

    if (result.ok) {
      setData(result.data);
      setError("");
    } else {
      setError(result.error);
    }
  }, [from, to]);

  useEffect(() => {
    reload();
  }, [reload]);

  const totalCars = data.byStatus.reduce((t, s) => t + s.count, 0);
  const mostInAStatus = Math.max(1, ...data.byStatus.map((s) => s.count));

  return (
    <div className="dash-anim">
      <div className="mb-6">
        <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
          Visão geral
        </p>
        <h1 className="hero-title text-3xl font-bold sm:text-4xl">Olá, {firstName}.</h1>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          O pátio como está agora, e o que o período rendeu.
        </p>
      </div>

      <PageError message={error} />

      <div className="mb-6 grid gap-4 sm:grid-cols-3">
        <Stat
          label="No pátio"
          value={String(data.inStock)}
          hint={`${totalCars} no total, vendidos incluídos`}
          icon={<Car size={17} className="text-[var(--signal)]" />}
        />
        <Stat
          label="Capital parado"
          value={formatMoney(data.invested)}
          hint="Compra mais gastos dos carros sem venda"
          icon={<Wallet size={17} className="text-[var(--signal)]" />}
        />
        <Stat
          label="Lucro projetado"
          value={formatMoney(data.projectedProfit)}
          hint="Se cada carro sair pelo preço desejado"
          icon={<TrendingUp size={17} className="text-[var(--signal)]" />}
        />
      </div>

      <section className="mb-6 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]">
        <p className="font-display text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
          Por etapa
        </p>

        {data.byStatus.length === 0 ? (
          <p className="mt-3 text-sm text-[var(--text-secondary)]">O pátio está vazio.</p>
        ) : (
          <ul className="mt-4 space-y-2.5">
            {data.byStatus.map((row) => (
              <li key={row.status} className="grid grid-cols-[130px_minmax(0,1fr)_auto] items-center gap-3 text-sm">
                <span className="truncate text-[var(--text-secondary)]">
                  {VEHICLE_STATUS_LABEL[row.status]}
                </span>
                <span className="h-2 overflow-hidden rounded-full bg-[var(--surface-2)]">
                  <span
                    className="block h-full rounded-full bg-[var(--signal)]"
                    style={{ width: `${(row.count / mostInAStatus) * 100}%` }}
                  />
                </span>
                <span className="num text-right">
                  <span className="font-semibold">{row.count}</span>
                  <span className="ml-2 text-xs text-[var(--text-muted)]">{formatMoney(row.cost)}</span>
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      {data.byYard.length > 0 && (
        <section className="mb-6 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]">
          <p className="font-display text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
            Por pátio
          </p>

          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Quanto está parado em cada lugar. Os números do topo continuam somando tudo.
          </p>

          <ul className="mt-4 space-y-2.5">
            {data.byYard.map((row) => (
              <li
                key={row.code ?? "sem-patio"}
                className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-3 border-b border-[var(--border)] pb-2.5 text-sm last:border-0 last:pb-0"
              >
                <span className="min-w-0">
                  <span className="flex items-center gap-1.5 truncate font-medium">
                    <MapPin size={14} className="shrink-0 text-[var(--signal)]" />
                    {row.name}
                  </span>
                  <span className="mt-0.5 block text-xs text-[var(--text-muted)]">
                    {row.count === 1 ? "1 carro" : `${row.count} carros`}
                    {row.kind !== null && ` · ${YARD_KIND_LABEL[row.kind]}`}
                    {row.averageDaysInStock !== null &&
                      ` · ${formatDays(row.averageDaysInStock)} em média`}
                  </span>
                </span>
                <span className="num text-right font-semibold">{formatMoney(row.invested)}</span>
              </li>
            ))}
          </ul>
        </section>
      )}

      <div className="mb-4">
        <p className="font-display text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
          O período
        </p>
        <div className="mt-2 grid max-w-sm grid-cols-2 gap-3">
          <Field label="De" type="date" value={from} onChange={setFrom} />
          <Field label="Até" type="date" value={to} onChange={setTo} placeholder="Hoje" />
        </div>
      </div>

      <div className="mb-6 grid gap-4 sm:grid-cols-3">
        <Stat
          label="Vendas"
          value={String(data.salesInPeriod)}
          hint={data.salesInPeriod > 0 ? `${formatMoney(data.soldInPeriod)} vendidos` : "Nenhuma no período"}
          icon={<HandCoins size={17} className="text-[var(--signal)]" />}
        />
        <Stat
          label="Lucro realizado"
          value={formatMoney(data.realizedProfit)}
          hint="Depois da loja, da comissão e do custo"
          icon={<TrendingUp size={17} className="text-[var(--success)]" />}
        />
        <Stat
          label="Dias para vender"
          value={data.averageDaysToSell === null ? "—" : `${data.averageDaysToSell}`}
          hint="Média entre a compra e a venda"
          icon={<Clock size={17} className="text-[var(--signal)]" />}
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <Ranking
          title="Mais dinheiro parado"
          rows={data.biggestInvestments}
          value={(v) => formatMoney(v.cost)}
        />
        <Ranking
          title="Maior sobra prometida"
          rows={data.biggestMargins}
          value={(v) => formatMoney(v.projectedProfit)}
          tone="var(--success)"
        />
        <Ranking
          title="Mais tempo parado"
          rows={data.longestInStock}
          value={(v) => formatDays(v.daysInStock)}
        />
      </div>

      <section className="mt-6 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]">
        <div className="mb-3 flex items-baseline justify-between gap-3">
          <p className="font-display text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
            Últimas vendas
          </p>
          <Link href="/sales" className="text-xs font-semibold text-[var(--primary)] hover:underline">
            Ver todas
          </Link>
        </div>

        {data.recentSales.length === 0 ? (
          <Empty title="Nenhuma venda no período." />
        ) : (
          <ul className="divide-y divide-[var(--border)]">
            {data.recentSales.map((sale) => (
              <li key={sale.code} className="flex items-center justify-between gap-3 py-2.5 text-sm">
                <div className="min-w-0">
                  <Link
                    href={`/vehicles/${sale.vehicleCode}`}
                    className="font-medium hover:text-[var(--primary)] hover:underline"
                  >
                    {sale.name}
                  </Link>
                  <span className="num block text-xs text-[var(--text-secondary)]">
                    {sale.plate}
                    <span className="font-sans"> · {sale.buyerName} · </span>
                    {formatDate(sale.date)}
                  </span>
                </div>
                <div className="text-right">
                  <span className="num block font-semibold">{formatMoney(sale.amount)}</span>
                  <span
                    className="num block text-xs font-semibold"
                    style={{ color: sale.netProfit >= 0 ? "var(--success)" : "var(--critical)" }}
                  >
                    sobrou {formatMoney(sale.netProfit)}
                    {sale.margin !== null && ` · ${formatPercent(sale.margin)}`}
                  </span>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

function Ranking({
  title,
  rows,
  value,
  tone,
}: {
  title: string;
  rows: RankedVehicle[];
  value: (row: RankedVehicle) => string;
  tone?: string;
}) {
  return (
    <section className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]">
      <p className="font-display mb-3 text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
        {title}
      </p>

      {rows.length === 0 ? (
        <p className="text-sm text-[var(--text-secondary)]">Nada por aqui.</p>
      ) : (
        <ol className="space-y-2.5">
          {rows.map((row, index) => (
            <li key={row.code} className="flex items-center gap-3 text-sm">
              <span className="num w-4 shrink-0 text-xs text-[var(--text-muted)]">{index + 1}</span>

              {row.coverThumbnailUrl ? (
                // Signed address, short lived: see ADR-0004.
                // eslint-disable-next-line @next/next/no-img-element
                <img src={row.coverThumbnailUrl} alt="" className="h-9 w-12 shrink-0 rounded object-cover" />
              ) : (
                <span className="grid h-9 w-12 shrink-0 place-items-center rounded bg-[var(--surface-2)] text-[var(--text-muted)]">
                  <Car size={14} />
                </span>
              )}

              <div className="min-w-0 flex-1">
                <Link
                  href={`/vehicles/${row.code}`}
                  className="block truncate font-medium hover:text-[var(--primary)] hover:underline"
                >
                  {row.name}
                </Link>
                <span className="flex items-center gap-2">
                  <span className="num text-xs text-[var(--text-secondary)]">{row.plate}</span>
                  <StatusPill status={row.status} />
                </span>
              </div>

              <span className="num shrink-0 font-semibold" style={tone ? { color: tone } : undefined}>
                {value(row)}
              </span>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
