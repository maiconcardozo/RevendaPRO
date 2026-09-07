import { SuppliersView } from "@/components/suppliers/SuppliersView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { Supplier, SupplierSegment } from "@/lib/types";

export default async function SuppliersPage() {
  await requireScreen("suppliers");

  const [suppliers, segments] = await Promise.all([
    fetchFromApi<Supplier[]>("suppliers"),
    fetchFromApi<SupplierSegment[]>("supplier-segments"),
  ]);

  return <SuppliersView initialSuppliers={suppliers} initialSegments={segments} />;
}
