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
  /** What this installation allows, so the screen can refuse before asking. */
  limits: { maxUploadSizeInBytes: number };
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
 * Where the vehicle is in the operation. The number is the one the backend enum uses, and
 * the human reading lives in `VEHICLE_STATUS_LABEL`.
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

/** What the vehicle cost. None of it is a column: every number arrives calculated. */
export type VehicleCost = {
  purchase: number;
  paidExpenses: number;
  plannedExpenses: number;
  total: number;
  projected: number;
  budgetUsedPercent: number | null;
  budgetRemaining: number | null;
  isOverBudget: boolean;
  /** Fits today and overflows with what is planned. The warning that arrives in time. */
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
  /** Where it can go from here. The screen offers only these. */
  allowedStatuses: number[];
  purchasePrice: number;
  purchaseDate: string | null;
  supplierName: string | null;
  purchasePaymentMethod: number | null;
  budgetCeiling: number | null;
  fipeValue: number | null;
  fipeReferenceDate: string | null;
  fipeCode: string | null;
  /** Year and fuel of the priced row (2014-5). Written by the lookup, never typed. */
  fipeYearFuel: string | null;
  /** 1 typed by a person, 2 read from the table. Null while there is no reference value. */
  fipeSource: number | null;
  /** Quantas tabelas publicadas a referência está atrasada. Zero é atual, nulo é sem valor. */
  fipeMonthsBehind: number | null;
  desiredNetPrice: number | null;
  minimumNetPrice: number | null;
  advertisedPrice: number | null;
  marketNotes: string | null;
  notes: string | null;
  cost: VehicleCost;
  daysInStock: number | null;
  photoCount: number;
  /** Signed address of the cover thumbnail. The listing loads this, never the full one. */
  coverThumbnailUrl: string | null;
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
  /** A type in use is never deleted. */
  expenseCount: number;
};

/** What the screen offers while somebody types the description of an expense. */
export type ExpenseSuggestion = {
  description: string;
  expenseTypeCode: string;
  expenseTypeName: string;
};

/** What the photo is for. Mirrors `VehiclePhotoKind` in the domain. */
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
 * A photo of the vehicle, with the three addresses the browser fetches.
 *
 * The addresses are signed and expire: nothing here is public, and a link that leaks is worth
 * little for long. The listing loads the thumbnail, never the full size.
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

/** Which kind of document. Mirrors `VehicleDocumentKind` in the domain. */
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
 * A document of the vehicle.
 *
 * Deleting takes it out of the listing and **leaves the file in the store**: an invoice, a
 * registration certificate and an auction paper are evidence, and can be demanded years
 * later. The confirmation on screen has to say so.
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

/**
 * Labels for the domain enums.
 *
 * Kept here, and never received from the API, because the API speaks numbers: the value is
 * the contract, the text is the screen. Renaming "Em análise" is one line, and nothing else.
 */
/**
 * A document that left the file of a vehicle, and whose file is still in the bucket.
 *
 * Excluir um documento sempre foi lógico, e o arquivo sempre ficou guardado: uma revenda
 * responde pelo que vendeu anos depois. O que faltava era a porta de volta.
 */
export type DeletedDocument = {
  code: string;
  kind: number;
  fileName: string;
  contentType: string;
  sizeInBytes: number;
  uploadedAt: string;
  deletedAt: string | null;
  deletedBy: string | null;
  vehicleCode: string;
  plate: string;
  brand: string;
  model: string;
  url: string;
};

/**
 * De onde veio o valor de referência.
 *
 * A ficha diz isso em voz alta porque as duas origens se leem diferente na hora de precificar:
 * o valor da tabela é o mercado, e o valor digitado carrega o julgamento de quem conhece um
 * carro raro, importado ou fora da tabela.
 */
export const FIPE_SOURCE_LABEL: Record<number, string> = {
  1: "informada à mão",
  2: "consulta automática",
};

/** Uma escolha do escolhedor: o que a fonte espera de volta, e o que a pessoa lê. */
export type FipeOption = {
  /** O que volta para a fonte: 23, 5635, 2014-5. */
  code: string;
  /** O que aparece na tela: "GM - Chevrolet", "2014 Flex". */
  name: string;
};

/** What the table answered on the last lookup. */
export type FipeReference = {
  value: number;
  referenceMonth: string;
  fipeCode: string;
  yearFuel: string;
  source: number;
  brand: string;
  model: string;
  /** What the sheet said before, so the screen can say how much the reference moved. */
  previousValue: number | null;
};

export const VEHICLE_STATUS_LABEL: Record<number, string> = {
  1: "Em análise",
  2: "Comprado",
  3: "Em reparo",
  4: "Pronto para venda",
  5: "Anunciado",
  6: "Em negociação",
  7: "Vendido",
};

export const VEHICLE_ORIGIN_LABEL: Record<number, string> = {
  1: "Leilão",
  2: "Particular",
  3: "Loja",
  4: "Troca",
  5: "Outra",
};

