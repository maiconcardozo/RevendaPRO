"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useState, type ReactNode } from "react";
import { ArrowLeft, ArrowRightLeft, Camera, FileText, History, Pencil, Receipt, Trash2 } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { Modal } from "@/components/common/Modal";
import { Select } from "@/components/common/Select";
import { TextArea } from "@/components/common/TextArea";
import { apiGet, apiSend } from "@/lib/api";
import { formatDate, formatMileage, formatMoney } from "@/lib/masks";
import {
  FUEL_TYPE_LABEL,
  PAYMENT_METHOD_LABEL,
  TRANSMISSION_LABEL,
  VEHICLE_ORIGIN_LABEL,
  VEHICLE_STATUS_LABEL,
  type ExpenseType,
  type Vehicle,
  type VehicleExpense,
} from "@/lib/types";
import { CostPanel } from "./CostPanel";
import { DocumentsPanel } from "./DocumentsPanel";
import { ExpensesPanel } from "./ExpensesPanel";
import { HistoryPanel } from "./HistoryPanel";
import { PhotosPanel } from "./PhotosPanel";
import { VehicleForm, draftOf } from "./VehicleForm";
import { PageError, StatusPill } from "./VehicleUi";

type Tab = "expenses" | "photos" | "documents" | "history" | "sheet";

const TABS: { key: Tab; label: string; icon: typeof Receipt }[] = [
  { key: "expenses", label: "Gastos", icon: Receipt },
  { key: "photos", label: "Fotos", icon: Camera },
  { key: "documents", label: "Documentos", icon: FileText },
  { key: "sheet", label: "Ficha", icon: Pencil },
  { key: "history", label: "Histórico", icon: History },
];

/**
 * The sheet of the vehicle.
 *
 * The cost stays pinned on the left, and never inside a tab, because it is the question that
 * gets asked again with every expense: whoever is entering a part has to watch the total
 * climb and the ceiling shrink without changing screens.
 */
