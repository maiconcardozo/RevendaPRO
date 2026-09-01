import { Construction } from "lucide-react";
import { requireScreen } from "@/lib/session";

export default async function Page() {
  await requireScreen("costs");

  return (
    <div className="dash-anim">
      <h1 className="hero-title text-3xl font-bold">Custos</h1>
      <p className="mt-1 text-sm text-[var(--text-secondary)]">
        A tela existe e ja e controlada por permissão. O módulo entra no marco M7.
      </p>

      <div className="mt-6 grid place-items-center rounded-xl border border-dashed border-[var(--border)] bg-[var(--surface)] p-12 text-center">
        <Construction size={26} className="mb-3 text-[var(--text-muted)]" />
        <p className="text-sm font-medium">Módulo em construção</p>
        <p className="mt-1 max-w-sm text-sm text-[var(--text-secondary)]">
          Você enxerga esta tela porque a chave <code className="num">costs</code> esta
          liberada no seu perfil.
        </p>
      </div>
    </div>
  );
}
