import { redirect } from "next/navigation";
import { firstAllowedRoute, requireSession } from "@/lib/session";

/**
 * The root sends the user to the first allowed route, not to /dashboard: a role may not
 * include the dashboard. See ADR-0002.
 */
export default async function Root() {
  const session = await requireSession();

  redirect(firstAllowedRoute(session));
}
