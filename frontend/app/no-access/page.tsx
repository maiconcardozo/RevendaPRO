import { LockKeyhole } from "lucide-react";
import { SignOutLink } from "@/components/common/SignOutLink";
import { requireSession } from "@/lib/session";

/** Valid role, but with no screens granted. See ADR-0002, decision 7. */
export default async function NoAccessPage() {
  const session = await requireSession();

  return (
    <main className="instrument-grid grid min-h-screen place-items-center bg-[var(--canvas)] p-6">
      <div className="w-full max-w-md rounded-xl border border-[var(--border)] bg-[var(--surface)] p-8 text-center shadow-[var(--shadow)]">
        <div className="mx-auto mb-5 grid h-12 w-12 place-items-center rounded-full bg-[color-mix(in_srgb,var(--warning)_14%,transparent)] text-[var(--warning)]">
          <LockKeyhole size={22} />
        </div>
        <h1 className="hero-title mb-2 text-2xl font-bold">Nenhuma tela liberada</h1>
        <p className="text-sm leading-relaxed text-[var(--text-secondary)]">
          Voce entrou como <strong>{session.user.name}</strong>, mas o perfil{" "}
          <strong>{session.roles[0] ?? "sem perfil"}</strong> ainda nao tem nenhuma tela
          liberada. Fale com o administrador da revenda.
        </p>
        <SignOutLink className="mt-6" />
      </div>
    </main>
  );
}
