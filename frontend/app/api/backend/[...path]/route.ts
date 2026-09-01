import { NextResponse } from "next/server";
import { cookies } from "next/headers";
import { ACCESS_COOKIE, INTERNAL_API_URL } from "@/lib/config";

/** Headers set by the client that must reach the API unchanged. */
const CLIENT_HEADERS = ["content-type", "accept"];

/** Response headers that must reach the browser. */
const API_HEADERS = ["content-type", "content-disposition", "cache-control"];

/**
 * Bridge between the client components and the API.
 *
 * The browser calls /api/backend/... and this handler injects the Bearer from the httpOnly
 * cookie, so no token circulates in the page JavaScript. The API stays the only owner of
 * authorization: if the role lacks the screen it answers 403, and the 403 arrives here with
 * no special handling.
 *
 * Body and response travel as bytes, not text: the same path serves JSON, a multipart
 * upload and an image download without corrupting any of them.
 */
async function forward(request: Request, path: string[]) {
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;

  if (!token) {
    return NextResponse.json({ detail: "Sessão expirada." }, { status: 401 });
  }

  const url = new URL(request.url);
  const target = `${INTERNAL_API_URL}/api/${path.join("/")}${url.search}`;

  const headers = new Headers({ Authorization: `Bearer ${token}` });

  for (const name of CLIENT_HEADERS) {
    const value = request.headers.get(name);

    if (value) {
      headers.set(name, value);
    }
  }

  const withoutBody = request.method === "GET" || request.method === "HEAD";
  const body = withoutBody ? undefined : await request.arrayBuffer();

  const response = await fetch(target, {
    method: request.method,
    headers,
    body: body && body.byteLength > 0 ? body : undefined,
    cache: "no-store",
  });

  if (response.status === 204 || response.status === 304) {
    return new NextResponse(null, { status: response.status });
  }

  const output = new Headers();

  for (const name of API_HEADERS) {
    const value = response.headers.get(name);

    if (value) {
      output.set(name, value);
    }
  }

  return new NextResponse(await response.arrayBuffer(), {
    status: response.status,
    headers: output,
  });
}

type Context = { params: Promise<{ path: string[] }> };

export async function GET(request: Request, { params }: Context) {
  return forward(request, (await params).path);
}

export async function POST(request: Request, { params }: Context) {
  return forward(request, (await params).path);
}

export async function PUT(request: Request, { params }: Context) {
  return forward(request, (await params).path);
}

export async function PATCH(request: Request, { params }: Context) {
  return forward(request, (await params).path);
}

export async function DELETE(request: Request, { params }: Context) {
  return forward(request, (await params).path);
}
