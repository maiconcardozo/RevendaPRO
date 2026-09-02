import { requireScreen } from "@/lib/session";

/**
 * Example of a screen WITH a permission and WITHOUT a menu item (ShowInMenu = false in the
 * catalog). It does not appear in the sidebar, but it is still enforced by the API.
 */
export default async function MyAccountPage() {
  const session = await requireScreen("my-account");

  return (
    <div className="dash-anim">
      <h1 className="hero-title text-3xl font-bold">Meus dados</h1>
      <p className="mt-1 text-sm text-[var(--text-secondary)]">
        Esta tela fica fora do menu, e exige permissão do mesmo jeito.
      </p>

      <dl className="mt-6 max-w-md space-y-3 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-6 shadow-[var(--shadow)]">
        <div>
          <dt className="text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
            Nome
          </dt>
          <dd className="text-sm font-medium">{session.user.name}</dd>
        </div>
        <div>
          <dt className="text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
            E-mail
          </dt>
          <dd className="text-sm font-medium">{session.user.email}</dd>
        </div>
        <div>
          <dt className="text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
            Perfil
          </dt>
          <dd className="text-sm font-medium">{session.roles.join(", ") || "Sem perfil"}</dd>
        </div>
      </dl>
    </div>
  );
}
