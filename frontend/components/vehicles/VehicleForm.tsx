"use client";

import { useState, type ReactNode } from "react";
import { Field } from "@/components/common/Field";
import { Modal } from "@/components/common/Modal";
import { Select, optionsOf } from "@/components/common/Select";
import { TextArea } from "@/components/common/TextArea";
import { apiSend } from "@/lib/api";
import { formatMoney, maskChassis, maskMileage, maskMoney, maskPlate, maskYear, moneyValue } from "@/lib/masks";
import {
  FUEL_TYPE_LABEL,
  PAYMENT_METHOD_LABEL,
  TRANSMISSION_LABEL,
  VEHICLE_ORIGIN_LABEL,
  type Vehicle,
  type Yard,
} from "@/lib/types";

/**
 * The draft is all text, numbers included.
 *
 * The field holds what the person typed, mask and all; converting to a number is the last
 * step, on the way out. Holding a number in state forces a decision about the half typed
 * field — "1.2" on its way to "1.200" — and every attempt to solve that erases what the
 * person is writing under their own finger.
 */
export type Draft = {
  code: string | null;
  plate: string;
  chassis: string;
  brand: string;
  model: string;
  version: string;
  modelYear: string;
  manufactureYear: string;
  color: string;
  mileage: string;
  mileageCorrection: boolean;
  fuelType: string;
  transmission: string;
  renavam: string;
  origin: string;
  hasDamage: boolean;
  damageDescription: string;
  purchasePrice: string;
  purchaseDate: string;
  supplierName: string;
  purchasePaymentMethod: string;
  budgetCeiling: string;
  fipeValue: string;
  fipeReferenceDate: string;
  fipeCode: string;
  desiredNetPrice: string;
  minimumNetPrice: string;
  advertisedPrice: string;
  marketNotes: string;
  notes: string;
  yardCode: string;
};

type Errors = Partial<Record<keyof Draft, string>>;

const today = () => new Date().toISOString().slice(0, 10);

/** Draft of a new car, already carrying what the operation does most of the time. */
export function emptyDraft(): Draft {
  return {
    code: null,
    plate: "",
    chassis: "",
    brand: "",
    model: "",
    version: "",
    modelYear: "",
    manufactureYear: "",
    color: "",
    mileage: "",
    mileageCorrection: false,
    fuelType: "1",
    transmission: "1",
    renavam: "",

    // Auction is the dominant origin in this operation, so it comes chosen.
    origin: "1",
    hasDamage: false,
    damageDescription: "",
    purchasePrice: "",
    purchaseDate: today(),
    supplierName: "",
    purchasePaymentMethod: "",
    budgetCeiling: "",
    fipeValue: "",
    fipeReferenceDate: "",
    fipeCode: "",
    desiredNetPrice: "",
    minimumNetPrice: "",
    advertisedPrice: "",
    marketNotes: "",
    notes: "",
    yardCode: "",
  };
}

/** Draft built from a vehicle that already exists. */
export function draftOf(vehicle: Vehicle): Draft {
  const money = (value: number | null) =>
    value === null ? "" : maskMoney(String(Math.round(value * 100)));

  return {
    code: vehicle.code,
    plate: vehicle.plate,
    chassis: vehicle.chassis,
    brand: vehicle.brand,
    model: vehicle.model,
    version: vehicle.version ?? "",
    modelYear: String(vehicle.modelYear),
    manufactureYear: String(vehicle.manufactureYear),
    color: vehicle.color ?? "",
    mileage: maskMileage(String(vehicle.mileage)),
    mileageCorrection: false,
    fuelType: String(vehicle.fuelType),
    transmission: String(vehicle.transmission),
    renavam: vehicle.renavam ?? "",
    origin: String(vehicle.origin),
    hasDamage: vehicle.hasDamage,
    damageDescription: vehicle.damageDescription ?? "",
    purchasePrice: money(vehicle.purchasePrice),
    purchaseDate: vehicle.purchaseDate?.slice(0, 10) ?? "",
    supplierName: vehicle.supplierName ?? "",
    purchasePaymentMethod: vehicle.purchasePaymentMethod
      ? String(vehicle.purchasePaymentMethod)
      : "",
    budgetCeiling: money(vehicle.budgetCeiling),
    fipeValue: money(vehicle.fipeValue),
    fipeReferenceDate: vehicle.fipeReferenceDate?.slice(0, 7) ?? "",
    fipeCode: vehicle.fipeCode ?? "",
    desiredNetPrice: money(vehicle.desiredNetPrice),
    minimumNetPrice: money(vehicle.minimumNetPrice),
    advertisedPrice: money(vehicle.advertisedPrice),
    marketNotes: vehicle.marketNotes ?? "",
    notes: vehicle.notes ?? "",
    yardCode: vehicle.yard?.code ?? "",
  };
}

