"use client";

import Link from "next/link";
import { useState } from "react";
import { ArrowRight, Undo2 } from "lucide-react";
import { Modal } from "@/components/common/Modal";
import { TextArea } from "@/components/common/TextArea";
import { apiSend } from "@/lib/api";
import { formatDate, formatDays, formatMoney, formatPercent } from "@/lib/masks";
import { PAYMENT_METHOD_LABEL, SALE_CHANNEL_LABEL, type Sale } from "@/lib/types";

/**
 * The sale, pinned at the top of a sold car: who bought, for how much, and what was left.
 *
 * Undoing it soft deletes the record and puts the car back on the lot. The car that came in
 * as a trade stays where it is — it exists.
 */
export function SaleBanner({
  vehicleCode,
  sale,
  canSell,
  onCancelled,
}: {
  vehicleCode: string;
  sale: Sale;
  /** Whether the person may undo it. */
  canSell: boolean;
  onCancelled: () => void;
}) {
  const [cancelling, setCancelling] = useState(false);
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function cancel() {
    setBusy(true);
    setError("");

    const result = await apiSend(
      "DELETE",
      `vehicles/${vehicleCode}/sale`,
      "Falha ao cancelar a venda.",
      { reason: reason.trim() || null },
    );

    setBusy(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    setCancelling(false);
    onCancelled();
  }

  const good = sale.result.netProfit >= 0;

  return (
    <section className="mb-6 rounded-xl border border-[color-mix(in_srgb,var(--success)_40%,transparent)] bg-[color-mix(in_srgb,var(--success)_8%,transparent)] p-5">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="font-display text-[11px] font-bold uppercase tracking-[.18em] text-[var(--success)]">
            Vendido em {formatDate(sale.date)}
          </p>
          <p className="mt-1 text-lg font-bold">
            {sale.buyerName}
            <span className="font-normal text-[var(--text-secondary)]">
              {" · "}
              {PAYMENT_METHOD_LABEL[sale.paymentMethod]}
              {" · "}
              {SALE_CHANNEL_LABEL[sale.channel]}
              {sale.partnerStoreName && ` (${sale.partnerStoreName})`}
            </span>
          </p>
          {sale.daysInStock !== null && (
            <p className="num mt-0.5 text-xs text-[var(--text-secondary)]">
              {formatDays(sale.daysInStock)} entre a compra e a venda
            </p>
          )}
        </div>

        <div className="grid grid-cols-2 gap-x-6 gap-y-1 text-sm sm:grid-cols-3">
          <Number label="Valor" value={formatMoney(sale.amount)} />
          {sale.tradeInValue !== null && (
            <Number label="Em carro" value={formatMoney(sale.tradeInValue)} muted />
          )}
          {sale.result.partnerCut > 0 && (
            <Number label="A loja ficou com" value={formatMoney(sale.result.partnerCut)} muted />
          )}
          {sale.commission > 0 && (
            <Number label="Comissão" value={formatMoney(sale.commission)} muted />
          )}
          <Number label="Custou" value={formatMoney(sale.result.cost)} muted />
          <Number
            label="Sobrou"
            value={`${formatMoney(sale.result.netProfit)}${
              sale.result.margin !== null ? ` · ${formatPercent(sale.result.margin)}` : ""
            }`}
            tone={good ? "var(--success)" : "var(--critical)"}
          />
        </div>
      </div>

      <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-[color-mix(in_srgb,var(--success)_25%,transparent)] pt-3">
        {sale.tradeInVehicleCode ? (
          <Link
            href={`/vehicles/${sale.tradeInVehicleCode}`}
            className="inline-flex items-center gap-1.5 text-sm font-semibold text-[var(--primary)] hover:underline"
          >
            Ver o carro que entrou na troca <ArrowRight size={15} />
          </Link>
        ) : (
          <span />
        )}

        {canSell && (
          <button
            type="button"
            onClick={() => setCancelling(true)}
            className="inline-flex items-center gap-1.5 rounded-md border border-[var(--border)] px-3 py-1.5 text-xs font-semibold text-[var(--text-secondary)] transition hover:border-[var(--critical)] hover:text-[var(--critical)]"
          >
            <Undo2 size={14} />
            Cancelar venda
          </button>
        )}
      </div>

      {cancelling && (
        <Modal
          title="Cancelar venda"
          onClose={() => setCancelling(false)}
          error={error}
          width="max-w-md"
          footer={
            <>
              <button
                type="button"
                onClick={() => setCancelling(false)}
                className="rounded-md border border-[var(--border)] px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--surface-2)]"
              >
                Voltar
              </button>
              <button
                type="button"
                onClick={cancel}
                disabled={busy}
                className="rounded-md bg-[var(--critical)] px-4 py-2 text-sm font-semibold text-white transition hover:brightness-110 disabled:opacity-60"
              >
                {busy ? "Aguarde..." : "Cancelar a venda"}
              </button>
            </>
          }
        >
          <div className="space-y-4">
            <p className="text-sm leading-relaxed text-[var(--text-secondary)]">
              O carro volta para <strong>Pronto para venda</strong>, e a proposta que fechou
              esta venda reabre.
              {sale.tradeInVehicleCode &&
                " O carro que entrou na troca continua no pátio: ele existe de verdade, e cabe a você decidir o que fazer com ele."}
            </p>
            <TextArea
              label="Motivo"
              rows={2}
              value={reason}
              onChange={setReason}
              placeholder="Comprador desistiu."
              hint="Fica no histórico. Opcional."
            />
          </div>
        </Modal>
      )}
    </section>
  );
}

function Number({
  label,
  value,
  muted = false,
  tone,
}: {
  label: string;
  value: string;
  muted?: boolean;
  tone?: string;
}) {
  return (
    <div>
      <p className="text-[10px] uppercase tracking-wide text-[var(--text-muted)]">{label}</p>
      <p
        className={["num font-semibold", muted ? "text-[var(--text-secondary)]" : ""].join(" ")}
        style={tone ? { color: tone } : undefined}
      >
        {value}
      </p>
    </div>
  );
}
