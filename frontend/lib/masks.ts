/**
 * Input masks.
 *
 * A mask is presentation: it formats while the person types and nothing more. What goes to
 * the database are the raw digits, and what decides whether a document is valid is the
 * backend (RevendaPro.Shared.Helpers.BrazilianDocuments). Any mask happily accepts
 * 111.111.111-11; the server does not.
 */

export const digitsOnly = (value: string) => value.replace(/\D/g, "");

export function maskCpf(value: string): string {
  const d = digitsOnly(value).slice(0, 11);

  if (d.length <= 3) return d;
  if (d.length <= 6) return `${d.slice(0, 3)}.${d.slice(3)}`;
  if (d.length <= 9) return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6)}`;

  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

export function maskCnpj(value: string): string {
  const d = digitsOnly(value).slice(0, 14);

  if (d.length <= 2) return d;
  if (d.length <= 5) return `${d.slice(0, 2)}.${d.slice(2)}`;
  if (d.length <= 8) return `${d.slice(0, 2)}.${d.slice(2, 5)}.${d.slice(5)}`;
  if (d.length <= 12) return `${d.slice(0, 2)}.${d.slice(2, 5)}.${d.slice(5, 8)}/${d.slice(8)}`;

  return `${d.slice(0, 2)}.${d.slice(2, 5)}.${d.slice(5, 8)}/${d.slice(8, 12)}-${d.slice(12)}`;
}

/** Switches from CPF to CNPJ on its own once it passes 11 digits. */
export function maskCpfCnpj(value: string): string {
  const d = digitsOnly(value);

  return d.length <= 11 ? maskCpf(d) : maskCnpj(d);
}

/** (00) 0000-0000 for a landline, (00) 00000-0000 for a mobile. */
export function maskPhone(value: string): string {
  const d = digitsOnly(value).slice(0, 11);

  if (d.length === 0) return "";
  if (d.length <= 2) return `(${d}`;

  const areaCode = `(${d.slice(0, 2)}) `;

  if (d.length <= 6) return areaCode + d.slice(2);

  return d.length <= 10
    ? `${areaCode}${d.slice(2, 6)}-${d.slice(6)}`
    : `${areaCode}${d.slice(2, 7)}-${d.slice(7)}`;
}

export function maskPostalCode(value: string): string {
  const d = digitsOnly(value).slice(0, 8);

  return d.length <= 5 ? d : `${d.slice(0, 5)}-${d.slice(5)}`;
}

/**
 * Old plate (ABC1234) and Mercosul plate (ABC1D23), with no hyphen.
 *
 * No hyphen on purpose: it is how a Mercosul plate is written, and it is how the domain
 * stores both. A mask that decorates the field and disappears in the database only creates a
 * gap between what the person sees and what they find when searching for it.
 */
export function maskPlate(value: string): string {
  return value
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, "")
    .slice(0, 7);
}

/**
 * E-mail has no mask: formatting while typing gets in the way. What can be done is
 * stripping the spaces and the capitals that came from a paste.
 */
export function normalizeEmail(value: string): string {
  return value.trim().toLowerCase().replace(/\s+/g, "");
}

// ------------------------------------------------------------------------ validation

export function isValidCpf(value: string): boolean {
  const cpf = digitsOnly(value);

  if (cpf.length !== 11 || /^(\d)\1{10}$/.test(cpf)) {
    return false;
  }

  const checkDigit = (length: number) => {
    let sum = 0;

    for (let i = 0; i < length; i++) {
      sum += Number(cpf[i]) * (length + 1 - i);
    }

    const remainder = sum % 11;
    return remainder < 2 ? 0 : 11 - remainder;
  };

  return checkDigit(9) === Number(cpf[9]) && checkDigit(10) === Number(cpf[10]);
}

export function isValidCnpj(value: string): boolean {
  const cnpj = digitsOnly(value);

  if (cnpj.length !== 14 || /^(\d)\1{13}$/.test(cnpj)) {
    return false;
  }

  const checkDigit = (weights: number[]) => {
    const sum = weights.reduce((total, weight, i) => total + Number(cnpj[i]) * weight, 0);
    const remainder = sum % 11;

    return remainder < 2 ? 0 : 11 - remainder;
  };

  return (
    checkDigit([5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]) === Number(cnpj[12]) &&
    checkDigit([6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]) === Number(cnpj[13])
  );
}

/** Empty is valid: the field is optional. */
export function isValidCpfOrCnpj(value: string): boolean {
  const d = digitsOnly(value);

  if (d.length === 0) return true;
  if (d.length === 11) return isValidCpf(d);
  if (d.length === 14) return isValidCnpj(d);

  return false;
}

export function isValidPhone(value: string): boolean {
  const d = digitsOnly(value);

  if (d.length === 0) return true;
  if (d.length !== 10 && d.length !== 11) return false;

  return Number(d.slice(0, 2)) >= 11 && (d.length === 10 || d[2] === "9");
}

export function isValidEmail(value: string): boolean {
  if (value.trim().length === 0) return false;

  return /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(value.trim());
}

/** Money as it reads. */
export function formatMoney(value: number | null | undefined): string {
  return typeof value === "number"
    ? value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })
    : "—";
}

/**
 * Money mask while typing: what the person types are cents, and the comma walks on its own.
 * Nobody has to land a dot and a comma in the middle of a number.
 */
export function maskMoney(value: string): string {
  const digits = value.replace(/\D/g, "").slice(0, 12);

  if (!digits) return "";

  return (Number(digits) / 100).toLocaleString("pt-BR", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

/** The number behind the money mask. */
export function moneyValue(masked: string): number {
  const digits = masked.replace(/\D/g, "");

  return digits ? Number(digits) / 100 : 0;
}

/** Mileage with a thousands separator, while typing. */
export function maskMileage(value: string): string {
  const digits = value.replace(/\D/g, "").slice(0, 7);

  return digits ? Number(digits).toLocaleString("pt-BR") : "";
}

/** Chassis: 17 characters, without I, O and Q, which the standard excludes. */
export function maskChassis(value: string): string {
  return value
    .toUpperCase()
    .replace(/[^A-HJ-NPR-Z0-9]/g, "")
    .slice(0, 17);
}

/** Four digit year. */
export function maskYear(value: string): string {
  return value.replace(/\D/g, "").slice(0, 4);
}

/** An ISO date (2026-07-03) the way Brazil reads it. */
export function formatDate(value: string | null | undefined): string {
  if (!value) return "—";

  const [year, month, day] = value.slice(0, 10).split("-");

  return day && month && year ? `${day}/${month}/${year}` : "—";
}

/**
 * O mês de uma tabela mensal, escrito como se fala: "setembro/2026".
 *
 * A tabela FIPE é publicada uma vez por mês, então "01/09/2026" sugere um dia que carrega
 * significado nenhum — e o dia 1 é só onde o sistema guarda o mês.
 */
export function formatMonth(value: string | null | undefined): string {
  if (!value) return "—";

  const [year, month] = value.slice(0, 10).split("-");
  const names = [
    "janeiro", "fevereiro", "março", "abril", "maio", "junho",
    "julho", "agosto", "setembro", "outubro", "novembro", "dezembro",
  ];

  const name = names[Number(month) - 1];

  return name && year ? `${name}/${year}` : "—";
}

/**
 * "1 mês" e "2 meses".
 *
 * A mesma regra do plural de formatDays, e pelo mesmo motivo: "1 meses" numa etiqueta que
 * aparece em toda a listagem do pátio faz o sistema inteiro parecer improvisado.
 */
export function formatMeses(value: number): string {
  return `${value} ${value === 1 ? "mês" : "meses"}`;
}

/** Date and time, for history and audit. */
export function formatMoment(value: string | null | undefined): string {
  if (!value) return "—";

  const moment = new Date(value.endsWith("Z") ? value : `${value}Z`);

  return Number.isNaN(moment.getTime())
    ? "—"
    : moment.toLocaleString("pt-BR", { dateStyle: "short", timeStyle: "short" });
}

/** Mileage with a thousands separator, for reading. */
export function formatMileage(value: number | null | undefined): string {
  return typeof value === "number" ? `${value.toLocaleString("pt-BR")} km` : "—";
}

/** File size, in the unit a person reads. */
export function formatBytes(value: number): string {
  if (value < 1024) return `${value} B`;

  const kb = value / 1024;

  return kb < 1024 ? `${kb.toFixed(0)} KB` : `${(kb / 1024).toFixed(1)} MB`;
}

/**
 * A percentage the way Brazil reads it: a comma, and at most two decimals.
 *
 * The number arrives from the server as 94.99, and writing `{value}%` on the screen prints
 * "94.99%" — with the JavaScript dot, which here is the thousands separator. A small detail
 * that makes a screen look half translated.
 */
export function formatPercent(value: number | null | undefined): string {
  return typeof value === "number"
    ? `${value.toLocaleString("pt-BR", { maximumFractionDigits: 2 })}%`
    : "—";
}

/**
 * "1 dia" e "2 dias".
 *
 * A regra do plural cabe numa linha, e escrever "1 dias" na tela de quem vive contando dias
 * de pátio é o tipo de detalhe que faz um sistema parecer improvisado.
 */
export function formatDays(value: number | null | undefined): string {
  return typeof value === "number" ? `${value} ${value === 1 ? "dia" : "dias"}` : "—";
}
