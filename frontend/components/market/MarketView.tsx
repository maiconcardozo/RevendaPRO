"use client";

import Link from "next/link";
import { ArrowDownRight, ArrowUpRight, HandCoins, Minus, TrendingDown } from "lucide-react";
import { Empty, StatusPill } from "@/components/vehicles/VehicleUi";
import { formatDate, formatDays, formatMoney, formatMonth, formatPercent } from "@/lib/masks";
import type { MarketAverage, MarketLine, MarketOverview } from "@/lib/types";

/**
 * A revenda contra a tabela de referência.
 *
 * Cada valor encontra a cotação **do mês dele**: a compra contra a tabela do mês em que o
 * carro entrou, a venda contra a do mês em que fechou, o pedido contra a de agora. Comparar
 * uma venda de agosto com a tabela de hoje mediria a passagem do tempo e chamaria isso de
 * resultado — e o tempo é justamente o que esta tela mede separado, na perda de referência.
 *
 * Onde falta cotação daquele mês, a tela escreve "sem comparação" em vez de inventar número.
 * O sistema só passou a guardar cotações no M11, então negócio anterior a isso fica sem.
 */
export function MarketView({ overview }: { overview: MarketOverview }) {
  const semNada = overview.yard.length === 0 && overview.sold.length === 0;

  return (
    <div className="dash-anim">
      <div className="mb-6">
        <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
          Operação
        </p>
        <h1 className="hero-title text-3xl font-bold">Mercado</h1>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          Cada negócio contra a tabela FIPE do mês em que ele aconteceu. Referência de{" "}
          <strong className="font-semibold text-[var(--text-primary)]">
            {formatMonth(overview.referenceMonth)}
          </strong>
          .
        </p>
      </div>

      {semNada ? (
        <Empty title="O pátio está vazio" />
      ) : (
        <>
          <div className="mb-6 grid gap-4 lg:grid-cols-3">
            <AverageCard
              label="Compramos"
              average={overview.purchases}
              good="below"
              hint="Preço de compra contra a tabela do mês da compra"
            />
            <AverageCard
              label="Vendemos"
              average={overview.sales}
              good="above"
              hint="Preço fechado contra a tabela do mês da venda"
            />
            <AverageCard
              label="Estamos pedindo"
              average={overview.asking}
              good="above"
              hint="Quero receber contra a tabela de agora"
            />
          </div>

          <div className="mb-6 grid gap-4 sm:grid-cols-2">
            <LossCard
              label="O pátio perdeu este mês"
              value={overview.lostThisMonth}
              hint="Queda da tabela de um mês para o outro, somada nos carros parados"
            />
            <LossCard
              label="Perdeu desde a compra"
              value={overview.lostSincePurchase}
              hint="Quanto a tabela caiu desde o dia em que cada carro entrou"
            />
          </div>

          {overview.withoutReference > 0 && (
            <p className="mb-6 rounded-md border border-[color-mix(in_srgb,var(--warning)_35%,transparent)] bg-[color-mix(in_srgb,var(--warning)_8%,transparent)] px-4 py-3 text-sm text-[var(--text-secondary)]">
              {overview.withoutReference === 1
                ? "1 carro está fora das médias acima"
                : `${overview.withoutReference} carros estão fora das médias acima`}
              , por falta de cotação deste mês. Aponte o modelo na tabela pela ficha do veículo.
            </p>
          )}

          <Section
            title="No pátio"
            subtitle="O que a revenda quer receber, contra a tabela de agora"
            lines={overview.yard}
            emptyLabel="Nenhum carro parado."
            showLoss
          />

          {overview.proposals.length > 0 && (
            <section className="mb-6 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]">
              <p className="font-display mb-1 text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
                Propostas na mesa
              </p>
              <p className="mb-4 text-xs text-[var(--text-secondary)]">
                O que ofereceram, contra a tabela de agora
              </p>

              <ul className="divide-y divide-[var(--border)]">
                {overview.proposals.map((proposal, index) => (
                  <li
                    key={`${proposal.vehicleCode}-${index}`}
                    className="flex flex-wrap items-center justify-between gap-3 py-3"
                  >
                    <div className="min-w-0">
                      <p className="truncate text-sm font-semibold">
                        <HandCoins size={13} className="mr-1.5 inline text-[var(--text-muted)]" />
                        {proposal.prospectName}
                      </p>
                      <p className="num mt-0.5 text-xs text-[var(--text-secondary)]">
                        {proposal.plate} · {proposal.brand} {proposal.model} ·{" "}
                        {formatDate(proposal.date)}
                      </p>
                    </div>

                    <div className="flex items-center gap-4">
                      <p className="num text-sm font-semibold">{formatMoney(proposal.amount)}</p>
                      <Against
                        difference={proposal.difference}
                        percent={proposal.percent}
                        good="above"
                        missing="tabela"
                      />
                    </div>
                  </li>
                ))}
              </ul>
            </section>
          )}

          <Section
            title="Vendidos"
            subtitle="O preço fechado, contra a tabela do mês da venda"
            lines={overview.sold}
            emptyLabel="Nenhuma venda registrada."
          />
        </>
      )}
    </div>
  );
}

