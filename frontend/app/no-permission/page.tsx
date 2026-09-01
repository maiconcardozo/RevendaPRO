import Link from "next/link";
import { ShieldAlert } from "lucide-react";
import { firstAllowedRoute, requireSession } from "@/lib/session";

/**
 * Route typed in the address bar without holding the screen.
 * The API blocks it on its own too: hiding a menu item is presentation, the security is
 * the guard on every endpoint.
 */
export default async function NoPermissionPage({
  searchParams,
}: {
  searchParams: Promise<{ screen?: string }>;
}) {
  const session = await requireSession();
  const { screen } = await searchParams;

  return (
    <main className="instrument-grid grid min-h-screen place-items-center bg-[var(--canvas)] p-6">
      <div className="w-full max-w-md rounded-xl border border-[var(--border)] bg-[var(--surface)] p-8 text-center shadow-[var(--shadow)]">
        <div className="mx-auto mb-5 grid h-12 w-12 place-items-center rounded-full bg-[color-mix(in_srgb,var(--critical)_14%,transparent)] text-[var(--critical)]">
          <ShieldAlert size={22} />
        </div>
        <h1 className="hero-title mb-2 text-2xl font-bold">Sem permissao</h1>
        <p className="text-sm leading-relaxed text-[var(--text-secondary)]">
          O perfil <strong>{session.roles[0] ?? "sem perfil"}</strong> nao tem acesso
          {screen ? (
            <>
              {" "}
              a tela <strong>{screen}</strong>
            </>
          ) : (
            " a esta tela"
          )}
          . Peca a liberacao ao administrador da revenda.
        </p>
        <Link
          href={firstAllowedRoute(session)}
          className="mt-6 inline-flex items-center rounded-md bg-[var(--primary)] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]"
        >
          Voltar
        </Link>
      </div>
    </main>
  );
}
