import type { ReactNode } from "react";
import { PanelShell } from "@/components/layout/PanelShell";
import { requireSession } from "@/lib/session";

export default async function PanelLayout({ children }: { children: ReactNode }) {
  const session = await requireSession();

  return <PanelShell session={session}>{children}</PanelShell>;
}
