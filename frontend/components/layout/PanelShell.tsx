"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import {
  Car,
  ChevronDown,
  CircleHelp,
  HandCoins,
  LayoutDashboard,
  LogOut,
  Menu,
  Moon,
  PanelLeft,
  Receipt,
  ShieldCheck,
  Sun,
  UserRound,
  Users,
  X,
  type LucideIcon,
} from "lucide-react";
import { Avatar } from "@/components/common/Avatar";
import { Confirmation } from "@/components/common/Confirmation";
import type { MenuGroup, MenuItem, Session } from "@/lib/types";

/**
 * Icons available to the menu. The name comes from ScreenCatalog on the backend.
 * A screen with an unknown icon does not break the sidebar: it falls back.
 */
const ICONS: Record<string, LucideIcon> = {
  LayoutDashboard,
  Car,
  Receipt,
  HandCoins,
  Users,
  ShieldCheck,
};

function iconFor(name: string | null): LucideIcon {
  return (name && ICONS[name]) || CircleHelp;
}

export function PanelShell({
  session,
  children,
}: {
  session: Session;
  children: ReactNode;
}) {
  const pathname = usePathname();
  const router = useRouter();

  const [mobileOpen, setMobileOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(false);
  const [dark, setDark] = useState(false);
  const [userOpen, setUserOpen] = useState(false);
  const [signingOut, setSigningOut] = useState(false);
  const [confirmingSignOut, setConfirmingSignOut] = useState(false);

  const userMenuRef = useRef<HTMLDivElement>(null);
  const drawerButtonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    setCollapsed(localStorage.getItem("revenda-pro-sidebar") === "1");
    setDark(document.documentElement.classList.contains("dark"));
  }, []);

  // Publishes the sidebar width on <html> for the modal scrim, which lives in a portal
  // outside the panel and needs to know where the menu ends.
  useEffect(() => {
    document.documentElement.dataset.sidebar = collapsed ? "collapsed" : "expanded";
  }, [collapsed]);

  // Escape closes the drawer and the user menu; focus returns to whoever opened it.
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== "Escape") return;

      if (mobileOpen) {
        setMobileOpen(false);
        drawerButtonRef.current?.focus();
      }

      setUserOpen(false);
    }

    addEventListener("keydown", onKeyDown);
    return () => removeEventListener("keydown", onKeyDown);
  }, [mobileOpen]);

  // A click outside closes the user menu.
  useEffect(() => {
    if (!userOpen) return;

    function onClick(event: MouseEvent) {
      if (!userMenuRef.current?.contains(event.target as Node)) {
        setUserOpen(false);
      }
    }

    addEventListener("mousedown", onClick);
    return () => removeEventListener("mousedown", onClick);
  }, [userOpen]);

  const toggleSidebar = useCallback(() => {
    setCollapsed((current) => {
      const next = !current;
      localStorage.setItem("revenda-pro-sidebar", next ? "1" : "0");
      return next;
    });
  }, []);

  function toggleTheme() {
    setDark((current) => {
      const next = !current;
      localStorage.setItem("revenda-pro-theme", next ? "dark" : "light");
      document.documentElement.classList.toggle("dark", next);
      return next;
    });
  }

  async function signOut() {
    setSigningOut(true);
    await fetch("/api/auth/logout", { method: "POST" });
    router.replace("/login");
  }

  function askSignOutConfirmation() {
    setUserOpen(false);
    setMobileOpen(false);
    setConfirmingSignOut(true);
  }

  const expanded = !collapsed || mobileOpen;
  const roleName = session.roles[0] ?? "Sem perfil";
  const currentScreen = currentScreenName(session.menu, pathname);

  return (
    <div
      className="app-shell flex h-screen overflow-hidden bg-[var(--canvas)]"
      data-collapsed={collapsed}
    >
      <a href="#content" className="skip-link">
        Ir para o conteúdo
      </a>

      <aside
        aria-label="Navegação principal"
        className={[
          "fixed inset-y-0 left-0 z-50 flex h-screen w-[264px] shrink-0 flex-col",
          "transition-transform duration-200 lg:static lg:translate-x-0 lg:transition-[width]",
          mobileOpen ? "translate-x-0" : "-translate-x-full",
          collapsed ? "lg:w-[76px]" : "lg:w-[264px]",
        ].join(" ")}
        style={{ backgroundColor: "var(--sidebar-bg)", color: "var(--sidebar-ink)" }}
      >
        <div
          className="relative flex h-16 shrink-0 items-center gap-3 border-b px-4"
          style={{ borderColor: "var(--sidebar-border)" }}
        >
          <span
            className="absolute inset-x-0 bottom-0 h-px"
            style={{ background: "linear-gradient(90deg,var(--primary),transparent)" }}
          />
          <div className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-[var(--primary)] text-xs font-black text-white">
            RP
          </div>
          {expanded && (
            <div className="min-w-0">
              <p className="font-display truncate text-[15px] font-bold leading-tight">
                Revenda Pro
              </p>
              <p className="truncate text-[10px] font-semibold uppercase leading-tight tracking-[.18em] text-[var(--sidebar-ink-muted)]">
                Painel de gestão
              </p>
            </div>
          )}
          <button
            type="button"
            onClick={() => setMobileOpen(false)}
            className="ml-auto grid h-8 w-8 place-items-center lg:hidden"
            aria-label="Fechar menu"
          >
            <X size={20} />
          </button>
        </div>

        <nav className="flex-1 space-y-5 overflow-y-auto px-3 py-4">
          {session.menu.length === 0 && expanded && (
            <p className="px-3 text-[13px] leading-relaxed text-[var(--sidebar-ink-muted)]">
              Seu perfil ainda nao tem telas liberadas.
            </p>
          )}

          {session.menu.map((group) => (
            <div key={group.group}>
              {expanded && group.group && (
                <p className="font-display mb-2 px-3 text-[10px] font-bold uppercase tracking-[.2em] text-[var(--sidebar-ink-muted)]">
                  {group.group}
                </p>
              )}
              <div className="space-y-1">
                {group.items.map((item) => (
                  <SidebarItem
                    key={item.key}
                    item={item}
                    pathname={pathname}
                    expanded={expanded}
                    onNavigate={() => setMobileOpen(false)}
                  />
                ))}
              </div>
            </div>
          ))}
        </nav>

        {/* Identity: who you are and which role decides your menu. */}
        <div
          className="shrink-0 border-t px-3 py-3"
          style={{ borderColor: "var(--sidebar-border)" }}
        >
          <div
            className={`flex items-center gap-3 px-2 py-2 ${expanded ? "" : "justify-center px-0"}`}
          >
            <Avatar
              name={session.user.name}
              code={session.user.code}
              hasPhoto={session.user.hasPhoto}
            />
            {expanded && (
              <div className="min-w-0">
                <p className="truncate text-[13px] font-medium">{session.user.name}</p>
                <p className="truncate text-[11px] text-[var(--sidebar-ink-muted)]">{roleName}</p>
              </div>
            )}
          </div>

          <button
            type="button"
            onClick={askSignOutConfirmation}
            title={!expanded ? "Sair" : undefined}
            aria-label="Sair"
            className={[
              "flex h-10 w-full items-center gap-3 rounded-md text-sm font-medium transition-colors",
              "text-[var(--sidebar-ink-muted)] hover:bg-[var(--sidebar-hover)] hover:text-white",
              expanded ? "px-3" : "justify-center px-0",
            ].join(" ")}
          >
            <LogOut className="h-[19px] w-[19px] shrink-0" />
            {expanded && <span>Sair</span>}
          </button>
        </div>
      </aside>

      {mobileOpen && (
        <button
          type="button"
          aria-label="Fechar menu"
          onClick={() => setMobileOpen(false)}
          className="fixed inset-0 z-40 bg-[#07152c]/60 lg:hidden"
        />
      )}

      <div className="flex h-screen min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-30 flex h-16 shrink-0 items-center gap-2 border-b border-[var(--border)] bg-[color-mix(in_srgb,var(--canvas)_88%,transparent)] px-4 backdrop-blur">
          <button
            ref={drawerButtonRef}
            type="button"
            onClick={() => setMobileOpen(true)}
            aria-label="Abrir menu"
            aria-expanded={mobileOpen}
            className="grid h-10 w-10 place-items-center rounded-md text-[var(--text-secondary)] lg:hidden"
          >
            <Menu size={23} />
          </button>

          <button
            type="button"
            onClick={toggleSidebar}
            aria-label={collapsed ? "Expandir menu" : "Recolher menu"}
            aria-expanded={!collapsed}
            className="hidden h-10 w-10 place-items-center rounded-md text-[var(--text-secondary)] hover:bg-[var(--surface-2)] lg:grid"
          >
            <PanelLeft size={21} />
          </button>

          {/* Where you are. */}
          {currentScreen && (
            <p className="font-display ml-1 truncate text-sm font-semibold text-[var(--text-primary)]">
              {currentScreen}
            </p>
          )}

          <div className="flex-1" />

          <button
            type="button"
            onClick={toggleTheme}
            aria-label={dark ? "Ativar tema claro" : "Ativar tema escuro"}
            className="grid h-10 w-10 place-items-center rounded-md border border-[var(--border)] bg-[var(--surface)] text-[var(--text-secondary)] transition hover:border-[var(--primary)] hover:text-[var(--primary)]"
          >
            {dark ? <Sun size={18} /> : <Moon size={18} />}
          </button>

          <div className="relative" ref={userMenuRef}>
            <button
              type="button"
              onClick={() => setUserOpen((v) => !v)}
              aria-label="Menu do usuário"
              aria-expanded={userOpen}
              aria-haspopup="menu"
              className="flex h-10 w-10 items-center justify-center rounded-full border border-[var(--border)] bg-[var(--surface)] sm:w-auto sm:justify-start sm:gap-2 sm:pl-1 sm:pr-2"
            >
              <Avatar
                name={session.user.name}
                code={session.user.code}
                hasPhoto={session.user.hasPhoto}
                size={32}
              />
              <ChevronDown size={15} className="hidden text-[var(--text-muted)] sm:block" />
            </button>

            {userOpen && (
              <div
                role="menu"
                className="absolute right-0 z-50 mt-2 w-64 overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--surface)] shadow-[var(--shadow-lg)]"
              >
                <div className="border-b border-[var(--border)] px-4 py-3">
                  <p className="truncate text-sm font-semibold">{session.user.name}</p>
                  <p className="truncate text-xs text-[var(--text-muted)]">
                    {session.user.email}
                  </p>
                </div>
                <div className="px-2 py-2">
                  <button
                    type="button"
                    role="menuitem"
                    onClick={askSignOutConfirmation}
                    className="flex w-full items-center gap-2 rounded-md px-3 py-2 text-sm text-[var(--critical)] hover:bg-[var(--surface-2)]"
                  >
                    <LogOut size={16} />
                    Sair
                  </button>
                </div>
              </div>
            )}
          </div>
        </header>

        <main id="content" className="instrument-grid flex-1 overflow-y-auto">
          <div className="mx-auto max-w-screen-xl p-4 sm:p-6">{children}</div>
        </main>
      </div>

      {confirmingSignOut && (
        <Confirmation
          title="Sair do sistema"
          message={
            <>
              Deseja realmente sair? Você vai precisar entrar de novo com e-mail e senha
              para voltar ao painel.
            </>
          }
          confirmLabel="Sair"
          danger
          busy={signingOut}
          onConfirm={signOut}
          onCancel={() => setConfirmingSignOut(false)}
        />
      )}
    </div>
  );
}

