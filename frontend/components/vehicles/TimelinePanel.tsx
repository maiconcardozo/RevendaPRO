"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Camera,
  FileText,
  HandCoins,
  Receipt,
  ShoppingCart,
  Tag,
  Waypoints,
} from "lucide-react";
import { apiGet } from "@/lib/api";
import { formatMoney } from "@/lib/masks";
import {
  PROPOSAL_STATUS_LABEL,
  TIMELINE_KIND,
  VEHICLE_STATUS_LABEL,
  type VehicleTimelineEntry,
} from "@/lib/types";
import { Empty, PageError } from "./VehicleUi";

/**
 * Everything that happened to the car, in the order it happened (RF-26).
 *
 * Until this existed the answer to "what happened with this Cruze?" was five tabs and some
 * mental arithmetic over dates: the purchase in the header, the expense under costs, the
 * photo under photos, the offer under proposals. The story was all there, and readable
 * nowhere.
 *
 * The API sends data, and the wording lives here — the same rule the rest of the frontend
 * follows. See ADR-0003.
 */
export function TimelinePanel({ vehicleCode }: { vehicleCode: string }) {
  const [entries, setEntries] = useState<VehicleTimelineEntry[] | null>(null);
  const [error, setError] = useState("");
  const [filter, setFilter] = useState<FilterKey>("all");

  const load = useCallback(async () => {
    const result = await apiGet<VehicleTimelineEntry[]>(
      `vehicles/${vehicleCode}/timeline`,
      "Falha ao carregar a linha do tempo.",
    );

    if (result.ok) {
      setEntries(result.data);
      setError("");
    } else {
      setEntries([]);
      setError(result.error);
    }
  }, [vehicleCode]);

  useEffect(() => {
    load();
  }, [load]);

  const shown = useMemo(
    () => (entries ?? []).filter((entry) => FILTERS[filter].kinds.includes(entry.kind)),
    [entries, filter],
  );

  const days = useMemo(() => groupByDay(shown), [shown]);

  if (error) {
    return <PageError message={error} />;
  }

  if (entries === null) {
    return <p className="text-sm text-[var(--text-muted)]">Carregando…</p>;
  }

  return (
    <div>
      <div className="mb-5 flex flex-wrap items-center gap-2">
        {FILTER_ORDER.map((key) => {
          const count = (entries ?? []).filter((entry) =>
            FILTERS[key].kinds.includes(entry.kind),
          ).length;

          return (
            <button
              key={key}
              type="button"
              onClick={() => setFilter(key)}
              aria-pressed={filter === key}
              className={`rounded-full border px-3 py-1.5 text-xs font-semibold transition ${
                filter === key
                  ? "border-[var(--primary)] bg-[var(--primary)] text-white"
                  : "border-[var(--border)] bg-[var(--surface)] text-[var(--text-secondary)] hover:border-[var(--primary)]"
              }`}
            >
              {FILTERS[key].label}
              <span className="num ml-1.5 opacity-70">{count}</span>
            </button>
          );
        })}
      </div>

      {days.length === 0 ? (
        <Empty title="A história deste carro começa no primeiro lançamento." />
      ) : (
        <div className="space-y-6">
          {days.map((day) => (
            <section key={day.key}>
              <h3 className="num mb-3 text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
                {day.label}
              </h3>

              <ol className="relative space-y-4 border-l border-[var(--border)] pl-6">
                {day.entries.map((entry, index) => (
                  <Event key={`${day.key}-${index}`} entry={entry} />
                ))}
              </ol>
            </section>
          ))}
        </div>
      )}
    </div>
  );
}

/** One thing that happened, with the icon and the words its kind deserves. */
function Event({ entry }: { entry: VehicleTimelineEntry }) {
  const Icon = ICONS[entry.kind] ?? Waypoints;

  return (
    <li className="relative">
      <span
        aria-hidden
        className="absolute -left-[35px] top-0 grid h-5 w-5 place-items-center rounded-full border-2 border-[var(--surface)] bg-[var(--canvas)] text-[var(--text-secondary)]"
      >
        <Icon size={11} />
      </span>

      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <p className="text-sm font-semibold">{headline(entry)}</p>

        {entry.kind === TIMELINE_KIND.expense && entry.isPaid === false && (
          <span className="rounded-full bg-[var(--warning)]/12 px-2 py-0.5 text-[11px] font-semibold text-[var(--warning)]">
            Previsto
          </span>
        )}

        {entry.kind === TIMELINE_KIND.proposal && entry.proposalStatus !== null && (
          <span className="rounded-full border border-[var(--border)] px-2 py-0.5 text-[11px] font-semibold text-[var(--text-secondary)]">
            {PROPOSAL_STATUS_LABEL[entry.proposalStatus]}
          </span>
        )}

        {entry.amount !== null && (
          <span
            className={`num text-sm font-semibold ${
              entry.kind === TIMELINE_KIND.sale
                ? "text-[var(--success)]"
                : "text-[var(--text-primary)]"
            }`}
          >
            {formatMoney(entry.amount)}
          </span>
        )}
      </div>

      {entry.detail && (
        <p className="mt-0.5 text-sm text-[var(--text-secondary)]">{entry.detail}</p>
      )}

      {meta(entry) && (
        <p className="num mt-0.5 text-xs text-[var(--text-muted)]">{meta(entry)}</p>
      )}
    </li>
  );
}

