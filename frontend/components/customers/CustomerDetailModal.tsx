"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Car, HandCoins, MessageCircle, Pencil } from "lucide-react";
import { Modal } from "@/components/common/Modal";
import { apiGet } from "@/lib/api";
import { formatDate, formatMoney, maskCpfCnpj, maskPhone } from "@/lib/masks";
import { whatsappUrl } from "@/lib/share";
import {
  PAYMENT_METHOD_LABEL,
  PROPOSAL_STATUS_LABEL,
  type Customer,
  type CustomerDetail,
} from "@/lib/types";

const STATUS_TONE: Record<number, string> = {
  1: "bg-[color-mix(in_srgb,var(--signal)_14%,transparent)] text-[var(--signal-strong)]",
  2: "bg-[color-mix(in_srgb,var(--success)_14%,transparent)] text-[var(--success)]",
  3: "bg-[var(--surface-2)] text-[var(--text-muted)]",
};

/**
 * A ficha do cliente (M21): os dados, o botão do WhatsApp, e a história — as propostas e as
 * compras, cada uma com o carro. Leitura: editar o cliente muda os dados dele, e a história
 * muda por si, pelas propostas e vendas que apontam para ele.
 */
export function CustomerDetailModal({
  customer,
  onClose,
  onEdit,
  actions,
}: {
  customer: Customer;
  onClose: () => void;
  onEdit: (customer: Customer) => void;
  /** Botões a mais no cabeçalho da ficha: o M21 V4 põe "Mandar ficha" aqui. */
  actions?: React.ReactNode;
}) {
  const [detail, setDetail] = useState<CustomerDetail | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;

    apiGet<CustomerDetail>(`customers/${customer.code}`, "Falha ao carregar a ficha do cliente.").then(
      (result) => {
        if (cancelled) return;
        if (result.ok) setDetail(result.data);
        else setError(result.error);
      },
    );

    return () => {
      cancelled = true;
    };
  }, [customer.code]);

  const who = detail?.customer ?? customer;
  const greeting = `Olá, ${who.name.split(" ")[0]}!`;

  return (
    <Modal title={who.name} onClose={onClose} error={error} width="max-w-3xl">
      <div className="space-y-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <dl className="grid gap-x-8 gap-y-1.5 text-sm sm:grid-cols-2">
            <Row label="Telefone" value={who.phone ? maskPhone(who.phone) : "—"} />
            <Row
              label={who.document && who.document.length === 14 ? "CNPJ" : "CPF"}
              value={who.document ? maskCpfCnpj(who.document) : "—"}
            />
            <Row label="E-mail" value={who.email ?? "—"} />
            <Row label="Endereço" value={who.address ?? "—"} />
            {who.notes && <Row label="Anotações" value={who.notes} wide />}
          </dl>

          <div className="flex flex-wrap gap-2">
            {who.phone && (
              <a
                href={whatsappUrl(who.phone, greeting)}
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center gap-2 rounded-md border border-[var(--border)] px-3 py-2 text-sm font-semibold text-[var(--text-secondary)] transition hover:border-[var(--success)] hover:text-[var(--success)]"
              >
                <MessageCircle size={15} />
                WhatsApp
              </a>
            )}
            {actions}
            <button
              type="button"
              onClick={() => onEdit(who)}
              className="inline-flex items-center gap-2 rounded-md border border-[var(--border)] px-3 py-2 text-sm font-semibold text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)]"
            >
              <Pencil size={15} />
              Editar
            </button>
          </div>
        </div>

        <div className="grid gap-3 sm:grid-cols-3">
          <Stat label="Propostas" value={String(who.proposalCount)} />
          <Stat label="Carros comprados" value={String(who.saleCount)} />
          <Stat label="Total comprado" value={formatMoney(who.boughtTotal)} />
        </div>

        {detail === null && !error && (
          <p className="text-sm text-[var(--text-secondary)]">Carregando a história...</p>
        )}

        {detail && (
          <>
            <section>
              <h3 className="font-display mb-2 flex items-center gap-2 text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
                <HandCoins size={13} />
                Compras
              </h3>
              {detail.sales.length === 0 ? (
                <p className="text-sm text-[var(--text-secondary)]">Nenhum carro comprado ainda.</p>
              ) : (
                <ul className="divide-y divide-[var(--border)] rounded-xl border border-[var(--border)] bg-[var(--surface)]">
                  {detail.sales.map((sale) => (
                    <li key={sale.code} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                      <Car size={16} className="shrink-0 text-[var(--signal)]" />
                      <span className="min-w-0 flex-1">
                        <Link
                          href={`/vehicles/${sale.vehicleCode}`}
                          className="block truncate font-semibold hover:text-[var(--primary)]"
                        >
                          {sale.vehicleName} {sale.modelYear}
                        </Link>
                        <span className="num block text-xs text-[var(--text-muted)]">
                          {sale.plate}
                          <span className="font-sans">
                            {" · "}
                            {formatDate(sale.date)}
                            {" · "}
                            {PAYMENT_METHOD_LABEL[sale.paymentMethod]}
                            {sale.hadTradeIn && " · com troca"}
                          </span>
                        </span>
                      </span>
                      <span className="num shrink-0 font-semibold">{formatMoney(sale.amount)}</span>
                    </li>
                  ))}
                </ul>
              )}
            </section>

            <section>
              <h3 className="font-display mb-2 flex items-center gap-2 text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
                <HandCoins size={13} />
                Propostas
              </h3>
              {detail.proposals.length === 0 ? (
                <p className="text-sm text-[var(--text-secondary)]">Nenhuma proposta registrada.</p>
              ) : (
                <ul className="divide-y divide-[var(--border)] rounded-xl border border-[var(--border)] bg-[var(--surface)]">
                  {detail.proposals.map((proposal) => (
                    <li key={proposal.code} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                      <Car size={16} className="shrink-0 text-[var(--signal)]" />
                      <span className="min-w-0 flex-1">
                        <Link
                          href={`/vehicles/${proposal.vehicleCode}`}
                          className="block truncate font-semibold hover:text-[var(--primary)]"
                        >
                          {proposal.vehicleName} {proposal.modelYear}
                        </Link>
                        <span className="num block text-xs text-[var(--text-muted)]">
                          {proposal.plate}
                          <span className="font-sans">
                            {" · "}
                            {formatDate(proposal.date)}
                            {" · "}
                            {PAYMENT_METHOD_LABEL[proposal.paymentMethod]}
                          </span>
                        </span>
                      </span>
                      <span
                        className={[
                          "shrink-0 rounded-full px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide",
                          STATUS_TONE[proposal.status] ?? STATUS_TONE[3],
                        ].join(" ")}
                      >
                        {PROPOSAL_STATUS_LABEL[proposal.status]}
                      </span>
                      <span className="num shrink-0 font-semibold">{formatMoney(proposal.amount)}</span>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </>
        )}
      </div>
    </Modal>
  );
}

function Row({ label, value, wide = false }: { label: string; value: string; wide?: boolean }) {
  return (
    <div className={wide ? "sm:col-span-2" : ""}>
      <dt className="text-[11px] font-semibold uppercase tracking-wide text-[var(--text-muted)]">{label}</dt>
      <dd className="font-medium">{value}</dd>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg bg-[var(--surface-2)] px-4 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-wide text-[var(--text-muted)]">{label}</p>
      <p className="num text-lg font-bold">{value}</p>
    </div>
  );
}
