"use client";

import { AlertTriangle } from "lucide-react";
import { formatDays, formatMeses, formatMonth, formatMoney, formatPercent } from "@/lib/masks";
import type { Vehicle } from "@/lib/types";
import { BudgetBar } from "./VehicleUi";

/**
 * What the car cost, which is the reason this system exists.
 *
 * None of these numbers is stored: every one is summed on each read. The real `GASTOS.docx`
 * shows R$ 350 too little precisely because the total was typed once and three expenses
 * arrived afterwards.
 */
export function CostPanel({ vehicle }: { vehicle: Vehicle }) {
  const { cost } = vehicle;

  return (
    <section className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]">
      <div className="mb-4 flex items-baseline justify-between gap-3">
        <p className="font-display text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
          Custo real
        </p>
        {vehicle.daysInStock !== null && (
          <p className="num text-xs text-[var(--text-muted)]">
            {formatDays(vehicle.daysInStock)} em estoque
          </p>
        )}
      </div>

      <p className="num text-3xl font-bold">{formatMoney(cost.total)}</p>

      <dl className="mt-4 space-y-2 text-sm">
        <Line label="Compra" value={formatMoney(cost.purchase)} />
        <Line label="Gastos pagos" value={formatMoney(cost.paidExpenses)} />

        {cost.plannedExpenses > 0 && (
          <Line
            label="Previsto ainda por pagar"
            value={formatMoney(cost.plannedExpenses)}
            muted
          />
        )}

        {cost.plannedExpenses > 0 && (
          <Line label="Custo se tudo for pago" value={formatMoney(cost.projected)} muted />
        )}
      </dl>

      {vehicle.budgetCeiling !== null && (
        <div className="mt-5 border-t border-[var(--border)] pt-4">
          <BudgetBar cost={cost} ceiling={vehicle.budgetCeiling} />
        </div>
      )}

      {cost.willExceedBudget && !cost.isOverBudget && (
        <p className="mt-4 flex items-start gap-2 rounded-md border border-[color-mix(in_srgb,var(--flare)_45%,transparent)] bg-[color-mix(in_srgb,var(--flare)_10%,transparent)] px-3 py-2.5 text-xs text-[var(--warning)]">
          <AlertTriangle size={15} className="mt-px shrink-0" />
          <span>
            O gasto de hoje cabe no teto, e o que está previsto passa dele. Dá tempo de rever a
            última peça.
          </span>
        </p>
      )}

      {cost.isOverBudget && (
        <p className="mt-4 flex items-start gap-2 rounded-md border border-[color-mix(in_srgb,var(--critical)_40%,transparent)] bg-[color-mix(in_srgb,var(--critical)_8%,transparent)] px-3 py-2.5 text-xs text-[var(--critical)]">
          <AlertTriangle size={15} className="mt-px shrink-0" />
          <span>
            Este carro já custou{" "}
            <span className="num font-semibold">
              {formatMoney(Math.abs(cost.budgetRemaining ?? 0))}
            </span>{" "}
            além do teto.
          </span>
        </p>
      )}

      {(vehicle.desiredNetPrice !== null
        || vehicle.fipeValue !== null
        || cost.percentOfFipe !== null) && (
        <div className="mt-5 space-y-2 border-t border-[var(--border)] pt-4 text-sm">
          {vehicle.desiredNetPrice !== null && (
            <>
              <Line label="Quero receber" value={formatMoney(vehicle.desiredNetPrice)} />
              <Line
                label="Sobra"
                value={`${formatMoney(cost.profitAtDesired)}${
                  cost.marginAtDesired !== null
                    ? `  ·  ${formatPercent(cost.marginAtDesired)}`
                    : ""
                }`}
                tone={
                  (cost.profitAtDesired ?? 0) >= 0 ? "var(--success)" : "var(--critical)"
                }
              />
            </>
          )}

          {vehicle.minimumNetPrice !== null && (
            <Line label="Mínimo aceito" value={formatMoney(vehicle.minimumNetPrice)} muted />
          )}

          {/* O valor da tabela vem antes do percentual porque é dele que o percentual sai.
              Ficava só na aba Ficha, atrás de mais quatro abas, e quem decide preço decidia
              sem o número na frente — a conta "é 66 de FIPE, quero 58" acontece aqui. */}
          {vehicle.fipeValue !== null && (
            <Line
              label="Tabela FIPE"
              value={formatMoney(vehicle.fipeValue)}
              hint={formatMonth(vehicle.fipeReferenceDate)}
            />
          )}

          {cost.percentOfFipe !== null && (
            <Line
              label="Custo final vs FIPE"
              value={formatPercent(cost.percentOfFipe)}
              tone={fipeTone(cost.percentOfFipe)}
            />
          )}

          {/* Carro parado perde valor de tabela todo mês. Um número velho aqui, ao lado do
              preço, é justamente o que faz alguém decidir por um mercado que já mudou. */}
          {(vehicle.fipeMonthsBehind ?? 0) > 0 && (
            <p className="pt-1 text-xs text-[var(--warning)]">
              Esta referência é de {formatMeses(vehicle.fipeMonthsBehind!)} atrás. A aba
              <strong className="font-semibold"> Ficha</strong> traz a tabela de agora.
            </p>
          )}
        </div>
      )}
    </section>
  );
}

/**
 * A cor do custo contra a tabela.
 *
 * Verde é a notícia boa da operação, e a razão de comprar em leilão: o carro custou menos do
 * que a tabela diz que ele vale, e sobra espaço entre o custo e o mercado.
 *
 * Os 90% são o ponto em que esse espaço deixa de dar conta de um negócio: vendendo pela
 * tabela cheia sobrariam 10% brutos, e o repasse da loja parceira e a comissão saem daí. A
 * partir de 100% o carro custou mais do que a tabela — dá para vender ainda, e o lucro passa
 * a ter de vir de fora dela.
 *
 * O corte de 90 é julgamento, e não conta: mudar de ideia sobre ele é mudar este número.
 */
function fipeTone(percent: number): string {
  if (percent >= 100) return "var(--critical)";
  if (percent >= 90) return "var(--warning)";

  return "var(--success)";
}

function Line({
  label,
  value,
  hint,
  muted = false,
  tone,
}: {
  label: string;
  value: string;
  /** Segunda linha sob o rótulo: o mês da referência, quando o número tem um mês. */
  hint?: string;
  muted?: boolean;
  tone?: string;
}) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <dt className={muted ? "text-[var(--text-muted)]" : "text-[var(--text-secondary)]"}>
        {label}
        {hint && (
          <span className="ml-1.5 text-xs text-[var(--text-muted)]">{hint}</span>
        )}
      </dt>
      <dd
        className={["num font-semibold", muted ? "text-[var(--text-muted)]" : ""].join(" ")}
        style={tone ? { color: tone } : undefined}
      >
        {value}
      </dd>
    </div>
  );
}