/** Uma das três respostas do topo: quanto a revenda ficou acima ou abaixo da tabela. */
function AverageCard({
  label,
  average,
  good,
  hint,
}: {
  label: string;
  average: MarketAverage;
  good: "above" | "below";
  hint: string;
}) {
  const semCarros = average.cars === 0;

  return (
    <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]">
      <p className="font-display mb-3 text-[10px] font-bold uppercase tracking-[.18em] text-[var(--text-muted)]">
        {label}
      </p>

      {semCarros ? (
        <>
          <p className="text-2xl font-bold text-[var(--text-muted)]">—</p>
          <p className="mt-1 text-xs text-[var(--text-secondary)]">
            Ainda sem carro com cotação para comparar
          </p>
        </>
      ) : (
        <>
          <p
            className="num text-2xl font-bold"
            style={{ color: tone(average.difference, good) }}
          >
            {formatPercent(average.percent)}
          </p>
          <p className="num mt-1 text-sm text-[var(--text-secondary)]">
            {formatMoney(average.amount)} contra {formatMoney(average.reference)} de tabela
          </p>
          <p className="mt-1 text-xs text-[var(--text-muted)]">
            {hint} · {average.cars === 1 ? "1 carro" : `${average.cars} carros`}
          </p>
        </>
      )}
    </div>
  );
}

/** Quanto o pátio perdeu de referência. Parar custa dinheiro, e este é o número disso. */
function LossCard({ label, value, hint }: { label: string; value: number; hint: string }) {
  return (
    <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]">
      <div className="mb-3 flex items-center justify-between">
        <p className="font-display text-[10px] font-bold uppercase tracking-[.18em] text-[var(--text-muted)]">
          {label}
        </p>
        <TrendingDown size={17} className="text-[var(--text-muted)]" />
      </div>
      <p
        className="num text-2xl font-bold"
        style={{ color: value > 0 ? "var(--critical)" : undefined }}
      >
        {value > 0 ? `− ${formatMoney(value)}` : formatMoney(0)}
      </p>
      <p className="mt-1 text-xs text-[var(--text-secondary)]">{hint}</p>
    </div>
  );
}

