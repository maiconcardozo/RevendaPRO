import { TrashView } from "@/components/trash/TrashView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import { TRASH_KIND, type DeletedItem } from "@/lib/types";

/**
 * A lixeira (M23).
 *
 * A rota e a chave de permissão continuam sendo as de "documentos excluídos", que é o nome que
 * a tela tinha quando só mostrava documento. Trocar a chave faria o sincronizador desativar a
 * tela antiga e criar outra, e toda revenda que já concedeu a permissão a alguém a perderia sem
 * saber; trocar a rota quebraria o endereço que já está salvo em algum navegador. O nome
 * interno envelhecido é o preço menor. Ver `docs/plans/m23-lixeira.md`.
 */
export default async function TrashPage() {
  await requireScreen("deleted-documents");

  const vehicles = await fetchFromApi<DeletedItem[]>(`trash?kind=${TRASH_KIND.vehicle}`);

  return <TrashView initialVehicles={vehicles} />;
}
