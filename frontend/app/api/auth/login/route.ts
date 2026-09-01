import { NextResponse } from "next/server";
import { ACCESS_COOKIE, INTERNAL_API_URL, REFRESH_COOKIE } from "@/lib/config";

/**
 * Takes the credentials from the form, authenticates against the API and stores the tokens
 * in httpOnly cookies. The token never reaches the browser JavaScript.
 */
export async function POST(request: Request) {
  const { email, password } = await request.json();

  let response: Response;

  try {
    response = await fetch(`${INTERNAL_API_URL}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
      cache: "no-store",
    });
  } catch {
    return NextResponse.json(
      { message: "Servidor indisponível. Tente novamente." },
      { status: 503 },
    );
  }

  const body = await response.json().catch(() => null);

  if (!response.ok) {
    return NextResponse.json(
      { message: body?.detail ?? "E-mail ou senha inválidos." },
      { status: response.status },
    );
  }

  const { tokens } = body.data;
  const output = NextResponse.json({ ok: true });
  const secure = process.env.NODE_ENV === "production";

  output.cookies.set(ACCESS_COOKIE, tokens.accessToken, {
    httpOnly: true,
    sameSite: "lax",
    secure,
    path: "/",
    expires: new Date(tokens.accessTokenExpiresAt),
  });

  output.cookies.set(REFRESH_COOKIE, tokens.refreshToken, {
    httpOnly: true,
    sameSite: "lax",
    secure,
    path: "/",
    expires: new Date(tokens.refreshTokenExpiresAt),
  });

  return output;
}
