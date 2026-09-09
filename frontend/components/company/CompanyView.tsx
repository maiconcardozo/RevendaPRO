"use client";

import { useState } from "react";
import { Building2, Check, ImagePlus, Trash2 } from "lucide-react";
import { Confirmation } from "@/components/common/Confirmation";
import { LogoCropper } from "@/components/company/LogoCropper";
import { Field } from "@/components/common/Field";
import { TextArea } from "@/components/common/TextArea";
import { PageError } from "@/components/vehicles/VehicleUi";
import { apiSend, messageOf } from "@/lib/api";
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

  // O logotipo (M25): a versão muda a cada troca, e é ela que faz o navegador pedir a imagem
  // de novo em vez de mostrar a que guardou.
  const [logo, setLogo] = useState<{ has: boolean; version: string | null }>({
    has: initial.hasLogo,
    version: initial.logoVersion,
  });
  const [cropping, setCropping] = useState<File | null>(null);
  const [logoBusy, setLogoBusy] = useState(false);
  const [logoError, setLogoError] = useState("");
  const [removing, setRemoving] = useState(false);

  async function uploadLogo(blob: Blob) {
    setLogoBusy(true);
    setLogoError("");

    const body = new FormData();
    body.append("file", blob, "logotipo.png");

    const response = await fetch("/api/backend/company/logo", { method: "POST", body });

    setLogoBusy(false);

    if (!response.ok) {
      setLogoError(await messageOf(response, "Falha ao enviar o logotipo."));
      return;
    }

    const saved = (await response.json()).data as Company;

    setLogo({ has: saved.hasLogo, version: saved.logoVersion });
    setCropping(null);
  }

  async function removeLogo() {
    setLogoBusy(true);
    setLogoError("");

    const result = await apiSend<Company>("DELETE", "company/logo", "Falha ao remover o logotipo.");

    setLogoBusy(false);

    if (!result.ok) {
      setLogoError(result.error);
      return;
    }

    setLogo({ has: false, version: null });
    setRemoving(false);
  }

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

      {/* O logotipo (M25): o que vai no alto da ficha e da proposta, ao lado do nome. */}
      <section className="mb-6 max-w-2xl rounded-xl border border-[var(--border)] bg-[var(--surface)] p-6 shadow-[var(--shadow)]">
        <p className="mb-1 flex items-center gap-2 font-semibold">
          <ImagePlus size={17} className="text-[var(--signal)]" />
          Logotipo
        </p>
        <p className="mb-5 text-sm text-[var(--text-secondary)]">
          Vai no alto da ficha para venda e da proposta, ao lado do nome da revenda. Sem logotipo,
          o nome ocupa o lugar inteiro, como sempre ocupou.
        </p>

        <PageError message={logoError} />

        <div className="flex flex-wrap items-center gap-5">
          <div
            className="flex h-24 w-48 items-center justify-center overflow-hidden rounded-lg border border-[var(--border)] bg-[var(--surface-2)]"
            aria-label="Prévia do logotipo, na proporção do papel"
          >
            {logo.has ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={`/api/backend/company/logo?v=${logo.version ?? ""}`}
                alt="Logotipo da revenda"
                className="max-h-full max-w-full object-contain"
              />
            ) : (
              <span className="px-3 text-center text-xs text-[var(--text-muted)]">
                Logotipo nenhum ainda
              </span>
            )}
          </div>

          <div className="flex flex-wrap gap-2">
            <label className="inline-flex cursor-pointer items-center gap-2 rounded-md bg-[var(--primary)] px-3.5 py-2 text-sm font-semibold text-white transition hover:bg-[var(--primary-strong)]">
              <ImagePlus size={15} />
              {logo.has ? "Trocar" : "Enviar logotipo"}
              <input
                type="file"
                accept="image/jpeg,image/png,image/webp"
                className="sr-only"
                onChange={(event) => {
                  const file = event.target.files?.[0];
                  if (file) {
                    setLogoError("");
                    setCropping(file);
                  }
                  event.target.value = "";
                }}
              />
            </label>

            {logo.has && (
              <button
                type="button"
                onClick={() => setRemoving(true)}
                className="inline-flex items-center gap-2 rounded-md border border-[var(--border)] px-3.5 py-2 text-sm font-semibold text-[var(--text-secondary)] transition hover:border-[var(--critical)] hover:text-[var(--critical)]"
              >
                <Trash2 size={15} />
                Remover
              </button>
            )}
          </div>
        </div>
      </section>

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

      {cropping && (
        <LogoCropper
          file={cropping}
          busy={logoBusy}
          error={logoError}
          onCancel={() => setCropping(null)}
          onCropped={uploadLogo}
        />
      )}

      {removing && (
        <Confirmation
          title="Remover o logotipo"
          message="A ficha para venda e a proposta voltam a sair só com o nome da revenda."
          confirmLabel="Remover"
          danger
          onConfirm={removeLogo}
          onCancel={() => setRemoving(false)}
          busy={logoBusy}
          error={logoError}
        />
      )}
    </div>
  );
}
