import { CompanyView } from "@/components/company/CompanyView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { Company } from "@/lib/types";

export default async function CompanyPage() {
  await requireScreen("company");

  const company = await fetchFromApi<Company>("company");

  return <CompanyView initial={company} />;
}
