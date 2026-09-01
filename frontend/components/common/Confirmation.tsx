"use client";

import type { ReactNode } from "react";
import { Modal } from "./Modal";

export function Confirmation({
  title,
  message,
  confirmLabel,
  onConfirm,
  onCancel,
  busy = false,
  danger = false,
  error,
}: {
  title: string;
  message: ReactNode;
  confirmLabel: string;
  onConfirm: () => void;
  onCancel: () => void;
  busy?: boolean;
  danger?: boolean;
  error?: string | null;
}) {
  return (
    <Modal
      title={title}
      onClose={onCancel}
      error={error}
      width="max-w-md"
      footer={
        <>
          <button
            type="button"
            onClick={onCancel}
            className="rounded-md border border-[var(--border)] px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--surface-2)]"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={busy}
            className={[
              "rounded-md px-4 py-2 text-sm font-semibold text-white transition disabled:opacity-60",
              danger
                ? "bg-[var(--critical)] hover:brightness-110"
                : "bg-[var(--primary)] hover:bg-[var(--primary-strong)]",
            ].join(" ")}
          >
            {busy ? "Aguarde..." : confirmLabel}
          </button>
        </>
      }
    >
      <p className="text-sm leading-relaxed text-[var(--text-secondary)]">{message}</p>
    </Modal>
  );
}
