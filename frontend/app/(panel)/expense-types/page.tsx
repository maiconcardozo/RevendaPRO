import { ExpenseTypesView } from "@/components/vehicles/ExpenseTypesView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { ExpenseType } from "@/lib/types";

export default async function ExpenseTypesPage() {
  await requireScreen("expense-types");

  const types = await fetchFromApi<ExpenseType[]>("expense-types");

  return <ExpenseTypesView initialTypes={types} />;
}
