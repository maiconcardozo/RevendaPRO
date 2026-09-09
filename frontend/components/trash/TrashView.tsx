"use client";

import { useState } from "react";
import Link from "next/link";
import { ArchiveRestore, Car, ExternalLink, FileText, Wrench } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { Empty, PageError } from "@/components/vehicles/VehicleUi";
import { apiGet, apiSend } from "@/lib/api";
import { formatBytes, formatDate, formatMoment, formatMoney } from "@/lib/masks";
import {
  TRASH_KIND,
  VEHICLE_DOCUMENT_KIND_LABEL,
  type DeletedItem,
} from "@/lib/types";

const TABS = [
  [TRASH_KIND.vehicle, "Veículos", Car],
  [TRASH_KIND.expense, "Gastos", Wrench],
  [TRASH_KIND.document, "Documentos", FileText],
] as const;

const EMPTY: Record<number, string> = {
  [TRASH_KIND.vehicle]: "Todo carro da revenda está no pátio.",
  [TRASH_KIND.expense]: "Gasto nenhum foi apagado.",
  [TRASH_KIND.document]: "Todo documento da revenda está na ficha do seu veículo.",
};

/**
 * A lixeira (M23): o que foi apagado por engano, e a porta de volta.
 *
 * Uma tela só, com uma aba por tipo, porque quem apagou por engano tem uma pergunta — "onde
 * está o que eu apaguei?" — e três telas seriam três lugares para procurar a mesma coisa. Ela
 * cresceu da tela de documentos excluídos, e a chave de permissão continua sendo a de lá.
 *
 * Botão para apagar de vez, jamais: guardar foi o que o negócio pediu.
 */
