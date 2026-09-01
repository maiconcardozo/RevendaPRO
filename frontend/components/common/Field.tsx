"use client";

import { useId, type ReactNode } from "react";

/**
 * Form field with a label, an optional mask and a per field error.
 *
 * The mask runs on every keystroke and returns the already formatted text. What the state
 * holds is the masked text; stripping the mask before sending it to the API is the screen's
 * job, so the database always receives raw digits.
 */
export function Field({
  label,
  value,
  onChange,
  mask,
  type = "text",
  hint,
  error,
  aside,
  placeholder,
  inputMode,
  autoComplete,
  maxLength,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  mask?: (value: string) => string;
  type?: "text" | "email" | "password" | "tel";
  hint?: ReactNode;
  error?: string;
  aside?: ReactNode;
  placeholder?: string;
  inputMode?: "text" | "email" | "numeric" | "tel";
  autoComplete?: string;
  maxLength?: number;
}) {
  const id = useId();
  const errorId = `${id}-error`;

  return (
    <label className="block" htmlFor={id}>
      <span className="mb-1.5 flex items-center justify-between gap-2">
        <span className="text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
          {label}
        </span>
        {aside}
      </span>

      <input
        id={id}
        type={type}
        value={value}
        placeholder={placeholder}
        inputMode={inputMode}
        autoComplete={autoComplete}
        maxLength={maxLength}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        onChange={(event) =>
          onChange(mask ? mask(event.target.value) : event.target.value)
        }
        className={[
          "w-full rounded-md border bg-[var(--canvas)] px-3 py-2 text-sm transition",
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
