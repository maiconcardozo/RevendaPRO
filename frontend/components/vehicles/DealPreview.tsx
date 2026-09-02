"use client";

import { useEffect, useState } from "react";
import { apiGet } from "@/lib/api";
import { formatMoney, formatPercent } from "@/lib/masks";
import type { DealResult } from "@/lib/types";

/**
 * What the deal leaves, while the person is still typing the amount (RF-19).
 *
 * Asked to the server on every change, and never computed here: the promise on this box and
 * the report after the sale come out of the same arithmetic. Debounced, because somebody
 * typing "55000" would otherwise fire five requests.
 */
export function DealPreview({
  vehicleCode,
  amount,
  channel,
  partnerCutPercent,
  partnerCutAmount,
  commission,
}: {
  vehicleCode: string;
  amount: number;
  channel: number;
  partnerCutPercent: number | null;
  partnerCutAmount: number | null;
  commission: number;
}) {
  const [result, setResult] = useState<DealResult | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    if (amount <= 0) {
      setResult(null);
      setError("");
      return;
    }

    const timer = setTimeout(async () => {
      const query = new URLSearchParams({ amount: String(amount), channel: String(channel) });

      if (partnerCutPercent !== null) query.set("partnerCutPercent", String(partnerCutPercent));
      if (partnerCutAmount !== null) query.set("partnerCutAmount", String(partnerCutAmount));
      if (commission > 0) query.set("commission", String(commission));

      const answer = await apiGet<DealResult>(
        `vehicles/${vehicleCode}/deal-preview?${query}`,
        "Falha ao simular.",
      );

      if (answer.ok) {
        setResult(answer.data);
        setError("");
      } else {
        setResult(null);
        setError(answer.error);
      }
    }, 250);

    return () => clearTimeout(timer);
  }, [vehicleCode, amount, channel, partnerCutPercent, partnerCutAmount, commission]);

  if (error) {
    return <p className="text-xs text-[var(--critical)]">{error}</p>;
  }

  if (!result) {
    return (
      <p className="rounded-lg border border-dashed border-[var(--border)] px-4 py-3 text-xs text-[var(--text-muted)]">
        Informe o valor para ver quanto sobra.
      </p>
    );
  }

  const good = result.netProfit >= 0;

  return (
    <div className="rounded-lg border border-[var(--border)] bg-[var(--surface-2)] px-4 py-3">
      <div className="flex items-baseline justify-between gap-3">
        <span className="text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
          Sobra
        </span>
        <span
          className="num text-xl font-bold"
          style={{ color: good ? "var(--success)" : "var(--critical)" }}
        >
          {formatMoney(result.netProfit)}
        </span>
      </div>

      <dl className="mt-2 space-y-1 text-xs text-[var(--text-secondary)]">
        {result.partnerCut > 0 && (
          <Line label="A loja fica com" value={formatMoney(result.partnerCut)} />
        )}
        <Line label="Você recebe" value={formatMoney(result.received)} />
        {result.commission > 0 && <Line label="Comissão" value={formatMoney(result.commission)} />}
        <Line label="O carro custou" value={formatMoney(result.cost)} />
        {result.margin !== null && <Line label="Margem" value={formatPercent(result.margin)} />}
      </dl>
    </div>
  );
}

function Line({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <dt>{label}</dt>
      <dd className="num font-semibold text-[var(--text-primary)]">{value}</dd>
    </div>
  );
}
