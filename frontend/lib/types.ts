/**
 * Contracts returned by the API.
 *
 * Field names are English because the code is English; the values that reach the screen —
 * a menu label, a role name — stay in Portuguese, because the user reads them. See ADR-0003.
 */

export type MenuItem = {
  key: string;
  /** Label shown in the sidebar, in Portuguese. */
  name: string;
  route: string;
  icon: string | null;
  children: MenuItem[];
};

export type MenuGroup = { group: string; items: MenuItem[] };

export type Session = {
  user: { code: string; name: string; email: string; hasPhoto: boolean };
  /** Role names, displayed to the user. */
  roles: string[];
  /** Every screen key allowed, including those outside the menu. */
  screens: string[];
  /** The sidebar, already filtered and ordered by the server. */
  menu: MenuGroup[];
};

export type Screen = {
  code: string;
  key: string;
  name: string;
  icon: string | null;
  group: string;
  order: number;
  showInMenu: boolean;
};

export type ScreenGroup = { group: string; screens: Screen[] };

export type Role = {
  code: string;
  name: string;
  description: string | null;
  isSystem: boolean;
  screenCount: number;
  /** Codes of the granted screens. */
  screens: string[];
};

export type User = {
  code: string;
  name: string;
  email: string;
  /** Barred from signing in. The row is still there. */
  isBlocked: boolean;
  /** Still present. False means deleted: only a listing that asks for it brings the row back. */
  isActive: boolean;
  /** Codes of the roles held. */
  roles: string[];
  /** Role names, displayed to the user. */
  roleNames: string[];
  hasPhoto: boolean;
  /** CPF or CNPJ, digits only. The mask lives in the UI. */
  document: string | null;
  /** Phone with area code, digits only. */
  phone: string | null;
};

/**
 * Onde o veículo está na operação. O número é o mesmo do enum do backend, e a leitura
 * humana vive em `vehicleStatusLabel`.
 */
export const VehicleStatus = {
  UnderReview: 1,
  Purchased: 2,
  InRepair: 3,
  ReadyForSale: 4,
  Advertised: 5,
  Negotiating: 6,
  Sold: 7,
} as const;

export const vehicleStatusLabel: Record<number, string> = {
  1: "Em análise",
  2: "Comprado",
  3: "Em reparo",
  4: "Pronto para venda",
  5: "Anunciado",
  6: "Negociando",
  7: "Vendido",
};

export const vehicleOriginLabel: Record<number, string> = {
  1: "Leilão",
  2: "Particular",
  3: "Loja",
  4: "Troca",
  5: "Outro",
};

export const fuelTypeLabel: Record<number, string> = {
  1: "Flex",
  2: "Gasolina",
  3: "Etanol",
  4: "Diesel",
  5: "Híbrido",
  6: "Elétrico",
  7: "GNV",
};

export const transmissionLabel: Record<number, string> = {
  1: "Manual",
  2: "Automático",
  3: "Automatizado",
  4: "CVT",
};

export const paymentMethodLabel: Record<number, string> = {
  1: "Dinheiro",
  2: "Transferência",
  3: "Financiamento",
  4: "Cartão",
  5: "Troca",
  6: "Troca com volta",
  7: "Outro",
};

/** O que o veículo custou. Nada disso é coluna: tudo vem calculado da API. */
export type VehicleCost = {
  purchase: number;
  paidExpenses: number;
  plannedExpenses: number;
  total: number;
  projected: number;
  budgetUsedPercent: number | null;
  budgetRemaining: number | null;
  isOverBudget: boolean;
  /** Cabe hoje e estoura com o que está previsto. É o aviso que chega a tempo. */
  willExceedBudget: boolean;
  percentOfFipe: number | null;
  profitAtDesired: number | null;
  marginAtDesired: number | null;
};

export type Vehicle = {
  code: string;
  plate: string;
  chassis: string;
  brand: string;
  model: string;
  version: string | null;
  modelYear: number;
  manufactureYear: number;
  color: string | null;
  mileage: number;
  fuelType: number;
  transmission: number;
  renavam: string | null;
  origin: number;
  hasDamage: boolean;
  damageDescription: string | null;
  status: number;
  /** Para onde ele pode ir a partir daqui. A tela oferece só esses. */
  allowedStatuses: number[];
  purchasePrice: number;
  purchaseDate: string | null;
  supplierName: string | null;
  purchasePaymentMethod: number | null;
  budgetCeiling: number | null;
  fipeValue: number | null;
  fipeReferenceDate: string | null;
  fipeCode: string | null;
  desiredNetPrice: number | null;
  minimumNetPrice: number | null;
  advertisedPrice: number | null;
  marketNotes: string | null;
  notes: string | null;
  cost: VehicleCost;
  daysInStock: number | null;
  photoCount: number;
  coverPhotoCode: string | null;
};

export type VehicleExpense = {
  code: string;
  expenseTypeCode: string;
  expenseTypeName: string;
  description: string;
  amount: number;
  date: string;
  notes: string | null;
  isPaid: boolean;
};

export type ExpenseType = {
  code: string;
  name: string;
  keywords: string | null;
  position: number;
  /** Tipo em uso jamais é excluído. */
  expenseCount: number;
};

/** O que a tela oferece enquanto a pessoa digita a descrição de um gasto. */
export type ExpenseSuggestion = {
  description: string;
  expenseTypeCode: string;
  expenseTypeName: string;
};

/** Para que serve a foto. Espelha `VehiclePhotoKind` do domínio. */
export const VEHICLE_PHOTO_KIND = {
  damage: 1,
  repair: 2,
  finished: 3,
  other: 4,
} as const;

export const VEHICLE_PHOTO_KIND_LABEL: Record<number, string> = {
  1: "Avaria",
  2: "Reparo",
  3: "Pronto",
  4: "Outra",
};

/**
 * Uma foto do veículo, com os três endereços que o navegador busca.
 *
 * Os endereços são assinados e expiram: nada aqui é público, e link que vaza vale pouco por
 * pouco tempo. A listagem carrega a miniatura, jamais a cheia.
 */
export type VehiclePhoto = {
  code: string;
  kind: number;
  position: number;
  isCover: boolean;
  width: number;
  height: number;
  sizeInBytes: number;
  thumbnailUrl: string;
  cardUrl: string;
  fullUrl: string;
};

/** Que documento é. Espelha `VehicleDocumentKind` do domínio. */
export const VEHICLE_DOCUMENT_KIND_LABEL: Record<number, string> = {
  1: "Nota fiscal",
  2: "Recibo de pagamento",
  3: "Documento de leilão",
  4: "Termo",
  5: "Vistoria",
  6: "Documento do despachante",
  7: "Comprovante de endereço",
  8: "Documento pessoal",
  9: "Outro",
};

/**
 * Um documento do veículo.
 *
 * Excluir tira da listagem e **deixa o arquivo no bucket**: nota fiscal, CRV e papel de leilão
 * são prova, e podem ser cobrados anos depois. A tela precisa dizer isso na confirmação.
 */
export type VehicleDocument = {
  code: string;
  kind: number;
  fileName: string;
  contentType: string;
  sizeInBytes: number;
  uploadedAt: string;
  url: string;
};
