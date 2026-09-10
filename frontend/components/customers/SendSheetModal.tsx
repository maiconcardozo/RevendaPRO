"use client";

import { useEffect, useState } from "react";
import { Car, MessageCircle, Search } from "lucide-react";
import { Modal } from "@/components/common/Modal";
import { apiGet } from "@/lib/api";
import { formatMoney } from "@/lib/masks";
import {
  SHARE_NOTICE,
  isHandheld,
  openPendingWindow,
  prepareDocument,
  sendDocument,
  sendThroughPendingWindow,
  type PreparedDocument,
} from "@/lib/share";
import { VEHICLE_STATUS_LABEL, type Customer, type Vehicle } from "@/lib/types";

/** Pronto para venda, anunciado, em negociação: o que dá para oferecer a alguém. */
const FOR_SALE = new Set([4, 5, 6]);

/**
 * Mandar a ficha de um carro a um cliente (M21): a lista do pátio, a pessoa escolhe um, e o
 * WhatsApp abre já no número do cliente com o PDF — pela folha do aparelho no celular, pelo
 * download e a conversa no computador (M20). É o carro novo chegando a quem já comprou, sem
 * procurar o contato.
 */
export function SendSheetModal({ customer, onClose }: { customer: Customer; onClose: () => void }) {
  const [vehicles, setVehicles] = useState<Vehicle[] | null>(null);
  const [search, setSearch] = useState("");
  const [sending, setSending] = useState<string | null>(null);

  // No celular a folha só abre dentro do toque, então o primeiro toque busca o PDF e o guarda
  // aqui, e o segundo manda. No computador é um toque só: a aba abre em branco e é apontada.
  const [prepared, setPrepared] = useState<Record<string, PreparedDocument>>({});
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;

    apiGet<Vehicle[]>("vehicles", "Falha ao carregar os carros.").then((result) => {
      if (cancelled) return;
      if (result.ok) setVehicles(result.data.filter((vehicle) => FOR_SALE.has(vehicle.status)));
      else setError(result.error);
    });

    return () => {
      cancelled = true;
    };
  }, []);

  const term = search.trim().toLowerCase();
  const shown = (vehicles ?? []).filter((vehicle) =>
    term.length === 0 || `${vehicle.brand} ${vehicle.model} ${vehicle.version ?? ""} ${vehicle.plate}`.toLowerCase().includes(term),
  );

  function send(vehicle: Vehicle) {
    setSending(vehicle.code);
    setNotice("");
    setError("");

    const name = `${vehicle.brand} ${vehicle.model}${vehicle.version ? ` ${vehicle.version}` : ""} ${vehicle.manufactureYear}/${vehicle.modelYear}`;
    const price = vehicle.advertisedPrice ? `, por ${formatMoney(vehicle.advertisedPrice)}` : "";
    const first = customer.name.split(" ")[0];
    const message = `Olá, ${first}! Chegou um ${name}${price}, e lembrei de você. A ficha em PDF vai em anexo. Qualquer dúvida, é só chamar.`;
    const path = `vehicles/${vehicle.code}/reports/sale-sheet`;
    const fallbackName = `Ficha${vehicle.plate}.pdf`;

    const finish = (result: Awaited<ReturnType<typeof sendDocument>>) => {
      setSending(null);
      if (!result.ok) setError(result.error);
      else setNotice(SHARE_NOTICE[result.how]);
    };

    const ready = prepared[vehicle.code];

    if (ready) {
      // Ainda dentro do toque: é o que a folha do aparelho exige.
      sendDocument(ready, message, customer.phone).then(finish);
      return;
    }

    if (isHandheld()) {
      prepareDocument(path, fallbackName).then((result) => {
        if (!result.ok) {
          finish(result);
          return;
        }
        setPrepared((previous) => ({ ...previous, [vehicle.code]: result.document }));
        finish({ ok: true, how: "preparing" });
      });
      return;
    }

    const popup = openPendingWindow();

    prepareDocument(path, fallbackName).then((result) => {
      if (!result.ok) {
        popup?.close();
        finish(result);
        return;
      }
      finish(sendThroughPendingWindow(popup, result.document, message, customer.phone));
    });
  }

  return (
    <Modal title={`Mandar ficha para ${customer.name.split(" ")[0]}`} onClose={onClose} error={error} width="max-w-2xl">
      <div className="space-y-4">
        <p className="text-sm text-[var(--text-secondary)]">
          Escolha o carro. A ficha em PDF vai pelo WhatsApp
          {customer.phone ? " para o número do cliente" : "; sem telefone no cadastro, o WhatsApp pergunta para quem"}.
        </p>

        {notice && <p className="text-sm font-medium text-[var(--success)]">{notice}</p>}

        <label className="relative block">
          <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[var(--text-muted)]" />
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Marca, modelo ou placa"
            aria-label="Buscar carro"
            className="control pl-9"
          />
        </label>

        {vehicles === null && !error && (
          <p className="text-sm text-[var(--text-secondary)]">Carregando o pátio...</p>
        )}

        {vehicles !== null && shown.length === 0 && (
          <p className="text-sm text-[var(--text-secondary)]">
            {term ? `Nenhum carro à venda com "${search.trim()}".` : "Nenhum carro à venda no momento."}
          </p>
        )}

        {shown.length > 0 && (
          <ul className="max-h-[50vh] divide-y divide-[var(--border)] overflow-auto rounded-xl border border-[var(--border)] bg-[var(--surface)]">
            {shown.map((vehicle) => (
              <li key={vehicle.code} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                <Car size={16} className="shrink-0 text-[var(--signal)]" />
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-semibold">
                    {vehicle.brand} {vehicle.model}
                    {vehicle.version ? ` ${vehicle.version}` : ""} {vehicle.modelYear}
                  </span>
                  <span className="num block text-xs text-[var(--text-muted)]">
                    {vehicle.plate}
                    <span className="font-sans">
                      {" · "}
                      {VEHICLE_STATUS_LABEL[vehicle.status]}
                      {" · "}
                      {vehicle.advertisedPrice ? formatMoney(vehicle.advertisedPrice) : "preço a definir"}
                    </span>
                  </span>
                </span>
                <button
                  type="button"
                  onClick={() => send(vehicle)}
                  disabled={sending !== null}
                  className="inline-flex shrink-0 items-center gap-1.5 rounded-md border border-[var(--border)] px-2.5 py-1.5 text-xs font-semibold text-[var(--text-secondary)] transition hover:border-[var(--success)] hover:text-[var(--success)] disabled:opacity-40"
                >
                  <MessageCircle size={14} />
                  {sending === vehicle.code ? "Preparando..." : "Mandar"}
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  );
}
