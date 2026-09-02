import { DashboardView } from "@/components/dashboard/DashboardView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { Dashboard } from "@/lib/types";

/** First day of the current month, the period the dashboard opens on. */
function monthStart(): string {
  const now = new Date();

  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`;
}

export default async function DashboardPage() {
  const session = await requireScreen("dashboard");

  const dashboard = await fetchFromApi<Dashboard>(`dashboard?from=${monthStart()}`);

  return (
    <DashboardView firstName={session.user.name.split(" ")[0]} initial={dashboard} />
  );
}
