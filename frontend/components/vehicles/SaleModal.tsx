"use client";

import { useState, type ReactNode } from "react";
import { Field } from "@/components/common/Field";
import { Modal } from "@/components/common/Modal";
import { Select, optionsOf } from "@/components/common/Select";
import { TextArea } from "@/components/common/TextArea";
import { apiSend } from "@/lib/api";
import {
  digitsOnly,
  isValidCpfOrCnpj,
  maskChassis,
  maskCpfCnpj,
  maskMileage,
  maskMoney,
  maskPhone,
  maskPlate,
  maskYear,
  moneyValue,
} from "@/lib/masks";
import { PAYMENT_METHOD_LABEL, SALE_CHANNEL, SALE_CHANNEL_LABEL, type Proposal, type Sale } from "@/lib/types";
import { DealPreview } from "./DealPreview";
import { PartnerCutFields } from "./ProposalsPanel";

const TRADE_IN = 5;
const TRADE_IN_WITH_CASH = 6;

type Draft = {
  proposalCode: string | null;
  date: string;
  amount: string;
  paymentMethod: string;
  channel: string;
  partnerStoreName: string;
  cutMode: "percent" | "amount";
  cut: string;
  commission: string;
  commissionNotes: string;
  buyerName: string;
  buyerDocument: string;
  buyerPhone: string;
  tradeInValue: string;
  tradeIn: {
    plate: string;
    chassis: string;
    brand: string;
    model: string;
    modelYear: string;
    manufactureYear: string;
    mileage: string;
  };
  notes: string;
};

const today = () => new Date().toISOString().slice(0, 10);

/** A blank sale, or one pre-filled from the proposal being accepted. */
function draftFrom(proposal: Proposal | null): Draft {
  const cut = proposal?.partnerCutPercent !== null && proposal?.partnerCutPercent !== undefined
    ? { cutMode: "percent" as const, cut: String(proposal.partnerCutPercent) }
    : { cutMode: "amount" as const, cut: proposal?.partnerCutAmount ? maskMoney(String(Math.round(proposal.partnerCutAmount * 100))) : "" };

  return {
    proposalCode: proposal?.code ?? null,
    date: today(),
    amount: proposal ? maskMoney(String(Math.round(proposal.amount * 100))) : "",
    paymentMethod: String(proposal?.paymentMethod ?? 1),
    channel: String(proposal?.channel ?? SALE_CHANNEL.direct),
    partnerStoreName: "",
    ...cut,
    commission: "",
    commissionNotes: "",
    buyerName: proposal?.prospectName ?? "",
    buyerDocument: "",
    buyerPhone: proposal?.prospectPhone ? maskPhone(proposal.prospectPhone) : "",
    tradeInValue: "",
    tradeIn: { plate: "", chassis: "", brand: "", model: "", modelYear: "", manufactureYear: "", mileage: "" },
    notes: "",
  };
}

/**
 * Registering the sale (RF-20). The only door to "sold".
 *
 * With a trade, the car that comes in is described here and registered in stock by the
 * server, valued at what the deal said. The profit box shows the same number the proposal
 * promised, because it is the same arithmetic.
 */