export function VehicleDetail({
  initialVehicle,
  initialExpenses,
  types,
  maxUploadSize,
}: {
  initialVehicle: Vehicle;
  initialExpenses: VehicleExpense[];
  types: ExpenseType[];
  /** Largest accepted file, straight from the server configuration. */
  maxUploadSize: number;
}) {
  const router = useRouter();

  const [vehicle, setVehicle] = useState(initialVehicle);
  const [expenses, setExpenses] = useState(initialExpenses);
  const [tab, setTab] = useState<Tab>("expenses");
  const [editing, setEditing] = useState(false);
  const [moving, setMoving] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  /**
   * Reads the vehicle back after any change.
   *
   * The cost is summed on every read, so entering an expense moves the total, the percentage
   * of the ceiling and what is left at the desired price. Patching the number on screen would
   * repeat the server arithmetic, and one of the two versions would end up wrong.
   */
  const refresh = useCallback(async () => {
    const [readVehicle, readExpenses] = await Promise.all([
      apiGet<Vehicle>(`vehicles/${vehicle.code}`, "Falha ao recarregar o veículo."),
      apiGet<VehicleExpense[]>(`vehicles/${vehicle.code}/expenses`, "Falha ao ler os gastos."),
    ]);

    if (readVehicle.ok) setVehicle(readVehicle.data);
    if (readExpenses.ok) setExpenses(readExpenses.data);

    // The listing shows the cover and the cost, so it has to hear about it too.
    router.refresh();
  }, [vehicle.code, router]);

  async function remove() {
    setBusy(true);

    const result = await apiSend(
      "DELETE",
      `vehicles/${vehicle.code}`,
      "Falha ao excluir o veículo.",
    );

    setBusy(false);

    if (!result.ok) {
      setError(result.error);
      setDeleting(false);
      return;
    }

    router.push("/vehicles");
  }

  return (
    <div className="dash-anim">
      <Link
        href="/vehicles"
        className="mb-4 inline-flex items-center gap-1.5 text-sm font-medium text-[var(--text-secondary)] hover:text-[var(--primary)]"
      >
        <ArrowLeft size={15} />
        Veículos
      </Link>

      <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="mb-1.5 flex flex-wrap items-center gap-3">
            <p className="num font-display text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
              {vehicle.plate}
            </p>
            <StatusPill status={vehicle.status} />
          </div>

          <h1 className="hero-title text-3xl font-bold">
            {vehicle.brand} {vehicle.model}
            {vehicle.version && (
              <span className="font-normal text-[var(--text-secondary)]"> {vehicle.version}</span>
            )}
          </h1>

          <p className="num mt-1 text-sm text-[var(--text-secondary)]">
            {vehicle.modelYear}/{vehicle.manufactureYear} · {formatMileage(vehicle.mileage)}
            <span className="font-sans">
              {vehicle.color && ` · ${vehicle.color}`}
              {` · ${VEHICLE_ORIGIN_LABEL[vehicle.origin]}`}
            </span>
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {vehicle.allowedStatuses.length > 0 && (
            <button
              type="button"
              onClick={() => setMoving(true)}
              className="inline-flex items-center gap-2 rounded-md bg-[var(--primary)] px-3.5 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]"
            >
              <ArrowRightLeft size={16} />
              Mudar situação
            </button>
          )}

          <button
            type="button"
            onClick={() => setEditing(true)}
            className="inline-flex items-center gap-2 rounded-md border border-[var(--border)] px-3.5 py-2 text-sm font-semibold text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)]"
          >
            <Pencil size={15} />
            Editar
          </button>

          <button
            type="button"
            onClick={() => setDeleting(true)}
            aria-label="Excluir veículo"
            title="Excluir veículo"
            className="grid h-9 w-9 place-items-center rounded-md border border-[var(--border)] text-[var(--text-secondary)] transition hover:border-[var(--critical)] hover:text-[var(--critical)]"
          >
            <Trash2 size={15} />
          </button>
        </div>
      </div>

      <PageError message={error} />

      {vehicle.hasDamage && vehicle.damageDescription && (
        <p className="mb-6 rounded-md border border-[color-mix(in_srgb,var(--flare)_45%,transparent)] bg-[color-mix(in_srgb,var(--flare)_10%,transparent)] px-4 py-3 text-sm text-[var(--warning)]">
          <span className="font-semibold">Sinistro: </span>
          {vehicle.damageDescription}
        </p>
      )}

      <div className="grid gap-6 lg:grid-cols-[320px_minmax(0,1fr)]">
        <div className="min-w-0 lg:sticky lg:top-4 lg:self-start">
          <CostPanel vehicle={vehicle} />
        </div>

        {/* min-w-0 because a grid item has min-width auto: without it the expenses table
            pushes the column and the whole page gains horizontal scroll on a phone. */}
        <div className="min-w-0">
          <div className="mb-5 flex flex-wrap gap-1 border-b border-[var(--border)]">
            {TABS.map(({ key, label, icon: Icon }) => (
              <button
                key={key}
                type="button"
                onClick={() => setTab(key)}
                aria-current={tab === key ? "page" : undefined}
                className={[
                  "inline-flex items-center gap-2 border-b-2 px-3.5 py-2.5 text-sm font-semibold transition",
                  tab === key
                    ? "border-[var(--primary)] text-[var(--primary)]"
                    : "border-transparent text-[var(--text-secondary)] hover:text-[var(--text-primary)]",
                ].join(" ")}
              >
                <Icon size={15} />
                {label}
              </button>
            ))}
          </div>

          {tab === "expenses" && (
            <ExpensesPanel
              vehicleCode={vehicle.code}
              types={types}
              initialExpenses={expenses}
              onChanged={refresh}
            />
          )}

          {tab === "photos" && (
            <PhotosPanel
              vehicleCode={vehicle.code}
              maxUploadSize={maxUploadSize}
              onChanged={refresh}
            />
          )}

          {tab === "documents" && (
            <DocumentsPanel vehicleCode={vehicle.code} maxUploadSize={maxUploadSize} />
          )}

          {tab === "history" && <HistoryPanel vehicleCode={vehicle.code} />}

          {tab === "sheet" && <Sheet vehicle={vehicle} />}
        </div>
      </div>

      {editing && (
        <VehicleForm
          draft={draftOf(vehicle)}
          onClose={() => setEditing(false)}
          onSaved={(saved) => {
            setVehicle(saved);
            setEditing(false);
            refresh();
          }}
        />
      )}

      {moving && (
        <MoveStatus
          vehicle={vehicle}
          onClose={() => setMoving(false)}
          onMoved={() => {
            setMoving(false);
            refresh();
          }}
        />
      )}

      {deleting && (
        <Confirmation
          title="Excluir veículo"
          message={
            <>
              Excluir <strong>{vehicle.plate}</strong> tira o carro do estoque e das somas. Os
              gastos, as fotos e os documentos continuam guardados, e um administrador consegue
              trazer tudo de volta.
            </>
          }
          confirmLabel="Excluir"
          danger
          busy={busy}
          onConfirm={remove}
          onCancel={() => setDeleting(false)}
        />
      )}
    </div>
  );
}

