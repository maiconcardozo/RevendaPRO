import { StoreExpensesView } from "@/components/cashflow/StoreExpensesView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { ExpenseType, StoreExpense, Supplier } from "@/lib/types";

/**
 * Caixa: o que a loja paga e que jamais pertence a um carro (M22).
 *
 * A lista chega do mês corrente, e o período é escolhido na tela. Os tipos vêm filtrados pelo
 * escopo da loja: Funilaria jamais aparece na lista do aluguel.
 */
export default async function CashflowPage() {
  await requireScreen("cashflow");

  const [expenses, types, suppliers] = await Promise.all([
    fetchFromApi<StoreExpense[]>("store-expenses"),
    fetchFromApi<ExpenseType[]>("expense-types?scope=2"),
    fetchFromApi<Supplier[]>("suppliers"),
  ]);

  return (
    <StoreExpensesView
      initialExpenses={expenses}
      initialTypes={types}
      initialSuppliers={suppliers}
    />
  );
}
