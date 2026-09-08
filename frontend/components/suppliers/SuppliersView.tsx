"use client";

import { useCallback, useEffect, useState } from "react";
import { Pencil, Plus, Receipt, Store, Tags, Trash2 } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { Field } from "@/components/common/Field";
import { Modal } from "@/components/common/Modal";
import { Select } from "@/components/common/Select";
import { TextArea } from "@/components/common/TextArea";
import { ListBar, ListFrame, useViewMode } from "@/components/common/ViewSwitch";
import { SegmentsModal } from "@/components/suppliers/SegmentsModal";
import { SupplierDashboard } from "@/components/suppliers/SupplierDashboard";
import { SupplierStatementModal } from "@/components/suppliers/SupplierStatementModal";
import { Empty, PageError } from "@/components/vehicles/VehicleUi";
import { apiGet, apiSend } from "@/lib/api";
import { formatDate, formatMoney, isValidCpfOrCnpj, maskCpfCnpj, maskPhone } from "@/lib/masks";
import type { Supplier, SupplierSegment, SupplierStatistics } from "@/lib/types";

/** Onde a escolha de mosaico ou lista fica guardada, no navegador de quem olha (M17). */
const VIEW_KEY = "revendapro.suppliers.view";

type Draft = {
  code: string | null;
  name: string;
  segmentCode: string;
  contactName: string;
  contactPhone: string;
  document: string;
  notes: string;
};

/**
 * De quem a revenda compra serviço e peça.
 *
 * Fornecedor diz **de quem**; tipo de gasto diz **o quê**. A mesma oficina cobra Mecânica num
 * carro e Peças no outro, e é por isso que o gasto aponta para os dois. O ramo é outro cadastro
 * da revenda, administrado daqui mesmo: quem mexe em ramo é quem mexe em fornecedor.
 */
