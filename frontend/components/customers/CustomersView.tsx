"use client";

import { useEffect, useState } from "react";
import { Contact, MessageCircle, Pencil, Plus, Search, Trash2, UserRound } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { ExportButtons } from "@/components/common/ExportButtons";
import { Field } from "@/components/common/Field";
import { Modal } from "@/components/common/Modal";
import { TextArea } from "@/components/common/TextArea";
import { ListBar, ListFrame, useViewMode } from "@/components/common/ViewSwitch";
import { CustomerDetailModal } from "@/components/customers/CustomerDetailModal";
import { Empty, PageError } from "@/components/vehicles/VehicleUi";
import { apiGet, apiSend } from "@/lib/api";
import { formatDate, formatMoney, isValidCpfOrCnpj, maskCpfCnpj, maskPhone } from "@/lib/masks";
import { whatsappUrl } from "@/lib/share";
import type { Customer } from "@/lib/types";

/** Onde a escolha de mosaico ou lista fica guardada, no navegador de quem olha (M17). */
const VIEW_KEY = "revendapro.customers.view";

type Draft = {
  code: string | null;
  name: string;
  document: string;
  phone: string;
  email: string;
  address: string;
  notes: string;
  /** A pessoa já disse que o telefone repetido é de outra pessoa. */
  confirmSamePhone: boolean;
};

/**
 * Quem a revenda conhece (M21): quem ofereceu, quem comprou, quem volta.
 *
 * Cliente antes de comprador: a pessoa recusada em junho está aqui desde a primeira proposta.
 * A lista mostra o que cada um já fez; a ficha conta a história; o WhatsApp abre no número.
 */