/**
 * Moving along the pipeline, offering only what the pipeline allows.
 *
 * The list comes from the server, which builds it from the status machine in the domain. The
 * screen never repeats that rule: if it did, the two versions would disagree one day.
 */
function MoveStatus({
  vehicle,
  onClose,
  onMoved,
}: {
  vehicle: Vehicle;
  onClose: () => void;
  onMoved: () => void;
}) {
  const [status, setStatus] = useState(String(vehicle.allowedStatuses[0] ?? ""));
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  async function move() {
    setSaving(true);
    setError("");

    const result = await apiSend(
      "PATCH",
      `vehicles/${vehicle.code}/status`,
      "Falha ao mudar a situação.",
      { code: vehicle.code, status: Number(status), reason: reason.trim() || null },
    );

    setSaving(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    onMoved();
  }

  return (
    <Modal
      title="Mudar situação"
      onClose={onClose}
      error={error}
      width="max-w-md"
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
            onClick={move}
            disabled={saving || !status}
            className="rounded-md bg-[var(--primary)] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)] disabled:opacity-50"
          >
            {saving ? "Movendo..." : "Mudar"}
          </button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-[var(--text-secondary)]">
          Hoje este carro está em{" "}
          <strong className="text-[var(--text-primary)]">
            {VEHICLE_STATUS_LABEL[vehicle.status]}
          </strong>
          .
        </p>

        <Select
          label="Vai para"
          required
          value={status}
          onChange={setStatus}
          options={vehicle.allowedStatuses.map((value) => ({
            value: String(value),
            label: VEHICLE_STATUS_LABEL[value],
          }))}
        />

        <TextArea
          label="Motivo"
          rows={2}
          value={reason}
          onChange={setReason}
          placeholder="Voltou para a oficina por causa do ar-condicionado."
          hint="Fica no histórico. Opcional."
        />
      </div>
    </Modal>
  );
}

/** The sheet in reading mode: everything registered, with no edit field in the way. */
function Sheet({ vehicle }: { vehicle: Vehicle }) {
  return (
    <div className="space-y-6">
      <Block title="Identificação">
        <Row label="Placa" value={vehicle.plate} mono />
        <Row label="Chassi" value={vehicle.chassis} mono />
        <Row label="Renavam" value={vehicle.renavam ?? "—"} mono />
        <Row label="Combustível" value={FUEL_TYPE_LABEL[vehicle.fuelType] ?? "—"} />
        <Row label="Câmbio" value={TRANSMISSION_LABEL[vehicle.transmission] ?? "—"} />
        <Row label="Quilometragem" value={formatMileage(vehicle.mileage)} mono />
      </Block>

      <Block title="Compra">
        <Row label="Valor" value={formatMoney(vehicle.purchasePrice)} mono />
        <Row label="Data" value={formatDate(vehicle.purchaseDate)} mono />
        <Row label="De quem" value={vehicle.supplierName ?? "—"} />
        <Row
          label="Pagamento"
          value={
            vehicle.purchasePaymentMethod
              ? PAYMENT_METHOD_LABEL[vehicle.purchasePaymentMethod]
              : "—"
          }
        />
        <Row
          label="Teto de orçamento"
          value={vehicle.budgetCeiling ? formatMoney(vehicle.budgetCeiling) : "Sem teto"}
          mono={vehicle.budgetCeiling !== null}
        />
      </Block>

      <Block title="Tabela FIPE">
        <Row
          label="Valor"
          value={vehicle.fipeValue ? formatMoney(vehicle.fipeValue) : "—"}
          mono={vehicle.fipeValue !== null}
        />
        <Row label="Referência" value={formatDate(vehicle.fipeReferenceDate)} mono />
        <Row label="Código" value={vehicle.fipeCode ?? "—"} mono />
      </Block>

      {vehicle.marketNotes && (
        <Block title="O que o mercado pede">
          <p className="text-sm text-[var(--text-secondary)]">{vehicle.marketNotes}</p>
        </Block>
      )}

      {vehicle.notes && (
        <Block title="Anotações">
          <p className="text-sm text-[var(--text-secondary)]">{vehicle.notes}</p>
        </Block>
      )}
    </div>
  );
}

function Block({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]">
      <p className="font-display mb-3 text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
        {title}
      </p>
      <dl className="grid gap-x-6 gap-y-2.5 sm:grid-cols-2">{children}</dl>
    </section>
  );
}

function Row({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex items-baseline justify-between gap-3 text-sm">
      <dt className="text-[var(--text-secondary)]">{label}</dt>
      <dd className={["font-medium", mono ? "num" : ""].join(" ")}>{value}</dd>
    </div>
  );
}
