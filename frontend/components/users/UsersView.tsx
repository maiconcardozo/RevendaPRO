"use client";

import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { Camera, Pencil, Plus, RotateCcw, Search, Trash2 } from "lucide-react";
import { Avatar } from "@/components/common/Avatar";
import { Confirmation } from "@/components/common/Confirmation";
import { Field } from "@/components/common/Field";
import { Modal } from "@/components/common/Modal";
import {
  digitsOnly,
  isValidCpfOrCnpj,
  isValidEmail,
  isValidPhone,
  maskCpfCnpj,
  maskPhone,
  normalizeEmail,
} from "@/lib/masks";
import type { Role, User } from "@/lib/types";

type Draft = {
  code: string | null;
  name: string;
  email: string;
  document: string;
  phone: string;
  password: string;
  isBlocked: boolean;
  role: string;
  hasPhoto: boolean;
  /** Photo chosen and not yet uploaded. It only goes up after the user is saved. */
  newPhoto: File | null;
  removePhoto: boolean;
};

type Errors = Partial<Record<"name" | "email" | "document" | "phone" | "password", string>>;

/**
 * The three states a row can be in, kept apart on purpose.
 *
 * "Inativo" is a person who stays in the list and can be let back in. "Excluído" is a row
 * taken out of every other reading, which only this screen brings back, and only when asked.
 */