export function SaleModal({
  vehicleCode,
  proposal,
  onClose,
  onSold,
}: {
  vehicleCode: string;
  /** The proposal being accepted, or null for a sale that walked in. */
  proposal: Proposal | null;
  onClose: () => void;
  onSold: (sale: Sale) => void;
}) {
  const [draft, setDraft] = useState<Draft>(() => draftFrom(proposal));
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  const update = (change: Partial<Draft>) => setDraft((current) => ({ ...current, ...change }));

  const hasTrade = Number(draft.paymentMethod) === TRADE_IN || Number(draft.paymentMethod) === TRADE_IN_WITH_CASH;
  const isPartner = Number(draft.channel) === SALE_CHANNEL.partnerStore;

  const cut = (() => {
    if (!isPartner || !draft.cut) return { percent: null, amount: null };

    return draft.cutMode === "percent"
      ? { percent: Number(draft.cut.replace(",", ".")), amount: null }
      : { percent: null, amount: moneyValue(draft.cut) };
  })();

  async function save() {
    if (moneyValue(draft.amount) <= 0) {
      setError("Informe o valor da venda.");
      return;
    }

    if (!draft.buyerName.trim()) {
      setError("Informe quem comprou.");
      return;
    }

    if (digitsOnly(draft.buyerDocument) && !isValidCpfOrCnpj(draft.buyerDocument)) {
      setError("CPF ou CNPJ do comprador inválido.");
      return;
    }

    if (isPartner && !draft.partnerStoreName.trim()) {
      setError("Informe a loja parceira.");
      return;
    }

    if (hasTrade) {
      if (moneyValue(draft.tradeInValue) <= 0) {
        setError("Informe o valor do carro que entrou na troca.");
        return;
      }

      const t = draft.tradeIn;

      if (!t.plate || !t.chassis || !t.brand || !t.model || !t.modelYear || !t.manufactureYear) {
        setError("Descreva o carro que entrou: placa, chassi, marca, modelo e anos.");
        return;
      }
    }

    setSaving(true);
    setError("");

    const result = await apiSend<Sale>(
      "POST",
      `vehicles/${vehicleCode}/sale`,
      "Falha ao registrar a venda.",
      {
        vehicleCode,
        proposalCode: draft.proposalCode,
        date: draft.date || today(),
        amount: moneyValue(draft.amount),
        paymentMethod: Number(draft.paymentMethod),
        channel: Number(draft.channel),
        partnerStoreName: isPartner ? draft.partnerStoreName.trim() : null,
        partnerCutPercent: cut.percent,
        partnerCutAmount: cut.amount,
        commission: moneyValue(draft.commission),
        commissionNotes: draft.commissionNotes.trim() || null,
        buyerName: draft.buyerName.trim(),
        buyerDocument: digitsOnly(draft.buyerDocument) || null,
        buyerPhone: digitsOnly(draft.buyerPhone) || null,
        tradeInValue: hasTrade ? moneyValue(draft.tradeInValue) : null,
        tradeIn: hasTrade
          ? {
              plate: draft.tradeIn.plate,
              chassis: draft.tradeIn.chassis,
              brand: draft.tradeIn.brand.trim(),
              model: draft.tradeIn.model.trim(),
              modelYear: Number(draft.tradeIn.modelYear),
              manufactureYear: Number(draft.tradeIn.manufactureYear),
              mileage: Number(draft.tradeIn.mileage.replace(/\D/g, "")) || 0,
            }
          : null,
        notes: draft.notes.trim() || null,
      },
    );

    setSaving(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    onSold(result.data);
  }

  return (
    <Modal
      title={proposal ? `Vender para ${proposal.prospectName}` : "Registrar venda"}
      onClose={onClose}
      error={error}
      width="max-w-3xl"
      footer={
        <>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md border border-[var(--border)] px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--surface-2)]"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={save}
            disabled={saving}
            className="rounded-md bg-[var(--success)] px-4 py-2 text-sm font-semibold text-white transition hover:brightness-110 disabled:opacity-50"
          >
            {saving ? "Registrando..." : "Registrar venda"}
          </button>
        </>
      }
    >
      <div className="grid gap-5 md:grid-cols-[minmax(0,1fr)_240px]">
        <div className="space-y-6">
          <Section title="O negócio">
            <div className="grid gap-4 sm:grid-cols-2">
              <Field
                label="Valor fechado"
                required
                inputMode="decimal"
                mask={maskMoney}
                value={draft.amount}
                onChange={(amount) => update({ amount })}
                placeholder="55.000,00"
                hint={hasTrade ? "Carro da troca incluído." : undefined}
                aside={<span className="text-xs text-[var(--text-muted)]">R$</span>}
              />
              <Field
                label="Data da venda"
                type="date"
                value={draft.date}
                onChange={(date) => update({ date })}
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <Select
                label="Como pagou"
                value={draft.paymentMethod}
                onChange={(paymentMethod) => update({ paymentMethod })}
                options={optionsOf(PAYMENT_METHOD_LABEL)}
              />
              <Select
                label="Canal"
                value={draft.channel}
                onChange={(channel) => update({ channel })}
                options={optionsOf(SALE_CHANNEL_LABEL)}
              />
            </div>

            {isPartner && (
              <>
                <Field
                  label="Loja parceira"
                  required
                  value={draft.partnerStoreName}
                  onChange={(partnerStoreName) => update({ partnerStoreName })}
                  placeholder="Loja do Thiago"
                />
                <PartnerCutFields
                  mode={draft.cutMode}
                  value={draft.cut}
                  onChange={(cutMode, cutValue) => update({ cutMode, cut: cutValue })}
                />
              </>
            )}

            <div className="grid gap-4 sm:grid-cols-2">
              <Field
                label="Comissão"
                inputMode="decimal"
                mask={maskMoney}
                value={draft.commission}
                onChange={(commission) => update({ commission })}
                placeholder="0,00"
                hint="Para quem trouxe o comprador. Zero quando ninguém."
                aside={<span className="text-xs text-[var(--text-muted)]">R$</span>}
              />
              <Field
                label="Comissão para quem"
                value={draft.commissionNotes}
                onChange={(commissionNotes) => update({ commissionNotes })}
                placeholder="Indicação do Clei"
              />
            </div>
          </Section>

          <Section title="Comprador">
            <Field
              label="Nome"
              required
              value={draft.buyerName}
              onChange={(buyerName) => update({ buyerName })}
            />
            <div className="grid gap-4 sm:grid-cols-2">
              <Field
                label="CPF ou CNPJ"
                inputMode="numeric"
                mask={maskCpfCnpj}
                value={draft.buyerDocument}
                onChange={(buyerDocument) => update({ buyerDocument })}
                placeholder="000.000.000-00"
                hint="Fica só aqui. Sai de qualquer exportação."
              />
              <Field
                label="Telefone"
                type="tel"
                inputMode="tel"
                mask={maskPhone}
                value={draft.buyerPhone}
                onChange={(buyerPhone) => update({ buyerPhone })}
                placeholder="(00) 00000-0000"
              />
            </div>
          </Section>

          {hasTrade && (
            <Section title="O carro que entrou">
              <Field
                label="Vale quanto no negócio"
                required
                inputMode="decimal"
                mask={maskMoney}
                value={draft.tradeInValue}
                onChange={(tradeInValue) => update({ tradeInValue })}
                placeholder="20.000,00"
                hint="Vira o preço de compra dele no pátio. O resto do valor fechado é dinheiro."
                aside={<span className="text-xs text-[var(--text-muted)]">R$</span>}
              />

              <div className="grid gap-4 sm:grid-cols-2">
                <Field
                  label="Placa"
                  required
                  mask={maskPlate}
                  value={draft.tradeIn.plate}
                  onChange={(plate) => update({ tradeIn: { ...draft.tradeIn, plate } })}
                  placeholder="ABC1D23"
                />
                <Field
                  label="Chassi"
                  required
                  mask={maskChassis}
                  value={draft.tradeIn.chassis}
                  onChange={(chassis) => update({ tradeIn: { ...draft.tradeIn, chassis } })}
                  placeholder="9BWZZZ377VT004251"
                />
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <Field
                  label="Marca"
                  required
                  value={draft.tradeIn.brand}
                  onChange={(brand) => update({ tradeIn: { ...draft.tradeIn, brand } })}
                />
                <Field
                  label="Modelo"
                  required
                  value={draft.tradeIn.model}
                  onChange={(model) => update({ tradeIn: { ...draft.tradeIn, model } })}
                />
              </div>

              <div className="grid gap-4 sm:grid-cols-3">
                <Field
                  label="Ano modelo"
                  required
                  inputMode="numeric"
                  mask={maskYear}
                  value={draft.tradeIn.modelYear}
                  onChange={(modelYear) => update({ tradeIn: { ...draft.tradeIn, modelYear } })}
                />
                <Field
                  label="Ano fabricação"
                  required
                  inputMode="numeric"
                  mask={maskYear}
                  value={draft.tradeIn.manufactureYear}
                  onChange={(manufactureYear) => update({ tradeIn: { ...draft.tradeIn, manufactureYear } })}
                />
                <Field
                  label="Quilometragem"
                  inputMode="numeric"
                  mask={maskMileage}
                  value={draft.tradeIn.mileage}
                  onChange={(mileage) => update({ tradeIn: { ...draft.tradeIn, mileage } })}
                />
              </div>

              <p className="text-xs text-[var(--text-secondary)]">
                Ele entra em análise, como todo carro. O resto da ficha se preenche depois.
              </p>
            </Section>
          )}

          <TextArea
            label="Observações"
            rows={2}
            value={draft.notes}
            onChange={(notes) => update({ notes })}
          />
        </div>

        <div className="md:pt-6">
          <DealPreview
            vehicleCode={vehicleCode}
            amount={moneyValue(draft.amount)}
            channel={Number(draft.channel)}
            partnerCutPercent={cut.percent}
            partnerCutAmount={cut.amount}
            commission={moneyValue(draft.commission)}
          />
        </div>
      </div>
    </Modal>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="space-y-4">
      <p className="font-display text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
        {title}
      </p>
      {children}
    </section>
  );
}
