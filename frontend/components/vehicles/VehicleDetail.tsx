"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState, type ReactNode } from "react";
import { ArrowLeft, ArrowRightLeft, Camera, FileText, HandCoins, History, MapPin, Pencil, Receipt, RefreshCw, Search, Trash2 } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { Modal } from "@/components/common/Modal";
import { Select } from "@/components/common/Select";
import { TextArea } from "@/components/common/TextArea";
import { apiGet, apiSend } from "@/lib/api";
import { formatDate, formatMeses, formatMileage, formatMoney, formatMonth } from "@/lib/masks";
import {
  FIPE_SOURCE_LABEL,
  FUEL_TYPE_LABEL,
  PAYMENT_METHOD_LABEL,
  TRANSMISSION_LABEL,
  VEHICLE_ORIGIN_LABEL,
  VEHICLE_STATUS_LABEL,
  VehicleStatus,
  type ExpenseType,
  type FipeCandidate,
  type FipeMatch,
  type FipeOption,
  type FipeReference,
  type Proposal,
  type Sale,
  type Vehicle,
  type VehicleExpense,
  type Yard,
} from "@/lib/types";
import { CostPanel } from "./CostPanel";
import { DocumentsPanel } from "./DocumentsPanel";
import { ExpensesPanel } from "./ExpensesPanel";
import { TimelinePanel } from "./TimelinePanel";
import { PhotosPanel } from "./PhotosPanel";
import { ProposalsPanel } from "./ProposalsPanel";
import { SaleBanner } from "./SaleBanner";
import { SaleModal } from "./SaleModal";
import { VehicleForm, draftOf } from "./VehicleForm";
import { PageError, StatusPill } from "./VehicleUi";

type Tab = "expenses" | "proposals" | "photos" | "documents" | "timeline" | "sheet";

