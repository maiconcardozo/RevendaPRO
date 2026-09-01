"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { LogOut } from "lucide-react";
import { Confirmation } from "./Confirmation";

export function SignOutLink({ className = "" }: { className?: string }) {
  const router = useRouter();
  const [confirming, setConfirming] = useState(false);
  const [signingOut, setSigningOut] = useState(false);

  async function signOut() {
    setSigningOut(true);
    await fetch("/api/auth/logout", { method: "POST" });
    router.replace("/login");
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setConfirming(true)}
        className={`inline-flex items-center gap-2 rounded-md border border-[var(--border)] px-4 py-2 text-sm font-medium text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)] ${className}`}
      >
        <LogOut size={16} />
        Sair
      </button>

      {confirming && (
        <Confirmation
          title="Sair do sistema"
          message="Deseja realmente sair? Você vai precisar entrar de novo com e-mail e senha para voltar ao painel."
          confirmLabel="Sair"
          danger
          busy={signingOut}
          onConfirm={signOut}
          onCancel={() => setConfirming(false)}
        />
      )}
    </>
  );
}
