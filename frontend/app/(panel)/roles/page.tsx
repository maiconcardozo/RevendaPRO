import { RolesView } from "@/components/roles/RolesView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { Role, ScreenGroup } from "@/lib/types";

export default async function RolesPage() {
  await requireScreen("roles");

  const [roles, catalog] = await Promise.all([
    fetchFromApi<Role[]>("roles"),
    fetchFromApi<ScreenGroup[]>("screens"),
  ]);

  return <RolesView initialRoles={roles} catalog={catalog} />;
}