function SidebarItem({
  item,
  pathname,
  expanded,
  onNavigate,
}: {
  item: MenuItem;
  pathname: string;
  expanded: boolean;
  onNavigate: () => void;
}) {
  const active = pathname === item.route || pathname.startsWith(`${item.route}/`);
  const Icon = iconFor(item.icon);

  return (
    <Link
      href={item.route}
      onClick={onNavigate}
      aria-label={item.name}
      aria-current={active ? "page" : undefined}
      className={[
        "relative flex h-11 items-center gap-3 rounded-md text-sm transition-colors",
        active
          ? "font-semibold text-white"
          : "font-medium text-[var(--sidebar-ink-muted)] hover:bg-[var(--sidebar-hover)] hover:text-white",
        expanded ? "px-3" : "justify-center px-0",
      ].join(" ")}
      style={
        active
          ? { backgroundColor: "color-mix(in srgb,var(--primary) 20%,transparent)" }
          : undefined
      }
    >
      {active && (
        <span className="absolute left-0 top-1/2 h-6 w-[3px] -translate-y-1/2 rounded-r-full bg-[var(--primary)]" />
      )}
      <Icon className="h-[21px] w-[21px] shrink-0" />
      {expanded && <span>{item.name}</span>}
    </Link>
  );
}

function currentScreenName(menu: MenuGroup[], pathname: string): string | null {
  for (const group of menu) {
    for (const item of group.items) {
      if (pathname === item.route || pathname.startsWith(`${item.route}/`)) {
        return item.name;
      }
    }
  }

  return null;
}
