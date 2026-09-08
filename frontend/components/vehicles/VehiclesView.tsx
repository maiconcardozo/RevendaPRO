"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { Camera, Car, Clock, Plus, Search, Wallet } from "lucide-react";
import { Field } from "@/components/common/Field";
import { ExportButtons } from "@/components/common/ExportButtons";
import { ListBar, useViewMode } from "@/components/common/ViewSwitch";
import { Select, optionsOf } from "@/components/common/Select";
import { VehicleForm, emptyDraft } from "@/components/vehicles/VehicleForm";
import { BudgetBar, Empty, PageError, Stat, StatusPill } from "@/components/vehicles/VehicleUi";
import { apiGet } from "@/lib/api";
import { formatDays, formatMeses, formatMileage, formatMoney } from "@/lib/masks";
import {
  VEHICLE_ORIGIN_LABEL,
  VEHICLE_STATUS_LABEL,
  VehicleStatus,
  type Vehicle,
  type Yard,
} from "@/lib/types";

/** Onde a escolha de quem olha fica guardada. Preferência de leitura, e jamais dado da empresa. */
const VIEW_KEY = "revendapro.vehicles.view";

export function VehiclesView({
  initialVehicles,
  yards = [],
}: {
  initialVehicles: Vehicle[];
  /** Os pátios cadastrados: o filtro por lugar, e o cadastro dizendo onde o carro vai ficar. */
  yards?: Yard[];
}) {
  const router = useRouter();

  const [vehicles, setVehicles] = useState(initialVehicles);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [origin, setOrigin] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [yard, setYard] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [creating, setCreating] = useState(false);
  const [view, chooseView] = useViewMode(VIEW_KEY);

  /**
   * The search and the filters go to the API, and are never applied here.
   *
   * Filtering in the browser only works while the whole yard fits in the screen's memory. The
   * query already knows how to look through plate, brand, model, version and chassis, and it
   * keeps knowing when there are five hundred cars.
   */
  /** A query da tela, a mesma da listagem e da planilha (M19). */
  const filterQuery = () => {
    const query = new URLSearchParams();

    if (search.trim()) query.set("search", search.trim());
    if (status) query.set("status", status);
    if (origin) query.set("origin", origin);
    if (from) query.set("from", from);
    if (to) query.set("to", to);
    if (yard) query.set("yard", yard);

    return query;
  };

  const reload = useCallback(async () => {
    setLoading(true);

    const query = new URLSearchParams();

    if (search.trim()) query.set("search", search.trim());
    if (status) query.set("status", status);
    if (origin) query.set("origin", origin);
    if (from) query.set("from", from);
    if (to) query.set("to", to);
    if (yard) query.set("yard", yard);

    const result = await apiGet<Vehicle[]>(
      `vehicles${query.size > 0 ? `?${query}` : ""}`,
      "Falha ao carregar os veículos.",
    );

    setLoading(false);

    if (result.ok) {
      setVehicles(result.data);
      setError("");
    } else {
      setError(result.error);
    }
  }, [search, status, origin, from, to, yard]);

  // Waits for the person to stop typing before asking the server.
  useEffect(() => {
    const timer = setTimeout(reload, 300);

    return () => clearTimeout(timer);
  }, [reload]);

  // Sold leaves the parked capital out: that money came back.
  const inStock = vehicles.filter((v) => v.status !== VehicleStatus.Sold);

  const parked = inStock.reduce((total, v) => total + v.cost.total, 0);

  const oldest = inStock.reduce(
    (worst, v) => Math.max(worst, v.daysInStock ?? 0),
    0,
  );

  return (
    <div className="dash-anim">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
            Operação
          </p>
          <h1 className="hero-title text-3xl font-bold">Veículos</h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Cada carro, do leilão até a venda, com o custo real somado a cada gasto.
          </p>
        </div>

        <button
          type="button"
          onClick={() => setCreating(true)}
          className="inline-flex items-center gap-2 rounded-md bg-[var(--primary)] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]"
        >
          <Plus size={17} />
          Novo veículo
        </button>
      </div>

      <div className="mb-6 grid gap-4 sm:grid-cols-3">
        <Stat
          label="No pátio"
          value={String(inStock.length)}
          hint={`${vehicles.length} no total`}
          icon={<Car size={17} className="text-[var(--signal)]" />}
        />
        <Stat
          label="Capital parado"
          value={formatMoney(parked)}
          hint="Compra mais gastos dos carros ainda sem venda"
          icon={<Wallet size={17} className="text-[var(--signal)]" />}
        />
        <Stat
          label="Mais tempo parado"
          value={oldest > 0 ? formatDays(oldest) : "—"}
          hint="Do carro que está há mais tempo no pátio"
          icon={<Clock size={17} className="text-[var(--signal)]" />}
        />
      </div>

      <PageError message={error} />

      <div className="mb-4 grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto_auto_auto_auto_auto]">
        <label className="block">
          <span className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
            Buscar
          </span>
          <span className="relative block">
            <Search
              size={16}
              className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[var(--text-muted)]"
            />
            {/* O pl-9 abre espaço para a lupa; a altura e o resto vêm do padrão .control. */}
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Placa, marca, modelo ou chassi"
              className="control pl-9"
            />
          </span>
        </label>

        <div className="min-w-44">
          <Select
            label="Situação"
            value={status}
            onChange={setStatus}
            options={optionsOf(VEHICLE_STATUS_LABEL)}
            placeholder="Todas"
          />
        </div>

        <div className="min-w-40">
          <Select
            label="Origem"
            value={origin}
            onChange={setOrigin}
            options={optionsOf(VEHICLE_ORIGIN_LABEL)}
            placeholder="Todas"
          />
        </div>

        {/* O filtro por pátio some para quem não tem a tela de pátios: sem ela a lista de
            lugares vem vazia, e um filtro sem opção só ocuparia espaço. */}
        {yards.length > 0 && (
          <div className="min-w-44">
            <Select
              label="Pátio"
              value={yard}
              onChange={setYard}
              options={yards.map((option) => ({ value: option.code, label: option.name }))}
              placeholder="Todos"
            />
          </div>
        )}

        {/* Período pela data de compra: a pergunta desta tela é o que entrou no pátio.
            Vazio traz o pátio inteiro — quem abre Veículos quer ver os carros, e um mês
            preenchido por conta própria esconderia metade do estoque sem avisar. */}
        <div className="min-w-36">
          <Field label="Comprado de" type="date" value={from} onChange={setFrom} />
        </div>

        <div className="min-w-36">
          <Field label="Até" type="date" value={to} onChange={setTo} placeholder="Hoje" />
        </div>
      </div>

      {/* A barra que fica entre o filtro e o resultado: quantos sobraram, e de que jeito
          olhar para eles. É o lugar onde todo marketplace põe o seletor de forma, e é o
          lugar onde o olho já está quando acaba de filtrar. */}
      <ListBar
        count={vehicles.length}
        singular="veículo"
        plural="veículos"
        loading={loading}
        view={view}
        onChange={chooseView}
        label="Como mostrar os veículos"
      >
        <ExportButtons
          path={`exports/vehicles${filterQuery().size > 0 ? `?${filterQuery()}` : ""}`}
          name="Veiculos"
          onError={setError}
        />
      </ListBar>

      {vehicles.length === 0 ? (
        <Empty
          title={
            search || status || origin || from || to || yard
              ? "Nenhum veículo com esses filtros"
              : "O pátio está vazio"
          }
          action={
            <button
              type="button"
              onClick={() => setCreating(true)}
              className="inline-flex items-center gap-2 rounded-md border border-[var(--border)] px-3 py-2 text-sm font-semibold text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)]"
            >
              <Plus size={15} />
              Cadastrar o primeiro
            </button>
          }
        />
      ) : view === "grid" ? (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {vehicles.map((vehicle) => (
            <VehicleCard key={vehicle.code} vehicle={vehicle} />
          ))}
        </div>
      ) : (
        <ul className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow)]">
          {vehicles.map((vehicle) => (
            <li key={vehicle.code} className="border-b border-[var(--border)] last:border-0">
              <VehicleRow vehicle={vehicle} />
            </li>
          ))}
        </ul>
      )}

      {creating && (
        <VehicleForm
          yards={yards}
          draft={emptyDraft()}
          onClose={() => setCreating(false)}
          onSaved={(vehicle) => {
            setCreating(false);

            // Straight to the sheet: whoever just registered a car wants to enter the
            // freight and upload the photos, and not go back to the grid.
            router.push(`/vehicles/${vehicle.code}`);
          }}
        />
      )}
    </div>
  );
}

