"use client";

import { useState } from "react";
import { Handshake, Pencil, Plus, Trash2, Warehouse } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { Field } from "@/components/common/Field";
import { Modal } from "@/components/common/Modal";
import { Select } from "@/components/common/Select";
import { TextArea } from "@/components/common/TextArea";
import { Empty, PageError } from "@/components/vehicles/VehicleUi";
import { apiGet, apiSend } from "@/lib/api";
import { formatMoney, formatPercent, maskPhone } from "@/lib/masks";
import { YARD_KIND_LABEL, YardKind, type Yard } from "@/lib/types";

type Draft = {
  code: string | null;
  name: string;
  kind: string;
  contactName: string;
  contactPhone: string;
  cut: string;
  cutIsPercent: boolean;
  notes: string;
  position: number;
};

/**
 * Os lugares onde os carros ficam.
 *
 * Um cadastro só: o pátio da própria revenda e a loja de terceiro onde ela deixou carro para
 * vender são a mesma coisa para a operação — um lugar onde o carro fica. O que o tipo muda é o
 * repasse, e é por isso que ele some da tela quando o pátio é da casa.
 *
 * O repasse guardado aqui é **sugestão**: a tela de venda chega preenchida com ele, e quem
 * fecha o negócio pode mudar, porque o combinado de hoje pode não ser o do próximo carro.
 */
