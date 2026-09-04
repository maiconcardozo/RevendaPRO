"use client";

import { AlertTriangle } from "lucide-react";
import { formatDays, formatMeses, formatMonth, formatMoney, formatPercent } from "@/lib/masks";
import { VehicleStatus, YardKind, type Vehicle } from "@/lib/types";
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

  const partner = throughPartner(vehicle);

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

          {/* O repasse da loja fica aqui, ao lado do preço, e jamais dentro do custo real: ele
              é custo do negócio, e não do carro — some no dia em que o carro volta para o
              pátio da casa. Somá-lo ao custo faria o custo mudar por causa de onde o carro
              está parado, e o percentual contra a tabela passaria a medir duas coisas. */}
          {partner && (
            <div className="mt-3 rounded-md border border-[var(--border)] bg-[var(--surface-2)] px-3 py-2.5">
              <p className="text-xs font-semibold text-[var(--text-secondary)]">
                Pela {vehicle.yard!.name} · {partner.label}
              </p>

              <dl className="mt-2 space-y-1.5">
                <Line label="Anúncio sai por" value={formatMoney(partner.price)} />
                <Line label="A loja fica com" value={formatMoney(partner.cut)} muted />
              </dl>

              <p className="mt-2 text-xs text-[var(--text-muted)]">
                Para você receber os {formatMoney(vehicle.desiredNetPrice!)} que quer. O repasse
                entra por cima, e a sobra segue a mesma.
              </p>
            </div>
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
 * O preço de anúncio que faz a loja parceira caber por cima do que a revenda quer receber.
 *
 * É a conta que o stakeholder faz de cabeça: <i>"eu quero 58 para mim, a loja põe a dela em
 * cima"</i>. Com 8% combinados, receber 53.500 exige anunciar por 58.152,17 — e a diferença é
 * exatamente o que fica com a loja.
 *
 * A conta é a do M8, e ela não muda aqui: o repasse é uma fatia <b>do preço de venda</b>, e não
 * um acréscimo sobre o líquido. Por isso o preço sai de uma divisão, e nunca de somar 8% aos
 * 53.500 — isso deixaria a revenda R$ 342,40 abaixo do que ela pediu.
 *
 * Nada disso aparece em carro vendido: ali a faixa da venda já mostra o que aconteceu de
 * verdade, e uma projeção ao lado dela seria um segundo número disputando o mesmo lugar.
 */
function throughPartner(
  vehicle: Vehicle,
): { price: number; cut: number; label: string } | null {
  const yard = vehicle.yard;
  const desired = vehicle.desiredNetPrice;

  if (
    !yard
    || yard.kind !== YardKind.Partner
    || vehicle.status === VehicleStatus.Sold
    || desired === null
    || desired <= 0
  ) {
    return null;
  }

  // Valor fechado: a loja fica com aquilo, e o anúncio é a soma.
  if (yard.cutAmount !== null && yard.cutAmount > 0) {
    return {
      price: desired + yard.cutAmount,
      cut: yard.cutAmount,
      label: formatMoney(yard.cutAmount),
    };
  }

  // Percentual de 100 deixaria a revenda sem nada, e a divisão sem resposta.
  if (yard.cutPercent !== null && yard.cutPercent > 0 && yard.cutPercent < 100) {
    const price = desired / (1 - yard.cutPercent / 100);

    return { price, cut: price - desired, label: formatPercent(yard.cutPercent) };
  }

  return null;
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
