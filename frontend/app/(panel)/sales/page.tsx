import { SalesView } from "@/components/sales/SalesView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { SaleListing } from "@/lib/types";

/** First day of the current month, the period the screen opens on. */
function monthStart(): string {
  const now = new Date();

  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`;
}

export default async function SalesPage() {
  await requireScreen("sales");

  const sales = await fetchFromApi<SaleListing[]>(`sales?from=${monthStart()}`);

  return <SalesView initialSales={sales} />;
}