/**
 * The sentence of each kind.
 *
 * Attachments come counted, because sending the photos of a car is one act: the API groups
 * what one person sent on one day, and the plural is decided here.
 */
function headline(entry: VehicleTimelineEntry): string {
  switch (entry.kind) {
    case TIMELINE_KIND.purchase:
      return entry.title ? `Compra · ${entry.title}` : "Compra";

    case TIMELINE_KIND.statusChange:
      return entry.fromStatus === null
        ? "Cadastro"
        : `${VEHICLE_STATUS_LABEL[entry.fromStatus]} → ${VEHICLE_STATUS_LABEL[entry.toStatus ?? 0]}`;

    case TIMELINE_KIND.expense:
      return entry.title ?? "Gasto";

    case TIMELINE_KIND.photos:
      return entry.quantity === 1 ? "Foto enviada" : `${entry.quantity} fotos enviadas`;

    case TIMELINE_KIND.documents:
      return entry.quantity === 1
        ? `Documento anexado${entry.title ? `: ${entry.title}` : ""}`
        : `${entry.quantity} documentos anexados`;

    case TIMELINE_KIND.proposal:
      return `Proposta de ${entry.title ?? "interessado"}`;

    case TIMELINE_KIND.sale:
      return `Venda para ${entry.title ?? "comprador"}`;

    default:
      return "Movimento";
  }
}

/**
 * Same day, same block.
 *
 * The events already arrive in order, so the grouping only has to notice when the day
 * changes — and the reading gains the shape of a calendar without any sorting here.
 */
function groupByDay(entries: VehicleTimelineEntry[]) {
  const days: { key: string; label: string; entries: VehicleTimelineEntry[] }[] = [];

  for (const entry of entries) {
    const key = entry.moment.slice(0, 10);

    if (days.at(-1)?.key !== key) {
      days.push({ key, label: dayLabel(key), entries: [] });
    }

    days.at(-1)!.entries.push(entry);
  }

  return days;
}

function dayLabel(day: string): string {
  const [year, month, date] = day.split("-").map(Number);

  return new Date(year, month - 1, date).toLocaleDateString("pt-BR", {
    day: "2-digit",
    month: "long",
    year: "numeric",
  });
}

/**
 * The line under the event: the hour and who did it, when there is either.
 *
 * A business date carries no hour, so it lands at midnight. Printing "00:00" for a purchase
 * would invent a precision the data lacks, and a dash in its place would look like something
 * failed to load — the day is already the heading above.
 */
function meta(entry: VehicleTimelineEntry): string {
  const clock = entry.moment.slice(11, 16);
  const parts = [clock === "00:00" ? "" : clock, entry.actorName ?? ""];

  return parts.filter(Boolean).join(" · ");
}

const ICONS: Record<number, typeof Receipt> = {
  [TIMELINE_KIND.purchase]: ShoppingCart,
  [TIMELINE_KIND.statusChange]: Waypoints,
  [TIMELINE_KIND.expense]: Receipt,
  [TIMELINE_KIND.photos]: Camera,
  [TIMELINE_KIND.documents]: FileText,
  [TIMELINE_KIND.proposal]: Tag,
  [TIMELINE_KIND.sale]: HandCoins,
};

type FilterKey = "all" | "deal" | "expenses" | "attachments" | "pipeline";

const FILTERS: Record<FilterKey, { label: string; kinds: number[] }> = {
  all: { label: "Tudo", kinds: Object.values(TIMELINE_KIND) },
  deal: {
    label: "Negócio",
    kinds: [TIMELINE_KIND.purchase, TIMELINE_KIND.proposal, TIMELINE_KIND.sale],
  },
  expenses: { label: "Gastos", kinds: [TIMELINE_KIND.expense] },
  attachments: {
    label: "Anexos",
    kinds: [TIMELINE_KIND.photos, TIMELINE_KIND.documents],
  },
  pipeline: { label: "Esteira", kinds: [TIMELINE_KIND.statusChange] },
};

const FILTER_ORDER: FilterKey[] = ["all", "deal", "expenses", "attachments", "pipeline"];