export function YardsView({ initialYards }: { initialYards: Yard[] }) {
  const [yards, setYards] = useState(initialYards);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [toDelete, setToDelete] = useState<Yard | null>(null);
  const [error, setError] = useState("");
  const [formError, setFormError] = useState("");
  const [deleteError, setDeleteError] = useState("");
  const [saving, setSaving] = useState(false);
  const [busy, setBusy] = useState(false);

  async function reload() {
    const result = await apiGet<Yard[]>("yards", "Falha ao carregar os pátios.");

    if (result.ok) setYards(result.data);
    else setError(result.error);
  }

  async function save() {
    if (!draft) return;

    if (!draft.name.trim()) {
      setFormError("Informe o nome do pátio.");
      return;
    }

    setSaving(true);
    setFormError("");

    const isNew = draft.code === null;
    const kind = Number(draft.kind);
    const cut = Number(draft.cut.replace(/\./g, "").replace(",", ".")) || null;

    const result = await apiSend(
      isNew ? "POST" : "PUT",
      isNew ? "yards" : `yards/${draft.code}`,
      "Falha ao salvar o pátio.",
      {
        name: draft.name.trim(),
        kind,
        contactName: draft.contactName.trim() || null,
        contactPhone: draft.contactPhone.replace(/\D/g, "") || null,

        // Percentual ou valor, e jamais os dois: é a mesma regra que a proposta e a venda
        // seguem desde o M8, e o formulário a torna impossível de quebrar.
        cutPercent: kind === YardKind.Partner && draft.cutIsPercent ? cut : null,
        cutAmount: kind === YardKind.Partner && !draft.cutIsPercent ? cut : null,
        notes: draft.notes.trim() || null,
        position: draft.position,
      },
    );

    setSaving(false);

    if (!result.ok) {
      setFormError(result.error);
      return;
    }

    setDraft(null);
    await reload();
  }

  async function remove(yard: Yard) {
    setBusy(true);
    setDeleteError("");

    const result = await apiSend("DELETE", `yards/${yard.code}`, "Falha ao excluir o pátio.");

    setBusy(false);

    if (!result.ok) {
      setDeleteError(result.error);
      return;
    }

    setToDelete(null);
    await reload();
  }

  function edit(yard: Yard) {
    setFormError("");
    setDraft({
      code: yard.code,
      name: yard.name,
      kind: String(yard.kind),
      contactName: yard.contactName ?? "",
      contactPhone: yard.contactPhone ? maskPhone(yard.contactPhone) : "",
      cut: yard.cutPercent
        ? String(yard.cutPercent).replace(".", ",")
        : yard.cutAmount
          ? String(yard.cutAmount).replace(".", ",")
          : "",
      cutIsPercent: yard.cutAmount === null,
      notes: yard.notes ?? "",
      position: yard.position,
    });
  }

  return (
    <div className="dash-anim">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
            Administração
          </p>
          <h1 className="hero-title text-3xl font-bold">Pátios</h1>
          <p className="mt-1 max-w-2xl text-sm text-[var(--text-secondary)]">
            Os lugares onde os carros ficam: o pátio da revenda e as lojas onde você deixa carro
            para vender. Cada carro fica em um pátio, e o painel mostra quanto está parado em
            cada um.
          </p>
        </div>

        <button
          type="button"
          onClick={() => {
            setFormError("");
            setDraft({
              code: null,
              name: "",
              kind: String(YardKind.Own),
              contactName: "",
              contactPhone: "",
              cut: "",
              cutIsPercent: true,
              notes: "",
              position: (yards.at(-1)?.position ?? 0) + 1,
            });
          }}
          className="inline-flex items-center gap-2 rounded-md bg-[var(--primary)] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]"
        >
          <Plus size={17} />
          Novo pátio
        </button>
      </div>

      <PageError message={error} />

      {yards.length === 0 ? (
        <Empty title="Nenhum pátio cadastrado." />
      ) : (
        <div className="grid items-start gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {yards.map((yard) => (
            <section
              key={yard.code}
              className="flex flex-col gap-3 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]"
            >
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="flex items-center gap-2 font-semibold">
                    {yard.kind === YardKind.Own ? (
                      <Warehouse size={16} className="shrink-0 text-[var(--signal)]" />
                    ) : (
                      <Handshake size={16} className="shrink-0 text-[var(--signal)]" />
                    )}
                    <span className="truncate">{yard.name}</span>
                  </p>
                  <p className="mt-0.5 text-xs text-[var(--text-secondary)]">
                    {YARD_KIND_LABEL[yard.kind]}
                  </p>
                </div>

                <div className="flex shrink-0 gap-1">
                  <button
                    type="button"
                    onClick={() => edit(yard)}
                    aria-label={`Editar ${yard.name}`}
                    className="rounded-md p-2 text-[var(--text-secondary)] transition hover:bg-[var(--surface-2)] hover:text-[var(--primary)]"
                  >
                    <Pencil size={15} />
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setDeleteError("");
                      setToDelete(yard);
                    }}
                    aria-label={`Excluir ${yard.name}`}
                    className="rounded-md p-2 text-[var(--text-secondary)] transition hover:bg-[var(--surface-2)] hover:text-[var(--critical)]"
                  >
                    <Trash2 size={15} />
                  </button>
                </div>
              </div>

              <dl className="grid gap-x-6 gap-y-1.5 text-sm">
                <Row
                  label="Carros aqui"
                  value={
                    yard.vehicleCount === 1 ? "1 carro" : `${yard.vehicleCount} carros`
                  }
                />

                {yard.kind === YardKind.Partner && (
                  <Row
                    label="Repasse combinado"
                    value={
                      yard.cutPercent
                        ? formatPercent(yard.cutPercent)
                        : yard.cutAmount
                          ? formatMoney(yard.cutAmount)
                          : "A combinar"
                    }
                  />
                )}

                {yard.contactName && <Row label="Falar com" value={yard.contactName} />}

                {yard.contactPhone && (
                  <Row label="Telefone" value={maskPhone(yard.contactPhone)} />
                )}
              </dl>

              {yard.notes && (
                <p className="text-xs text-[var(--text-secondary)]">{yard.notes}</p>
              )}
            </section>
          ))}
        </div>
      )}

      {draft && (
        <Modal
          title={draft.code ? "Editar pátio" : "Novo pátio"}
          onClose={() => setDraft(null)}
          error={formError}
          width="max-w-xl"
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
                {saving ? "Salvando..." : "Salvar"}
              </button>
            </>
          }
        >
          <div className="space-y-4">
            <Field
              label="Nome"
              required
              value={draft.name}
              onChange={(name) => setDraft({ ...draft, name })}
              placeholder="Pátio Centro, Loja do Joãozinho"
            />

            <Select
              label="De quem é o pátio"
              required
              value={draft.kind}
              onChange={(kind) => setDraft({ ...draft, kind })}
              options={[
                { value: String(YardKind.Own), label: YARD_KIND_LABEL[YardKind.Own] },
                { value: String(YardKind.Partner), label: YARD_KIND_LABEL[YardKind.Partner] },
              ]}
              hint="O pátio da própria revenda fica sem repasse."
            />

            {Number(draft.kind) === YardKind.Partner && (
              <>
                <Field
                  label="Falar com"
                  value={draft.contactName}
                  onChange={(contactName) => setDraft({ ...draft, contactName })}
                  placeholder="Quem responde pela loja"
                />

                <Field
                  label="Telefone"
                  value={draft.contactPhone}
                  onChange={(contactPhone) =>
                    setDraft({ ...draft, contactPhone: maskPhone(contactPhone) })
                  }
                  inputMode="numeric"
                />

                <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto]">
                  <Field
                    label="Repasse combinado"
                    value={draft.cut}
                    onChange={(cut) => setDraft({ ...draft, cut })}
                    inputMode="decimal"
                    hint="Chega preenchido na venda, e quem fecha o negócio pode mudar."
                  />

                  <div className="flex items-end pb-1">
                    <div className="inline-flex overflow-hidden rounded-md border border-[var(--border)]">
                      {[
                        { percent: true, label: "%" },
                        { percent: false, label: "R$" },
                      ].map((option) => (
                        <button
                          key={option.label}
                          type="button"
                          onClick={() => setDraft({ ...draft, cutIsPercent: option.percent })}
                          aria-pressed={draft.cutIsPercent === option.percent}
                          className={[
                            "px-3.5 py-2 text-sm font-semibold transition",
                            draft.cutIsPercent === option.percent
                              ? "bg-[var(--primary)] text-white"
                              : "text-[var(--text-secondary)] hover:bg-[var(--surface-2)]",
                          ].join(" ")}
                        >
                          {option.label}
                        </button>
                      ))}
                    </div>
                  </div>
                </div>
              </>
            )}

            <TextArea
              label="Anotações"
              rows={2}
              value={draft.notes}
              onChange={(notes) => setDraft({ ...draft, notes })}
              placeholder="Combinado, horário, o que for útil lembrar."
            />
          </div>
        </Modal>
      )}

      {toDelete && (
        <Confirmation
          title={`Excluir ${toDelete.name}?`}
          message={
            toDelete.vehicleCount > 0
              ? `Este pátio guarda ${toDelete.vehicleCount === 1 ? "1 carro" : `${toDelete.vehicleCount} carros`}. Mova ${toDelete.vehicleCount === 1 ? "o carro" : "os carros"} para outro pátio antes de excluir.`
              : "O pátio sai da lista e continua guardado, com o histórico dos carros que passaram por ele."
          }
          confirmLabel="Excluir"
          busy={busy}
          error={deleteError}
          onCancel={() => setToDelete(null)}
          onConfirm={() => remove(toDelete)}
        />
      )}
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <dt className="text-[var(--text-secondary)]">{label}</dt>
      <dd className="font-medium">{value}</dd>
    </div>
  );
}