export const FUEL_TYPE_LABEL: Record<number, string> = {
  1: "Flex",
  2: "Gasolina",
  3: "Etanol",
  4: "Diesel",
  5: "Híbrido",
  6: "Elétrico",
  7: "GNV",
};

export const TRANSMISSION_LABEL: Record<number, string> = {
  1: "Manual",
  2: "Automático",
  3: "Automatizado",
  4: "CVT",
};

export const PAYMENT_METHOD_LABEL: Record<number, string> = {
  1: "Dinheiro",
  2: "Transferência",
  3: "Financiamento",
  4: "Cartão",
  5: "Troca",
  6: "Troca com volta",
  7: "Outra",
};

/**
 * What kind of thing happened to the car. Mirrors `TimelineEventKind` in the domain.
 */
export const TIMELINE_KIND = {
  purchase: 1,
  statusChange: 2,
  expense: 3,
  photos: 4,
  documents: 5,
  proposal: 6,
  sale: 7,
} as const;

/**
 * One thing that happened to the car, in the single history the file shows (RF-26).
 *
 * Most fields are null most of the time, because the kinds differ: an expense has an
 * amount and no status, a move along the pipeline has statuses and no amount. The screen
 * reads only what the kind of the entry carries.
 */
export type VehicleTimelineEntry = {
  moment: string;
  kind: number;
  /** Null when the entry counts several records: the attachments of one day. */
  code: string | null;
  title: string | null;
  detail: string | null;
  amount: number | null;
  quantity: number;
  fromStatus: number | null;
  toStatus: number | null;
  proposalStatus: number | null;
  isPaid: boolean | null;
  /** Null when the system did it, or when the author is unknown. */
  actorName: string | null;
};

/** Who the car is sold through. Mirrors `SaleChannel` in the domain. */
export const SALE_CHANNEL = { direct: 1, partnerStore: 2 } as const;

export const SALE_CHANNEL_LABEL: Record<number, string> = {
  1: "Venda direta",
  2: "Loja parceira",
};

/** Where a proposal stands. Mirrors `ProposalStatus` in the domain. */
export const PROPOSAL_STATUS = { open: 1, accepted: 2, declined: 3 } as const;

export const PROPOSAL_STATUS_LABEL: Record<number, string> = {
  1: "Em aberto",
  2: "Aceita",
  3: "Recusada",
};

/**
 * What a deal leaves in hand. The same shape before (proposal) and after (sale), because the
 * server runs the same arithmetic in both places. None of it is stored.
 */
export type DealResult = {
  amount: number;
  partnerCut: number;
  commission: number;
  cost: number;
  received: number;
  grossProfit: number;
  netProfit: number;
  margin: number | null;
};

export type Proposal = {
  code: string;
  prospectName: string;
  prospectPhone: string | null;
  amount: number;
  date: string;
  paymentMethod: number;
  channel: number;
  partnerCutPercent: number | null;
  partnerCutAmount: number | null;
  status: number;
  notes: string | null;
  result: DealResult;
};

export type Sale = {
  code: string;
  proposalCode: string | null;
  date: string;
  amount: number;
  cashAmount: number;
  paymentMethod: number;
  channel: number;
  partnerStoreName: string | null;
  partnerCutPercent: number | null;
  partnerCutAmount: number | null;
  commission: number;
  commissionNotes: string | null;
  buyerName: string;
  /** CPF or CNPJ, digits only. Personal data: shown here, exported nowhere. */
  buyerDocument: string | null;
  buyerPhone: string | null;
  tradeInValue: number | null;
  tradeInVehicleCode: string | null;
  notes: string | null;
  daysInStock: number | null;
  result: DealResult;
};

/** One sale as the listing and the dashboard show it. */
export type SaleListing = {
  code: string;
  vehicleCode: string;
  plate: string;
  name: string;
  date: string;
  buyerName: string;
  channel: number;
  partnerStoreName: string | null;
  paymentMethod: number;
  amount: number;
  cost: number;
  netProfit: number;
  margin: number | null;
  daysInStock: number | null;
  hadTradeIn: boolean;
};

export type RankedVehicle = {
  code: string;
  plate: string;
  name: string;
  status: number;
  cost: number;
  projectedProfit: number | null;
  daysInStock: number | null;
  coverThumbnailUrl: string | null;
};

export type Dashboard = {
  from: string | null;
  to: string | null;
  inStock: number;
  invested: number;
  projectedProfit: number;
  byStatus: { status: number; count: number; cost: number }[];
  salesInPeriod: number;
  soldInPeriod: number;
  realizedProfit: number;
  averageDaysToSell: number | null;
  biggestInvestments: RankedVehicle[];
  biggestMargins: RankedVehicle[];
  longestInStock: RankedVehicle[];
  recentSales: SaleListing[];
};
