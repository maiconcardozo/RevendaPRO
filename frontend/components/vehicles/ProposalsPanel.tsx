"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { Contact, FileDown, HandCoins, MessageCircle, Plus, ThumbsDown, Trash2 } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { CustomerPicker } from "@/components/customers/CustomerPicker";
import { Field } from "@/components/common/Field";
import { Modal } from "@/components/common/Modal";
import { Select, optionsOf } from "@/components/common/Select";
import { TextArea } from "@/components/common/TextArea";
import { apiGet, apiSend } from "@/lib/api";
import { downloadFile } from "@/lib/download";
import { SHARE_NOTICE, shareDocument } from "@/lib/share";
import { formatDate, formatMoney, formatPercent, maskMoney, maskPhone, moneyValue } from "@/lib/masks";
import {
  PAYMENT_METHOD_LABEL,
  PROPOSAL_STATUS,
  PROPOSAL_STATUS_LABEL,
  SALE_CHANNEL,
  SALE_CHANNEL_LABEL,
  type Proposal,
} from "@/lib/types";
import { DealPreview } from "./DealPreview";
import { Empty, PageError } from "./VehicleUi";

/** How the store's cut was agreed. Both go to the server; only one may be filled. */
type CutMode = "percent" | "amount";

type Draft = {
  /** O cliente escolhido no seletor (M21). Nulo cadastra um novo com o nome e o telefone. */
  customerCode: string | null;
  prospectName: string;
  prospectPhone: string;
  amount: string;
  date: string;
  paymentMethod: string;
  channel: string;
  cutMode: CutMode;
  cut: string;
  notes: string;
};

const today = () => new Date().toISOString().slice(0, 10);

/**
 * What people offered for the car, each with how much would be left (RF-18, RF-19).
 *
 * The decision the business described takes seconds — "o cara me manda 55 no dinheiro, ganhar
 * 15 mil? já dou-lhe fogo" — and this list exists so the number is on screen when the offer
 * arrives, instead of being worked out by hand from the cost sheet.
 */
