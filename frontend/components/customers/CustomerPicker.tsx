"use client";

import { useEffect, useId, useRef, useState } from "react";
import { Check, UserPlus, X } from "lucide-react";
import { apiGet } from "@/lib/api";
import { maskPhone } from "@/lib/masks";
import type { Customer } from "@/lib/types";

/** O que o seletor devolve: o cliente escolhido, ou só o nome digitado de um novo. */
export type CustomerPick = {
  /** O cliente escolhido na lista. Nulo enquanto a pessoa só digitou um nome. */
  customerCode: string | null;
  name: string;
  /** O telefone do cliente escolhido, já com máscara. Ausente quando a pessoa só digitou. */
  phone?: string;
  /** O documento do cliente escolhido, só dígitos. Ausente quando a pessoa só digitou. */
  document?: string | null;
};

/**
 * Quem ofereceu, quem comprou (M21): um campo que busca enquanto digita, por nome, telefone
 * ou documento, e oferece os clientes que a revenda já conhece. Escolher um preenche o resto;
 * digitar um nome novo cadastra na hora de salvar — a proposta jamais espera um cadastro.
 *
 * A busca é do banco, a cada trecho de duas letras, com um respiro de 250 ms para o servidor
 * receber o que a pessoa quis, e não cada tecla.
 */
export function CustomerPicker({
  label,
  name,
  customerCode,
  onChange,
  required = false,
  placeholder = "Nome, telefone ou CPF",
  hint,
}: {
  label: string;
  name: string;
  customerCode: string | null;
  onChange: (pick: CustomerPick) => void;
  required?: boolean;
  placeholder?: string;
  hint?: string;
}) {
  const id = useId();
  // O resultado guarda a pergunta que o gerou: "buscando" é a pergunta de agora ser outra, e
  // nenhum estado precisa ser escrito de dentro do efeito.
  const [results, setResults] = useState<{ query: string; list: Customer[] }>({ query: "", list: [] });
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const boxRef = useRef<HTMLDivElement>(null);

  const query = name.trim();
  const enabled = !customerCode && query.length >= 2;
  const matches = enabled && results.query === query ? results.list : [];
  const searching = enabled && results.query !== query;

  useEffect(() => {
    if (!enabled) return;

    let cancelled = false;

    const timer = setTimeout(async () => {
      const result = await apiGet<Customer[]>(
        `customers?search=${encodeURIComponent(query)}`,
        "Falha ao buscar clientes.",
      );

      if (cancelled) return;
      setResults({ query, list: result.ok ? result.data.slice(0, 8) : [] });
      setActive(0);
    }, 250);

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [query, enabled]);

  useEffect(() => {
    function close(event: MouseEvent) {
      if (boxRef.current && !boxRef.current.contains(event.target as Node)) setOpen(false);
    }

    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, []);

  function pick(customer: Customer) {
    onChange({
      customerCode: customer.code,
      name: customer.name,
      phone: customer.phone ? maskPhone(customer.phone) : "",
      document: customer.document,
    });
    setOpen(false);
  }

  function unlink() {
    onChange({ customerCode: null, name: "" });
  }

  const showList = open && enabled && !searching;

  return (
    <div className="block" ref={boxRef}>
      <label htmlFor={id} className="mb-1.5 block">
        <span className="text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
          {label}
          {required && (
            <span className="ml-1 text-[var(--critical)]" title="Obrigatório">
              *
            </span>
          )}
        </span>
      </label>

      <div className="relative">
        <input
          id={id}
          role="combobox"
          aria-expanded={showList}
          aria-controls={`${id}-list`}
          aria-autocomplete="list"
          value={name}
          readOnly={!!customerCode}
          placeholder={placeholder}
          aria-required={required || undefined}
          onFocus={() => setOpen(true)}
          onChange={(event) => {
            setOpen(true);
            onChange({ customerCode: null, name: event.target.value });
          }}
          onKeyDown={(event) => {
            if (!showList) return;
            if (event.key === "ArrowDown") {
              event.preventDefault();
              setActive((current) => Math.min(current + 1, matches.length - 1));
            } else if (event.key === "ArrowUp") {
              event.preventDefault();
              setActive((current) => Math.max(current - 1, 0));
            } else if (event.key === "Enter" && matches[active]) {
              event.preventDefault();
              pick(matches[active]);
            } else if (event.key === "Escape") {
              setOpen(false);
            }
          }}
          className={customerCode ? "control pr-9 font-semibold" : "control"}
        />

        {customerCode && (
          <button
            type="button"
            onClick={unlink}
            aria-label="Trocar o cliente"
            title="Trocar o cliente"
            className="absolute inset-y-0 right-0 grid w-9 place-items-center text-[var(--text-muted)] hover:text-[var(--critical)]"
          >
            <X size={15} />
          </button>
        )}

        {showList && (
          <ul
            id={`${id}-list`}
            role="listbox"
            className="absolute z-20 mt-1 max-h-64 w-max min-w-full max-w-[min(26rem,80vw)] overflow-auto rounded-md border border-[var(--border)] bg-[var(--surface)] py-1 shadow-[var(--shadow)]"
          >
            {matches.map((customer, index) => (
              <li
                key={customer.code}
                role="option"
                aria-selected={index === active}
                onMouseDown={(event) => {
                  event.preventDefault();
                  pick(customer);
                }}
                onMouseEnter={() => setActive(index)}
                className={[
                  "flex cursor-pointer items-center justify-between gap-3 px-3 py-2 text-sm",
                  index === active ? "bg-[var(--surface-2)]" : "",
                ].join(" ")}
              >
                <span className="min-w-0">
                  <span className="block truncate font-semibold">{customer.name}</span>
                  <span className="block truncate text-xs text-[var(--text-muted)]">
                    {customer.phone ? maskPhone(customer.phone) : "Sem telefone"}
                    {customer.saleCount > 0 &&
                      ` · ${customer.saleCount === 1 ? "1 carro comprado" : `${customer.saleCount} carros comprados`}`}
                    {customer.saleCount === 0 &&
                      customer.proposalCount > 0 &&
                      ` · ${customer.proposalCount === 1 ? "1 proposta" : `${customer.proposalCount} propostas`}`}
                  </span>
                </span>
              </li>
            ))}

            <li
              role="option"
              aria-selected={false}
              className="flex items-center gap-2 border-t border-[var(--border)] px-3 py-2 text-xs text-[var(--text-secondary)]"
            >
              <UserPlus size={13} className="shrink-0 text-[var(--signal)]" />
              {matches.length > 0
                ? `Ninguém destes? "${name.trim()}" entra como cliente novo ao salvar.`
                : `"${name.trim()}" entra como cliente novo ao salvar.`}
            </li>
          </ul>
        )}
      </div>

      {customerCode ? (
        <span className="mt-1.5 inline-flex items-center gap-1 text-xs font-semibold text-[var(--success)]">
          <Check size={12} />
          Cliente cadastrado
        </span>
      ) : name.trim().length > 0 ? (
        <span className="mt-1.5 inline-flex items-center gap-1 text-xs font-semibold text-[var(--signal)]">
          <UserPlus size={12} />
          Novo cliente: entra no cadastro ao salvar
        </span>
      ) : (
        hint && <span className="mt-1.5 block text-xs text-[var(--text-secondary)]">{hint}</span>
      )}
    </div>
  );
}
