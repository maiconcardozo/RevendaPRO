import { YardsView } from "@/components/yards/YardsView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { Yard } from "@/lib/types";

export default async function YardsPage() {
  await requireScreen("yards");

  const yards = await fetchFromApi<Yard[]>("yards");

  return <YardsView initialYards={yards} />;
}
