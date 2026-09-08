"use client";

import Link from "next/link";
import { useEffect, useId, useRef, useState, type ReactNode } from "react";
import { Car, Receipt, Store, Wallet } from "lucide-react";
import { formatMoney } from "@/lib/masks";
import type { SpendSlice, SupplierSpend, SupplierStatistics } from "@/lib/types";

/**
 * O painel do gasto com fornecedores.
 *
 * Quatro leituras, cada uma numa forma: os totais em **indicadores**, quem mais recebeu em
 * **barras** (magnitude, uma cor só), a parte de cada ramo numa **rosca** (identidade, paleta
 * categórica validada, com legenda e rótulo direto), e a evolução em **colunas** mês a mês,
 * com o previsto hachurado por cima do pago. Tudo em SVG, sem biblioteca: as cores vêm dos
 * tokens do tema, então claro e escuro saem certos sem trabalho extra.
 *
 * `compact` é a versão do dashboard — as mesmas peças, menos linhas.
 */
export function SupplierDashboard({
  stats,
  compact = false,
  onOpenSupplier,
}: {
  stats: SupplierStatistics;
  compact?: boolean;
  /** Abre a ficha de um fornecedor, quando a tela tem uma. */
  onOpenSupplier?: (code: string) => void;
}) {
  const empty = stats.expenseCount === 0;

  return (
    <div className="space-y-4">
      <div className={`grid gap-3 ${compact ? "sm:grid-cols-3" : "sm:grid-cols-2 xl:grid-cols-4"}`}>
        <Tile
          label="Pago a fornecedores"
          value={formatMoney(stats.paidTotal)}
          hint={
            stats.unassignedPaid > 0
              ? `Mais ${formatMoney(stats.unassignedPaid)} pagos sem fornecedor no gasto`
              : "Só o que já foi pago entra aqui"
          }
          icon={<Wallet size={17} className="text-[var(--signal)]" />}
          hero
        />
        <Tile
          label="Previsto"
          value={formatMoney(stats.plannedTotal)}
          hint="Orçado e ainda fora do custo real"
          icon={<Receipt size={17} className="text-[var(--warning)]" />}
        />
        <Tile
          label="Fornecedores"
          value={String(stats.supplierCount)}
          hint={stats.expenseCount === 1 ? "1 gasto no período" : `${stats.expenseCount} gastos no período`}
          icon={<Store size={17} className="text-[var(--signal)]" />}
        />
        {!compact && (
          <Tile
            label="Carros atendidos"
            value={String(stats.vehicleCount)}
            hint={
              stats.vehicleCount > 0
                ? `${formatMoney(stats.paidTotal / stats.vehicleCount)} por carro, em média`
                : "Nenhum carro com gasto de fornecedor"
            }
            icon={<Car size={17} className="text-[var(--signal)]" />}
          />
        )}
      </div>

      {empty ? (
        <p className="rounded-xl border border-dashed border-[var(--border)] px-5 py-6 text-center text-sm text-[var(--text-secondary)]">
          Nenhum gasto com fornecedor no período. Escolha o fornecedor ao lançar um gasto na
          ficha do carro, ou amplie o período.
        </p>
      ) : (
        <>
          <div className="grid gap-4 lg:grid-cols-5">
            <Panel
              title="Quem mais recebeu"
              subtitle="Pelo que já foi pago"
              className="lg:col-span-3"
            >
              <RankingBars
                rows={stats.bySupplier}
                limit={compact ? 5 : 8}
                onOpen={onOpenSupplier}
              />
            </Panel>

            <Panel title="Por ramo" subtitle="A parte de cada ramo no pago" className="lg:col-span-2">
              <Donut slices={stats.bySegment} total={stats.paidTotal} limit={compact ? 4 : 6} />
            </Panel>
          </div>

          <div className={`grid gap-4 ${compact ? "" : "lg:grid-cols-5"}`}>
            <Panel
              title="Mês a mês"
              subtitle="Pago, e o previsto por cima"
              className={compact ? "" : "lg:col-span-3"}
            >
              <MonthColumns months={stats.byMonth} />
            </Panel>

            {!compact && (
              <Panel title="Em quê" subtitle="Por tipo de gasto" className="lg:col-span-2">
                <TypeBars slices={stats.byType} total={stats.paidTotal} />
              </Panel>
            )}
          </div>
        </>
      )}

      {compact && (
        <p className="text-right text-xs">
          <Link href="/suppliers" className="font-semibold text-[var(--primary)] hover:underline">
            Ver o painel completo e a ficha de cada fornecedor
          </Link>
        </p>
      )}
    </div>
  );
}

