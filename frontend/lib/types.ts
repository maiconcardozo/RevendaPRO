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
  /**
   * Onde o carro está, com o combinado junto. Nulo enquanto ninguém disse onde ele fica.
   *
   * O repasse vem aqui porque quem registra a venda precisa dele na mesma leitura — e é
   * sugestão: quem fecha a venda pode mudar o número.
   */
  yard: {
    code: string;
    name: string;
    kind: number;
    cutPercent: number | null;
    cutAmount: number | null;
  } | null;
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
  /** De quem foi comprado, quando cadastrado. Nulo para IPVA, multa e taxa. */
  supplierCode: string | null;
  supplierName: string | null;
  /** Quando vence (M22). Sem prazo informado, é a data do gasto. */
  dueDate: string;
  /** Quando o dinheiro saiu (M22). Nulo enquanto está previsto. */
  paidDate: string | null;
  /** Vencido e sem pagamento, hoje (M22). */
  isOverdue: boolean;
};

/** De onde uma linha do caixa veio (M22). */
export const CASHFLOW_KIND = { vehicleExpense: 1, storeExpense: 2, saleReceivable: 3 } as const;

/** Uma linha do extrato do caixa (M22). */
export type CashflowLine = {
  code: string;
  kind: number;
  description: string;
  party: string | null;
  category: string | null;
  amount: number;
  dueDate: string | null;
  settledDate: string | null;
  isSettled: boolean;
  isOverdue: boolean;
  vehicleCode: string | null;
  plate: string | null;
};

/** O caixa de um período (M22): os totais, o que falta pagar e o que falta receber. */
export type Cashflow = {
  from: string | null;
  to: string | null;
  payableOpen: number;
  payableOverdue: number;
  payableDueSoon: number;
  receivableOpen: number;
  receivableOverdue: number;
  paidInPeriod: number;
  receivedInPeriod: number;
  payables: CashflowLine[];
  receivables: CashflowLine[];
};

/** Para onde um tipo de gasto serve (M22). */
export const EXPENSE_SCOPE = { vehicle: 1, store: 2, both: 3 } as const;

export const EXPENSE_SCOPE_LABEL: Record<number, string> = {
  1: "Carro",
  2: "Loja",
  3: "Os dois",
};

/**
 * O que a loja paga e que jamais pertence a um carro (M22): aluguel, energia, salário, imposto.
 */
export type StoreExpense = {
  code: string;
  description: string;
  expenseTypeCode: string;
  expenseTypeName: string;
  supplierCode: string | null;
  supplierName: string | null;
  amount: number;
  /** A que dia a despesa pertence — o mês do aluguel. */
  date: string;
  dueDate: string;
  paidDate: string | null;
  isPaid: boolean;
  isOverdue: boolean;
  notes: string | null;
};

