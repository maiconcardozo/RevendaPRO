import { CustomersView } from "@/components/customers/CustomersView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { Customer } from "@/lib/types";

/**
 * Clientes: quem ofereceu, quem comprou, quem volta (M21).
 *
 * A lista chega inteira e a busca refina no servidor. `?open=<código>` abre a ficha de um
 * cliente ao chegar — é como a lista de vendas e o card da proposta chegam aqui.
 */
export default async function CustomersPage({
  searchParams,
}: {
  searchParams: Promise<{ open?: string }>;
}) {
  await requireScreen("customers");

  const [customers, params] = await Promise.all([
    fetchFromApi<Customer[]>("customers"),
    searchParams,
  ]);

  return <CustomersView initialCustomers={customers} openCode={params.open ?? null} />;
}