/* ───────────────────────────── peças ───────────────────────────── */

function Tile({
  label,
  value,
  hint,
  icon,
  hero = false,
}: {
  label: string;
  value: string;
  hint: string;
  icon: ReactNode;
  hero?: boolean;
}) {
  return (
    <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] px-4 py-3.5 shadow-[var(--shadow)]">
      <div className="flex items-center justify-between gap-2">
        <p className="text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">{label}</p>
        {icon}
      </div>
      <p className={`num mt-1 font-bold ${hero ? "text-2xl text-[var(--signal)]" : "text-xl"}`}>{value}</p>
      <p className="mt-0.5 text-xs text-[var(--text-secondary)]">{hint}</p>
    </div>
  );
}

function Panel({
  title,
  subtitle,
  className = "",
  children,
}: {
  title: string;
  subtitle: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <section
      className={`rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)] ${className}`}
    >
      <p className="font-display text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
        {title}
      </p>
      <p className="mt-0.5 text-xs text-[var(--text-secondary)]">{subtitle}</p>
      <div className="mt-4">{children}</div>
    </section>
  );
}

/** Um valor curto para eixo e rótulo: "12,4 mil" cabe onde "R$ 12.400,00" jamais caberia. */
function shortMoney(value: number): string {
  if (value >= 1_000_000) return `${(value / 1_000_000).toLocaleString("pt-BR", { maximumFractionDigits: 1 })} mi`;
  if (value >= 1_000) return `${(value / 1_000).toLocaleString("pt-BR", { maximumFractionDigits: 1 })} mil`;
  return value.toLocaleString("pt-BR", { maximumFractionDigits: 0 });
}

function percent(part: number, total: number): string {
  if (total <= 0) return "0%";
  return `${Math.round((part / total) * 100)}%`;
}

/* ───────────────────────────── ranking ───────────────────────────── */

function RankingBars({
  rows,
  limit,
  onOpen,
}: {
  rows: SupplierSpend[];
  limit: number;
  onOpen?: (code: string) => void;
}) {
  const shown = rows.slice(0, limit);
  const rest = rows.slice(limit);
  const restTotal = rest.reduce((t, r) => t + r.paidTotal, 0);
  const max = Math.max(1, ...shown.map((r) => r.paidTotal), restTotal);

  return (
    <ol className="space-y-2.5">
      {shown.map((row, index) => (
        <li key={row.code} className="grid grid-cols-[1.25rem_minmax(0,1fr)_auto] items-center gap-x-3 text-sm">
          <span className="num text-xs font-bold text-[var(--text-muted)]">{index + 1}</span>
          <div className="min-w-0">
            <div className="flex items-baseline justify-between gap-3">
              {onOpen ? (
                <button
                  type="button"
                  onClick={() => onOpen(row.code)}
                  className="truncate text-left font-medium hover:text-[var(--primary)] hover:underline"
                >
                  {row.name}
                </button>
              ) : (
                <span className="truncate font-medium">{row.name}</span>
              )}
              <span className="shrink-0 text-xs text-[var(--text-muted)]">
                {row.segmentName}
                {row.segmentName && " · "}
                {row.expenseCount === 1 ? "1 gasto" : `${row.expenseCount} gastos`}
              </span>
            </div>
            <div
              className="mt-1 h-2 overflow-hidden rounded-full bg-[var(--surface-2)]"
              title={`${row.name}: ${formatMoney(row.paidTotal)} pagos, ${formatMoney(row.plannedTotal)} previstos`}
            >
              <div
                className="h-full rounded-full bg-[var(--signal)] transition-[width] duration-500"
                style={{ width: `${Math.max(1.5, (row.paidTotal / max) * 100)}%` }}
              />
            </div>
          </div>
          <span className="num text-right font-semibold">{formatMoney(row.paidTotal)}</span>
        </li>
      ))}

      {rest.length > 0 && (
        <li className="grid grid-cols-[1.25rem_minmax(0,1fr)_auto] items-center gap-x-3 text-sm">
          <span />
          <div className="min-w-0">
            <span className="text-[var(--text-secondary)]">
              Mais {rest.length === 1 ? "1 fornecedor" : `${rest.length} fornecedores`}
            </span>
            <div className="mt-1 h-2 overflow-hidden rounded-full bg-[var(--surface-2)]">
              <div
                className="h-full rounded-full bg-[var(--chart-muted)]"
                style={{ width: `${Math.max(1.5, (restTotal / max) * 100)}%` }}
              />
            </div>
          </div>
          <span className="num text-right font-semibold text-[var(--text-secondary)]">
            {formatMoney(restTotal)}
          </span>
        </li>
      )}
    </ol>
  );
}

