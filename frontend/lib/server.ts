import { cookies } from "next/headers";
import { ACCESS_COOKIE, INTERNAL_API_URL } from "./config";

/** Authenticated GET against the API, for use in server components. */
export async function fetchFromApi<T>(path: string): Promise<T> {
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;

  const response = await fetch(`${INTERNAL_API_URL}/api/${path}`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (!response.ok) {
    throw new Error(`GET /api/${path} responded ${response.status}`);
  }

  const body = await response.json();
  return body.data as T;
}
