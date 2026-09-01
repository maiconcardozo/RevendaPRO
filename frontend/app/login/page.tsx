"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState, type FormEvent } from "react";
import { ArrowRight, LockKeyhole } from "lucide-react";

function SignInForm() {
  const router = useRouter();
  const params = useSearchParams();

  const [email, setEmail] = useState("");
  const [password, setSenha] = useState("");
  const [error, setError] = useState("");
  const [signingIn, setSigningIn] = useState(false);

  async function signIn(event: FormEvent) {
    event.preventDefault();
    setSigningIn(true);
    setError("");

    const response = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
    });

    if (!response.ok) {
      const body = await response.json().catch(() => null);
      setError(body?.message ?? "E-mail ou senha inválidos.");
      setSigningIn(false);
      return;
    }

    router.replace(params.get("from") ?? "/");
    router.refresh();
  }

  return (
    <form onSubmit={signIn} className="w-full max-w-sm">
      <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
        Revenda Pro
      </p>
      <h1 className="hero-title mb-1 text-3xl font-bold">Entrar no painel</h1>
      <p className="mb-7 text-sm text-[var(--text-secondary)]">
        Use as credenciais da sua revenda.
      </p>

      {error && (
        <p
          role="alert"
          className="mb-4 rounded-md border border-[color-mix(in_srgb,var(--critical)_40%,transparent)] bg-[color-mix(in_srgb,var(--critical)_8%,transparent)] px-4 py-3 text-sm text-[var(--critical)]"
        >
          {error}
        </p>
      )}

      <label className="mb-4 block">
        <span className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
          E-mail
        </span>
        <input
          type="email"
          required
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          className="w-full rounded-md border border-[var(--border)] bg-[var(--surface)] px-3 py-2.5 text-sm"
        />
      </label>

      <label className="mb-6 block">
        <span className="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-[var(--text-muted)]">
          Senha
        </span>
        <input
          type="password"
          required
          autoComplete="current-password"
          value={password}
          onChange={(e) => setSenha(e.target.value)}
          className="w-full rounded-md border border-[var(--border)] bg-[var(--surface)] px-3 py-2.5 text-sm"
        />
      </label>

      <button
        type="submit"
        disabled={signingIn}
        className="flex w-full items-center justify-center gap-2 rounded-md bg-[var(--primary)] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)] disabled:opacity-60"
      >
        {signingIn ? "Entrando..." : "Entrar"}
        {!signingIn && <ArrowRight size={16} />}
      </button>
    </form>
  );
}

export default function Login() {
  return (
    <main className="grid min-h-screen bg-[var(--canvas)] lg:grid-cols-2">
      <section
        className="instrument-grid hidden flex-col justify-between p-14 text-white lg:flex"
        style={{ backgroundColor: "var(--sidebar-bg)" }}
      >
        <div className="flex items-center gap-3">
          <div className="grid h-10 w-10 place-items-center rounded-lg bg-[var(--primary)] text-xs font-black">
            RP
          </div>
          <div>
            <p className="font-display text-xl font-bold">Revenda Pro</p>
            <p className="text-[10px] font-semibold uppercase tracking-[.2em] text-white/60">
              Painel de gestão
            </p>
          </div>
        </div>

        <div>
          <p className="hero-title max-w-lg text-4xl font-bold leading-[1.1]">
            Cada carro.
            <br />
            Cada custo.
            <br />
            <span className="text-[var(--primary)]">O lucro real.</span>
          </p>

          <p className="mt-5 max-w-md text-sm leading-relaxed text-white/60">
            Da compra ao emplacamento, da funilaria a venda: o Revenda Pro guarda cada
            gasto de cada veículo e mostra quanto sobrou de verdade.
          </p>
        </div>

        <div className="flex items-start gap-2.5 text-xs leading-relaxed text-white/45">
          <LockKeyhole size={14} className="mt-0.5 shrink-0" />
          <p className="max-w-sm">
            Cada pessoa da revenda enxerga apenas as telas do seu perfil.
          </p>
        </div>
      </section>

      <section className="grid place-items-center p-8">
        <Suspense>
          <SignInForm />
        </Suspense>
      </section>
    </main>
  );
}