export function CustomersView({
  initialCustomers,
  openCode,
}: {
  initialCustomers: Customer[];
  /** Um cliente para abrir ao chegar, vindo da lista de vendas ou do card da proposta. */
  openCode: string | null;
}) {
  const [customers, setCustomers] = useState(initialCustomers);
  const [view, chooseView] = useViewMode(VIEW_KEY);
  const [search, setSearch] = useState("");
  const [draft, setDraft] = useState<Draft | null>(null);
  const [detailOf, setDetailOf] = useState<Customer | null>(
    () => initialCustomers.find((customer) => customer.code === openCode) ?? null,
  );
  const [toDelete, setToDelete] = useState<Customer | null>(null);
  const [error, setError] = useState("");
  const [formError, setFormError] = useState("");
  const [deleteError, setDeleteError] = useState("");
  const [saving, setSaving] = useState(false);
  const [busy, setBusy] = useState(false);

  // A busca é do banco, com um respiro para o servidor receber o que a pessoa quis digitar.
  useEffect(() => {
    const timer = setTimeout(async () => {
      const result = await apiGet<Customer[]>(
        `customers${search.trim() ? `?search=${encodeURIComponent(search.trim())}` : ""}`,
        "Falha ao carregar os clientes.",
      );

      if (result.ok) setCustomers(result.data);
      else setError(result.error);
    }, search ? 250 : 0);

    return () => clearTimeout(timer);
  }, [search]);

  async function reload() {
    const result = await apiGet<Customer[]>(
      `customers${search.trim() ? `?search=${encodeURIComponent(search.trim())}` : ""}`,
      "Falha ao carregar os clientes.",
    );

    if (result.ok) {
      setCustomers(result.data);
      if (detailOf) {
        setDetailOf(result.data.find((customer) => customer.code === detailOf.code) ?? null);
      }
    } else {
      setError(result.error);
    }
  }

  function openNew() {
    setFormError("");
    setDraft({
      code: null,
      name: "",
      document: "",
      phone: "",
      email: "",
      address: "",
      notes: "",
      confirmSamePhone: false,
    });
  }

  function edit(customer: Customer) {
    setFormError("");
    setDraft({
      code: customer.code,
      name: customer.name,
      document: customer.document ? maskCpfCnpj(customer.document) : "",
      phone: customer.phone ? maskPhone(customer.phone) : "",
      email: customer.email ?? "",
      address: customer.address ?? "",
      notes: customer.notes ?? "",
      confirmSamePhone: false,
    });
  }

  async function save() {
    if (!draft) return;

    if (!draft.name.trim()) {
      setFormError("Informe o nome do cliente.");
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
      isNew ? "customers" : `customers/${draft.code}`,
      "Falha ao salvar o cliente.",
      {
        name: draft.name.trim(),
        document: document || null,
        phone: draft.phone.replace(/\D/g, "") || null,
        email: draft.email.trim() || null,
        address: draft.address.trim() || null,
        notes: draft.notes.trim() || null,
        confirmSamePhone: draft.confirmSamePhone,
      },
    );

    setSaving(false);

    if (!result.ok) {
      // O aviso de telefone repetido vem com a opção de seguir: a segunda tentativa confirma.
      if (result.error.includes("confirme que é outra pessoa")) {
        setDraft({ ...draft, confirmSamePhone: true });
        setFormError(`${result.error} Salve de novo para confirmar.`);
        return;
      }

      setFormError(result.error);
      return;
    }

    setDraft(null);
    await reload();
  }

  async function remove(customer: Customer) {
    setBusy(true);
    setDeleteError("");

    const result = await apiSend("DELETE", `customers/${customer.code}`, "Falha ao excluir o cliente.");

    setBusy(false);

    if (!result.ok) {
      setDeleteError(result.error);
      return;
    }

    setToDelete(null);
    setDetailOf(null);
    await reload();
  }

  /** Editar e excluir, iguais no card e na linha. */
  function actions(customer: Customer) {
    return (
      <>
        <button
          type="button"
          onClick={() => edit(customer)}
          aria-label={`Editar ${customer.name}`}
          className="rounded-md p-2 text-[var(--text-secondary)] transition hover:bg-[var(--surface-2)] hover:text-[var(--primary)]"
        >
          <Pencil size={15} />
        </button>
        <button
          type="button"
          onClick={() => {
            setDeleteError("");
            setToDelete(customer);
          }}
          aria-label={`Excluir ${customer.name}`}
          className="rounded-md p-2 text-[var(--text-secondary)] transition hover:bg-[var(--surface-2)] hover:text-[var(--critical)]"
        >
          <Trash2 size={15} />
        </button>
      </>
    );
  }

  function history(customer: Customer) {
    const parts: string[] = [];

    if (customer.saleCount > 0) {
      parts.push(customer.saleCount === 1 ? "1 carro comprado" : `${customer.saleCount} carros comprados`);
    }

    if (customer.proposalCount > 0) {
      parts.push(customer.proposalCount === 1 ? "1 proposta" : `${customer.proposalCount} propostas`);
    }

    return parts.length > 0 ? parts.join(" · ") : "Só cadastrado, por enquanto";
  }

  const exportQuery = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : "";

  return (
    <div className="dash-anim">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
            Operação
          </p>
          <h1 className="hero-title text-3xl font-bold">Clientes</h1>
          <p className="mt-1 max-w-2xl text-sm text-[var(--text-secondary)]">
            Quem ofereceu, quem comprou, quem volta. Toda proposta e toda venda apontam para
            alguém daqui, e a ficha conta a história: os carros que a pessoa quis, os que levou,
            e o WhatsApp para chamar quando chegar o próximo.
          </p>
        </div>

        <button
          type="button"
          onClick={openNew}
          className="inline-flex items-center gap-2 rounded-md bg-[var(--primary)] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]"
        >
          <Plus size={17} />
          Novo cliente
        </button>
      </div>

      <PageError message={error} />

      <div className="mb-4 max-w-md">
        <label className="relative block">
          <Search
            size={16}
            className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[var(--text-muted)]"
          />
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Buscar por nome, telefone ou CPF"
            aria-label="Buscar cliente"
            className="control pl-9"
          />
        </label>
      </div>

      <ListBar
        count={customers.length}
        singular="cliente"
        plural="clientes"
        view={view}
        onChange={chooseView}
        label="Como mostrar os clientes"
      >
        <ExportButtons path={`exports/customers${exportQuery}`} name="Clientes" onError={setError} />
      </ListBar>

      {customers.length === 0 ? (
        <Empty
          title={
            search.trim()
              ? `Nenhum cliente com "${search.trim()}".`
              : "Nenhum cliente ainda. O primeiro nasce na primeira proposta, ou aqui."
          }
        />
      ) : view === "list" ? (
        <ListFrame>
          {customers.map((customer) => (
            <li
              key={customer.code}
              className="flex items-center gap-3 px-3 py-2.5 transition hover:bg-[var(--surface-2)] sm:gap-4 sm:px-4"
            >
              <span className="grid h-9 w-9 shrink-0 place-items-center rounded-md bg-[var(--surface-2)] text-[var(--signal)]">
                <UserRound size={17} />
              </span>

              <button
                type="button"
                onClick={() => setDetailOf(customer)}
                className="min-w-0 flex-1 text-left"
                title="Abrir a ficha"
              >
                <span className="block truncate font-semibold hover:text-[var(--primary)]">{customer.name}</span>
                <span className="mt-0.5 block truncate text-xs text-[var(--text-muted)]">
                  {customer.phone ? maskPhone(customer.phone) : "Sem telefone"}
                  {customer.document && ` · ${maskCpfCnpj(customer.document)}`}
                </span>
              </button>

              <span className="hidden shrink-0 text-right sm:block">
                <span className="num block text-sm font-semibold">{formatMoney(customer.boughtTotal)}</span>
                <span className="block text-xs text-[var(--text-muted)]">{history(customer)}</span>
              </span>

              {customer.phone && (
                <a
                  href={whatsappUrl(customer.phone, `Olá, ${customer.name.split(" ")[0]}!`)}
                  target="_blank"
                  rel="noreferrer"
                  aria-label={`WhatsApp de ${customer.name}`}
                  title="Abrir o WhatsApp"
                  className="hidden shrink-0 rounded-md p-2 text-[var(--text-secondary)] transition hover:bg-[var(--surface-2)] hover:text-[var(--success)] sm:block"
                >
                  <MessageCircle size={15} />
                </a>
              )}

              <span className="flex shrink-0 gap-1">{actions(customer)}</span>
            </li>
          ))}
        </ListFrame>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {customers.map((customer) => (
            <section
              key={customer.code}
              className="flex h-full flex-col gap-3 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]"
            >
              <div className="flex items-start justify-between gap-3">
                <button type="button" onClick={() => setDetailOf(customer)} className="min-w-0 text-left" title="Abrir a ficha">
                  <p className="flex items-center gap-2 font-semibold hover:text-[var(--primary)]">
                    <UserRound size={16} className="shrink-0 text-[var(--signal)]" />
                    <span className="truncate">{customer.name}</span>
                  </p>
                  <p className="mt-0.5 text-xs text-[var(--text-secondary)]">
                    {customer.phone ? maskPhone(customer.phone) : "Sem telefone"}
                  </p>
                </button>

                <div className="flex shrink-0 gap-1">{actions(customer)}</div>
              </div>

              <div className="rounded-lg bg-[var(--surface-2)] px-4 py-3">
                <p className="text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
                  Total comprado
                </p>
                <p className="num text-xl font-bold">{formatMoney(customer.boughtTotal)}</p>
                <p className="text-xs text-[var(--text-secondary)]">
                  {history(customer)}
                  {customer.lastDate && ` · último contato em ${formatDate(customer.lastDate)}`}
                </p>
              </div>

              <dl className="grid gap-x-6 gap-y-1.5 text-sm">
                {customer.document && (
                  <Row
                    label={customer.document.length === 11 ? "CPF" : "CNPJ"}
                    value={maskCpfCnpj(customer.document)}
                  />
                )}
                {customer.email && <Row label="E-mail" value={customer.email} />}
                {customer.address && <Row label="Endereço" value={customer.address} />}
              </dl>

              {customer.notes && <p className="text-xs text-[var(--text-secondary)]">{customer.notes}</p>}

              <div className="mt-auto grid grid-cols-2 gap-2">
                <button
                  type="button"
                  onClick={() => setDetailOf(customer)}
                  className="inline-flex items-center justify-center gap-2 rounded-md border border-[var(--border)] px-3 py-2 text-sm font-semibold text-[var(--primary)] transition hover:bg-[var(--surface-2)]"
                >
                  <Contact size={15} />
                  Ficha
                </button>
                {customer.phone ? (
                  <a
                    href={whatsappUrl(customer.phone, `Olá, ${customer.name.split(" ")[0]}!`)}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center justify-center gap-2 rounded-md border border-[var(--border)] px-3 py-2 text-sm font-semibold text-[var(--text-secondary)] transition hover:border-[var(--success)] hover:text-[var(--success)]"
                  >
                    <MessageCircle size={15} />
                    WhatsApp
                  </a>
                ) : (
                  <span className="inline-flex items-center justify-center rounded-md border border-dashed border-[var(--border)] px-3 py-2 text-xs text-[var(--text-muted)]">
                    Sem telefone
                  </span>
                )}
              </div>
            </section>
          ))}
        </div>
      )}

      {draft && (
        <Modal
          title={draft.code ? "Editar cliente" : "Novo cliente"}
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
                {saving ? "Salvando..." : draft.confirmSamePhone ? "Salvar mesmo assim" : "Salvar"}
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
              placeholder="Como a pessoa se apresenta"
              maxLength={120}
            />

            <div className="grid gap-4 sm:grid-cols-2">
              <Field
                label="Telefone"
                type="tel"
                inputMode="tel"
                mask={maskPhone}
                value={draft.phone}
                onChange={(phone) => setDraft({ ...draft, phone, confirmSamePhone: false })}
                placeholder="(00) 00000-0000"
                hint="É por onde o WhatsApp chega."
              />

              <Field
                label="CPF ou CNPJ"
                inputMode="numeric"
                mask={maskCpfCnpj}
                value={draft.document}
                onChange={(document) => setDraft({ ...draft, document })}
                placeholder="000.000.000-00"
                hint="Vai na proposta em PDF e na venda."
              />
            </div>

            <Field
              label="E-mail"
              type="email"
              inputMode="email"
              value={draft.email}
              onChange={(email) => setDraft({ ...draft, email })}
              maxLength={160}
            />

            <Field
              label="Endereço"
              value={draft.address}
              onChange={(address) => setDraft({ ...draft, address })}
              placeholder="Rua, número, bairro, cidade"
              maxLength={240}
            />

            <TextArea
              label="Anotações"
              rows={2}
              value={draft.notes}
              onChange={(notes) => setDraft({ ...draft, notes })}
              placeholder="Prefere carro branco, indicado pelo Zé, o que for útil lembrar."
            />
          </div>
        </Modal>
      )}

      {toDelete && (
        <Confirmation
          title={`Excluir ${toDelete.name}?`}
          message={
            toDelete.proposalCount > 0 || toDelete.saleCount > 0
              ? `${toDelete.name} tem história na loja: ${history(toDelete)}. O cliente fica no cadastro enquanto houver proposta ou venda apontando para ele.`
              : "O cliente sai da lista e continua guardado."
          }
          confirmLabel="Excluir"
          busy={busy}
          error={deleteError}
          onCancel={() => setToDelete(null)}
          onConfirm={() => remove(toDelete)}
        />
      )}

      {detailOf && (
        <CustomerDetailModal
          customer={detailOf}
          onClose={() => setDetailOf(null)}
          onEdit={(customer) => {
            setDetailOf(null);
            edit(customer);
          }}
        />
      )}
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <dt className="text-[var(--text-secondary)]">{label}</dt>
      <dd className="truncate font-medium">{value}</dd>
    </div>
  );
}