export function TrashView({ initialVehicles }: { initialVehicles: DeletedItem[] }) {
  const [tab, setTab] = useState<number>(TRASH_KIND.vehicle);
  const [pages, setPages] = useState<Record<number, DeletedItem[]>>({
    [TRASH_KIND.vehicle]: initialVehicles,
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [toRestore, setToRestore] = useState<DeletedItem | null>(null);
  const [restoreError, setRestoreError] = useState("");
  const [busy, setBusy] = useState(false);

  const items = pages[tab];

  async function load(kind: number) {
    setLoading(true);

    const result = await apiGet<DeletedItem[]>(
      `trash?kind=${kind}`,
      "Falha ao carregar a lixeira.",
    );

    setLoading(false);

    if (result.ok) {
      setPages((previous) => ({ ...previous, [kind]: result.data }));
      setError("");
    } else {
      setError(result.error);
    }
  }

  async function open(kind: number) {
    setTab(kind);

    // Cada aba é buscada uma vez: trocar de aba para conferir e voltar jamais custa outra ida
    // ao banco.
    if (pages[kind] === undefined) {
      await load(kind);
    }
  }

  async function restore() {
    if (!toRestore) return;

    setBusy(true);
    setRestoreError("");

    const result = await apiSend(
      "POST",
      `trash/${toRestore.kind}/${toRestore.code}/restore`,
      "Falha ao devolver.",
    );

    setBusy(false);

    if (!result.ok) {
      // As duas recusas do marco chegam com o motivo escrito, e é o motivo que a pessoa lê:
      // de quem é a placa agora, ou que o carro do gasto volta primeiro.
      setRestoreError(result.error);
      return;
    }

    setToRestore(null);

    // Devolver um carro muda a aba de gastos junto — um gasto que dizia "este carro está na
    // lixeira" passa a apontar para a ficha. Por isso o que se guardou é descartado inteiro.
    setPages({});
    await load(tab);
  }

  return (
    <div className="dash-anim">
      <div className="mb-6">
        <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
          Administração
        </p>
        <h1 className="hero-title text-3xl font-bold">Lixeira</h1>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          Tudo que foi excluído continua guardado, com o dia e o nome de quem apagou. Confira
          aqui, e devolva quando a exclusão tiver sido engano.
        </p>
      </div>

      <PageError message={error} />

      <div className="mb-6 flex gap-1 overflow-x-auto border-b border-[var(--border)] [scrollbar-width:none]">
        {TABS.map(([kind, label, Icon]) => (
          <button
            key={kind}
            type="button"
            onClick={() => open(kind)}
            aria-current={tab === kind ? "page" : undefined}
            className={[
              "inline-flex shrink-0 items-center gap-2 whitespace-nowrap border-b-2 px-3.5 py-2.5 text-sm font-semibold transition",
              tab === kind
                ? "border-[var(--primary)] text-[var(--primary)]"
                : "border-transparent text-[var(--text-secondary)] hover:text-[var(--text-primary)]",
            ].join(" ")}
          >
            <Icon size={15} />
            {label}
          </button>
        ))}
      </div>

      {items === undefined || loading ? (
        <p className="py-10 text-center text-sm text-[var(--text-muted)]">Carregando…</p>
      ) : items.length === 0 ? (
        <Empty title={EMPTY[tab]} />
      ) : (
        <div className="overflow-x-auto rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow)]">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[var(--border)] text-left text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
                <th className="px-4 py-3">
                  {tab === TRASH_KIND.vehicle
                    ? "Veículo"
                    : tab === TRASH_KIND.expense
                      ? "Gasto"
                      : "Documento"}
                </th>
                {tab !== TRASH_KIND.vehicle && <th className="px-4 py-3">Veículo</th>}
                {tab !== TRASH_KIND.document && <th className="px-4 py-3">Valor</th>}
                <th className="px-4 py-3">Excluído em</th>
                <th className="px-4 py-3">Por</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>

            <tbody>
              {items.map((item) => (
                <tr key={item.code} className="border-b border-[var(--border)] last:border-0">
                  <td className="px-4 py-3">
                    <p className="font-semibold">{item.title}</p>
                    <p className="num text-xs text-[var(--text-muted)]">
                      {describe(item)}
                    </p>
                  </td>

                  {tab !== TRASH_KIND.vehicle && (
                    <td className="px-4 py-3">
                      {item.vehicleCode && item.vehicleIsInYard ? (
                        <Link
                          href={`/vehicles/${item.vehicleCode}`}
                          className="font-semibold text-[var(--primary)] hover:underline"
                        >
                          {item.vehiclePlate}
                        </Link>
                      ) : (
                        <p className="font-semibold">{item.vehiclePlate}</p>
                      )}
                      <p className="text-xs text-[var(--text-muted)]">
                        {item.vehicleIsInYard === false
                          ? "Este carro está na lixeira"
                          : item.vehicleName}
                      </p>
                    </td>
                  )}

                  {tab !== TRASH_KIND.document && (
                    <td className="num px-4 py-3 text-[var(--text-secondary)]">
                      {formatMoney(item.amount)}
                    </td>
                  )}

                  <td className="num px-4 py-3 text-[var(--text-secondary)]">
                    {formatMoment(item.deletedAt)}
                  </td>

                  <td className="px-4 py-3 text-[var(--text-secondary)]">
                    {item.deletedBy ?? "—"}
                  </td>

                  <td className="px-4 py-3">
                    <div className="flex items-center justify-end gap-2">
                      {item.fileUrl && (
                        <a
                          href={item.fileUrl}
                          target="_blank"
                          rel="noreferrer"
                          title="Abrir o arquivo"
                          className="inline-flex items-center gap-1.5 rounded-md border border-[var(--border)] px-2.5 py-1.5 text-xs font-semibold text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)]"
                        >
                          <ExternalLink size={14} />
                          Abrir
                        </a>
                      )}

                      <button
                        type="button"
                        onClick={() => {
                          setRestoreError("");
                          setToRestore(item);
                        }}
                        className="inline-flex items-center gap-1.5 rounded-md bg-[var(--primary)] px-2.5 py-1.5 text-xs font-semibold text-white transition hover:bg-[var(--primary-strong)]"
                      >
                        <ArchiveRestore size={14} />
                        Devolver
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {toRestore && (
        <Confirmation
          title={
            toRestore.kind === TRASH_KIND.vehicle
              ? "Devolver o veículo"
              : toRestore.kind === TRASH_KIND.expense
                ? "Devolver o gasto"
                : "Devolver o documento"
          }
          message={
            <>
              <span className="font-semibold">{toRestore.title}</span>
              {toRestore.kind === TRASH_KIND.vehicle ? (
                <> volta para o pátio com as fotos, os gastos, os documentos e a linha do tempo.</>
              ) : (
                <>
                  {" "}
                  volta para a ficha do {toRestore.vehicleName}, placa {toRestore.vehiclePlate}.
                </>
              )}
            </>
          }
          confirmLabel="Devolver"
          onConfirm={restore}
          onCancel={() => setToRestore(null)}
          busy={busy}
          error={restoreError}
        />
      )}
    </div>
  );
}

/** A segunda linha da primeira coluna, que muda com o tipo. */
function describe(item: DeletedItem): string {
  if (item.kind === TRASH_KIND.document) {
    const kind = item.documentKind ? VEHICLE_DOCUMENT_KIND_LABEL[item.documentKind] : "Documento";

    return `${kind} · ${formatBytes(item.sizeInBytes ?? 0)}`;
  }

  if (item.kind === TRASH_KIND.expense) {
    return [item.subtitle, formatDate(item.date)].filter(Boolean).join(" · ");
  }

  return item.subtitle ?? "";
}