export function ProposalsPanel({
  vehicleCode,
  vehicleName,
  canSell,
  onSell,
}: {
  vehicleCode: string;
  /** Marca e modelo, para a mensagem do WhatsApp da proposta (M19). */
  vehicleName: string;
  /** Whether the car can be sold right now, and the person holds the sales screen. */
  canSell: boolean;
  /** Opens the sale from a proposal, already filled in. */
  onSell: (proposal: Proposal) => void;
}) {
  const [proposals, setProposals] = useState<Proposal[] | null>(null);
  const [error, setError] = useState("");
  const [draft, setDraft] = useState<Draft | null>(null);
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);
  const [busy, setBusy] = useState(false);
  const [toDelete, setToDelete] = useState<Proposal | null>(null);

  const load = useCallback(async () => {
    const result = await apiGet<Proposal[]>(
      `vehicles/${vehicleCode}/proposals`,
      "Falha ao carregar as propostas.",
    );

    if (result.ok) {
      setProposals(result.data);
      setError("");
    } else {
      setProposals([]);
      setError(result.error);
    }
  }, [vehicleCode]);

  useEffect(() => {
    load();
  }, [load]);

  function openNew() {
    setFormError("");
    setDraft({
      customerCode: null,
      prospectName: "",
      prospectPhone: "",
      amount: "",
      date: today(),
      paymentMethod: "1",
      channel: String(SALE_CHANNEL.direct),
      cutMode: "amount",
      cut: "",
      notes: "",
    });
  }

  const cutValues = (d: Draft) => {
    if (Number(d.channel) !== SALE_CHANNEL.partnerStore || !d.cut) {
      return { percent: null, amount: null };
    }

    return d.cutMode === "percent"
      ? { percent: Number(d.cut.replace(",", ".")), amount: null }
      : { percent: null, amount: moneyValue(d.cut) };
  };

  async function save() {
    if (!draft) return;

    if (!draft.prospectName.trim()) {
      setFormError("Informe quem fez a proposta.");
      return;
    }

    if (moneyValue(draft.amount) <= 0) {
      setFormError("Informe o valor da proposta.");
      return;
    }

    setSaving(true);
    setFormError("");

    const cut = cutValues(draft);

    const result = await apiSend(
      "POST",
      `vehicles/${vehicleCode}/proposals`,
      "Falha ao registrar a proposta.",
      {
        vehicleCode,
        customerCode: draft.customerCode,
        prospectName: draft.prospectName.trim(),
        prospectPhone: draft.prospectPhone || null,
        amount: moneyValue(draft.amount),
        date: draft.date || today(),
        paymentMethod: Number(draft.paymentMethod),
        channel: Number(draft.channel),
        partnerCutPercent: cut.percent,
        partnerCutAmount: cut.amount,
        notes: draft.notes.trim() || null,
      },
    );

    setSaving(false);

    if (!result.ok) {
      setFormError(result.error);
      return;
    }

    setDraft(null);
    await load();
  }

  async function decline(proposal: Proposal) {
    setBusy(true);

    const result = await apiSend(
      "PATCH",
      `vehicles/${vehicleCode}/proposals/${proposal.code}/decline`,
      "Falha ao recusar a proposta.",
    );

    setBusy(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    await load();
  }

  async function remove(proposal: Proposal) {
    setBusy(true);

    const result = await apiSend(
      "DELETE",
      `vehicles/${vehicleCode}/proposals/${proposal.code}`,
      "Falha ao excluir a proposta.",
    );

    setBusy(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    setToDelete(null);
    await load();
  }

  const open = (proposals ?? []).filter((p) => p.status === PROPOSAL_STATUS.open);

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-[var(--text-secondary)]">
          {proposals === null
            ? "Carregando…"
            : `${proposals.length} ${proposals.length === 1 ? "proposta" : "propostas"}`}
          {open.length > 0 && (
            <>
              {" · "}
              <span className="num font-semibold text-[var(--text-primary)]">{open.length}</span>{" "}
              em aberto
            </>
          )}
        </p>

        <button
          type="button"
          onClick={openNew}
          className="inline-flex items-center gap-2 rounded-md bg-[var(--primary)] px-3.5 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]"
        >
          <Plus size={16} />
          Registrar proposta
        </button>
      </div>

      <PageError message={error} />

      {proposals !== null && proposals.length === 0 ? (
        <Empty title="Nenhuma proposta ainda. Quando alguém oferecer, registre aqui e veja quanto sobra." />
      ) : (
        <ul className="space-y-3">
          {(proposals ?? []).map((proposal) => (
            <ProposalCard
              key={proposal.code}
              proposal={proposal}
              vehicleCode={vehicleCode}
              vehicleName={vehicleName}
              busy={busy}
              canSell={canSell}
              onSell={() => onSell(proposal)}
              onDecline={() => decline(proposal)}
              onDelete={() => setToDelete(proposal)}
            />
          ))}
        </ul>
      )}

      {draft && (
        <Modal
          title="Registrar proposta"
          onClose={() => setDraft(null)}
          error={formError}
          width="max-w-2xl"
          footer={
            <>
              <button
                type="button"
                onClick={() => setDraft(null)}
                className="rounded-md border border-[var(--border)] px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--surface-2)]"
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={save}
                disabled={saving}
                className="rounded-md bg-[var(--primary)] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)] disabled:opacity-50"
              >
                {saving ? "Salvando..." : "Registrar"}
              </button>
            </>
          }
        >
          <div className="grid gap-5 md:grid-cols-[minmax(0,1fr)_240px]">
            <div className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <CustomerPicker
                  label="Quem ofereceu"
                  required
                  name={draft.prospectName}
                  customerCode={draft.customerCode}
                  onChange={(pick) =>
                    setDraft({
                      ...draft,
                      customerCode: pick.customerCode,
                      prospectName: pick.name,
                      prospectPhone: pick.phone ?? draft.prospectPhone,
                    })
                  }
                  hint="Quem já comprou ou ofereceu aparece ao digitar. Um nome novo vira cliente ao salvar."
                />
                <Field
                  label="Telefone"
                  type="tel"
                  inputMode="tel"
                  mask={maskPhone}
                  value={draft.prospectPhone}
                  onChange={(prospectPhone) => setDraft({ ...draft, prospectPhone })}
                  placeholder="(00) 00000-0000"
                  hint="Opcional"
                />
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <Field
                  label="Valor oferecido"
                  required
                  inputMode="decimal"
                  mask={maskMoney}
                  value={draft.amount}
                  onChange={(amount) => setDraft({ ...draft, amount })}
                  placeholder="55.000,00"
                  aside={<span className="text-xs text-[var(--text-muted)]">R$</span>}
                />
                <Field
                  label="Data"
                  type="date"
                  value={draft.date}
                  onChange={(date) => setDraft({ ...draft, date })}
                />
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <Select
                  label="Como paga"
                  value={draft.paymentMethod}
                  onChange={(paymentMethod) => setDraft({ ...draft, paymentMethod })}
                  options={optionsOf(PAYMENT_METHOD_LABEL)}
                  hint="No dinheiro costuma valer menos, e fechar mais rápido."
                />
                <Select
                  label="Canal"
                  value={draft.channel}
                  onChange={(channel) => setDraft({ ...draft, channel })}
                  options={optionsOf(SALE_CHANNEL_LABEL)}
                />
              </div>

              {Number(draft.channel) === SALE_CHANNEL.partnerStore && (
                <PartnerCutFields
                  mode={draft.cutMode}
                  value={draft.cut}
                  onChange={(cutMode, cut) => setDraft({ ...draft, cutMode, cut })}
                />
              )}

              <TextArea
                label="Observações"
                rows={2}
                value={draft.notes}
                onChange={(notes) => setDraft({ ...draft, notes })}
                placeholder="Quer levar hoje, sem troca."
              />
            </div>

            <div className="md:pt-6">
              <DealPreview
                vehicleCode={vehicleCode}
                amount={moneyValue(draft.amount)}
                channel={Number(draft.channel)}
                partnerCutPercent={cutValues(draft).percent}
                partnerCutAmount={cutValues(draft).amount}
                commission={0}
              />
            </div>
          </div>
        </Modal>
      )}

      {toDelete && (
        <Confirmation
          title="Excluir proposta"
          message={
            <>
              Excluir a proposta de <strong>{toDelete.prospectName}</strong>, de{" "}
              <strong>{formatMoney(toDelete.amount)}</strong>? Para guardar o registro de que ela
              existiu, prefira recusar.
            </>
          }
          confirmLabel="Excluir"
          danger
          busy={busy}
          onConfirm={() => remove(toDelete)}
          onCancel={() => setToDelete(null)}
        />
      )}
    </div>
  );
}

