"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { Camera, Car, Clock, Plus, Search, Wallet } from "lucide-react";
import { Select, optionsOf } from "@/components/common/Select";
import { VehicleForm, emptyDraft } from "@/components/vehicles/VehicleForm";
import { BudgetBar, Empty, PageError, Stat, StatusPill } from "@/components/vehicles/VehicleUi";
import { apiGet } from "@/lib/api";
import { formatMileage, formatMoney } from "@/lib/masks";
import {
  VEHICLE_ORIGIN_LABEL,
  VEHICLE_STATUS_LABEL,
  VehicleStatus,
  type Vehicle,
} from "@/lib/types";

export function VehiclesView({ initialVehicles }: { initialVehicles: Vehicle[] }) {
  const router = useRouter();

  const [vehicles, setVehicles] = useState(initialVehicles);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [origin, setOrigin] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [creating, setCreating] = useState(false);

  /**
   * The search and the filters go to the API, and are never applied here.
   *
   * Filtering in the browser only works while the whole yard fits in the screen's memory. The
   * query already knows how to look through plate, brand, model, version and chassis, and it
   * keeps knowing when there are five hundred cars.
   */
  const reload = useCallback(async () => {
    setLoading(true);

    const query = new URLSearchParams();

    if (search.trim()) query.set("search", search.trim());
    if (status) query.set("status", status);
    if (origin) query.set("origin", origin);

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
  }, [search, status, origin]);

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
          value={oldest > 0 ? `${oldest} dias` : "—"}
          hint="Do carro que está há mais tempo no pátio"
          icon={<Clock size={17} className="text-[var(--signal)]" />}
        />
      </div>

      <PageError message={error} />

      <div className="mb-4 grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto_auto]">
        <label className="block">
          <span className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
            Buscar
          </span>
          <span className="relative block">
            <Search
              size={16}
              className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[var(--text-muted)]"
            />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Placa, marca, modelo ou chassi"
              className="w-full rounded-md border border-[var(--border)] bg-[var(--canvas)] py-2 pl-9 pr-3 text-sm"
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
      </div>

      {loading && (
        <p className="mb-3 text-xs text-[var(--text-muted)]">Carregando…</p>
      )}

      {vehicles.length === 0 ? (
        <Empty
          title={
            search || status || origin
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
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {vehicles.map((vehicle) => (
            <VehicleCard key={vehicle.code} vehicle={vehicle} />
          ))}
        </div>
      )}

      {creating && (
        <VehicleForm
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

        <p className="mt-auto pt-1 text-[11px] text-[var(--text-muted)]">
          {vehicle.daysInStock === null
            ? "Sem data de compra"
            : vehicle.status === VehicleStatus.Sold
              ? `Ficou ${vehicle.daysInStock} dias no pátio`
              : `${vehicle.daysInStock} dias parado`}
        </p>
      </div>
    </Link>
  );
}
