import { DeletedDocumentsView } from "@/components/vehicles/DeletedDocumentsView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { DeletedDocument } from "@/lib/types";

export default async function DeletedDocumentsPage() {
  await requireScreen("deleted-documents");

  const documents = await fetchFromApi<DeletedDocument[]>("deleted-documents");

  return <DeletedDocumentsView initialDocuments={documents} />;
}
