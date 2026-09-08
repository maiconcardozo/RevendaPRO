import { SuppliersView } from "@/components/suppliers/SuppliersView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { Supplier, SupplierSegment, SupplierStatistics } from "@/lib/types";

/**
 * Fornecedores, e quanto já foi para cada um.
 *
 * O painel em cima e o cadastro embaixo, na mesma tela: quem cadastra o fornecedor é quem
 * quer saber quanto foi para ele. O painel chega "desde o início"; o período é escolhido na tela.
 */
export default async function SuppliersPage() {
  await requireScreen("suppliers");

  const [suppliers, segments, statistics] = await Promise.all([
    fetchFromApi<Supplier[]>("suppliers"),
    fetchFromApi<SupplierSegment[]>("supplier-segments"),
    fetchFromApi<SupplierStatistics>("suppliers/statistics"),
  ]);

  return (
    <SuppliersView
      initialSuppliers={suppliers}
      initialSegments={segments}
      initialStatistics={statistics}
    />
  );
}