/**
 * Registering and editing a vehicle.
 *
 * One form, and never a multi step wizard: a car arrives from the auction with half the data,
 * and a mandatory sequence would jam the registration at the first empty field. What is truly
 * required is marked; the rest goes in when it is known.
 */
export function VehicleForm({
  draft: initial,
  yards = [],
  onClose,
  onSaved,
}: {
  draft: Draft;
  /**
   * Os pátios cadastrados. Vazio para quem lê a tela de veículos sem ter a tela de pátios: a
   * escolha some, e o carro fica onde já estava — a permissão decide o que aparece, e a API
   * confere de novo.
   */
  yards?: Yard[];
  onClose: () => void;
  onSaved: (vehicle: Vehicle) => void;
}) {
  const [draft, setDraft] = useState(initial);
  const [errors, setErrors] = useState<Errors>({});
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  const isNew = draft.code === null;

  /** Touching a field clears its error: the message goes away when the problem does. */
  function update(change: Partial<Draft>) {
    setDraft((current) => ({ ...current, ...change }));

    setErrors((current) => {
      const next = { ...current };

      for (const field of Object.keys(change)) {
        delete next[field as keyof Draft];
      }

      return next;
    });
  }

  function validate(): Errors {
    const found: Errors = {};

    if (!/^[A-Z]{3}[0-9][0-9A-Z][0-9]{2}$/.test(draft.plate)) {
      found.plate = "Placa inválida. Use ABC1234 ou ABC1D23.";
    }

    if (draft.chassis.length !== 17) {
      found.chassis = "O chassi tem 17 caracteres, sem as letras I, O e Q.";
    }

    if (!draft.brand.trim()) found.brand = "Informe a marca.";
    if (!draft.model.trim()) found.model = "Informe o modelo.";

    const modelYear = Number(draft.modelYear);
    const manufactureYear = Number(draft.manufactureYear);

    if (modelYear < 1900 || modelYear > 2100) {
      found.modelYear = "Informe o ano do modelo.";
    }

    if (manufactureYear < 1900 || manufactureYear > 2100) {
      found.manufactureYear = "Informe o ano de fabricação.";
    } else if (modelYear && modelYear < manufactureYear) {
      found.modelYear = "O ano do modelo é igual ou posterior ao de fabricação.";
    }

    if (draft.hasDamage && !draft.damageDescription.trim()) {
      found.damageDescription = "Descreva o sinistro.";
    }

    if (draft.fipeValue && !draft.fipeReferenceDate) {
      found.fipeReferenceDate = "Informe o mês de referência da FIPE.";
    }

    const desired = moneyValue(draft.desiredNetPrice);
    const minimum = moneyValue(draft.minimumNetPrice);

    if (draft.minimumNetPrice && draft.desiredNetPrice && minimum > desired) {
      found.minimumNetPrice = "O mínimo aceito é igual ou menor que o desejado.";
    }

    return found;
  }

  async function save() {
    const found = validate();

    if (Object.keys(found).length > 0) {
      setErrors(found);
      setError("Revise os campos destacados.");
      return;
    }

    setSaving(true);
    setError("");

    const money = (value: string) => (value ? moneyValue(value) : null);

    const body = {
      plate: draft.plate,
      chassis: draft.chassis,
      brand: draft.brand.trim(),
      model: draft.model.trim(),
      version: draft.version.trim() || null,
      modelYear: Number(draft.modelYear),
      manufactureYear: Number(draft.manufactureYear),
      color: draft.color.trim() || null,
      mileage: Number(draft.mileage.replace(/\D/g, "")) || 0,
      mileageCorrection: draft.mileageCorrection,
      fuelType: Number(draft.fuelType),
      transmission: Number(draft.transmission),
      renavam: draft.renavam.trim() || null,
      origin: Number(draft.origin),
      hasDamage: draft.hasDamage,
      damageDescription: draft.hasDamage ? draft.damageDescription.trim() : null,
      purchasePrice: money(draft.purchasePrice) ?? 0,
      purchaseDate: draft.purchaseDate || null,
      supplierName: draft.supplierName.trim() || null,
      purchasePaymentMethod: draft.purchasePaymentMethod
        ? Number(draft.purchasePaymentMethod)
        : null,
      budgetCeiling: money(draft.budgetCeiling),
      fipeValue: money(draft.fipeValue),

      // The input answers "2026-09"; the API stores a date, and the day is always the first.
      fipeReferenceDate: draft.fipeReferenceDate ? `${draft.fipeReferenceDate}-01` : null,
      fipeCode: draft.fipeCode.trim() || null,
      desiredNetPrice: money(draft.desiredNetPrice),
      minimumNetPrice: money(draft.minimumNetPrice),
      advertisedPrice: money(draft.advertisedPrice),
      marketNotes: draft.marketNotes.trim() || null,
      notes: draft.notes.trim() || null,
      yardCode: draft.yardCode || null,
    };

    const result = await apiSend<Vehicle>(
      isNew ? "POST" : "PUT",
      isNew ? "vehicles" : `vehicles/${draft.code}`,
      "Falha ao salvar o veículo.",
      body,
    );

    setSaving(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    onSaved(result.data);
  }

  /**
   * FIPE as a price base, which is how the business thinks: "é 66 de FIPE, quero 58". The
   * button fills the desired price with 88% of the table, and he adjusts from there.
   */
  function suggestFromFipe() {
    const fipe = moneyValue(draft.fipeValue);

    if (fipe > 0) {
      update({ desiredNetPrice: maskMoney(String(Math.round(fipe * 88))) });
    }
  }

  return (
    <Modal
      title={isNew ? "Novo veículo" : `Editar ${draft.plate}`}
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
            className="rounded-md bg-[var(--primary)] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)] disabled:opacity-50"
          >
            {saving ? "Salvando..." : isNew ? "Cadastrar veículo" : "Salvar"}
          </button>
        </>
      }
    >
      <div className="space-y-7">
        <Section title="Identificação">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field
              label="Placa"
              required
              value={draft.plate}
              mask={maskPlate}
              onChange={(v) => update({ plate: v })}
              placeholder="ABC1D23"
              error={errors.plate}
            />
            <Field
              label="Chassi"
              required
              value={draft.chassis}
              mask={maskChassis}
              onChange={(v) => update({ chassis: v })}
              placeholder="9BWZZZ377VT004251"
              hint="17 caracteres, sem I, O e Q"
              error={errors.chassis}
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <Field
              label="Marca"
              required
              value={draft.brand}
              onChange={(v) => update({ brand: v })}
              placeholder="Chevrolet"
              error={errors.brand}
            />
            <Field
              label="Modelo"
              required
              value={draft.model}
              onChange={(v) => update({ model: v })}
              placeholder="Cruze"
              error={errors.model}
            />
            <Field
              label="Versão"
              value={draft.version}
              onChange={(v) => update({ version: v })}
              placeholder="LT 1.8 Hatch"
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-4">
            <Field
              label="Ano modelo"
              required
              inputMode="numeric"
              mask={maskYear}
              value={draft.modelYear}
              onChange={(v) => update({ modelYear: v })}
              placeholder="2014"
              error={errors.modelYear}
            />
            <Field
              label="Ano fabricação"
              required
              inputMode="numeric"
              mask={maskYear}
              value={draft.manufactureYear}
              onChange={(v) => update({ manufactureYear: v })}
              placeholder="2013"
              error={errors.manufactureYear}
            />
            <Field
              label="Cor"
              value={draft.color}
              onChange={(v) => update({ color: v })}
              placeholder="Branco"
            />
            <Field
              label="Quilometragem"
              inputMode="numeric"
              mask={maskMileage}
              value={draft.mileage}
              onChange={(v) => update({ mileage: v })}
              placeholder="118.000"
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <Select
              label="Combustível"
              value={draft.fuelType}
              onChange={(v) => update({ fuelType: v })}
              options={optionsOf(FUEL_TYPE_LABEL)}
            />
            <Select
              label="Câmbio"
              value={draft.transmission}
              onChange={(v) => update({ transmission: v })}
              options={optionsOf(TRANSMISSION_LABEL)}
            />
            <Field
              label="Renavam"
              inputMode="numeric"
              value={draft.renavam}
              onChange={(v) => update({ renavam: v.replace(/\D/g, "").slice(0, 11) })}
            />
          </div>

          {!isNew && (
            <label className="flex items-start gap-2.5">
              <input
                type="checkbox"
                className="mt-0.5"
                checked={draft.mileageCorrection}
                onChange={(e) => update({ mileageCorrection: e.target.checked })}
              />
              <span className="text-sm">
                Corrigir a quilometragem para baixo
                <span className="mt-0.5 block text-xs text-[var(--text-secondary)]">
                  A quilometragem só sobe. Marque isto quando o número anterior estava errado, e
                  registre o motivo nas observações.
                </span>
              </span>
            </label>
          )}
        </Section>

        <Section title="Origem">
          <div className="grid gap-4 sm:grid-cols-2">
            <Select
              label="Origem"
              value={draft.origin}
              onChange={(v) => update({ origin: v })}
              options={optionsOf(VEHICLE_ORIGIN_LABEL)}
            />

            <label className="flex items-center gap-2.5 sm:mt-6">
              <input
                type="checkbox"
                checked={draft.hasDamage}
                onChange={(e) => update({ hasDamage: e.target.checked })}
              />
              <span className="text-sm">Veículo com sinistro</span>
            </label>
          </div>

          {draft.hasDamage && (
            <TextArea
              label="O que o carro tem"
              required
              rows={2}
              value={draft.damageDescription}
              onChange={(v) => update({ damageDescription: v })}
              placeholder="Batida leve na dianteira, já reparada."
              hint="É esta descrição que acompanha as fotos enviadas ao comprador."
              error={errors.damageDescription}
            />
          )}
        </Section>

        {yards.length > 0 && (
          <Section title="Onde o carro fica">
            <Select
              label="Pátio"
              value={draft.yardCode}
              onChange={(v) => update({ yardCode: v })}
              options={yards.map((yard) => ({ value: yard.code, label: yard.name }))}
              placeholder="Sem pátio definido"
              hint="Mudar o pátio aqui fica registrado na linha do tempo do carro."
            />
          </Section>
        )}

        <Section title="Compra">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field
              label="Valor da compra"
              required
              inputMode="decimal"
              mask={maskMoney}
              value={draft.purchasePrice}
              onChange={(v) => update({ purchasePrice: v })}
              placeholder="29.450,00"
              aside={<span className="text-xs text-[var(--text-muted)]">R$</span>}
            />
            <Field
              label="Data da compra"
              type="date"
              value={draft.purchaseDate}
              onChange={(v) => update({ purchaseDate: v })}
              hint="Conta os dias em estoque."
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <Field
              label="De quem comprou"
              value={draft.supplierName}
              onChange={(v) => update({ supplierName: v })}
              placeholder="Leilão Copart"
            />
            <Select
              label="Forma de pagamento"
              value={draft.purchasePaymentMethod}
              onChange={(v) => update({ purchasePaymentMethod: v })}
              options={optionsOf(PAYMENT_METHOD_LABEL)}
              placeholder="A informar"
            />
          </div>

          <Field
            label="Teto de orçamento"
            inputMode="decimal"
            mask={maskMoney}
            value={draft.budgetCeiling}
            onChange={(v) => update({ budgetCeiling: v })}
            placeholder="40.000,00"
            hint="Quanto este carro pode custar no total, compra mais gastos. A ficha avisa quando o previsto passa daqui."
            aside={<span className="text-xs text-[var(--text-muted)]">R$</span>}
          />
        </Section>

        <Section title="Tabela FIPE">
          <div className="grid gap-4 sm:grid-cols-3">
            <Field
              label="Valor na FIPE"
              inputMode="decimal"
              mask={maskMoney}
              value={draft.fipeValue}
              onChange={(v) => update({ fipeValue: v })}
              placeholder="66.000,00"
              aside={<span className="text-xs text-[var(--text-muted)]">R$</span>}
            />
            <Field
              label="Mês de referência"
              type="month"
              value={draft.fipeReferenceDate}
              onChange={(v) => update({ fipeReferenceDate: v })}
              error={errors.fipeReferenceDate}
            />
            <Field
              label="Código FIPE"
              value={draft.fipeCode}
              onChange={(v) => update({ fipeCode: v })}
              placeholder="004445-2"
              hint="O botão Achar o modelo, na aba Ficha, preenche isto sozinho."
            />
          </div>
        </Section>

        <Section title="Preço">
          <div className="grid gap-4 sm:grid-cols-3">
            <Field
              label="Quero receber"
              inputMode="decimal"
              mask={maskMoney}
              value={draft.desiredNetPrice}
              onChange={(v) => update({ desiredNetPrice: v })}
              placeholder="58.000,00"
              hint="Limpo, para a revenda."
              aside={
                moneyValue(draft.fipeValue) > 0 ? (
                  <button
                    type="button"
                    onClick={suggestFromFipe}
                    className="text-[11px] font-semibold text-[var(--primary)] hover:underline"
                  >
                    88% da FIPE
                  </button>
                ) : (
                  <span className="text-xs text-[var(--text-muted)]">R$</span>
                )
              }
            />
            <Field
              label="Mínimo aceito"
              inputMode="decimal"
              mask={maskMoney}
              value={draft.minimumNetPrice}
              onChange={(v) => update({ minimumNetPrice: v })}
              placeholder="55.000,00"
              error={errors.minimumNetPrice}
              aside={<span className="text-xs text-[var(--text-muted)]">R$</span>}
            />
            <Field
              label="Anunciado"
              inputMode="decimal"
              mask={maskMoney}
              value={draft.advertisedPrice}
              onChange={(v) => update({ advertisedPrice: v })}
              hint="Quando sai por terceiro, o repasse entra por cima."
              aside={<span className="text-xs text-[var(--text-muted)]">R$</span>}
            />
          </div>

          {moneyValue(draft.desiredNetPrice) > 0 && moneyValue(draft.purchasePrice) > 0 && (
            <p className="text-xs text-[var(--text-secondary)]">
              Só com a compra, sobrariam{" "}
              <span className="num font-semibold text-[var(--text-primary)]">
                {formatMoney(
                  moneyValue(draft.desiredNetPrice) - moneyValue(draft.purchasePrice),
                )}
              </span>
              . Os gastos entram na ficha do veículo e derrubam esse número.
            </p>
          )}

          <TextArea
            label="O que o mercado está pedindo"
            rows={2}
            value={draft.marketNotes}
            onChange={(v) => update({ marketNotes: v })}
            placeholder="Cinco anúncios em Joinville entre 57 e 63 mil."
          />
        </Section>

        <Section title="Observações">
          <TextArea
            label="Anotações"
            rows={3}
            value={draft.notes}
            onChange={(v) => update({ notes: v })}
          />
        </Section>
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