/* ───────────────────────────── rosca ───────────────────────────── */

const SERIES = [
  "var(--chart-1)",
  "var(--chart-2)",
  "var(--chart-3)",
  "var(--chart-4)",
  "var(--chart-5)",
  "var(--chart-6)",
  "var(--chart-7)",
];

function Donut({ slices, total, limit }: { slices: SpendSlice[]; total: number; limit: number }) {
  const [active, setActive] = useState<number | null>(null);

  const shown = slices.filter((s) => s.paidTotal > 0).slice(0, limit);
  const rest = slices.filter((s) => s.paidTotal > 0).slice(limit);
  const restTotal = rest.reduce((t, s) => t + s.paidTotal, 0);

  const parts: { name: string; value: number; color: string }[] = shown.map((s, i) => ({
    name: s.name,
    value: s.paidTotal,
    color: SERIES[i % SERIES.length],
  }));

  if (restTotal > 0) parts.push({ name: "Outros ramos", value: restTotal, color: "var(--chart-muted)" });

  const sum = parts.reduce((t, p) => t + p.value, 0) || 1;
  const size = 180;
  const r = 70;
  const stroke = 22;
  const c = 2 * Math.PI * r;
  const gap = 2; // o vão de 2px na cor do fundo entre as fatias

  // O deslocamento de cada fatia, calculado antes de desenhar: cada uma começa onde a
  // anterior terminou.
  const offsets = parts.reduce<number[]>((acc, p, i) => {
    acc.push(i === 0 ? 0 : acc[i - 1] + (parts[i - 1].value / sum) * c);
    return acc;
  }, []);

  return (
    <div className="flex flex-col items-center gap-4">
      <svg
        viewBox={`0 0 ${size} ${size}`}
        className="h-44 w-44 shrink-0"
        role="img"
        aria-label={`Gasto por ramo: ${parts.map((p) => `${p.name} ${percent(p.value, sum)}`).join(", ")}`}
      >
        <g transform={`translate(${size / 2} ${size / 2}) rotate(-90)`}>
          {parts.map((p, i) => {
            const length = (p.value / sum) * c;
            const dash = Math.max(0, length - gap);
            return (
              <circle
                key={p.name}
                r={r}
                fill="none"
                stroke={p.color}
                strokeWidth={active === null || active === i ? stroke : stroke - 6}
                strokeDasharray={`${dash} ${c - dash}`}
                strokeDashoffset={-offsets[i]}
                opacity={active === null || active === i ? 1 : 0.45}
                onMouseEnter={() => setActive(i)}
                onMouseLeave={() => setActive(null)}
                style={{ transition: "stroke-width .15s, opacity .15s" }}
              >
                <title>{`${p.name}: ${formatMoney(p.value)} (${percent(p.value, sum)})`}</title>
              </circle>
            );
          })}
        </g>
        <text
          x="50%"
          y="47%"
          textAnchor="middle"
          className="num"
          style={{ fill: "var(--text-primary)", fontSize: 20, fontWeight: 700 }}
        >
          {active === null ? shortMoney(total) : percent(parts[active].value, sum)}
        </text>
        <text
          x="50%"
          y="60%"
          textAnchor="middle"
          style={{ fill: "var(--text-muted)", fontSize: 10, fontWeight: 600, letterSpacing: 1 }}
        >
          {active === null ? "PAGO" : parts[active].name.toUpperCase().slice(0, 18)}
        </text>
      </svg>

      <ul className="w-full space-y-1.5 text-sm">
        {parts.map((p, i) => (
          <li
            key={p.name}
            onMouseEnter={() => setActive(i)}
            onMouseLeave={() => setActive(null)}
            className={`grid grid-cols-[10px_minmax(0,1fr)_auto_auto] items-center gap-x-2 rounded px-1 ${active === i ? "bg-[var(--surface-2)]" : ""}`}
          >
            <span className="h-2.5 w-2.5 rounded-full" style={{ background: p.color }} />
            <span className="min-w-0 leading-tight">{p.name}</span>
            <span className="num text-xs text-[var(--text-muted)]">{percent(p.value, sum)}</span>
            <span className="num font-semibold">{shortMoney(p.value)}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

/* ───────────────────────────── colunas mês a mês ───────────────────────────── */

/** A largura real do contêiner, para o gráfico desenhar em pixels e o texto sair sempre do mesmo tamanho. */
function useWidth<T extends HTMLElement>(fallback: number): [React.RefObject<T | null>, number] {
  const ref = useRef<T | null>(null);
  const [width, setWidth] = useState(fallback);

  useEffect(() => {
    const element = ref.current;
    if (!element) return;

    const observer = new ResizeObserver((entries) => {
      const next = Math.round(entries[0]?.contentRect.width ?? fallback);
      if (next > 0) setWidth(next);
    });

    observer.observe(element);
    return () => observer.disconnect();
  }, [fallback]);

  return [ref, width];
}

function MonthColumns({ months }: { months: SpendSlice[] }) {
  const patternId = useId();
  const [active, setActive] = useState<number | null>(null);
  const [box, width] = useWidth<HTMLDivElement>(640);

  const height = 220;
  const pad = { top: 18, right: 8, bottom: 26, left: 48 };
  const innerW = width - pad.left - pad.right;
  const innerH = height - pad.top - pad.bottom;

  const max = Math.max(1, ...months.map((m) => m.paidTotal + m.plannedTotal));
  const maxPaid = Math.max(...months.map((m) => m.paidTotal));
  const step = niceStep(max);
  const top = Math.ceil(max / step) * step;
  const ticks = Array.from({ length: Math.floor(top / step) + 1 }, (_, i) => i * step);

  const band = innerW / Math.max(1, months.length);
  const barW = Math.min(24, Math.max(6, band * 0.6));
  const y = (v: number) => pad.top + innerH - (v / top) * innerH;

  const current = active === null ? null : months[active];

  return (
    <div ref={box}>
      <div className="mb-3 flex flex-wrap items-center gap-4 text-xs text-[var(--text-secondary)]">
        <span className="inline-flex items-center gap-1.5">
          <span className="inline-block h-2.5 w-4 rounded-sm bg-[var(--signal)]" /> Pago
        </span>
        <span className="inline-flex items-center gap-1.5">
          <svg width="16" height="10" className="rounded-sm">
            <rect width="16" height="10" fill={`url(#${patternId})`} />
          </svg>
          Previsto
        </span>
        <span className="ml-auto num">
          {current
            ? `${current.name}: ${formatMoney(current.paidTotal)} pagos, ${formatMoney(current.plannedTotal)} previstos, ${current.expenseCount === 1 ? "1 gasto" : `${current.expenseCount} gastos`}`
            : "Passe o mouse sobre um mês"}
        </span>
      </div>

      <svg
        width={width}
        height={height}
        viewBox={`0 0 ${width} ${height}`}
        className="block max-w-full"
        role="img"
        aria-label="Gasto com fornecedores mês a mês"
        onMouseLeave={() => setActive(null)}
      >
        <defs>
          <pattern id={patternId} width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
            <rect width="6" height="6" fill="var(--surface)" />
            <rect width="2.5" height="6" fill="var(--signal)" opacity="0.7" />
          </pattern>
        </defs>

        {ticks.map((t) => (
          <g key={t}>
            <line x1={pad.left} x2={width - pad.right} y1={y(t)} y2={y(t)} stroke="var(--chart-grid)" strokeWidth={1} />
            <text
              x={pad.left - 8}
              y={y(t) + 3.5}
              textAnchor="end"
              className="num"
              style={{ fill: "var(--text-muted)", fontSize: 10 }}
            >
              {shortMoney(t)}
            </text>
          </g>
        ))}

        {months.map((m, i) => {
          const x = pad.left + i * band + (band - barW) / 2;
          const paidH = (m.paidTotal / top) * innerH;
          const plannedH = (m.plannedTotal / top) * innerH;
          const baseline = pad.top + innerH;
          const dim = active !== null && active !== i;

          return (
            <g
              key={m.key}
              onMouseEnter={() => setActive(i)}
              opacity={dim ? 0.45 : 1}
              style={{ transition: "opacity .15s" }}
            >
              {/* alvo de hover maior que a coluna */}
              <rect x={pad.left + i * band} y={pad.top} width={band} height={innerH} fill="transparent" />

              {m.paidTotal > 0 && (
                <path d={roundedTop(x, baseline - paidH, barW, paidH)} fill="var(--signal)" />
              )}
              {m.plannedTotal > 0 && (
                <path
                  d={roundedTop(x, baseline - paidH - plannedH - (m.paidTotal > 0 ? 2 : 0), barW, plannedH)}
                  fill={`url(#${patternId})`}
                />
              )}

              {(active === i || (active === null && m.paidTotal === maxPaid)) && m.paidTotal > 0 && (
                <text
                  x={x + barW / 2}
                  y={baseline - paidH - plannedH - 6}
                  textAnchor="middle"
                  className="num"
                  style={{ fill: "var(--text-secondary)", fontSize: 10, fontWeight: 600 }}
                >
                  {shortMoney(m.paidTotal)}
                </text>
              )}

              <text
                x={x + barW / 2}
                y={height - 8}
                textAnchor="middle"
                style={{ fill: "var(--text-muted)", fontSize: 10 }}
              >
                {m.name}
              </text>
            </g>
          );
        })}
      </svg>
    </div>
  );
}

/** Uma coluna com o topo arredondado em 4px e a base reta, crescendo da linha de base. */
function roundedTop(x: number, y: number, w: number, h: number): string {
  if (h <= 0) return "";
  const r = Math.min(4, h, w / 2);
  return [
    `M${x} ${y + h}`,
    `V${y + r}`,
    `Q${x} ${y} ${x + r} ${y}`,
    `H${x + w - r}`,
    `Q${x + w} ${y} ${x + w} ${y + r}`,
    `V${y + h}`,
    "Z",
  ].join(" ");
}

/** Um passo redondo para o eixo: 1, 2, 5 × 10^n, com quatro a seis linhas na tela. */
function niceStep(max: number): number {
  const rough = max / 4;
  const magnitude = 10 ** Math.floor(Math.log10(Math.max(1, rough)));
  const candidates = [1, 2, 5, 10].map((m) => m * magnitude);
  return candidates.find((c) => c >= rough) ?? candidates[candidates.length - 1];
}

/* ───────────────────────────── por tipo ───────────────────────────── */

function TypeBars({ slices, total }: { slices: SpendSlice[]; total: number }) {
  const shown = slices.filter((s) => s.paidTotal > 0).slice(0, 7);
  const max = Math.max(1, ...shown.map((s) => s.paidTotal));

  if (shown.length === 0) {
    return <p className="text-sm text-[var(--text-secondary)]">Nada pago no período.</p>;
  }

  return (
    <ul className="space-y-2.5">
      {shown.map((s) => (
        <li key={s.key || s.name} className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-3 gap-y-1 text-sm sm:grid-cols-[150px_minmax(0,1fr)_auto]">
          <span className="truncate text-[var(--text-secondary)]" title={s.name}>
            {s.name}
          </span>
          <span
            className="col-span-2 h-2 overflow-hidden rounded-full bg-[var(--surface-2)] sm:col-span-1"
            title={`${s.name}: ${formatMoney(s.paidTotal)} em ${s.expenseCount === 1 ? "1 gasto" : `${s.expenseCount} gastos`}`}
          >
            <span
              className="block h-full rounded-full bg-[var(--signal)]"
              style={{ width: `${Math.max(1.5, (s.paidTotal / max) * 100)}%` }}
            />
          </span>
          <span className="num text-right">
            <span className="font-semibold">{shortMoney(s.paidTotal)}</span>
            <span className="ml-1.5 text-xs text-[var(--text-muted)]">{percent(s.paidTotal, total)}</span>
          </span>
        </li>
      ))}
    </ul>
  );
}