/**
 * The store's cut, as a percentage or as an amount — whichever way the store put it. The
 * business does not know yet which one its partners use, so the screen takes either.
 */
export function PartnerCutFields({
  mode,
  value,
  onChange,
}: {
  mode: CutMode;
  value: string;
  onChange: (mode: CutMode, value: string) => void;
}) {
  return (
    <div className="grid gap-4 sm:grid-cols-[180px_minmax(0,1fr)]">
      <Select
        label="Repasse da loja"
        value={mode}
        onChange={(next) => onChange(next as CutMode, "")}
        options={[
          { value: "amount", label: "Em valor" },
          { value: "percent", label: "Em percentual" },
        ]}
      />

      {mode === "percent" ? (
        <Field
          label="Percentual"
          inputMode="decimal"
          value={value}
          onChange={(next) => onChange(mode, next.replace(/[^\d,.]/g, "").slice(0, 6))}
          placeholder="8"
          aside={<span className="text-xs text-[var(--text-muted)]">%</span>}
          hint="A loja põe a dela em cima do que você quer receber."
        />
      ) : (
        <Field
          label="Valor"
          inputMode="decimal"
          mask={maskMoney}
          value={value}
          onChange={(next) => onChange(mode, next)}
          placeholder="5.000,00"
          aside={<span className="text-xs text-[var(--text-muted)]">R$</span>}
          hint="A loja põe a dela em cima do que você quer receber."
        />
      )}
    </div>
  );
}

