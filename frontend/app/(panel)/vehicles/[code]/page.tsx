import { notFound } from "next/navigation";
import { VehicleDetail } from "@/components/vehicles/VehicleDetail";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { ExpenseType, Vehicle, VehicleExpense } from "@/lib/types";

/**
 * A ficha de um veículo.
 *
 * Veículo, gastos e tipos vêm do servidor, porque são o que a tela mostra na primeira pintura.
 * Fotos, documentos e histórico ficam atrás de abas e carregam quando alguém abre a aba — não
 * faz sentido esperar por vinte endereços assinados para mostrar uma lista de gastos.
 */
export default async function VehiclePage({
  params,
}: {
  params: Promise<{ code: string }>;
}) {
  const session = await requireScreen("vehicles");

  const { code } = await params;

  const vehicle = await fetchFromApi<Vehicle>(`vehicles/${code}`).catch(() => null);

  if (!vehicle) {
    notFound();
  }

  const [expenses, types] = await Promise.all([
    fetchFromApi<VehicleExpense[]>(`vehicles/${code}/expenses`).catch(
      () => [] as VehicleExpense[],
    ),
    fetchFromApi<ExpenseType[]>("expense-types").catch(() => [] as ExpenseType[]),
  ]);

  return (
    <VehicleDetail
      initialVehicle={vehicle}
      initialExpenses={expenses}
      types={types}
      maxUploadSize={session.limits.maxUploadSizeInBytes}
      canSell={session.screens.includes("sales")}
    />
  );
}
