"use client";

import { useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { Check, Lock, Pencil, Plus, ShieldCheck, Trash2 } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { Modal } from "@/components/common/Modal";
import type { Role, ScreenGroup } from "@/lib/types";

type Draft = {
  code: string | null;
  name: string;
  description: string;
  screens: Set<string>;
};

export function RolesView({
  initialRoles,
  catalog,
}: {
  initialRoles: Role[];
  catalog: ScreenGroup[];
}) {
  const router = useRouter();

  const [roles, setRoles] = useState(initialRoles);
  const [draft, setDraft] = useState<Draft | null>(null);

  /** Page level error, outside any modal. */
  const [error, setError] = useState("");

  /** Modal errors show INSIDE the modal, never behind it. */
  const [formError, setFormError] = useState("");
  const [deleteError, setDeleteError] = useState("");

  const [saving, setSaving] = useState(false);
  const [toDelete, setToDelete] = useState<Role | null>(null);
  const [deleting, setDeleting] = useState(false);

  const totalScreens = useMemo(
    () => catalog.reduce((total, group) => total + group.screens.length, 0),
    [catalog],
  );

  function openNew() {
    setFormError("");
    setError("");
    setDraft({ code: null, name: "", description: "", screens: new Set() });
  }

  function openEdit(role: Role) {
    setFormError("");
    setError("");
    setDraft({
      code: role.code,
      name: role.name,
      description: role.description ?? "",
      screens: new Set(role.screens),
    });
  }

  function toggleScreen(code: string) {
    setDraft((current) => {
      if (!current) return current;

      const screens = new Set(current.screens);
      screens.has(code) ? screens.delete(code) : screens.add(code);

      return { ...current, screens };
    });
  }

  function toggleGroup(group: ScreenGroup, check: boolean) {
    setDraft((current) => {
      if (!current) return current;

      const screens = new Set(current.screens);

      for (const screen of group.screens) {
        check ? screens.add(screen.code) : screens.delete(screen.code);
      }

      return { ...current, screens };
    });
  }

  async function save() {
    if (!draft) return;

    if (!draft.name.trim()) {
      setFormError("Informe o nome do perfil.");
      return;
    }

    setSaving(true);
    setFormError("");

    const isNew = draft.code === null;

    const response = await fetch(
      isNew ? "/api/backend/roles" : `/api/backend/roles/${draft.code}`,
      {
        method: isNew ? "POST" : "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: draft.name,
          description: draft.description || null,
          screens: [...draft.screens],
        }),
      },
    );

    setSaving(false);

    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      setFormError(problem?.detail ?? "Falha ao salvar o perfil.");
      return;
    }

    const { data } = await response.json();

    setRoles((current) =>
      isNew
        ? [...current, data].sort((a, b) => a.name.localeCompare(b.name))
        : current.map((r) => (r.code === data.code ? data : r)),
    );

    setDraft(null);

    // The screens of the signed in user may have changed: the menu has to be rebuilt.
    router.refresh();
  }

  async function remove(role: Role) {
    setDeleting(true);
    setDeleteError("");

    const response = await fetch(`/api/backend/roles/${role.code}`, { method: "DELETE" });

    setDeleting(false);

    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      setDeleteError(problem?.detail ?? "Falha ao excluir o perfil.");
      return;
    }

    setToDelete(null);
    setRoles((current) => current.filter((r) => r.code !== role.code));
    router.refresh();
  }

  return (
    <div className="dash-anim">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
            Acesso
          </p>
          <h1 className="hero-title text-3xl font-bold">Perfis de acesso</h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Marque as telas de cada perfil. Quem tem o perfil passa a ver essas telas no
            menu no próximo carregamento.
          </p>
        </div>

        <button
          type="button"
          onClick={openNew}
          className="inline-flex items-center gap-2 rounded-md bg-[var(--primary)] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]"
        >
          <Plus size={17} />
          Novo perfil
        </button>
      </div>

      {error && (
        <p className="mb-4 rounded-md border border-[color-mix(in_srgb,var(--critical)_40%,transparent)] bg-[color-mix(in_srgb,var(--critical)_8%,transparent)] px-4 py-3 text-sm text-[var(--critical)]">
          {error}
        </p>
      )}

      <div className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow)]">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-[var(--border)] bg-[var(--surface-2)]">
            <tr>
              <th className="px-5 py-3 font-semibold">Perfil</th>
              <th className="hidden px-5 py-3 font-semibold sm:table-cell">Descrição</th>
              <th className="px-5 py-3 font-semibold">Telas</th>
              <th className="px-5 py-3" />
            </tr>
          </thead>
          <tbody>
            {roles.map((role) => (
              <tr key={role.code} className="border-b border-[var(--border)] last:border-0">
                <td className="px-5 py-3.5">
                  <span className="flex items-center gap-2 font-medium">
                    <ShieldCheck size={16} className="text-[var(--signal)]" />
                    {role.name}
                    {role.isSystem && (
                      <span
                        title="Perfil de sistema: permanente"
                        className="inline-flex items-center gap-1 rounded-full border border-[var(--border)] px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-[var(--text-muted)]"
                      >
                        <Lock size={10} />
                        Sistema
                      </span>
                    )}
                  </span>
                </td>
                <td className="hidden px-5 py-3.5 text-[var(--text-secondary)] sm:table-cell">
                  {role.description ?? "—"}
                </td>
                <td className="px-5 py-3.5">
                  <span className="num text-[var(--text-secondary)]">
                    {role.screenCount} de {totalScreens}
                  </span>
                </td>
                <td className="px-5 py-3.5">
                  <div className="flex justify-end gap-1">
                    <button
                      type="button"
                      onClick={() => openEdit(role)}
                      aria-label={`Editar ${role.name}`}
                      className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--primary)]"
                    >
                      <Pencil size={15} />
                    </button>
                    <button
                      type="button"
                      onClick={() => setToDelete(role)}
                      disabled={role.isSystem}
                      aria-label={`Excluir ${role.name}`}
                      title={role.isSystem ? "Perfil de sistema e permanente" : undefined}
                      className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--critical)] disabled:cursor-not-allowed disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-[var(--text-secondary)]"
                    >
                      <Trash2 size={15} />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {draft && (
        <Modal
          title={draft.code ? "Editar perfil" : "Novo perfil"}
          onClose={() => setDraft(null)}
          error={formError}
          footer={
            <>
              <button
                type="button"
                onClick={() => setDraft(null)}
                className="rounded-md border border-[var(--border)] px-4 py-2 text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--surface-2)]"
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={save}
                disabled={saving || !draft.name.trim()}
                className="rounded-md bg-[var(--primary)] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)] disabled:opacity-50"
              >
                {saving ? "Salvando..." : "Salvar perfil"}
              </button>
            </>
          }
        >
          <div className="space-y-5">
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="block">
                <span className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
                  Nome
                </span>
                <input
                  value={draft.name}
                  onChange={(e) => setDraft({ ...draft, name: e.target.value })}
                  className="w-full rounded-md border border-[var(--border)] bg-[var(--canvas)] px-3 py-2 text-sm"
                />
              </label>
              <label className="block">
                <span className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
                  Descrição
                </span>
                <input
                  value={draft.description}
                  onChange={(e) => setDraft({ ...draft, description: e.target.value })}
                  className="w-full rounded-md border border-[var(--border)] bg-[var(--canvas)] px-3 py-2 text-sm"
                />
              </label>
            </div>

            <div>
              <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
                Telas do perfil
              </p>
              <p className="mb-3 text-sm text-[var(--text-secondary)]">
                Cada tela marcada e uma permissão. Telas sem item de menu também podem ser
                liberadas.
              </p>

              <div className="space-y-3">
                {catalog.map((group) => {
                  const checked = group.screens.filter((s) => draft.screens.has(s.code)).length;
                  const all = checked === group.screens.length;

                  return (
                    <section
                      key={group.group}
                      className="rounded-lg border border-[var(--border)] p-4"
                    >
                      <div className="mb-3 flex items-baseline justify-between gap-3">
                        <h3 className="font-display text-[11px] font-bold uppercase tracking-[.16em] text-[var(--signal)]">
                          {group.group}
                        </h3>
                        <button
                          type="button"
                          onClick={() => toggleGroup(group, !all)}
                          className="text-[11px] font-semibold text-[var(--text-muted)] underline-offset-2 hover:text-[var(--primary)] hover:underline"
                        >
                          {all ? "desmarcar tudo" : "marcar tudo"}
                        </button>
                      </div>

                      <div className="grid gap-2 sm:grid-cols-2">
                        {group.screens.map((screen) => {
                          const selected = draft.screens.has(screen.code);

                          return (
                            <label
                              key={screen.code}
                              className={[
                                "flex cursor-pointer items-center gap-3 rounded-md border px-3 py-2.5 text-sm transition",
                                selected
                                  ? "border-[var(--primary)] bg-[color-mix(in_srgb,var(--primary)_8%,transparent)]"
                                  : "border-[var(--border)] hover:bg-[var(--surface-2)]",
                              ].join(" ")}
                            >
                              <input
                                type="checkbox"
                                checked={selected}
                                onChange={() => toggleScreen(screen.code)}
                                className="sr-only"
                              />
                              <span
                                aria-hidden
                                className={[
                                  "grid h-[18px] w-[18px] shrink-0 place-items-center rounded border transition",
                                  selected
                                    ? "border-[var(--primary)] bg-[var(--primary)] text-white"
                                    : "border-[var(--border)]",
                                ].join(" ")}
                              >
                                {selected && <Check size={12} strokeWidth={3} />}
                              </span>
                              <span className="min-w-0">
                                <span className="block truncate font-medium leading-tight">
                                  {screen.name}
                                </span>
                                <span className="num block truncate text-[11px] leading-tight text-[var(--text-muted)]">
                                  {screen.key}
                                  {!screen.showInMenu && " · fora do menu"}
                                </span>
                              </span>
                            </label>
                          );
                        })}
                      </div>
                    </section>
                  );
                })}
              </div>
            </div>
          </div>
        </Modal>
      )}

      {toDelete && (
        <Confirmation
          title="Excluir perfil"
          message={
            <>
              Deseja realmente excluir o perfil <strong>{toDelete.name}</strong>? Quem usa
              este perfil perde as telas que ele liberava.
            </>
          }
          confirmLabel="Excluir"
          danger
          busy={deleting}
          error={deleteError}
          onConfirm={() => remove(toDelete)}
          onCancel={() => {
            setDeleteError("");
            setToDelete(null);
          }}
        />
      )}
    </div>
  );
}
