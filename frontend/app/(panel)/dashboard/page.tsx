import Link from "next/link";
import { ArrowRight, KeyRound, LayoutGrid, Users } from "lucide-react";
import { requireScreen } from "@/lib/session";

export default async function DashboardPage() {
  const session = await requireScreen("dashboard");

  const cards = [
    { label: "Perfil de acesso", value: session.roles[0] ?? "Sem perfil", icon: KeyRound },
    { label: "Telas liberadas", value: String(session.screens.length), icon: LayoutGrid },
    {
      label: "Itens no menu",
      value: String(session.menu.reduce((total, g) => total + g.items.length, 0)),
      icon: Users,
    },
  ];

  return (
    <div className="dash-anim">
      <div className="mb-6">
        <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
          Visão geral
        </p>
        <h1 className="hero-title text-3xl font-bold sm:text-4xl">
          Olá, {session.user.name.split(" ")[0]}.
        </h1>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          O menu ao lado foi montado a partir das telas do seu perfil.
        </p>
      </div>

      <div className="mb-6 grid gap-4 sm:grid-cols-3">
        {cards.map(({ label, value, icon: Icon }) => (
          <div
            key={label}
            className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-5 shadow-[var(--shadow)]"
          >
            <div className="mb-3 flex items-center justify-between">
              <p className="font-display text-[10px] font-bold uppercase tracking-[.18em] text-[var(--text-muted)]">
                {label}
              </p>
              <Icon size={17} className="text-[var(--signal)]" />
            </div>
            <p className="num text-2xl font-bold">{value}</p>
          </div>
        ))}
      </div>

      <section className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-6 shadow-[var(--shadow)]">
        <p className="font-display text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
          Suas telas
        </p>
        <h2 className="mt-2 text-xl font-bold">Estas são as chaves liberadas para você</h2>
        <p className="mt-2 max-w-2xl text-sm text-[var(--text-secondary)]">
          Cada chave é, ao mesmo tempo, uma permissão e um item de menu. Quem define quais
          você enxerga é o seu perfil de acesso.
        </p>

        <div className="mt-4 flex flex-wrap gap-2">
          {session.screens.map((key) => (
            <span
              key={key}
              className="num rounded-md border border-[var(--border)] bg-[var(--surface-2)] px-2.5 py-1 text-xs font-medium"
            >
              {key}
            </span>
          ))}
        </div>

        {session.screens.includes("roles") && (
          <Link
            href="/roles"
            className="mt-5 inline-flex items-center gap-2 text-sm font-semibold text-[var(--primary)] hover:underline"
          >
            Ajustar telas por perfil <ArrowRight size={15} />
          </Link>
        )}
      </section>
    </div>
  );
}
