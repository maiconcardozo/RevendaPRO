import { notFound } from "next/navigation";
import { VehicleDetail } from "@/components/vehicles/VehicleDetail";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { ExpenseType, Supplier, Vehicle, VehicleExpense, Yard } from "@/lib/types";

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

  const [expenses, types, suppliers, yards] = await Promise.all([
    fetchFromApi<VehicleExpense[]>(`vehicles/${code}/expenses`).catch(
      () => [] as VehicleExpense[],
    ),
    fetchFromApi<ExpenseType[]>("expense-types").catch(() => [] as ExpenseType[]),

    // A lista de fornecedores chega para quem registra gasto, mesmo sem poder cadastrá-los.
    fetchFromApi<Supplier[]>("suppliers").catch(() => [] as Supplier[]),

    // Só para quem tem a tela de pátios. Sem ela o botão de mudar de lugar some, e onde o
    // carro está continua na ficha — ler é informação, mover é decisão.
    session.screens.includes("yards")
      ? fetchFromApi<Yard[]>("yards").catch(() => [] as Yard[])
      : Promise.resolve([] as Yard[]),
  ]);

  return (
    <VehicleDetail
      initialVehicle={vehicle}
      initialExpenses={expenses}
      types={types}
      suppliers={suppliers}
      maxUploadSize={session.limits.maxUploadSizeInBytes}
      canSell={session.screens.includes("sales")}
      yards={yards}
    />
  );
}