/**
 * Um carro em uma linha, para comparar muitos.
 *
 * A leitura vai da esquerda para a direita e termina no dinheiro: miniatura, placa com a
 * situação, nome com a versão, os atributos que separam um carro do outro e, encostados na
 * direita, o custo e o quero receber. **É a margem direita que faz a lista valer**: os
 * números empilham no mesmo lugar em toda linha, e comparar vinte carros vira descer o olho.
 *
 * A barra de teto do card não cabe aqui — ela tem três linhas de altura, e esta forma existe
 * para caber gente na tela. O aviso dela vira selo, e só quando há o que avisar.
 */
function VehicleRow({ vehicle }: { vehicle: Vehicle }) {
  const sold = vehicle.status === VehicleStatus.Sold;

  return (
    <Link
      href={`/vehicles/${vehicle.code}`}
      className="flex items-center gap-3 px-3 py-2.5 transition hover:bg-[var(--surface-2)] sm:gap-4 sm:px-4"
    >
      <span className="relative block h-14 w-20 shrink-0 overflow-hidden rounded-md bg-[var(--surface-2)]">
        {vehicle.coverThumbnailUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={vehicle.coverThumbnailUrl}
            alt=""
            loading="lazy"
            className="h-full w-full object-cover"
          />
        ) : (
          <span className="grid h-full w-full place-items-center text-[var(--text-muted)]">
            <Car size={18} />
          </span>
        )}
      </span>

      <span className="min-w-0 flex-1">
        <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
          <span className="num text-sm font-bold tracking-wide">{vehicle.plate}</span>
          <StatusPill status={vehicle.status} />
        </span>

        <span className="mt-0.5 block truncate font-semibold">
          {vehicle.brand} {vehicle.model}
          {vehicle.version && (
            <span className="font-normal text-[var(--text-secondary)]"> {vehicle.version}</span>
          )}
        </span>

        <span className="num mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-[11px] text-[var(--text-muted)]">
          <span>
            {vehicle.modelYear}/{vehicle.manufactureYear} · {formatMileage(vehicle.mileage)}
            {vehicle.color && <span className="font-sans"> · {vehicle.color}</span>}
          </span>

          <span className="font-sans">
            {vehicle.daysInStock === null
              ? "Sem data de compra"
              : sold
                ? `Ficou ${formatDays(vehicle.daysInStock)} no pátio`
                : `${formatDays(vehicle.daysInStock)} parado`}
          </span>

          {/* Os dois selos que o card mostra em gráfico. Aparecem só quando há o que dizer:
              um selo permanente vira parte do fundo e para de ser lido. */}
          {vehicle.cost.isOverBudget ? (
            <span className="rounded-full bg-[color-mix(in_srgb,var(--critical)_15%,transparent)] px-2 py-0.5 font-sans font-semibold text-[var(--critical)]">
              Passou do teto
            </span>
          ) : (
            vehicle.cost.willExceedBudget && (
              <span className="rounded-full bg-[color-mix(in_srgb,var(--warning)_15%,transparent)] px-2 py-0.5 font-sans font-semibold text-[var(--warning)]">
                O previsto estoura
              </span>
            )
          )}

          {!sold && (vehicle.fipeMonthsBehind ?? 0) > 0 && (
            <span className="rounded-full bg-[color-mix(in_srgb,var(--warning)_15%,transparent)] px-2 py-0.5 font-sans font-semibold text-[var(--warning)]">
              FIPE de {formatMeses(vehicle.fipeMonthsBehind!)} atrás
            </span>
          )}
        </span>
      </span>

      {/* A coluna do dinheiro: largura fixa e alinhada à direita, para os valores de vinte
          linhas caírem na mesma margem. É o que separa uma lista de uma pilha de cards. */}
      <span className="shrink-0 text-right">
        <span className="block text-[11px] uppercase tracking-wide text-[var(--text-muted)]">
          Custo
        </span>
        <span className="num block font-bold">{formatMoney(vehicle.cost.total)}</span>

        {vehicle.desiredNetPrice !== null && (
          <span className="num mt-0.5 hidden text-xs text-[var(--text-secondary)] sm:block">
            Quero {formatMoney(vehicle.desiredNetPrice)}
          </span>
        )}
      </span>
    </Link>
  );
}

