import { NextResponse, type NextRequest } from "next/server";
import { INTERNAL_API_URL, ACCESS_COOKIE, REFRESH_COOKIE } from "@/lib/config";

/** Le o exp do JWT sem validar assinatura: serve so para decidir a renovacao. */
function isExpired(token: string): boolean {
  try {
    const payload = JSON.parse(
      Buffer.from(token.split(".")[1], "base64").toString("utf8"),
    );

    // 30s de folga para nao mandar um token que expira no meio do request.
    return payload.exp * 1000 < Date.now() + 30_000;
  } catch {
    return true;
  }
}

async function renew(refreshToken: string) {
  const response = await fetch(`${INTERNAL_API_URL}/api/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
    cache: "no-store",
  });

  if (!response.ok) {
    return null;
  }

  const corpo = await response.json();
  return corpo.data.tokens as {
    accessToken: string;
    accessTokenExpiresAt: string;
    refreshToken: string;
    refreshTokenExpiresAt: string;
  };
}

export async function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const onLoginPage = pathname === "/login";

  let access = request.cookies.get(ACCESS_COOKIE)?.value;
  const refresh = request.cookies.get(REFRESH_COOKIE)?.value;

  let newTokens: Awaited<ReturnType<typeof renew>> = null;

  // Access vencido mas refresh valido: renova de forma transparente.
  if ((!access || isExpired(access)) && refresh) {
    newTokens = await renew(refresh);
    access = newTokens?.accessToken;
  }

  const isAuthenticated = Boolean(access) && !isExpired(access!);

  if (!isAuthenticated && !onLoginPage) {
    const target = new URL("/login", request.url);

    if (pathname !== "/") {
      target.searchParams.set("from", pathname);
    }

    const output = NextResponse.redirect(target);
    output.cookies.delete(ACCESS_COOKIE);
    output.cookies.delete(REFRESH_COOKIE);

    return output;
  }

  const response =
    isAuthenticated && onLoginPage
      ? NextResponse.redirect(new URL("/", request.url))
      : NextResponse.next();

  if (newTokens) {
    const secure = process.env.NODE_ENV === "production";

    response.cookies.set(ACCESS_COOKIE, newTokens.accessToken, {
      httpOnly: true,
      sameSite: "lax",
      secure: secure,
      path: "/",
      expires: new Date(newTokens.accessTokenExpiresAt),
    });

    response.cookies.set(REFRESH_COOKIE, newTokens.refreshToken, {
      httpOnly: true,
      sameSite: "lax",
      secure: secure,
      path: "/",
      expires: new Date(newTokens.refreshTokenExpiresAt),
    });
  }

  return response;
}

// Proxy sempre roda no runtime Node.js: config de segmento nao e permitida aqui.
export const config = {
  matcher: ["/((?!api|_next/static|_next/image|favicon.ico|.*\\.svg).*)"],
};