export function SuppliersView({
  initialSuppliers,
  initialSegments,
  initialStatistics,
}: {
  initialSuppliers: Supplier[];
  initialSegments: SupplierSegment[];
  /** O painel desde o início — o período muda isso na tela. */
  initialStatistics: SupplierStatistics;
}) {
  const [suppliers, setSuppliers] = useState(initialSuppliers);
  const [segments, setSegments] = useState(initialSegments);
  const [view, chooseView] = useViewMode(VIEW_KEY);
  const [statistics, setStatistics] = useState(initialStatistics);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [statementOf, setStatementOf] = useState<Supplier | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [toDelete, setToDelete] = useState<Supplier | null>(null);
  const [managingSegments, setManagingSegments] = useState(false);
  const [error, setError] = useState("");
  const [formError, setFormError] = useState("");
  const [deleteError, setDeleteError] = useState("");
  const [saving, setSaving] = useState(false);
  const [busy, setBusy] = useState(false);

  /**
   * Quanto foi para cada um, no período escolhido. Sem período é "desde o início": a pergunta
   * desta tela é acumulada — a leitura do mês mora no painel.
   */
  const reloadSpending = useCallback(async () => {
    const query = new URLSearchParams();

    if (from) query.set("from", from);
    if (to) query.set("to", to);

    const result = await apiGet<SupplierStatistics>(
      `suppliers/statistics${query.size > 0 ? `?${query}` : ""}`,
      "Falha ao carregar o painel de fornecedores.",
    );

    if (result.ok) setStatistics(result.data);
    else setError(result.error);
  }, [from, to]);

  useEffect(() => {
    reloadSpending();
  }, [reloadSpending]);

  async function reload() {
    const [list, kinds] = await Promise.all([
      apiGet<Supplier[]>("suppliers", "Falha ao carregar os fornecedores."),
      apiGet<SupplierSegment[]>("supplier-segments", "Falha ao carregar os ramos."),
    ]);

    if (list.ok) setSuppliers(list.data);
    else setError(list.error);

    if (kinds.ok) setSegments(kinds.data);
    else setError(kinds.error);

    await reloadSpending();
  }

  const spendingOf = new Map(statistics.bySupplier.map((row) => [row.code, row]));

  // Quem mais recebeu vem primeiro: esta tela existe para responder "quanto já foi para cada
  // um", e a ordem é parte da resposta. Empate e zero ficam por nome.
  const ordered = [...suppliers].sort((a, b) => {
    const paid = (spendingOf.get(b.code)?.paidTotal ?? 0) - (spendingOf.get(a.code)?.paidTotal ?? 0);

    return paid !== 0 ? paid : a.name.localeCompare(b.name, "pt-BR");
  });


  async function save() {
    if (!draft) return;

    if (!draft.name.trim()) {
      setFormError("Informe o nome do fornecedor.");
      return;
    }

    if (!draft.segmentCode) {
      setFormError("Escolha o ramo do fornecedor.");
      return;
    }

    const document = draft.document.replace(/\D/g, "");

    if (document && !isValidCpfOrCnpj(document)) {
      setFormError("Informe um CPF ou CNPJ válido.");
      return;
    }

    setSaving(true);
    setFormError("");

    const isNew = draft.code === null;

    const result = await apiSend(
      isNew ? "POST" : "PUT",
      isNew ? "suppliers" : `suppliers/${draft.code}`,
      "Falha ao salvar o fornecedor.",
      {
        name: draft.name.trim(),
        segmentCode: draft.segmentCode,
        contactName: draft.contactName.trim() || null,
        contactPhone: draft.contactPhone.replace(/\D/g, "") || null,
        document: document || null,
        notes: draft.notes.trim() || null,
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

  async function remove(supplier: Supplier) {
    setBusy(true);
    setDeleteError("");

    const result = await apiSend(
      "DELETE",
      `suppliers/${supplier.code}`,
      "Falha ao excluir o fornecedor.",
    );

    setBusy(false);

    if (!result.ok) {
      setDeleteError(result.error);
      return;
    }

    setToDelete(null);
    await reload();
  }

  function edit(supplier: Supplier) {
    setFormError("");
    setDraft({
      code: supplier.code,
      name: supplier.name,
      segmentCode: supplier.segmentCode,
      contactName: supplier.contactName ?? "",
      contactPhone: supplier.contactPhone ? maskPhone(supplier.contactPhone) : "",
      document: supplier.document ? maskCpfCnpj(supplier.document) : "",
      notes: supplier.notes ?? "",
    });
  }

  const segmentOptions = segments.map((segment) => ({
    value: segment.code,
    label: segment.name,
  }));

  /** Editar e excluir, iguais no card e na linha. */
  function actions(supplier: Supplier) {
    return (
      <>
        <button
          type="button"
          onClick={() => edit(supplier)}
          aria-label={`Editar ${supplier.name}`}
          className="rounded-md p-2 text-[var(--text-secondary)] transition hover:bg-[var(--surface-2)] hover:text-[var(--primary)]"
        >
          <Pencil size={15} />
        </button>
        <button
          type="button"
          onClick={() => {
            setDeleteError("");
            setToDelete(supplier);
          }}
          aria-label={`Excluir ${supplier.name}`}
          className="rounded-md p-2 text-[var(--text-secondary)] transition hover:bg-[var(--surface-2)] hover:text-[var(--critical)]"
        >
          <Trash2 size={15} />
        </button>
      </>
    );
  }

  return (
    <div className="dash-anim">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
            Administração
          </p>
          <h1 className="hero-title text-3xl font-bold">Fornecedores</h1>
          <p className="mt-1 max-w-2xl text-sm text-[var(--text-secondary)]">
            De quem você compra serviço e peça: a oficina, a funilaria, a loja de autopeças, o
            despachante. Cada gasto do carro pode dizer de quem foi, e é isso que responde quanto
            já foi para cada um.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => setManagingSegments(true)}
            className="inline-flex items-center gap-2 rounded-md border border-[var(--border)] px-4 py-2.5 text-sm font-semibold text-[var(--text-secondary)] transition hover:bg-[var(--surface-2)]"
          >
            <Tags size={17} />
            Ramos
          </button>

          <button
            type="button"
            onClick={() => {
              setFormError("");
              setDraft({
                code: null,
                name: "",
                segmentCode: segments[0]?.code ?? "",
                contactName: "",
                contactPhone: "",
                document: "",
                notes: "",
              });
            }}
            className="inline-flex items-center gap-2 rounded-md bg-[var(--primary)] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]"
          >
            <Plus size={17} />
            Novo fornecedor
          </button>
        </div>
      </div>

      <PageError message={error} />

      {suppliers.length > 0 && (
        <div className="mb-6 space-y-4">
          <div className="flex flex-wrap items-end justify-between gap-4">
            <div>
              <p className="font-display text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
                Quanto já foi para cada um
              </p>
              <p className="mt-0.5 text-xs text-[var(--text-secondary)]">
                {from || to ? "No período escolhido." : "Desde o início. Escolha um período para ver só uma parte."}
              </p>
            </div>
            <div className="grid max-w-sm grid-cols-2 gap-3">
              <Field label="De" type="date" value={from} onChange={setFrom} placeholder="Início" />
              <Field label="Até" type="date" value={to} onChange={setTo} placeholder="Hoje" />
            </div>
          </div>

          <SupplierDashboard
            stats={statistics}
            onOpenSupplier={(code) => {
              const supplier = suppliers.find((s) => s.code === code);
              if (supplier) setStatementOf(supplier);
            }}
          />

          <p className="font-display pt-2 text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
            O cadastro
          </p>
        </div>
      )}

      <ListBar
        count={suppliers.length}
        singular="fornecedor"
        plural="fornecedores"
        view={view}
        onChange={chooseView}
        label="Como mostrar os fornecedores"
      />

      {suppliers.length === 0 ? (
        <Empty title="Nenhum fornecedor cadastrado. Cadastre o primeiro." />
      ) : view === "list" ? (
        <ListFrame>
          {ordered.map((supplier) => {
            const spend = spendingOf.get(supplier.code);

            return (
              <li
                key={supplier.code}
                className="flex items-center gap-3 px-3 py-2.5 transition hover:bg-[var(--surface-2)] sm:gap-4 sm:px-4"
              >
                <span className="grid h-9 w-9 shrink-0 place-items-center rounded-md bg-[var(--surface-2)] text-[var(--signal)]">
                  <Store size={17} />
                </span>

                <span className="min-w-0 flex-1">
                  <span className="block truncate font-semibold">{supplier.name}</span>
                  <span className="mt-0.5 block truncate text-xs text-[var(--text-muted)]">
                    {supplier.segmentName || "Sem ramo"}
                    {supplier.contactName && ` · ${supplier.contactName}`}
                    {supplier.contactPhone && ` · ${maskPhone(supplier.contactPhone)}`}
                  </span>
                </span>

                <span className="shrink-0 text-right">
                  <span className="num block text-sm font-semibold">{formatMoney(spend?.paidTotal ?? 0)}</span>
                  <span className="num block text-xs text-[var(--text-muted)]">
                    {spend
                      ? `${spend.expenseCount === 1 ? "1 gasto" : `${spend.expenseCount} gastos`}${spend.plannedTotal > 0 ? ` · ${formatMoney(spend.plannedTotal)} previsto` : ""}`
                      : "sem gasto no período"}
                  </span>
                </span>

                <button
                  type="button"
                  onClick={() => setStatementOf(supplier)}
                  aria-label={`Ver gastos de ${supplier.name}`}
                  title="Ver gastos"
                  className="hidden shrink-0 rounded-md p-2 text-[var(--text-secondary)] transition hover:bg-[var(--surface-2)] hover:text-[var(--primary)] sm:block"
                >
                  <Receipt size={15} />
                </button>

                <span className="flex shrink-0 gap-1">{actions(supplier)}</span>
              </li>
            );
          })}
        </ListFrame>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {ordered.map((supplier) => {
            const spend = spendingOf.get(supplier.code);

            return (
              <section
                key={supplier.code}
                className="flex h-full flex-col gap-3 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="flex items-center gap-2 font-semibold">
                      <Store size={16} className="shrink-0 text-[var(--signal)]" />
                      <span className="truncate">{supplier.name}</span>
                    </p>
                    <p className="mt-0.5 text-xs text-[var(--text-secondary)]">
                      {supplier.segmentName || "Sem ramo"}
                    </p>
                  </div>

                  <div className="flex shrink-0 gap-1">{actions(supplier)}</div>
                </div>

                <div className="rounded-lg bg-[var(--surface-2)] px-4 py-3">
                  <p className="text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
                    Pago
                  </p>
                  <p className="num text-xl font-bold">{formatMoney(spend?.paidTotal ?? 0)}</p>
                  <p className="text-xs text-[var(--text-secondary)]">
                    {spend
                      ? `${spend.expenseCount === 1 ? "1 gasto" : `${spend.expenseCount} gastos`}${
                          spend.plannedTotal > 0 ? ` · ${formatMoney(spend.plannedTotal)} previsto` : ""
                        }${spend.lastDate ? ` · último em ${formatDate(spend.lastDate)}` : ""}`
                      : "Nenhum gasto no período"}
                  </p>
                </div>

                <dl className="grid gap-x-6 gap-y-1.5 text-sm">
                  {supplier.contactName && <Row label="Falar com" value={supplier.contactName} />}

                  {supplier.contactPhone && (
                    <Row label="Telefone" value={maskPhone(supplier.contactPhone)} />
                  )}

                  {supplier.document && (
                    <Row
                      label={supplier.document.length === 11 ? "CPF" : "CNPJ"}
                      value={maskCpfCnpj(supplier.document)}
                    />
                  )}
                </dl>

                {supplier.notes && (
                  <p className="text-xs text-[var(--text-secondary)]">{supplier.notes}</p>
                )}

                <button
                  type="button"
                  onClick={() => setStatementOf(supplier)}
                  className="mt-auto inline-flex items-center justify-center gap-2 rounded-md border border-[var(--border)] px-3 py-2 text-sm font-semibold text-[var(--primary)] transition hover:bg-[var(--surface-2)]"
                >
                  <Receipt size={15} />
                  Ver gastos
                </button>
              </section>
            );
          })}
        </div>
      )}

      {draft && (
        <Modal
          title={draft.code ? "Editar fornecedor" : "Novo fornecedor"}
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
              placeholder="Auto Mecânica Silva, Funilaria do Zé"
              maxLength={120}
            />

            <Select
              label="Ramo"
              required
              value={draft.segmentCode}
              onChange={(segmentCode) => setDraft({ ...draft, segmentCode })}
              options={segmentOptions}
              placeholder="Escolha o ramo"
              hint="O que o fornecedor faz. A lista se ajusta no botão Ramos."
            />

            <div className="grid gap-4 sm:grid-cols-2">
              <Field
                label="Falar com"
                value={draft.contactName}
                onChange={(contactName) => setDraft({ ...draft, contactName })}
                placeholder="Quem atende"
                maxLength={120}
              />

              <Field
                label="Telefone"
                value={draft.contactPhone}
                onChange={(contactPhone) =>
                  setDraft({ ...draft, contactPhone: maskPhone(contactPhone) })
                }
                inputMode="numeric"
              />
            </div>

            <Field
              label="CPF ou CNPJ"
              value={draft.document}
              onChange={(document) => setDraft({ ...draft, document: maskCpfCnpj(document) })}
              inputMode="numeric"
              hint="Opcional. Útil para a nota fiscal e para achar o fornecedor depois."
            />

            <TextArea
              label="Anotações"
              rows={2}
              value={draft.notes}
              onChange={(notes) => setDraft({ ...draft, notes })}
              placeholder="Horário, garantia, quem indicou, o que for útil lembrar."
            />
          </div>
        </Modal>
      )}

      {toDelete && (
        <Confirmation
          title={`Excluir ${toDelete.name}?`}
          message={
            toDelete.expenseCount > 0
              ? `Este fornecedor está em ${toDelete.expenseCount === 1 ? "1 gasto" : `${toDelete.expenseCount} gastos`}. Ele fica no cadastro enquanto houver gasto apontando para ele.`
              : "O fornecedor sai da lista e continua guardado."
          }
          confirmLabel="Excluir"
          busy={busy}
          error={deleteError}
          onCancel={() => setToDelete(null)}
          onConfirm={() => remove(toDelete)}
        />
      )}

      {managingSegments && (
        <SegmentsModal
          segments={segments}
          onClose={() => setManagingSegments(false)}
          onChanged={reload}
        />
      )}

      {statementOf && (
        <SupplierStatementModal
          supplier={statementOf}
          from={from}
          to={to}
          onClose={() => setStatementOf(null)}
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
