import { VehiclesView } from "@/components/vehicles/VehiclesView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { Vehicle, Yard } from "@/lib/types";

export default async function VehiclesPage() {
  const session = await requireScreen("vehicles");

  const vehicles = await fetchFromApi<Vehicle[]>("vehicles");

  // Só para quem tem a tela de pátios, e mesmo assim a API confere de novo: sem a permissão a
  // lista vem vazia e a escolha de pátio simplesmente não aparece no cadastro.
  const yards = session.screens.includes("yards")
    ? await fetchFromApi<Yard[]>("yards").catch(() => [] as Yard[])
    : [];

  return <VehiclesView initialVehicles={vehicles} yards={yards} />;
}
