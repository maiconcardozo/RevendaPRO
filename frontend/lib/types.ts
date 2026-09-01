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