const TABS: { key: Tab; label: string; icon: typeof Receipt }[] = [
  { key: "expenses", label: "Gastos", icon: Receipt },
  { key: "proposals", label: "Propostas", icon: HandCoins },
  { key: "photos", label: "Fotos", icon: Camera },
  { key: "documents", label: "Documentos", icon: FileText },
  { key: "sheet", label: "Ficha", icon: Pencil },
  { key: "timeline", label: "Linha do tempo", icon: History },
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
  canSell,
  yards = [],
}: {
  initialVehicle: Vehicle;
  initialExpenses: VehicleExpense[];
  types: ExpenseType[];
  /** Largest accepted file, straight from the server configuration. */
  maxUploadSize: number;
  /** Whether the person holds the sales screen. Without it the sale actions stay hidden. */
  canSell: boolean;
  /**
   * Os pátios cadastrados. Vem vazio para quem não tem a tela de pátios, e aí o botão de mudar
   * de lugar some — onde o carro está continua visível, porque isso é informação da ficha.
   */
  yards?: Yard[];
}) {
  const router = useRouter();

  const [vehicle, setVehicle] = useState(initialVehicle);
  const [expenses, setExpenses] = useState(initialExpenses);
  const [tab, setTab] = useState<Tab>("expenses");
  const [editing, setEditing] = useState(false);
  const [moving, setMoving] = useState(false);
  const [movingYard, setMovingYard] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  /** The proposal being turned into a sale, or a bare object for a sale that walked in. */
  const [selling, setSelling] = useState<{ proposal: Proposal | null } | null>(null);
  const [sale, setSale] = useState<Sale | null>(null);

  const isSold = vehicle.status === VehicleStatus.Sold;

  // The car can take a buyer from ready, advertised or negotiating. Mirrors Vehicle.Sellable.
  const sellable = [4, 5, 6].includes(vehicle.status);

  // The sale is read only when the car is sold: a car on the lot has none, and asking would
  // be one more round trip on every sheet.
  useEffect(() => {
    if (!isSold || !canSell) {
      setSale(null);
      return;
    }

    apiGet<Sale | null>(`vehicles/${vehicle.code}/sale`, "Falha ao carregar a venda.").then(
      (result) => setSale(result.ok ? result.data : null),
    );
  }, [isSold, canSell, vehicle.code]);

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

          {vehicle.yard && (
            <p className="mt-1.5 inline-flex items-center gap-1.5 text-sm text-[var(--text-secondary)]">
              <MapPin size={14} className="text-[var(--signal)]" />
              {vehicle.yard.name}
            </p>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {canSell && sellable && (
            <button
              type="button"
              onClick={() => setSelling({ proposal: null })}
              className="inline-flex items-center gap-2 rounded-md bg-[var(--success)] px-3.5 py-2 text-sm font-semibold text-white transition hover:brightness-110"
            >
              <HandCoins size={16} />
              Vender
            </button>
          )}

          {yards.length > 0 && (
            <button
              type="button"
              onClick={() => setMovingYard(true)}
              className="inline-flex items-center gap-2 rounded-md border border-[var(--border)] px-3.5 py-2 text-sm font-semibold text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)]"
            >
              <MapPin size={15} />
              Mudar de pátio
            </button>
          )}

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

      {sale && (
        <SaleBanner
          vehicleCode={vehicle.code}
          sale={sale}
          canSell={canSell}
          onCancelled={() => {
            setSale(null);
            refresh();
          }}
        />
      )}

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

          {tab === "proposals" && (
            <ProposalsPanel
              vehicleCode={vehicle.code}
              canSell={canSell && sellable}
              onSell={(proposal) => setSelling({ proposal })}
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

          {tab === "timeline" && <TimelinePanel vehicleCode={vehicle.code} />}

          {tab === "sheet" && <Sheet vehicle={vehicle} onFipeUpdated={refresh} />}
        </div>
      </div>

      {editing && (
        <VehicleForm
          draft={draftOf(vehicle)}
          yards={yards}
          onClose={() => setEditing(false)}
          onSaved={(saved) => {
            setVehicle(saved);
            setEditing(false);
            refresh();
          }}
        />
      )}

      {selling && (
        <SaleModal
          vehicle={vehicle}
          proposal={selling.proposal}
          onClose={() => setSelling(null)}
          onSold={(sold) => {
            setSelling(null);
            setSale(sold);
            refresh();
          }}
        />
      )}

      {movingYard && (
        <MoveYard
          vehicle={vehicle}
          yards={yards}
          onClose={() => setMovingYard(false)}
          onMoved={() => {
            setMovingYard(false);
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
/**
 * Mudar o carro de lugar.
 *
 * Separado de "mudar situação" porque são duas perguntas diferentes: uma é onde o carro está
 * na esteira, a outra é onde ele está no mundo. Um carro pronto para venda pode estar no pátio
 * da casa ou na loja de um parceiro, e misturar as duas coisas perderia justamente a resposta
 * que interessa depois — quanto tempo ele ficou lá, e se voltou sem vender.
 */
function MoveYard({
  vehicle,
  yards,
  onClose,
  onMoved,
}: {
  vehicle: Vehicle;
  yards: Yard[];
  onClose: () => void;
  onMoved: () => void;
}) {
  const [yardCode, setYardCode] = useState(vehicle.yard?.code ?? "");
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  const same = (yardCode || null) === (vehicle.yard?.code ?? null);

  async function move() {
    setSaving(true);
    setError("");

    const result = await apiSend(
      "PATCH",
      `vehicles/${vehicle.code}/yard`,
      "Falha ao mudar o carro de pátio.",
      { code: vehicle.code, yardCode: yardCode || null, reason: reason.trim() || null },
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
      title="Mudar de pátio"
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
            disabled={saving || same}
            className="rounded-md bg-[var(--primary)] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)] disabled:opacity-50"
          >
            {saving ? "Movendo..." : "Mudar"}
          </button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-[var(--text-secondary)]">
          {vehicle.yard ? (
            <>
              Hoje este carro está em{" "}
              <strong className="text-[var(--text-primary)]">{vehicle.yard.name}</strong>.
            </>
          ) : (
            "Este carro ainda está sem pátio definido."
          )}
        </p>

        <Select
          label="Vai para"
          value={yardCode}
          onChange={setYardCode}
          options={yards.map((yard) => ({ value: yard.code, label: yard.name }))}
          placeholder="Sem pátio definido"
        />

        <TextArea
          label="Motivo"
          rows={2}
          value={reason}
          onChange={setReason}
          placeholder="Levado para a Loja do Joãozinho para exposição."
          hint="Fica no histórico. Opcional."
        />
      </div>
    </Modal>
  );
}

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
function Sheet({ vehicle, onFipeUpdated }: { vehicle: Vehicle; onFipeUpdated: () => Promise<void> }) {
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

      <FipeBlock vehicle={vehicle} onUpdated={onFipeUpdated} />

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

/**
 * A tabela de referência, e o botão que vai buscá-la.
 *
 * Ela fica ao lado do custo e dos preços de propósito: a decisão de preço é feita olhando as
 * três coisas juntas. E o botão escreve **apenas** a referência — quanto a revenda quer
 * receber, o mínimo que aceita e o anunciado continuam sendo de quem entende do carro.
 */
function FipeBlock({ vehicle, onUpdated }: { vehicle: Vehicle; onUpdated: () => Promise<void> }) {
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [failure, setFailure] = useState("");
  const [choosing, setChoosing] = useState(false);
  const [candidates, setCandidates] = useState<FipeCandidate[] | null>(null);

  /** Conta o que a tabela respondeu, do mesmo jeito para o botão e para o escolhedor. */
  function announce(reference: FipeReference) {
    // O quanto a referência andou é a informação que o número sozinho esconde: um carro
    // parado perde valor de tabela todo mês, e é isso que o painel do M11 vai medir.
    const moved =
      reference.previousValue !== null && reference.previousValue !== reference.value
        ? ` ${reference.value > reference.previousValue ? "Subiu" : "Caiu"} ` +
          `${formatMoney(Math.abs(reference.value - reference.previousValue))} ` +
          `em relação ao que a ficha trazia.`
        : "";

    // O nome que a tabela imprime costuma terminar em ponto ("4p Aut."), e a frase acrescenta
    // o dela. Uma condição lê melhor do que um padrão — e o padrão que eu tinha escrito aqui
    // comia o nome inteiro, o que só a foto da tela mostrou.
    const nome = reference.model.endsWith(".") ? reference.model.slice(0, -1) : reference.model;

    setMessage(
      `A tabela de ${formatMonth(reference.referenceMonth)} diz ` +
        `${formatMoney(reference.value)} para ${nome}.${moved}`,
    );
  }

  /**
   * O botão que sempre busca.
   *
   * Com código, ele pergunta o preço direto. Sem código, ele procura o modelo antes — e a
   * busca resolve sozinha quando sobra um candidato só. Antes deste caminho o botão
   * simplesmente sumia no carro sem código, e a ficha não dizia por quê.
   */
  async function sync() {
    if (vehicle.fipeCode) {
      await refresh();
      return;
    }

    setBusy(true);
    setMessage("");
    setFailure("");

    const result = await apiSend<FipeMatch>(
      "POST",
      `vehicles/${vehicle.code}/fipe/match`,
      "Falha ao procurar o modelo na tabela FIPE.",
    );

    setBusy(false);

    if (!result.ok) {
      setFailure(result.error);
      return;
    }

    if (result.data.applied) {
      announce(result.data.applied);
      await onUpdated();
      return;
    }

    if (result.data.candidates.length > 0) {
      setCandidates(result.data.candidates);
      return;
    }

    // A tabela segue sem este carro pelo nome que ele tem cadastrado. O caminho longo continua
    // aberto, e é onde a pessoa acha o modelo pela lista inteira.
    setFailure(
      "A tabela segue sem um modelo parecido com este. Use Achar o modelo para escolher na lista.",
    );
  }

  async function refresh() {
    setBusy(true);
    setMessage("");
    setFailure("");

    const result = await apiSend<FipeReference>(
      "POST",
      `vehicles/${vehicle.code}/fipe`,
      "Falha ao consultar a tabela FIPE.",
    );

    if (!result.ok) {
      setBusy(false);
      setFailure(result.error);
      return;
    }

    announce(result.data);

    await onUpdated();
    setBusy(false);
  }

  /** Fecha o escolhedor e conta o que ele trouxe. */
  async function chosen(reference: FipeReference) {
    setChoosing(false);
    setFailure("");
    announce(reference);

    await onUpdated();
  }

  return (
    <section className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]">
      <div className="mb-3 flex items-center justify-between gap-3">
        <p className="font-display text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
          Tabela FIPE
        </p>

        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setChoosing(true)}
            className="inline-flex items-center gap-1.5 rounded-md border border-[var(--border)] px-2.5 py-1.5 text-xs font-semibold text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)]"
          >
            <Search size={13} />
            {vehicle.fipeCode ? "Trocar modelo" : "Achar o modelo"}
          </button>

          {/* Vale nos dois casos: com código pergunta o preço, sem código procura o modelo. */}
          <button
            type="button"
            onClick={sync}
            disabled={busy}
            className="inline-flex items-center gap-1.5 rounded-md border border-[var(--border)] px-2.5 py-1.5 text-xs font-semibold text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)] disabled:opacity-60"
          >
            <RefreshCw size={13} className={busy ? "animate-spin" : ""} />
            {busy ? "Consultando…" : "Consultar agora"}
          </button>
        </div>
      </div>

      <dl className="grid gap-x-6 gap-y-2.5 sm:grid-cols-2">
        <Row
          label="Valor"
          value={vehicle.fipeValue ? formatMoney(vehicle.fipeValue) : "—"}
          mono={vehicle.fipeValue !== null}
        />
        <Row label="Referência" value={formatMonth(vehicle.fipeReferenceDate)} />
        <Row label="Código" value={vehicle.fipeCode ?? "—"} mono />
        <Row
          label="Origem"
          value={vehicle.fipeSource ? FIPE_SOURCE_LABEL[vehicle.fipeSource] : "—"}
        />
      </dl>

      {/* Um carro parado perde valor de tabela todo mês. Enquanto o número da ficha for de
          dois meses atrás, quem está decidindo preço decide por um mercado que já mudou. */}
      {!message && (vehicle.fipeMonthsBehind ?? 0) > 0 && (
        <p className="mt-3 rounded-md border border-[color-mix(in_srgb,var(--warning)_35%,transparent)] bg-[color-mix(in_srgb,var(--warning)_8%,transparent)] px-3 py-2 text-xs text-[var(--text-secondary)]">
          Esta referência é de {formatMeses(vehicle.fipeMonthsBehind!)} atrás. O pátio se
          atualiza sozinho todo mês, e o botão acima traz a tabela de agora.
        </p>
      )}

      {message && (
        <p className="mt-3 rounded-md border border-[color-mix(in_srgb,var(--success)_35%,transparent)] bg-[color-mix(in_srgb,var(--success)_8%,transparent)] px-3 py-2 text-xs text-[var(--text-secondary)]">
          {message}
        </p>
      )}

      {failure && (
        <p
          role="alert"
          className="mt-3 rounded-md border border-[color-mix(in_srgb,var(--critical)_40%,transparent)] bg-[color-mix(in_srgb,var(--critical)_8%,transparent)] px-3 py-2 text-xs text-[var(--critical)]"
        >
          {failure}
        </p>
      )}

      {choosing && (
        <FipeChooser
          vehicle={vehicle}
          onClose={() => setChoosing(false)}
          onChosen={chosen}
        />
      )}

      {candidates && (
        <FipeCandidates
          vehicle={vehicle}
          candidates={candidates}
          onClose={() => setCandidates(null)}
          onBrowse={() => {
            setCandidates(null);
            setChoosing(true);
          }}
          onChosen={async (reference) => {
            setCandidates(null);
            await chosen(reference);
          }}
        />
      )}
    </section>
  );
}

/**
 * O que sobrou da busca, para a pessoa confirmar qual é.
 *
 * A tabela chama de "modelo" a versão inteira, e duas versões do mesmo carro são dois preços —
 * às vezes dezenas de milhares distantes. Por isso o sistema para aqui: ele estreitou de cem
 * para poucos, e a escolha entre preços é de quem conhece o carro.
 *
 * O ano vem junto de cada candidato quando a busca conseguiu conferir. Um candidato que a
 * tabela jamais precificou no ano do carro chega sem ano nenhum, e aí a lista completa dele é
 * buscada na hora — porque recusar a escolha seria pior do que mostrar os anos que existem.
 */
function FipeCandidates({
  vehicle,
  candidates,
  onClose,
  onBrowse,
  onChosen,
}: {
  vehicle: Vehicle;
  candidates: FipeCandidate[];
  onClose: () => void;
  /** Abre a lista inteira da marca, para quando nenhum candidato responde pelo ano. */
  onBrowse: () => void;
  onChosen: (reference: FipeReference) => Promise<void>;
}) {
  const [picked, setPicked] = useState<FipeCandidate | null>(null);
  const [years, setYears] = useState<FipeOption[]>([]);
  const [year, setYear] = useState("");
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  // A busca só devolve candidato sem ano quando camada nenhuma respondeu pelo ano do carro.
  const semOAno = candidates.every((candidate) => candidate.years.length === 0);

  async function pick(candidate: FipeCandidate) {
    setPicked(candidate);
    setError("");

    if (candidate.years.length > 0) {
      setYears(candidate.years);
      setYear(candidate.years.length === 1 ? candidate.years[0].code : "");
      return;
    }

    // Sem ano conferido, a lista inteira deste modelo resolve — e ela mostra, de quebra, em que
    // anos a tabela precificou esta versão.
    setLoading(true);
    setYears([]);
    setYear("");

    const result = await apiGet<FipeOption[]>(
      `fipe/brands/${candidate.brandCode}/models/${candidate.modelCode}/years`,
      "Falha ao carregar os anos deste modelo.",
    );

    setLoading(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    setYears(result.data);

    const doAno = result.data.filter((option) => option.code.startsWith(`${vehicle.modelYear}-`));

    if (doAno.length === 1) setYear(doAno[0].code);
  }

  async function use() {
    if (!picked || !year) return;

    setSaving(true);
    setError("");

    const result = await apiSend<FipeReference>(
      "POST",
      `vehicles/${vehicle.code}/fipe/model`,
      "Falha ao definir o modelo da tabela.",
      { brandCode: picked.brandCode, modelCode: picked.modelCode, yearFuel: year },
    );

    setSaving(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    await onChosen(result.data);
  }

  return (
    <Modal
      title={candidates.length === 1 ? "Confirme o modelo" : "Qual destes é o carro?"}
      onClose={onClose}
      error={error}
      width="max-w-xl"
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
            onClick={use}
            disabled={saving || !year}
            className="rounded-md bg-[var(--primary)] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)] disabled:opacity-50"
          >
            {saving ? "Consultando..." : "Usar este modelo"}
          </button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-[var(--text-secondary)]">
          A busca chegou a{" "}
          <strong className="text-[var(--text-primary)]">
            {candidates.length === 1 ? "um modelo" : `${candidates.length} modelos`}
          </strong>{" "}
          para este {vehicle.brand} {vehicle.model} {vehicle.version ?? ""}. O nome é da própria
          tabela, e é ele que separa uma versão da outra.
        </p>

        {/* O caso do Gol: "1.6 MSI" acerta duas linhas da tabela, e as duas só existem de 2019
            em diante. Dizer isso vale mais do que mostrar um ano que a pessoa vai estranhar. */}
        {semOAno && (
          <div className="rounded-md border border-[color-mix(in_srgb,var(--warning)_35%,transparent)] bg-[color-mix(in_srgb,var(--warning)_8%,transparent)] px-3 py-2.5 text-xs text-[var(--text-secondary)]">
            <p>
              A tabela lista este modelo <strong>sem o ano deste carro ({vehicle.modelYear})</strong>.
              Ela costuma trocar o nome entre gerações — o mesmo motor aparece com outro nome nos
              anos anteriores.
            </p>
            <button
              type="button"
              onClick={onBrowse}
              className="mt-2 font-semibold text-[var(--primary)] underline underline-offset-2"
            >
              Ver a lista completa da marca
            </button>
          </div>
        )}

        <ul className="space-y-2">
          {candidates.map((candidate) => {
            const chosen = picked?.modelCode === candidate.modelCode;

            return (
              <li key={candidate.modelCode}>
                <button
                  type="button"
                  onClick={() => pick(candidate)}
                  className={[
                    "grid w-full grid-cols-[minmax(0,1fr)_auto] items-center gap-4",
                    "rounded-md border px-3 py-2.5 text-left text-sm transition",
                    chosen
                      ? "border-[var(--primary)] bg-[color-mix(in_srgb,var(--primary)_8%,transparent)]"
                      : "border-[var(--border)] hover:border-[var(--primary)]",
                  ].join(" ")}
                >
                  <span className="min-w-0">
                    <span className="block font-medium">{candidate.name}</span>
                    <span className="num mt-0.5 block text-xs text-[var(--text-muted)]">
                      {candidate.years.length > 0
                        ? candidate.years.map((option) => anoLegivel(option)).join(" · ")
                        : `Modelo ${candidate.modelCode}`}
                      {candidate.fipeCode && ` · ${candidate.fipeCode}`}
                    </span>
                  </span>

                  {/* O preço fica na coluna da direita, alinhado à esquerda dela e centrado na
                      vertical: os valores empilham na mesma margem, e a coluna vira uma lista de
                      preços que se compara de cima a baixo sem procurar onde cada um começa. */}
                  {candidate.value !== null && (
                    <span className="num self-center text-left font-semibold tabular-nums">
                      {formatMoney(candidate.value)}
                    </span>
                  )}
                </button>
              </li>
            );
          })}
        </ul>

        {picked && (
          <Select
            label="Ano e combustível"
            required
            value={year}
            onChange={setYear}
            placeholder={loading ? "Carregando..." : "Escolha o ano"}
            options={years.map((option) => ({ value: option.code, label: anoLegivel(option) }))}
            hint={`Este veículo é ${vehicle.modelYear}.`}
          />
        )}
      </div>
    </Modal>
  );
}

/**
 * O ano como a pessoa lê.
 *
 * A tabela escreve o zero quilômetro como o ano **32000**, e a fonte devolve isso cru:
 * "32000 Flex". É convenção da tabela, e vira nome de opção numa lista que alguém precisa
 * entender.
 */
function anoLegivel(option: FipeOption): string {
  return option.code.startsWith("32000-") ? option.name.replace("32000", "Zero km") : option.name;
}

/**
 * Marca, modelo e ano — as três escolhas que dão um código FIPE ao carro.
 *
 * Existe porque ninguém decora código de tabela. Depois destas três escolhas o veículo guarda
 * o código e o ano-combustível, e da segunda vez em diante a consulta é direta.
 *
 * As listas vêm em cascata: escolher a marca carrega os modelos, escolher o modelo carrega os
 * anos. Cada passo é uma ida à fonte, então o passo seguinte só existe depois do anterior.
 */
function FipeChooser({
  vehicle,
  onClose,
  onChosen,
}: {
  vehicle: Vehicle;
  onClose: () => void;
  onChosen: (reference: FipeReference) => Promise<void>;
}) {
  const [brands, setBrands] = useState<FipeOption[]>([]);
  const [models, setModels] = useState<FipeOption[]>([]);
  const [years, setYears] = useState<FipeOption[]>([]);

  const [brand, setBrand] = useState("");
  const [model, setModel] = useState("");
  const [year, setYear] = useState("");

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    setLoading(true);

    apiGet<FipeOption[]>("fipe/brands", "Falha ao carregar as marcas.").then((result) => {
      setLoading(false);
      if (result.ok) setBrands(result.data);
      else setError(result.error);
    });
  }, []);

  async function pickBrand(value: string) {
    setBrand(value);
    setModel("");
    setYear("");
    setModels([]);
    setYears([]);
    setError("");

    if (!value) return;

    setLoading(true);

    const result = await apiGet<FipeOption[]>(
      `fipe/brands/${encodeURIComponent(value)}/models`,
      "Falha ao carregar os modelos.",
    );

    setLoading(false);

    if (result.ok) setModels(result.data);
    else setError(result.error);
  }

  async function pickModel(value: string) {
    setModel(value);
    setYear("");
    setYears([]);
    setError("");

    if (!value) return;

    setLoading(true);

    const result = await apiGet<FipeOption[]>(
      `fipe/brands/${encodeURIComponent(brand)}/models/${encodeURIComponent(value)}/years`,
      "Falha ao carregar os anos.",
    );

    setLoading(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    setYears(result.data);

    // O ano do veículo já está cadastrado, então deixar a escolha pronta poupa um clique —
    // e, quando o mesmo ano existe como flex e como gasolina, a pessoa vê as duas e decide.
    const doAno = result.data.filter((option) => option.code.startsWith(`${vehicle.modelYear}-`));

    if (doAno.length === 1) setYear(doAno[0].code);
  }

  async function use() {
    setSaving(true);
    setError("");

    const result = await apiSend<FipeReference>(
      "POST",
      `vehicles/${vehicle.code}/fipe/model`,
      "Falha ao definir o modelo da tabela.",
      { brandCode: brand, modelCode: model, yearFuel: year },
    );

    setSaving(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    await onChosen(result.data);
  }

  return (
    <Modal
      title="Achar o modelo na tabela FIPE"
      onClose={onClose}
      error={error}
      width="max-w-xl"
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
            onClick={use}
            disabled={saving || !year}
            className="rounded-md bg-[var(--primary)] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)] disabled:opacity-50"
          >
            {saving ? "Consultando..." : "Usar este modelo"}
          </button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-[var(--text-secondary)]">
          Três escolhas, e este {vehicle.brand} {vehicle.model} passa a consultar a tabela
          sozinho daqui em diante.
        </p>

        <Select
          label="Marca"
          required
          value={brand}
          onChange={pickBrand}
          placeholder="Escolha a marca"
          options={brands.map((option) => ({ value: option.code, label: option.name }))}
        />

        {brand && (
          <Select
            label="Modelo"
            required
            value={model}
            onChange={pickModel}
            placeholder={loading && models.length === 0 ? "Carregando..." : "Escolha o modelo"}
            options={models.map((option) => ({ value: option.code, label: option.name }))}
          />
        )}

        {model && (
          <Select
            label="Ano e combustível"
            required
            value={year}
            onChange={setYear}
            placeholder={loading && years.length === 0 ? "Carregando..." : "Escolha o ano"}
            options={years.map((option) => ({ value: option.code, label: anoLegivel(option) }))}
            hint={`Este veículo é ${vehicle.modelYear}.`}
          />
        )}
      </div>
    </Modal>
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
