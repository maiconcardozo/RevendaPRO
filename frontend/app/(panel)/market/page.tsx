import { MarketView } from "@/components/market/MarketView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { MarketOverview } from "@/lib/types";

export default async function MarketPage() {
  await requireScreen("market");

  const overview = await fetchFromApi<MarketOverview>("market");

  return <MarketView overview={overview} />;
}
