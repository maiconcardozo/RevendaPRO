import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { ACCESS_COOKIE, INTERNAL_API_URL } from "./config";
import type { Session } from "./types";

/**
 * Loads the session from the API. The menu arrives ready from the server: the frontend
 * never receives the full screen catalog to hide items on the client.
 */
export async function getSession(): Promise<Session | null> {
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;

  if (!token) {
    return null;
  }

  const response = await fetch(`${INTERNAL_API_URL}/api/auth/me`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (!response.ok) {
    return null;
  }

  const body = await response.json();
  return body.data as Session;
}

/** Session is mandatory. Without one, back to the sign in page. */
export async function requireSession(): Promise<Session> {
  const session = await getSession();

  if (!session) {
    redirect("/login");
  }

  return session;
}

/**
 * Route guard on the server. Typing a route without holding the screen lands on
 * /no-permission — and the API blocks it again on its own.
 */
export async function requireScreen(key: string): Promise<Session> {
  const session = await requireSession();

  if (!session.screens.includes(key)) {
    redirect(`/no-permission?screen=${encodeURIComponent(key)}`);
  }

  return session;
}

/** First route the user can reach. A role may not include the dashboard. */
export function firstAllowedRoute(session: Session): string {
  return session.menu[0]?.items[0]?.route ?? "/no-access";
}
