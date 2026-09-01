"use client";

import { useEffect, useId, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { X } from "lucide-react";

const FOCUSABLE =
  'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';

/**
 * Panel modal.
 *
 * It goes into a portal on the body on purpose: any ancestor carrying a transform becomes
 * a containing block for position: fixed, and the modal would then position itself against
 * the content instead of the viewport. The portal isolates it from that trap.
 *
 * The scrim covers the whole panel and stops at the sidebar (see .panel-scrim).
 * Header and footer are sticky: in a tall form, the title and the action buttons never
 * leave the screen.
 */
export function Modal({
  title,
  onClose,
  footer,
  children,
  error,
  width = "max-w-2xl",
}: {
  title: string;
  onClose: () => void;
  footer?: ReactNode;
  children: ReactNode;
  /** Operation error. Shows INSIDE the modal: a warning behind it goes unread. */
  error?: string | null;
  width?: string;
}) {
  const [mounted, setMounted] = useState(false);
  const box = useRef<HTMLDivElement>(null);
  const previousFocus = useRef<HTMLElement | null>(null);
  const titleId = useId();

  useEffect(() => setMounted(true), []);

  // Locks background scrolling while the modal is open.
  useEffect(() => {
    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    return () => {
      document.body.style.overflow = previous;
    };
  }, []);

  // Focus enters the modal on open and returns to whoever opened it on close.
  useEffect(() => {
    previousFocus.current = document.activeElement as HTMLElement | null;

    const first = box.current?.querySelector<HTMLElement>(FOCUSABLE);
    first?.focus();

    return () => previousFocus.current?.focus();
  }, [mounted]);

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.stopPropagation();
        onClose();
        return;
      }

      if (event.key !== "Tab" || !box.current) {
        return;
      }

      const focusable = [...box.current.querySelectorAll<HTMLElement>(FOCUSABLE)];

      if (focusable.length === 0) {
        return;
      }

      const first = focusable[0];
      const last = focusable[focusable.length - 1];

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener("keydown", onKeyDown, true);
    return () => document.removeEventListener("keydown", onKeyDown, true);
  }, [onClose]);

  if (!mounted) {
    return null;
  }

  return createPortal(
    <div
      className="panel-scrim"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      {/* min-h-full + items-center centres it when it fits and scrolls over it when it does not,
          without ever clipping the top of the card. */}
      <div className="flex min-h-full items-center justify-center p-4 sm:p-6">
        <div
          ref={box}
          role="dialog"
          aria-modal="true"
          aria-labelledby={titleId}
          onMouseDown={(event) => event.stopPropagation()}
          className={`modal-card w-full ${width} overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow-lg)]`}
        >
          <div className="sticky top-0 z-10 flex items-center justify-between border-b border-[var(--border)] bg-[var(--surface)] px-6 py-4">
            <h2 id={titleId} className="text-lg font-bold">
              {title}
            </h2>
            <button
              type="button"
              onClick={onClose}
              aria-label="Fechar"
              className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)]"
            >
              <X size={18} />
            </button>
          </div>

          {error && (
            <p
              role="alert"
              className="mx-6 mt-5 rounded-md border border-[color-mix(in_srgb,var(--critical)_40%,transparent)] bg-[color-mix(in_srgb,var(--critical)_8%,transparent)] px-4 py-3 text-sm text-[var(--critical)]"
            >
              {error}
            </p>
          )}

          <div className="px-6 py-5">{children}</div>

          {footer && (
            <div className="sticky bottom-0 flex justify-end gap-2 border-t border-[var(--border)] bg-[var(--surface)] px-6 py-4">
              {footer}
            </div>
          )}
        </div>
      </div>
    </div>,
    document.body,
  );
}
