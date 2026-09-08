"use client";

import { useState } from "react";
import { Building2, Check } from "lucide-react";
import { Field } from "@/components/common/Field";
import { TextArea } from "@/components/common/TextArea";
import { PageError } from "@/components/vehicles/VehicleUi";
import { apiSend } from "@/lib/api";
import { isValidCpfOrCnpj, maskCpfCnpj, maskPhone } from "@/lib/masks";
import type { Company } from "@/lib/types";

/**
 * Os dados da revenda: o que sai impresso em cima de cada documento gerado (M19).
 *
 * Um papel que sai da loja precisa dizer de quem é. A ficha para venda e a proposta para o
 * cliente trazem estes campos no cabeçalho, e é aqui que eles são escritos uma vez só.
 */
export function CompanyView({ initial }: { initial: Company }) {
  const [name, setName] = useState(initial.name);
  const [document, setDocument] = useState(initial.document ? maskCpfCnpj(initial.document) : "");
  const [phone, setPhone] = useState(initial.phone ? maskPhone(initial.phone) : "");
  const [email, setEmail] = useState(initial.email ?? "");
  const [address, setAddress] = useState(initial.address ?? "");
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const [savedAt, setSavedAt] = useState<Date | null>(null);

  async function save() {
    if (!name.trim()) {
      setError("Informe o nome da revenda.");
      return;
    }

    const digits = document.replace(/\D/g, "");

    if (digits && !isValidCpfOrCnpj(digits)) {
      setError("Informe um CPF ou CNPJ válido.");
      return;
    }

    setSaving(true);
    setError("");

    const result = await apiSend("PUT", "company", "Falha ao salvar os dados da revenda.", {
      name: name.trim(),
      document: digits || null,
      phone: phone.replace(/\D/g, "") || null,
      email: email.trim() || null,
      address: address.trim() || null,
    });

    setSaving(false);

    if (!result.ok) {
      setError(result.error);
      return;
    }

    setSavedAt(new Date());
  }

  return (
    <div className="dash-anim">
      <div className="mb-6">
        <p className="font-display mb-1 text-xs font-bold uppercase tracking-[.18em] text-[var(--signal)]">
          Administração
        </p>
        <h1 className="hero-title text-3xl font-bold">Dados da revenda</h1>
        <p className="mt-1 max-w-2xl text-sm text-[var(--text-secondary)]">
          O que sai impresso em cima de cada documento: a ficha para venda e a proposta para o
          cliente. Um papel que sai da loja precisa dizer de quem é.
        </p>
      </div>

      <PageError message={error} />

      <section className="max-w-2xl rounded-xl border border-[var(--border)] bg-[var(--surface)] p-6 shadow-[var(--shadow)]">
        <p className="mb-5 flex items-center gap-2 font-semibold">
          <Building2 size={17} className="text-[var(--signal)]" />
          Como a revenda aparece no papel
        </p>

        <div className="space-y-4">
          <Field label="Nome" required value={name} onChange={setName} maxLength={160} />

          <div className="grid gap-4 sm:grid-cols-2">
            <Field
              label="CNPJ ou CPF"
              value={document}
              onChange={(value) => setDocument(maskCpfCnpj(value))}
              inputMode="numeric"
              hint="Opcional. Aparece abaixo do nome."
            />
            <Field
              label="Telefone"
              value={phone}
              onChange={(value) => setPhone(maskPhone(value))}
              inputMode="numeric"
              hint="O número que o cliente liga depois de ler a proposta."
            />
          </div>

          <Field label="E-mail" type="email" value={email} onChange={setEmail} maxLength={160} />

          <TextArea
            label="Endereço"
            rows={2}
            value={address}
            onChange={setAddress}
            placeholder="Rua, número, bairro, cidade e estado, em uma linha."
          />
        </div>

        <div className="mt-6 flex items-center justify-between gap-3">
          <p className="text-xs text-[var(--text-muted)]">
            {savedAt && (
              <span className="inline-flex items-center gap-1 text-[var(--success)]">
                <Check size={14} />
                Salvo às {savedAt.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })}
              </span>
            )}
          </p>
          <button
            type="button"
            onClick={save}
            disabled={saving}
            className="rounded-md bg-[var(--primary)] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)] disabled:opacity-50"
          >
            {saving ? "Salvando..." : "Salvar"}
          </button>
        </div>
      </section>
    </div>
  );
}
