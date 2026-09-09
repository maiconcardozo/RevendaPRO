import { CashflowView } from "@/components/cashflow/CashflowView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { Cashflow, ExpenseType, StoreExpense, Supplier } from "@/lib/types";

/**
 * Caixa: o que vence, o que entrou, o que atrasou (M22).
 *
 * Duas abas — o resumo, com as três origens do dinheiro, e as despesas da loja, que é onde o
 * aluguel e a energia são lançados. Os tipos vêm filtrados pelo escopo da loja: Funilaria
 * jamais aparece na lista do aluguel.
 */
export default async function CashflowPage() {
  await requireScreen("cashflow");

  const [cashflow, expenses, types, suppliers] = await Promise.all([
    fetchFromApi<Cashflow>("cashflow"),
    fetchFromApi<StoreExpense[]>("store-expenses"),
    fetchFromApi<ExpenseType[]>("expense-types?scope=2"),
    fetchFromApi<Supplier[]>("suppliers"),
  ]);

  return (
    <CashflowView
      initialCashflow={cashflow}
      initialExpenses={expenses}
      initialTypes={types}
      initialSuppliers={suppliers}
    />
  );
}