const STATUS_TONE: Record<number, string> = {
  1: "bg-[color-mix(in_srgb,var(--signal)_14%,transparent)] text-[var(--signal-strong)]",
  2: "bg-[color-mix(in_srgb,var(--success)_14%,transparent)] text-[var(--success)]",
  3: "bg-[var(--surface-2)] text-[var(--text-muted)]",
};

function ProposalCard({
  proposal,
  vehicleCode,
  vehicleName,
  busy,
  canSell,
  onSell,
  onDecline,
  onDelete,
}: {
  proposal: Proposal;
  vehicleCode: string;
  /** Marca e modelo, para a mensagem do WhatsApp. */
  vehicleName: string;
  busy: boolean;
  canSell: boolean;
  onSell: () => void;
  onDecline: () => void;
  onDelete: () => void;
}) {
  const isOpen = proposal.status === PROPOSAL_STATUS.open;
  const good = proposal.result.netProfit >= 0;
  const [printing, setPrinting] = useState(false);
  const [sending, setSending] = useState(false);
  const [printError, setPrintError] = useState("");
  const [notice, setNotice] = useState("");

  const documentPath = `vehicles/${vehicleCode}/reports/proposals/${proposal.code}`;

  /**
   * A mensagem que abre a conversa (M19): o carro, o valor e a validade. O PDF vai junto —
   * anexado pela folha do aparelho no celular, arrastado da pasta de downloads no computador.
   */
  const message = `Olá, ${proposal.prospectName}! Segue a nossa proposta para o ${vehicleName}: ${formatMoney(proposal.amount)}, ${PAYMENT_METHOD_LABEL[proposal.paymentMethod].toLowerCase()}. Ela vale por 7 dias. A proposta em PDF vai em anexo.`;

  async function print() {
    setPrinting(true);
    setPrintError("");
    setNotice("");
    const result = await downloadFile(documentPath, "Proposta.pdf");
    setPrinting(false);
    if (!result.ok) setPrintError(result.error);
  }

  /** Mandar pelo WhatsApp (M20): o aparelho decide entre a folha de compartilhamento e o wa.me. */
  async function send() {
    setSending(true);
    setPrintError("");
    setNotice("");
    const result = await shareDocument({
      path: documentPath,
      fallbackName: "Proposta.pdf",
      message,
      phone: proposal.prospectPhone,
    });
    setSending(false);
    if (!result.ok) setPrintError(result.error);
    else setNotice(SHARE_NOTICE[result.how]);
  }

  return (
    <li
      className={[
        "rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4 shadow-[var(--shadow)]",
        proposal.status === PROPOSAL_STATUS.declined ? "opacity-70" : "",
      ].join(" ")}
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            {proposal.customerCode ? (
              <Link
                href={`/customers?open=${proposal.customerCode}`}
                title="Abrir a ficha do cliente"
                className="inline-flex items-center gap-1.5 font-semibold hover:text-[var(--primary)]"
              >
                <Contact size={14} className="text-[var(--signal)]" />
                {proposal.prospectName}
              </Link>
            ) : (
              <p className="font-semibold">{proposal.prospectName}</p>
            )}
            <span
              className={[
                "rounded-full px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide",
                STATUS_TONE[proposal.status] ?? STATUS_TONE[3],
              ].join(" ")}
            >
              {PROPOSAL_STATUS_LABEL[proposal.status]}
            </span>
          </div>

          <p className="num mt-0.5 text-xs text-[var(--text-secondary)]">
            {formatDate(proposal.date)}
            <span className="font-sans">
              {" · "}
              {PAYMENT_METHOD_LABEL[proposal.paymentMethod]}
              {" · "}
              {SALE_CHANNEL_LABEL[proposal.channel]}
              {proposal.result.partnerCut > 0 && (
                <>
                  {", fica com "}
                  <span className="num">{formatMoney(proposal.result.partnerCut)}</span>
                </>
              )}
            </span>
          </p>

          {proposal.notes && (
            <p className="mt-1 text-sm text-[var(--text-secondary)]">{proposal.notes}</p>
          )}
        </div>

        <div className="text-right">
          <p className="num text-lg font-bold">{formatMoney(proposal.amount)}</p>
          <p className="text-[11px] uppercase tracking-wide text-[var(--text-muted)]">
            Sobra
          </p>
          <p
            className="num text-sm font-semibold"
            style={{ color: good ? "var(--success)" : "var(--critical)" }}
          >
            {formatMoney(proposal.result.netProfit)}
            {proposal.result.margin !== null && (
              <span className="text-[var(--text-muted)]">
                {" · "}
                {formatPercent(proposal.result.margin)}
              </span>
            )}
          </p>
        </div>
      </div>

      {printError && <p className="mt-2 text-xs text-[var(--critical)]">{printError}</p>}
      {notice && <p className="mt-2 text-xs text-[var(--success)]">{notice}</p>}

      {/* No celular os botões ocupam a largura, um por linha, e vender fecha embaixo; no computador, a linha de sempre (M20). */}
      <div className="mt-3 flex flex-col gap-2 border-t border-[var(--border)] pt-3 sm:flex-row sm:flex-wrap sm:items-center sm:justify-between">
        <div className="grid grid-cols-1 gap-2 sm:flex sm:flex-wrap">
          <button
            type="button"
            onClick={print}
            disabled={printing}
            title="A proposta em PDF, com a revenda em cima, para imprimir ou mandar"
            className="inline-flex items-center justify-center gap-1.5 rounded-md border border-[var(--border)] px-2.5 py-1.5 text-xs font-semibold text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)] disabled:opacity-40"
          >
            <FileDown size={14} />
            {printing ? "Gerando..." : "Proposta em PDF"}
          </button>
          <button
            type="button"
            onClick={send}
            disabled={sending}
            title="Abre o WhatsApp com a proposta em PDF e a mensagem pronta"
            className="inline-flex items-center justify-center gap-1.5 rounded-md border border-[var(--border)] px-2.5 py-1.5 text-xs font-semibold text-[var(--text-secondary)] transition hover:border-[var(--success)] hover:text-[var(--success)] disabled:opacity-40"
          >
            <MessageCircle size={14} />
            {sending ? "Preparando..." : "Mandar pelo WhatsApp"}
          </button>
        </div>

      {isOpen && (
        <div className="grid grid-cols-2 gap-2 sm:flex sm:flex-wrap sm:justify-end">
          <button
            type="button"
            onClick={onDelete}
            disabled={busy}
            className="inline-flex items-center justify-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs font-semibold text-[var(--text-muted)] hover:text-[var(--critical)] disabled:opacity-40"
          >
            <Trash2 size={14} />
            Excluir
          </button>
          <button
            type="button"
            onClick={onDecline}
            disabled={busy}
            className="inline-flex items-center justify-center gap-1.5 rounded-md border border-[var(--border)] px-2.5 py-1.5 text-xs font-semibold text-[var(--text-secondary)] transition hover:border-[var(--text-secondary)] disabled:opacity-40"
          >
            <ThumbsDown size={14} />
            Recusar
          </button>
          {canSell && (
            <button
              type="button"
              onClick={onSell}
              disabled={busy}
              className="col-span-2 inline-flex items-center justify-center gap-1.5 rounded-md bg-[var(--success)] px-3 py-1.5 text-xs font-semibold text-white transition hover:brightness-110 disabled:opacity-40 sm:col-span-1"
            >
              <HandCoins size={14} />
              Aceitar e vender
            </button>
          )}
        </div>
      )}
      </div>
    </li>
  );
}
