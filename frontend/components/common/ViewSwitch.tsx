"use client";

import { useSyncExternalStore, type ReactNode } from "react";
import { LayoutGrid, List } from "lucide-react";

/**
 * As duas formas de mostrar uma lista (M17): o **mosaico** de cards e a **lista** densa.
 *
 * Card e lista respondem perguntas diferentes. O mosaico responde *"qual é este?"* — cada item
 * grande o bastante para ser reconhecido. A lista responde *"qual destes?"* — cada linha curta o
 * bastante para comparar vinte. Nasceu nos veículos e vale para todo cadastro do sistema.
 */
export type ViewMode = "grid" | "list";

/**
 * A forma escolhida, guardada no navegador de quem olha — preferência de leitura, e jamais
 * dado da empresa: guardá-la no servidor faria a escolha do vendedor mudar a tela do financeiro.
 *
 * Lida como um armazenamento externo (`useSyncExternalStore`): o servidor desenha sempre o
 * mosaico, e o navegador troca para o que está guardado logo depois da hidratação, sem os dois
 * desenharem coisas diferentes no mesmo render — o erro que o React acusa em voz alta. O preço
 * é um quadro de mosaico antes da lista aparecer, para quem escolheu lista.
 *
 * @param key A chave no `localStorage`, uma por tela: `revendapro.<tela>.view`.
 */
export function useViewMode(key: string): [ViewMode, (next: ViewMode) => void] {
  const view = useSyncExternalStore(
    (onChange) => {
      window.addEventListener("storage", onChange);
      window.addEventListener(VIEW_EVENT, onChange);

      return () => {
        window.removeEventListener("storage", onChange);
        window.removeEventListener(VIEW_EVENT, onChange);
      };
    },
    () => read(key),
    () => "grid" as ViewMode,
  );

  function choose(next: ViewMode) {
    try {
      localStorage.setItem(key, next);
    } catch {
      // Guardar falhou (janela anônima, site sem permissão de armazenamento): a escolha vale
      // enquanto a tela estiver aberta, e é o que dá para prometer.
      memory.set(key, next);
    }

    window.dispatchEvent(new Event(VIEW_EVENT));
  }

  return [view, choose];
}

/** O evento que avisa a tela de que a escolha mudou nesta mesma janela. */
const VIEW_EVENT = "revendapro:view";

/** A escolha desta visita, para quando o navegador recusa guardar. */
const memory = new Map<string, ViewMode>();

function read(key: string): ViewMode {
  try {
    const saved = localStorage.getItem(key);

    if (saved === "list" || saved === "grid") {
      return saved;
    }
  } catch {
    // Sem armazenamento: cai na escolha desta visita, ou no mosaico.
  }

  return memory.get(key) ?? "grid";
}

/**
 * O seletor entre mosaico e lista.
 *
 * Dois botões lado a lado, e não um ícone que alterna: assim ele mostra as opções e o estado
 * atual no mesmo lugar. Um ícone sozinho que troca de cara ao ser clicado esconde metade da
 * informação.
 */
export function ViewSwitch({
  value,
  onChange,
  label = "Como mostrar a lista",
}: {
  value: ViewMode;
  onChange: (view: ViewMode) => void;
  label?: string;
}) {
  const options: { key: ViewMode; label: string; icon: typeof LayoutGrid }[] = [
    { key: "grid", label: "Mosaico", icon: LayoutGrid },
    { key: "list", label: "Lista", icon: List },
  ];

  return (
    <div
      role="group"
      aria-label={label}
      className="inline-flex overflow-hidden rounded-md border border-[var(--border)]"
    >
      {options.map((option) => {
        const active = value === option.key;

        return (
          <button
            key={option.key}
            type="button"
            onClick={() => onChange(option.key)}
            aria-pressed={active}
            title={`Ver em ${option.label.toLowerCase()}`}
            className={[
              "inline-flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-semibold transition",
              active
                ? "bg-[var(--primary)] text-white"
                : "text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--primary)]",
            ].join(" ")}
          >
            <option.icon size={14} />
            <span className="hidden sm:inline">{option.label}</span>
          </button>
        );
      })}
    </div>
  );
}

/**
 * A barra entre o filtro e o resultado: quantos sobraram, e o seletor de forma.
 *
 * A contagem nasceu do lugar — uma barra ali pedia dizer quantos há, que é a primeira pergunta
 * de quem acabou de filtrar. O seletor some com a lista vazia: escolher entre duas formas de
 * mostrar nada é uma pergunta sem resposta útil.
 */
export function ListBar({
  count,
  singular,
  plural,
  loading = false,
  view,
  onChange,
  label,
  children,
}: {
  count: number;
  singular: string;
  plural: string;
  loading?: boolean;
  view: ViewMode;
  onChange: (view: ViewMode) => void;
  label?: string;
  /** O que mais couber na barra, à esquerda do seletor. */
  children?: ReactNode;
}) {
  if (count === 0 && !loading) {
    return null;
  }

  return (
    <div className="mb-3 flex items-center justify-between gap-3">
      <p className="text-xs text-[var(--text-muted)]">
        {loading ? (
          "Carregando…"
        ) : (
          <>
            <span className="num font-semibold text-[var(--text-secondary)]">{count}</span>{" "}
            {count === 1 ? singular : plural}
          </>
        )}
      </p>

      <div className="flex items-center gap-3">
        {children}
        <ViewSwitch value={view} onChange={onChange} label={label} />
      </div>
    </div>
  );
}

/** A moldura da lista densa: uma borda, e as linhas separadas por fio. */
export function ListFrame({ children }: { children: ReactNode }) {
  return (
    <ul className="divide-y divide-[var(--border)] overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow)]">
      {children}
    </ul>
  );
}