function statusOf(user: User) {
  if (!user.isActive) {
    return {
      label: "Excluído",
      className:
        "bg-[color-mix(in_srgb,var(--critical)_12%,transparent)] text-[var(--critical)]",
    };
  }

  if (user.isBlocked) {
    return { label: "Inativo", className: "bg-[var(--surface-2)] text-[var(--text-muted)]" };
  }

  return {
    label: "Ativo",
    className: "bg-[color-mix(in_srgb,var(--success)_12%,transparent)] text-[var(--success)]",
  };
}
export function UsersView({
  initialUsers,
  roles,
  currentUserCode,
}: {
  initialUsers: User[];
  roles: Role[];
  currentUserCode: string;
}) {
  const router = useRouter();

  const [users, setUsers] = useState(initialUsers);
  const [search, setSearch] = useState("");

  /** Deleted rows stay out until somebody asks for them. */
  const [showDeleted, setShowDeleted] = useState(false);
  const [loadingList, setLoadingList] = useState(false);
  const [restoring, setRestoring] = useState<string | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [errors, setErrors] = useState<Errors>({});

  /** Page level error, outside any modal. */
  const [error, setError] = useState("");

  /** Modal errors show INSIDE the modal: a warning behind it goes unread. */
  const [formError, setFormError] = useState("");
  const [deleteError, setDeleteError] = useState("");

  const [saving, setSaving] = useState(false);
  const [toDelete, setToDelete] = useState<User | null>(null);
  const [deleting, setDeleting] = useState(false);

  const photoPreview = useMemo(
    () => (draft?.newPhoto ? URL.createObjectURL(draft.newPhoto) : null),
    [draft?.newPhoto],
  );

  // createObjectURL holds the file in memory until the URL is revoked.
  useEffect(() => {
    return () => {
      if (photoPreview) URL.revokeObjectURL(photoPreview);
    };
  }, [photoPreview]);


  /**
   * Reloads the listing whenever the deleted filter changes. Deleted rows come from the API,
   * and not from a client side filter, because the default listing never carries them.
   */
  useEffect(() => {
    let cancelled = false;

    async function reload() {
      setLoadingList(true);

      const response = await fetch(
        `/api/backend/users${showDeleted ? "?includeDeleted=true" : ""}`,
      );

      if (!cancelled) {
        if (response.ok) {
          setUsers((await response.json()).data);
        } else {
          setError("Falha ao carregar a lista de usuários.");
        }

        setLoadingList(false);
      }
    }

    reload();

    return () => {
      cancelled = true;
    };
  }, [showDeleted]);
  const filtered = users.filter((u) => {
    const term = search.trim().toLowerCase();

    if (!term) return true;

    return (
      u.name.toLowerCase().includes(term) ||
      u.email.toLowerCase().includes(term) ||
      u.roleNames.some((r) => r.toLowerCase().includes(term))
    );
  });


  /**
   * Brings a deleted person back. They return blocked, so whoever restores decides
   * afterwards whether that person may sign in again.
   */
  async function restore(user: User) {
    setError("");
    setRestoring(user.code);

    const response = await fetch(`/api/backend/users/${user.code}/restore`, {
      method: "POST",
    });

    setRestoring(null);

    if (!response.ok) {
      setError("Falha ao restaurar o usuário.");
      return;
    }

    setUsers((current) =>
      current.map((u) =>
        u.code === user.code ? { ...u, isActive: true, isBlocked: true } : u,
      ),
    );

    router.refresh();
  }
  function openForm(newDraft: Draft) {
    setErrors({});
    setFormError("");
    setError("");
    setDraft(newDraft);
  }

  function openNew() {
    openForm({
      code: null,
      name: "",
      email: "",
      document: "",
      phone: "",
      password: "",
      isBlocked: false,
      role: roles[0]?.code ?? "",
      hasPhoto: false,
      newPhoto: null,
      removePhoto: false,
    });
  }

  function openEdit(user: User) {
    openForm({
      code: user.code,
      name: user.name,
      email: user.email,
      document: user.document ? maskCpfCnpj(user.document) : "",
      phone: user.phone ? maskPhone(user.phone) : "",
      password: "",
      isBlocked: user.isBlocked,
      role: user.roles[0] ?? roles[0]?.code ?? "",
      hasPhoto: user.hasPhoto,
      newPhoto: null,
      removePhoto: false,
    });
  }

  /** Touching a field clears its error: the message goes away when the problem does. */
  function update(change: Partial<Draft>) {
    setDraft((current) => (current ? { ...current, ...change } : current));

    setErrors((current) => {
      const next = { ...current };

      for (const field of Object.keys(change)) {
        delete next[field as keyof Errors];
      }

      return next;
    });
  }

  function choosePhoto(file: File | null) {
    if (!file) return;

    if (file.size > 2 * 1024 * 1024) {
      setFormError("A imagem passa de 2 MB. Escolha um arquivo menor.");
      return;
    }

    setFormError("");
    update({ newPhoto: file, removePhoto: false });
  }

  /**
   * Validates before calling the API. A per field error says where to fix it; a general
   * warning only says something is wrong. The backend validates again, on its own.
   */
  function validate(d: Draft): Errors {
    const found: Errors = {};

    if (!d.name.trim()) {
      found.name = "Informe o nome.";
    }

    if (!isValidEmail(d.email)) {
      found.email = "E-mail inválido.";
    }

    if (!digitsOnly(d.document)) {
      found.document = "Informe o CPF ou CNPJ.";
    } else if (!isValidCpfOrCnpj(d.document)) {
      found.document = "CPF ou CNPJ inválido.";
    }

    if (!isValidPhone(d.phone)) {
      found.phone = "Telefone inválido. Informe DDD e número.";
    }

    const passwordRequired = d.code === null;

    if ((passwordRequired || d.password.length > 0) && d.password.length < 8) {
      found.password = "A senha precisa ter pelo menos 8 caracteres.";
    }

    return found;
  }

  async function save() {
    if (!draft) return;

    const found = validate(draft);

    if (Object.keys(found).length > 0) {
      setErrors(found);
      setFormError("Revise os campos destacados.");
      return;
    }

    setSaving(true);
    setFormError("");

    const isNew = draft.code === null;

    const response = await fetch(
      isNew ? "/api/backend/users" : `/api/backend/users/${draft.code}`,
      {
        method: isNew ? "POST" : "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: draft.name,
          email: draft.email,
          password: draft.password || null,
          isBlocked: draft.isBlocked,
          roles: draft.role ? [draft.role] : [],
          // The database stores raw digits; the mask lives only on the screen.
          document: digitsOnly(draft.document) || null,
          phone: digitsOnly(draft.phone) || null,
        }),
      },
    );

    if (!response.ok) {
      setSaving(false);

      const problem = await response.json().catch(() => null);
      const perField = problem?.errors
        ? Object.values(problem.errors as Record<string, string[]>).flat().join(" ")
        : null;

      setFormError(perField ?? problem?.detail ?? "Falha ao salvar o usuário.");
      return;
    }

    let { data } = await response.json();

    // The photo only goes up after the user exists: its code is needed in the route.
    const photo = await syncPhoto(data.code);

    setSaving(false);

    if (photo.error) {
      setFormError(photo.error);
    }

    data = { ...data, hasPhoto: photo.hasPhoto };

    setUsers((current) =>
      isNew
        ? [...current, data].sort((a, b) => a.name.localeCompare(b.name))
        : current.map((u) => (u.code === data.code ? data : u)),
    );

    if (!photo.error) {
      setDraft(null);
    }

    router.refresh();
  }

  /**
   * Uploads or removes the photo. A failure here does not undo the user that was already
   * saved: the record stands, and the message says what happened to the image.
   */
  async function syncPhoto(code: string) {
    if (!draft) {
      return { hasPhoto: false, error: null as string | null };
    }

    if (draft.newPhoto) {
      const body = new FormData();
      body.append("file", draft.newPhoto);

      const upload = await fetch(`/api/backend/users/${code}/photo`, {
        method: "POST",
        body,
      });

      if (!upload.ok) {
        const problem = await upload.json().catch(() => null);

        return {
          hasPhoto: draft.hasPhoto,
          error:
            problem?.detail ??
            "Usuário salvo. A foto falhou no envio; tente outra imagem.",
        };
      }

      return { hasPhoto: true, error: null };
    }

    if (draft.removePhoto && draft.hasPhoto) {
      await fetch(`/api/backend/users/${code}/photo`, { method: "DELETE" });
      return { hasPhoto: false, error: null };
    }

    return { hasPhoto: draft.hasPhoto, error: null };
  }

  async function remove(user: User) {
    setDeleting(true);
    setDeleteError("");

    const response = await fetch(`/api/backend/users/${user.code}`, { method: "DELETE" });

    setDeleting(false);

    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      setDeleteError(problem?.detail ?? "Falha ao excluir o usuário.");
      return;
    }

    setToDelete(null);
    setUsers((current) => current.filter((u) => u.code !== user.code));
  }

  return (
    <div className="dash-anim">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
            Acesso
          </p>
          <h1 className="hero-title text-3xl font-bold">Usuários</h1>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            O perfil de cada pessoa define as telas que ela enxerga.
          </p>
        </div>

        <button
          type="button"
          onClick={openNew}
          className="inline-flex items-center gap-2 rounded-md bg-[var(--primary)] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]"
        >
          <Plus size={17} />
          Novo usuário
        </button>
      </div>

      {error && (
        <p className="mb-4 rounded-md border border-[color-mix(in_srgb,var(--critical)_40%,transparent)] bg-[color-mix(in_srgb,var(--critical)_8%,transparent)] px-4 py-3 text-sm text-[var(--critical)]">
          {error}
        </p>
      )}

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div className="relative w-full max-w-sm">
          <Search
            size={16}
            className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[var(--text-muted)]"
          />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Buscar por nome, e-mail ou perfil"
            aria-label="Buscar usuários"
            className="w-full rounded-md border border-[var(--border)] bg-[var(--surface)] py-2.5 pl-9 pr-3 text-sm"
          />
        </div>

        <label className="flex items-center gap-2.5 text-sm text-[var(--text-secondary)]">
          <input
            type="checkbox"
            checked={showDeleted}
            onChange={(e) => setShowDeleted(e.target.checked)}
          />
          Mostrar excluídos
        </label>

        {loadingList && (
          <span className="text-xs text-[var(--text-muted)]">Carregando…</span>
        )}
      </div>

      <div className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow)]">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-[var(--border)] bg-[var(--surface-2)]">
            <tr>
              <th className="px-5 py-3 font-semibold">Nome</th>
              <th className="hidden px-5 py-3 font-semibold sm:table-cell">E-mail</th>
              <th className="px-5 py-3 font-semibold">Perfil</th>
              <th className="px-5 py-3 font-semibold">Situação</th>
              <th className="px-5 py-3" />
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 && (
              <tr>
                <td colSpan={5} className="px-5 py-12 text-center">
                  <p className="text-sm font-medium">Nenhum usuário encontrado</p>
                  <p className="mt-1 text-sm text-[var(--text-secondary)]">
                    {search ? "Ajuste a busca." : "Cadastre a primeira pessoa da equipe."}
                  </p>
                </td>
              </tr>
            )}

            {filtered.map((user) => {
              const isMe = user.code === currentUserCode;

              return (
                <tr key={user.code} className="border-b border-[var(--border)] last:border-0">
                  <td className="px-5 py-3.5 font-medium">
                    <span className="flex items-center gap-3">
                      <Avatar
                        name={user.name}
                        code={user.code}
                        hasPhoto={user.hasPhoto}
                        size={32}
                      />
                      <span className="min-w-0 truncate">
                        {user.name}
                        {isMe && (
                          <span className="ml-2 text-[11px] font-normal text-[var(--text-muted)]">
                            (você)
                          </span>
                        )}
                      </span>
                    </span>
                  </td>
                  <td className="hidden px-5 py-3.5 text-[var(--text-secondary)] sm:table-cell">
                    {user.email}
                  </td>
                  <td className="px-5 py-3.5 text-[var(--text-secondary)]">
                    {user.roleNames.join(", ") || "—"}
                  </td>
                  <td className="px-5 py-3.5">
                    <span
                      className={[
                        "inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-semibold",
                        statusOf(user).className,
                      ].join(" ")}
                    >
                      <span
                        aria-hidden
                        className="h-1.5 w-1.5 rounded-full"
                        style={{ background: "currentColor" }}
                      />
                      {statusOf(user).label}
                    </span>
                  </td>
                  <td className="px-5 py-3.5">
                    <div className="flex justify-end gap-1">
                      {user.isActive ? (
                        <>
                          <button
                            type="button"
                            onClick={() => openEdit(user)}
                            aria-label={`Editar ${user.name}`}
                            className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--primary)]"
                          >
                            <Pencil size={15} />
                          </button>
                          <button
                            type="button"
                            onClick={() => setToDelete(user)}
                            disabled={isMe}
                            aria-label={`Excluir ${user.name}`}
                            title={isMe ? "Outro administrador precisa excluir a sua conta" : undefined}
                            className="grid h-8 w-8 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] hover:text-[var(--critical)] disabled:cursor-not-allowed disabled:opacity-30 disabled:hover:bg-transparent disabled:hover:text-[var(--text-secondary)]"
                          >
                            <Trash2 size={15} />
                          </button>
                        </>
                      ) : (
                        <button
                          type="button"
                          onClick={() => restore(user)}
                          disabled={restoring === user.code}
                          className="inline-flex items-center gap-1.5 rounded-md border border-[var(--border)] px-2.5 py-1.5 text-xs font-semibold text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)] disabled:opacity-50"
                        >
                          <RotateCcw size={14} />
                          Restaurar
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {draft && (
        <Modal
          title={draft.code ? "Editar usuário" : "Novo usuário"}
          onClose={() => setDraft(null)}
          error={formError}
          width="max-w-lg"
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
                disabled={saving}
                className="rounded-md bg-[var(--primary)] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)] disabled:opacity-50"
              >
                {saving ? "Salvando..." : "Salvar usuário"}
              </button>
            </>
          }
        >
          <div className="space-y-4">
            <div className="flex items-center gap-4 rounded-lg border border-[var(--border)] p-4">
              {photoPreview ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={photoPreview}
                  alt=""
                  className="h-16 w-16 shrink-0 rounded-full object-cover"
                />
              ) : (
                <Avatar
                  name={draft.name || "?"}
                  code={draft.code ?? ""}
                  hasPhoto={draft.hasPhoto && !draft.removePhoto}
                  size={64}
                />
              )}

              <div className="min-w-0">
                <p className="mb-2 text-sm text-[var(--text-secondary)]">
                  Sem foto, aparece a inicial do nome.
                </p>

                <div className="flex flex-wrap items-center gap-2">
                  <label className="inline-flex cursor-pointer items-center gap-2 rounded-md border border-[var(--border)] px-3 py-1.5 text-sm font-medium text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)]">
                    <Camera size={15} />
                    {draft.hasPhoto || draft.newPhoto ? "Trocar foto" : "Escolher foto"}
                    <input
                      type="file"
                      accept="image/jpeg,image/png,image/webp"
                      className="sr-only"
                      onChange={(e) => choosePhoto(e.target.files?.[0] ?? null)}
                    />
                  </label>

                  {(draft.hasPhoto || draft.newPhoto) && !draft.removePhoto && (
                    <button
                      type="button"
                      onClick={() => setDraft({ ...draft, newPhoto: null, removePhoto: true })}
                      className="rounded-md px-2 py-1.5 text-sm font-medium text-[var(--text-muted)] hover:text-[var(--critical)]"
                    >
                      Remover
                    </button>
                  )}
                </div>

                <p className="mt-2 text-xs text-[var(--text-muted)]">
                  JPG, PNG ou WEBP, ate 2 MB.
                </p>
              </div>
            </div>

            <Field
              label="Nome"
              required
              value={draft.name}
              onChange={(v) => update({ name: v })}
              autoComplete="name"
              error={errors.name}
            />

            <Field
              label="E-mail"
              required
              type="email"
              inputMode="email"
              autoComplete="email"
              placeholder="nome@empresa.com.br"
              value={draft.email}
              onChange={(v) => update({ email: normalizeEmail(v) })}
              error={errors.email}
            />

            <div className="grid gap-4 sm:grid-cols-2">
              <Field
                label="CPF ou CNPJ"
                required
                inputMode="numeric"
                placeholder="000.000.000-00"
                mask={maskCpfCnpj}
                value={draft.document}
                onChange={(v) => update({ document: v })}
                error={errors.document}
              />

              <Field
                label="Telefone"
                type="tel"
                inputMode="tel"
                autoComplete="tel"
                placeholder="(00) 00000-0000"
                mask={maskPhone}
                value={draft.phone}
                onChange={(v) => update({ phone: v })}
                hint="Opcional"
                error={errors.phone}
              />
            </div>

            <Field
              label="Senha"
              type="password"
              autoComplete="new-password"
              value={draft.password}
              onChange={(v) => update({ password: v })}
              placeholder={
                draft.code ? "Deixe vazio para manter a atual" : "Minimo 8 caracteres"
              }
              error={errors.password}
            />

            <label className="block">
              <span className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
                Perfil de acesso
              </span>
              <select
                value={draft.role}
                onChange={(e) => update({ role: e.target.value })}
                className="w-full rounded-md border border-[var(--border)] bg-[var(--canvas)] px-3 py-2 text-sm"
              >
                {roles.map((role) => (
                  <option key={role.code} value={role.code}>
                    {role.name}
                  </option>
                ))}
              </select>
              <span className="mt-1.5 block text-xs text-[var(--text-secondary)]">
                Define as telas que esta pessoa vai ver no menu.
              </span>
            </label>

            <label className="flex items-center gap-2.5">
              <input
                type="checkbox"
                checked={!draft.isBlocked}
                onChange={(e) => update({ isBlocked: !e.target.checked })}
              />
              <span className="text-sm">Usuário ativo</span>
            </label>
          </div>
        </Modal>
      )}

      {toDelete && (
        <Confirmation
          title="Excluir usuário"
          message={
            <>
              Deseja realmente excluir <strong>{toDelete.name}</strong>? A pessoa perde o
              acesso imediatamente. O histórico dela continua guardado.
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
