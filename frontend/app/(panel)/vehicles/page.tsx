import { VehiclesView } from "@/components/vehicles/VehiclesView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { Vehicle } from "@/lib/types";

export default async function VehiclesPage() {
  await requireScreen("vehicles");

  const vehicles = await fetchFromApi<Vehicle[]>("vehicles");

  return <VehiclesView initialVehicles={vehicles} />;
}