export type ExpenseType = {
  code: string;
  name: string;
  keywords: string | null;
  position: number;
  /** A type in use is never deleted. Conta os dois lados desde o M22. */
  expenseCount: number;
  /** Para onde serve: 1 carro, 2 loja, 3 os dois (M22). */
  scope: number;
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
/** Um modelo da tabela que pode ser este carro, com as linhas de ano que servem para ele. */
export type FipeCandidate = {
  brandCode: string;
  modelCode: string;
  /** O nome como a tabela escreve. É o que distingue duas linhas de preço. */
  name: string;
  /** Vazio quando a tabela segue sem este modelo no ano do carro. */
  years: FipeOption[];
  /**
   * O que a tabela cobra por esta linha, no mês publicado.
   *
   * É o número que decide a escolha: entre duas versões do mesmo carro, quem conhece o carro
   * reconhece a faixa de preço antes de reconhecer a sigla do acabamento. Nulo quando a lista
   * ficou longa demais para perguntar o preço de cada uma.
   */
  value: number | null;
  /** O código impresso da tabela, que só existe depois de perguntar o preço. */
  fipeCode: string | null;
  /**
   * O quanto deste carro este nome responde, de 0 a 100.
   *
   * Mede o que foi conferido — versão, ano, câmbio e combustível —, e jamais o que foi
   * digitado. Um carro cadastrado só como "Gol" fica em 50 na lista inteira, e é isso que a
   * tela precisa mostrar.
   */
  accuracy: number;
  /**
   * Se este é o candidato que a nota aponta.
   *
   * Vem em um só, e apenas quando a nota dele é maior que a do segundo — empate volta sem
   * recomendado nenhum. É destaque, e jamais escolha: quem grava é a pessoa.
   */
  recommended: boolean;
};

/**
 * O que a busca achou, do mais provável para o menos.
 *
 * Uma forma só: um candidato é uma lista de um, e ela abre o mesmo pop-up que uma lista de
 * vinte. A lista vazia quer dizer que a tabela segue sem este carro.
 */
export type FipeMatch = {
  candidates: FipeCandidate[];
};

export const TIMELINE_KIND = {
  purchase: 1,
  statusChange: 2,
  expense: 3,
  photos: 4,
  documents: 5,
  proposal: 6,
  sale: 7,
  yardChange: 8,
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
  /** De onde o evento veio: o pátio que o carro deixou. Só na mudança de pátio. */
  fromTitle: string | null;
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
  /** The customer who offered (M21). Null only on rows the start-up routine has not reached. */
  customerCode: string | null;
  /** The customer's CPF or CNPJ, digits only, when known. */
  customerDocument: string | null;
  /** The customer's current name, or what was typed on the day. */
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
  /** The customer who bought (M21). */
  customerCode: string | null;
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
  /** Quanto se espera receber em dinheiro: o valor menos a troca e o repasse (M22). */
  expectedCash: number;
  /** Quanto já entrou, somado das entradas (M22). */
  receivedTotal: number;
  /** Quando o que falta é esperado (M22). Nulo quando dinheiro nenhum ficou para depois. */
  dueDate: string | null;
  /** Cada entrada de dinheiro, da mais antiga para a mais nova (M22). */
  receipts: SaleReceipt[];
};

/** Uma entrada de dinheiro de uma venda (M22). */
export type SaleReceipt = {
  code: string;
  amount: number;
  date: string;
  paymentMethod: number;
  notes: string | null;
};

/** One sale as the listing and the dashboard show it. */
export type SaleListing = {
  code: string;
  vehicleCode: string;
  plate: string;
  name: string;
  date: string;
  /** The customer who bought (M21): the name in the listing opens their record. */
  customerCode: string | null;
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
  /**
   * Quanto está parado em cada lugar, e uma linha "Sem pátio" quando há carro sem lugar.
   *
   * Vem junto dos números do topo, e jamais no lugar deles: a pergunta era "de cada um e um
   * todo junto". Vazio enquanto a revenda ainda não cadastrou pátio.
   */
  byYard: {
    code: string | null;
    name: string;
    kind: number | null;
    count: number;
    invested: number;
    averageDaysInStock: number | null;
  }[];
  salesInPeriod: number;
  soldInPeriod: number;
  realizedProfit: number;
  averageDaysToSell: number | null;
  biggestInvestments: RankedVehicle[];
  biggestMargins: RankedVehicle[];
  longestInStock: RankedVehicle[];
  recentSales: SaleListing[];
  /**
   * O gasto com fornecedores no período (M18). O período é o mesmo das vendas — o painel é a
   * leitura do mês; o acumulado mora em Fornecedores.
   */
  suppliers: SupplierStatistics;
};

/**
 * A revenda contra a tabela de referência (M11).
 *
 * Cada valor vem com a cotação do mês dele: a compra contra a tabela do mês da compra, a venda
 * contra a do mês da venda, o pedido contra a de agora. Nulo em `reference` quer dizer que
 * aquele mês jamais foi buscado — e a tela escreve isso, em vez de inventar número.
 */
export type MarketOverview = {
  referenceMonth: string;
  purchases: MarketAverage;
  sales: MarketAverage;
  asking: MarketAverage;
  /** Queda da tabela de um mês para o outro, somada nos carros parados. */
  lostThisMonth: number;
  lostSincePurchase: number;
  yard: MarketLine[];
  sold: MarketLine[];
  proposals: MarketProposalLine[];
  /** Carros fora das médias, por falta de cotação deste mês. */
  withoutReference: number;
};

export type MarketAverage = {
  cars: number;
  amount: number;
  reference: number;
  difference: number;
  percent: number | null;
};

export type MarketLine = {
  code: string;
  plate: string;
  brand: string;
  model: string;
  version: string | null;
  modelYear: number;
  status: number;
  daysInStock: number | null;
  amount: number;
  reference: number | null;
  difference: number | null;
  percent: number | null;
  purchaseDifference: number | null;
  purchasePercent: number | null;
  lostSincePurchase: number | null;
};

export type MarketProposalLine = {
  vehicleCode: string;
  plate: string;
  brand: string;
  model: string;
  prospectName: string;
  amount: number;
  date: string;
  reference: number | null;
  difference: number | null;
  percent: number | null;
};

/**
 * De quem é o lugar onde o carro está.
 *
 * Um cadastro só, com o tipo dentro: pátio próprio e loja de terceiro são a mesma coisa para a
 * operação — um lugar onde o carro fica. O que o tipo muda é o repasse.
 */
export const YardKind = {
  Own: 1,
  Partner: 2,
} as const;

export const YARD_KIND_LABEL: Record<number, string> = {
  1: "Pátio da revenda",
  2: "Loja de terceiro",
};

export type Yard = {
  code: string;
  name: string;
  kind: number;
  contactName: string | null;
  contactPhone: string | null;
  /** Repasse combinado em percentual. Nulo quando o combinado foi em valor. */
  cutPercent: number | null;
  /** Repasse combinado em valor. Nulo quando o combinado foi em percentual. */
  cutAmount: number | null;
  notes: string | null;
  position: number;
  /** Quantos carros estão nele agora. */
  vehicleCount: number;
};

/**
 * De quem a revenda compra serviço e peça.
 *
 * Fornecedor diz **de quem**; tipo de gasto diz **o quê**. A mesma oficina cobra Mecânica num
 * carro e Peças no outro, e é por isso que o gasto aponta para os dois.
 */
/**
 * Quem a revenda conhece (M21): quem ofereceu, quem comprou, quem volta. Cliente antes de
 * comprador — a pessoa recusada em junho é cliente desde a primeira proposta.
 */
export type Customer = {
  code: string;
  name: string;
  /** CPF ou CNPJ, só dígitos. */
  document: string | null;
  /** Telefone, só dígitos. É por onde o WhatsApp chega. */
  phone: string | null;
  email: string | null;
  address: string | null;
  notes: string | null;
  proposalCount: number;
  saleCount: number;
  boughtTotal: number;
  lastDate: string | null;
};

/** Uma proposta na ficha do cliente, com o carro. */
export type CustomerProposal = {
  code: string;
  date: string;
  amount: number;
  status: number;
  paymentMethod: number;
  vehicleCode: string;
  plate: string;
  vehicleName: string;
  modelYear: number;
};

/** Uma compra na ficha do cliente, com o carro. */
export type CustomerSale = {
  code: string;
  date: string;
  amount: number;
  paymentMethod: number;
  hadTradeIn: boolean;
  vehicleCode: string;
  plate: string;
  vehicleName: string;
  modelYear: number;
};

/** A ficha do cliente: os dados e a história. */
export type CustomerDetail = {
  customer: Customer;
  proposals: CustomerProposal[];
  sales: CustomerSale[];
};

export type Supplier = {
  code: string;
  name: string;
  /** O ramo, pelo código público. */
  segmentCode: string;
  segmentName: string;
  contactName: string | null;
  /** Só dígitos. */
  contactPhone: string | null;
  /** CNPJ ou CPF, só dígitos. */
  document: string | null;
  notes: string | null;
  /** Quantos gastos apontam para ele. Um fornecedor em uso fica no cadastro. */
  expenseCount: number;
};

/** O ramo de um fornecedor: oficina mecânica, funilaria e pintura, autopeças. Cadastro da revenda. */
export type SupplierSegment = {
  code: string;
  name: string;
  position: number;
  /** Quantos fornecedores estão nele. Um ramo em uso fica no cadastro. */
  supplierCount: number;
};

/** Quanto foi para um fornecedor num período: a linha do ranking e do card. */
export type SupplierSpend = {
  code: string;
  name: string;
  segmentName: string;
  /** O que foi pago. É o "quanto gastei". */
  paidTotal: number;
  /** O que está previsto, fora do custo real. */
  plannedTotal: number;
  expenseCount: number;
  lastDate: string | null;
};

/** Um gasto de um fornecedor, com o carro em que foi feito. */
export type SupplierExpense = {
  code: string;
  date: string;
  description: string;
  expenseTypeName: string;
  amount: number;
  isPaid: boolean;
  vehicleCode: string;
  plate: string;
  vehicleName: string;
};

/** A ficha de um fornecedor: quanto já foi para ele, em que carros, e em quê. */
export type SupplierStatement = {
  supplier: Supplier;
  paidTotal: number;
  plannedTotal: number;
  byType: { expenseTypeName: string; paidTotal: number; expenseCount: number }[];
  expenses: SupplierExpense[];
};

/** Uma fatia do gasto com fornecedores: um ramo, um tipo de gasto ou um mês ("AAAA-MM"). */
export type SpendSlice = {
  key: string;
  name: string;
  paidTotal: number;
  plannedTotal: number;
  expenseCount: number;
};

/**
 * O painel do gasto com fornecedores num período: totais, ranking, por ramo, por tipo e mês a
 * mês. A tela Fornecedores mostra inteiro; o dashboard, a versão curta.
 */
export type SupplierStatistics = {
  from: string | null;
  to: string | null;
  /** O que foi pago a fornecedores. É o "quanto gastei". */
  paidTotal: number;
  plannedTotal: number;
  expenseCount: number;
  supplierCount: number;
  vehicleCount: number;
  /** Pago sem fornecedor no gasto: taxa, multa, ou esquecimento. */
  unassignedPaid: number;
  bySupplier: SupplierSpend[];
  bySegment: SpendSlice[];
  byType: SpendSlice[];
  /** Em ordem cronológica, com os meses vazios preenchidos. */
  byMonth: SpendSlice[];
};

/** A revenda como a tela e os documentos leem: o que sai impresso em cima de cada papel. */
export type Company = {
  name: string;
  /** CNPJ ou CPF, só dígitos. */
  document: string | null;
  /** Só dígitos. */
  phone: string | null;
  email: string | null;
  /** Em uma linha, como vai no papel. */
  address: string | null;
};