function VehicleCard({ vehicle }: { vehicle: Vehicle }) {
  return (
    <Link
      href={`/vehicles/${vehicle.code}`}
      className="group flex flex-col overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow)] transition hover:border-[var(--primary)]"
    >
      <div className="relative aspect-[16/10] overflow-hidden bg-[var(--surface-2)]">
        {vehicle.coverThumbnailUrl ? (
          // <img> and not next/image on purpose: the address is signed and expires in
          // fifteen minutes, so storing an optimized copy of a URL that dies would only
          // produce a broken square later. See ADR-0004.
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={vehicle.coverThumbnailUrl}
            alt=""
            loading="lazy"
            className="h-full w-full object-cover transition duration-300 group-hover:scale-[1.03]"
          />
        ) : (
          <span className="grid h-full w-full place-items-center text-[var(--text-muted)]">
            <Car size={30} />
          </span>
        )}

        <span className="absolute left-3 top-3">
          <StatusPill status={vehicle.status} />
        </span>

        {vehicle.photoCount > 0 && (
          <span className="num absolute right-3 top-3 inline-flex items-center gap-1 rounded-full bg-[rgba(11,30,63,.62)] px-2 py-1 text-[11px] font-semibold text-white">
            <Camera size={12} />
            {vehicle.photoCount}
          </span>
        )}
      </div>

      <div className="flex flex-1 flex-col gap-3 p-4">
        <div>
          <p className="num text-sm font-bold tracking-wide">{vehicle.plate}</p>
          <p className="mt-0.5 truncate font-semibold">
            {vehicle.brand} {vehicle.model}
            {vehicle.version && (
              <span className="font-normal text-[var(--text-secondary)]"> {vehicle.version}</span>
            )}
          </p>
          <p className="num mt-0.5 text-xs text-[var(--text-secondary)]">
            {vehicle.modelYear}/{vehicle.manufactureYear} · {formatMileage(vehicle.mileage)}
            {vehicle.color && <span className="font-sans"> · {vehicle.color}</span>}
          </p>
        </div>

        <div className="flex items-end justify-between gap-3">
          <div>
            <p className="text-[11px] uppercase tracking-wide text-[var(--text-muted)]">
              Custo
            </p>
            <p className="num text-lg font-bold">{formatMoney(vehicle.cost.total)}</p>
          </div>

          {vehicle.desiredNetPrice !== null && (
            <div className="text-right">
              <p className="text-[11px] uppercase tracking-wide text-[var(--text-muted)]">
                Quero
              </p>
              <p className="num text-sm font-semibold text-[var(--text-secondary)]">
                {formatMoney(vehicle.desiredNetPrice)}
              </p>
            </div>
          )}
        </div>

        <BudgetBar cost={vehicle.cost} ceiling={vehicle.budgetCeiling} />

        <p className="mt-auto flex flex-wrap items-center gap-x-2 gap-y-1 pt-1 text-[11px] text-[var(--text-muted)]">
          <span>
            {vehicle.daysInStock === null
              ? "Sem data de compra"
              : vehicle.status === VehicleStatus.Sold
                ? `Ficou ${formatDays(vehicle.daysInStock)} no pátio`
                : `${formatDays(vehicle.daysInStock)} parado`}
          </span>

          {/* Carro parado perde valor de tabela todo mês, e um número velho na listagem é
              justamente o que faz alguém decidir por um mercado que já mudou. */}
          {vehicle.status !== VehicleStatus.Sold && (vehicle.fipeMonthsBehind ?? 0) > 0 && (
            <span className="rounded-full bg-[color-mix(in_srgb,var(--warning)_15%,transparent)] px-2 py-0.5 font-semibold text-[var(--warning)]">
              FIPE de {formatMeses(vehicle.fipeMonthsBehind!)} atrás
            </span>
          )}
        </p>
      </div>
    </Link>
  );
}
