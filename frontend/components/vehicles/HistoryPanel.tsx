"use client";

import { useCallback, useEffect, useState } from "react";
import { apiGet } from "@/lib/api";
import { formatMoment } from "@/lib/masks";
import { VEHICLE_STATUS_LABEL, type VehicleStatusEntry } from "@/lib/types";
import { Empty, PageError } from "./VehicleUi";

/**
 * Where the car has been, in the order it went (RF-26).
 *
 * The history is what answers "how long did this car sit in the workshop" without depending
 * on anybody memory, and it is what remains when somebody asks why the car went back for
 * repair after being ready.
 */
export function HistoryPanel({ vehicleCode }: { vehicleCode: string }) {
  const [entries, setEntries] = useState<VehicleStatusEntry[] | null>(null);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    const result = await apiGet<VehicleStatusEntry[]>(
      `vehicles/${vehicleCode}/history`,
      "Falha ao carregar o histórico.",
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

  if (error) {
    return <PageError message={error} />;
  }

  if (entries === null) {
    return <p className="text-sm text-[var(--text-muted)]">Carregando…</p>;
  }

  if (entries.length === 0) {
    return <Empty title="Este carro segue na situação em que entrou." />;
  }

  return (
    <ol className="relative space-y-5 border-l border-[var(--border)] pl-6">
      {entries.map((entry) => (
        <li key={entry.code} className="relative">
          <span
            aria-hidden
            className="absolute -left-[27px] top-1.5 h-2.5 w-2.5 rounded-full border-2 border-[var(--surface)] bg-[var(--primary)]"
          />

          <p className="text-sm font-semibold">
            {entry.fromStatus === null ? (
              "Cadastro"
            ) : (
              <>
                {VEHICLE_STATUS_LABEL[entry.fromStatus]}
                <span className="mx-1.5 text-[var(--text-muted)]">→</span>
                {VEHICLE_STATUS_LABEL[entry.toStatus]}
              </>
            )}
          </p>

          {entry.reason && (
            <p className="mt-0.5 text-sm text-[var(--text-secondary)]">{entry.reason}</p>
          )}

          <p className="num mt-0.5 text-xs text-[var(--text-muted)]">
            {formatMoment(entry.movedAt)}
          </p>
        </li>
      ))}
    </ol>
  );
}
