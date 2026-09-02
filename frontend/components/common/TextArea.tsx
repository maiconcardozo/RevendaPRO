"use client";

import { useId, type ReactNode } from "react";

/**
 * Free text.
 *
 * The business writes things like "comprado na autopeças joãozinho" and "cinco anúncios em
 * Joinville entre 57 e 63 mil". That fits no structured field, and structuring it now would
 * only slow the entry down — and the entry has to beat the Word document it replaces.
 */
export function TextArea({
  label,
  value,
  onChange,
  rows = 3,
  required = false,
  hint,
  error,
  placeholder,
  maxLength,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  rows?: number;
  required?: boolean;
  hint?: ReactNode;
  error?: string;
  placeholder?: string;
  maxLength?: number;
}) {
  const id = useId();
  const errorId = `${id}-error`;

  return (
    <label className="block" htmlFor={id}>
      <span className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
        {label}
        {required && (
          <span className="ml-1 text-[var(--critical)]" title="Obrigatório">
            *
          </span>
        )}
      </span>

      <textarea
        id={id}
        rows={rows}
        value={value}
        placeholder={placeholder}
        maxLength={maxLength}
        aria-required={required || undefined}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        onChange={(event) => onChange(event.target.value)}
        className={[
          "w-full resize-y rounded-md border bg-[var(--canvas)] px-3 py-2 text-sm transition",
          error
            ? "border-[var(--critical)]"
            : "border-[var(--border)] focus:border-[var(--primary)]",
        ].join(" ")}
      />

      {error ? (
        <span id={errorId} role="alert" className="mt-1.5 block text-xs text-[var(--critical)]">
          {error}
        </span>
      ) : (
        hint && <span className="mt-1.5 block text-xs text-[var(--text-secondary)]">{hint}</span>
      )}
    </label>
  );
}