/** Uma lista de carros com a comparação de cada um. */
function Section({
  title,
  subtitle,
  lines,
  emptyLabel,
  showLoss = false,
}: {
  title: string;
  subtitle: string;
  lines: MarketLine[];
  emptyLabel: string;
  showLoss?: boolean;
}) {
  return (
    <section className="mb-6 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]">
      <p className="font-display mb-1 text-[11px] font-bold uppercase tracking-[.18em] text-[var(--signal)]">
        {title}
      </p>
      <p className="mb-4 text-xs text-[var(--text-secondary)]">{subtitle}</p>

      {lines.length === 0 ? (
        <p className="py-2 text-sm text-[var(--text-secondary)]">{emptyLabel}</p>
      ) : (
        <ul className="divide-y divide-[var(--border)]">
          {lines.map((line) => (
            <li key={line.code} className="py-3">
              <Link
                href={`/vehicles/${line.code}`}
                className="flex flex-wrap items-center justify-between gap-3 hover:opacity-80"
              >
                <div className="min-w-0">
                  <p className="flex flex-wrap items-center gap-2 text-sm font-semibold">
                    <span className="num text-xs text-[var(--signal)]">{line.plate}</span>
                    <span className="truncate">
                      {line.brand} {line.model}
                      {line.version && (
                        <span className="font-normal text-[var(--text-secondary)]">
                          {" "}
                          {line.version}
                        </span>
                      )}
                    </span>
                    <StatusPill status={line.status} />
                  </p>

                  <p className="num mt-0.5 text-xs text-[var(--text-secondary)]">
                    {line.modelYear}
                    {line.daysInStock !== null && ` · ${formatDays(line.daysInStock)} de pátio`}
                    {line.purchasePercent !== null && (
                      <span className="font-sans">
                        {" "}
                        · comprado {formatPercent(Math.abs(line.purchasePercent))}{" "}
                        {line.purchasePercent < 0 ? "abaixo" : "acima"} da tabela
                      </span>
                    )}
                    {showLoss && line.lostSincePurchase !== null && line.lostSincePurchase > 0 && (
                      <span className="font-sans">
                        {" "}
                        · perdeu {formatMoney(line.lostSincePurchase)} de referência
                      </span>
                    )}
                  </p>
                </div>

                <div className="flex items-center gap-4">
                  <div className="text-right">
                    <p className="num text-sm font-semibold">
                      {line.amount > 0 ? formatMoney(line.amount) : "—"}
                    </p>
                    {line.reference !== null && (
                      <p className="num text-[11px] text-[var(--text-muted)]">
                        tabela {formatMoney(line.reference)}
                      </p>
                    )}
                  </div>

                  <Against
                    difference={line.difference}
                    percent={line.percent}
                    good="above"
                    missing={line.amount > 0 ? "tabela" : "preço"}
                  />
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/** A distância até a tabela, com a seta que diz de que lado ela caiu. */
function Against({
  difference,
  percent,
  good,
  missing,
}: {
  difference: number | null;
  percent: number | null;
  good: "above" | "below";
  /** O que está faltando para comparar. São coisas diferentes, e a tela diz qual. */
  missing: "tabela" | "preço";
}) {
  if (difference === null || percent === null) {
    return (
      <span className="rounded-full bg-[var(--canvas)] px-2.5 py-1 text-[11px] font-semibold text-[var(--text-muted)]">
        {missing === "preço" ? "Preço em aberto" : "Sem comparação"}
      </span>
    );
  }

  const Icon = difference > 0 ? ArrowUpRight : difference < 0 ? ArrowDownRight : Minus;

  return (
    <span
      className="inline-flex min-w-24 items-center justify-end gap-1 text-sm font-bold"
      style={{ color: tone(difference, good) }}
    >
      <Icon size={15} />
      <span className="num">{formatPercent(Math.abs(percent))}</span>
    </span>
  );
}

/**
 * A cor da distância.
 *
 * Acima da tabela é bom vendendo e ruim comprando, então quem chama diz de que lado está o
 * bom. Um verde fixo diria que comprar caro é uma vitória.
 */
function tone(difference: number | null, good: "above" | "below"): string | undefined {
  if (difference === null || difference === 0) {
    return undefined;
  }

  const favorable = good === "above" ? difference > 0 : difference < 0;

  return favorable ? "var(--success)" : "var(--critical)";
}
