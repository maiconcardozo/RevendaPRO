import { NextResponse } from "next/server";
import { cookies } from "next/headers";
import { ACCESS_COOKIE, INTERNAL_API_URL, REFRESH_COOKIE } from "@/lib/config";

/** Revokes the refresh tokens on the API and clears the browser cookies. */
export async function POST() {
  const jar = await cookies();
  const token = jar.get(ACCESS_COOKIE)?.value;

  if (token) {
    await fetch(`${INTERNAL_API_URL}/api/auth/logout`, {
      method: "POST",
      headers: { Authorization: `Bearer ${token}` },
      cache: "no-store",
    }).catch(() => undefined);
  }

  const output = NextResponse.json({ ok: true });
  output.cookies.delete(ACCESS_COOKIE);
  output.cookies.delete(REFRESH_COOKIE);

  return output;
}
