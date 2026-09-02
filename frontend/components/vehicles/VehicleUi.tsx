"use client";

import type { ReactNode } from "react";
import { formatMoney, formatPercent } from "@/lib/masks";
import { VEHICLE_STATUS_LABEL, type VehicleCost } from "@/lib/types";

/**
 * Visual pieces the listing and the vehicle sheet share.
 *
 * They live together because they mean the same thing in both places: the colour of a status
 * and the budget bar have to tell the same story on the grid and on the sheet, or the person
 * learns two languages for one piece of data.
 */

/**
 * The colour of each step of the pipeline.
 *
 * Grey while the car is still a bet, blue while it is being worked on, amber once it can be
 * sold, green once it turned into money. The colour is the summary read before the text.
 */
const STATUS_TONE: Record<number, string> = {
  1: "bg-[var(--surface-2)] text-[var(--text-secondary)]",
  2: "bg-[color-mix(in_srgb,var(--signal)_14%,transparent)] text-[var(--signal-strong)]",
  3: "bg-[color-mix(in_srgb,var(--signal)_14%,transparent)] text-[var(--signal-strong)]",
  4: "bg-[color-mix(in_srgb,var(--flare)_20%,transparent)] text-[var(--warning)]",
  5: "bg-[color-mix(in_srgb,var(--flare)_20%,transparent)] text-[var(--warning)]",
  6: "bg-[color-mix(in_srgb,var(--flare)_20%,transparent)] text-[var(--warning)]",
  7: "bg-[color-mix(in_srgb,var(--success)_14%,transparent)] text-[var(--success)]",
};

export function StatusPill({ status }: { status: number }) {
  return (
    <span
      className={[
        "inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-semibold",
        STATUS_TONE[status] ?? STATUS_TONE[1],
      ].join(" ")}
    >
      <span aria-hidden className="h-1.5 w-1.5 rounded-full" style={{ background: "currentColor" }} />
      {VEHICLE_STATUS_LABEL[status] ?? "—"}
    </span>
  );
}

/**
 * The budget ceiling, the way the business asked for it: how much is gone, and **how much
 * still fits** — which is the question of somebody at the parts counter with a phone in hand.
 *
 * The bar turns amber when the planned spending passes the ceiling, even while today's
 * spending is still inside it. That is the warning that arrives in time to drop the part.
 */
export function BudgetBar({ cost, ceiling }: { cost: VehicleCost; ceiling: number | null }) {
  if (!ceiling) {
    return null;
  }

  const used = Math.min(cost.budgetUsedPercent ?? 0, 100);

  const tone = cost.isOverBudget
    ? "var(--critical)"
    : cost.willExceedBudget
      ? "var(--flare)"
      : "var(--success)";

  return (
    <div>
      <div className="mb-1.5 flex items-baseline justify-between gap-2 text-xs">
        <span className="text-[var(--text-secondary)]">
          {cost.isOverBudget ? "Passou do teto" : "Ainda cabe"}
        </span>
        <span className="num font-semibold" style={{ color: tone }}>
          {formatMoney(cost.budgetRemaining)}
        </span>
      </div>

      <div
        className="h-1.5 overflow-hidden rounded-full bg-[var(--surface-2)]"
        role="img"
        aria-label={`${formatPercent(cost.budgetUsedPercent ?? 0)} do teto de ${formatMoney(ceiling)}`}
      >
        <div
          className="h-full rounded-full transition-[width]"
          style={{ width: `${used}%`, background: tone }}
        />
      </div>

      <p className="mt-1.5 text-[11px] text-[var(--text-muted)]">
        <span className="num">{formatPercent(cost.budgetUsedPercent ?? 0)}</span> de{" "}
        <span className="num">{formatMoney(ceiling)}</span>
        {cost.willExceedBudget && !cost.isOverBudget && (
          <span className="ml-1 font-semibold text-[var(--warning)]">
            · o previsto estoura
          </span>
        )}
      </p>
    </div>
  );
}

/** A number with a label, for the summary strip. */
export function Stat({
  label,
  value,
  hint,
  icon,
}: {
  label: string;
  value: string;
  hint?: string;
  icon?: ReactNode;
}) {
  return (
    <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]">
      <div className="mb-3 flex items-center justify-between">
        <p className="font-display text-[10px] font-bold uppercase tracking-[.18em] text-[var(--text-muted)]">
          {label}
        </p>
        {icon}
      </div>
      <p className="num text-2xl font-bold">{value}</p>
      {hint && <p className="mt-1 text-xs text-[var(--text-secondary)]">{hint}</p>}
    </div>
  );
}

/** Page level error, outside any modal. */
export function PageError({ message }: { message: string }) {
  if (!message) return null;

  return (
    <p
      role="alert"
      className="mb-4 rounded-md border border-[color-mix(in_srgb,var(--critical)_40%,transparent)] bg-[color-mix(in_srgb,var(--critical)_8%,transparent)] px-4 py-3 text-sm text-[var(--critical)]"
    >
      {message}
    </p>
  );
}

/** Empty state: says what is there, and the next step. */
export function Empty({ title, action }: { title: string; action?: ReactNode }) {
  return (
    <div className="grid place-items-center rounded-xl border border-dashed border-[var(--border)] bg-[var(--surface)] p-10 text-center">
      <p className="text-sm font-medium">{title}</p>
      {action && <div className="mt-3">{action}</div>}
    </div>
  );
}
