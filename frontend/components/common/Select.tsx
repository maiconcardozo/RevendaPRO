"use client";

import { useId, type ReactNode } from "react";

/** One option of the list. The value is always text; converting is the screen's job. */
export type Option = { value: string; label: string };

/**
 * A choice list, with the same label and the same per field error as `Field`.
 *
 * Kept apart from `Field` because a `select` takes neither a mask nor an `inputMode`, and
 * forcing both into one component would leave half the props meaningless at every call site.
 */
export function Select({
  label,
  value,
  onChange,
  options,
  required = false,
  hint,
  error,
  placeholder,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: Option[];
  required?: boolean;
  hint?: ReactNode;
  error?: string;
  /** First row, empty, for when the field is optional. */
  placeholder?: string;
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

      <select
        id={id}
        value={value}
        aria-required={required || undefined}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        onChange={(event) => onChange(event.target.value)}
        className={[
          "w-full rounded-md border bg-[var(--canvas)] px-3 py-2 text-sm transition",
          error
            ? "border-[var(--critical)]"
            : "border-[var(--border)] focus:border-[var(--primary)]",
        ].join(" ")}
      >
        {placeholder && <option value="">{placeholder}</option>}

        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>

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

/** Options built from a map of labels keyed by numeric value. */
export function optionsOf(labels: Record<number, string>): Option[] {
  return Object.entries(labels).map(([value, label]) => ({ value, label }));
}
