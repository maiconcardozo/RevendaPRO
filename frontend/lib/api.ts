/**
 * Calls to the API from the browser.
 *
 * Always through `/api/backend/...`, the Next handler that injects the Bearer from the
 * httpOnly cookie. No token ever circulates in the page JavaScript.
 *
 * The reason this file exists is the error message. Every screen repeated the same block to
 * read the `detail` out of ProblemDetails, and whenever one forgot, the person read "Falha ao
 * salvar" instead of "A placa ABC1D23 já pertence a outro veículo" — which is the sentence
 * that actually solves the problem.
 */

/** What a call answers: either the data, or the sentence to put on the screen. */
export type Result<T> = { ok: true; data: T } | { ok: false; error: string };

/**
 * Reads the sentence the API sent.
 *
 * The backend answers ProblemDetails with `detail` in Portuguese, and with `errors` per field
 * when validation failed. A per field message is more useful than a general one, so it wins.
 */
async function messageOf(response: Response, fallback: string): Promise<string> {
  const problem = await response.json().catch(() => null);

  if (problem?.errors) {
    const byField = Object.values(problem.errors as Record<string, string[]>)
      .flat()
      .join(" ");

    if (byField) return byField;
  }

  return problem?.detail ?? fallback;
}

/** GET, never cached: a stored answer would show the state from before the last change. */
export async function apiGet<T>(path: string, fallback: string): Promise<Result<T>> {
  const response = await fetch(`/api/backend/${path}`, { cache: "no-store" });

  if (!response.ok) {
    return { ok: false, error: await messageOf(response, fallback) };
  }

  return { ok: true, data: (await response.json()).data as T };
}

/** POST, PUT, PATCH or DELETE with a JSON body. */
export async function apiSend<T>(
  method: "POST" | "PUT" | "PATCH" | "DELETE",
  path: string,
  fallback: string,
  body?: unknown,
): Promise<Result<T>> {
  const response = await fetch(`/api/backend/${path}`, {
    method,
    headers: body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    return { ok: false, error: await messageOf(response, fallback) };
  }

  // 204 is what the state changing operations answer, and they return nothing.
  if (response.status === 204) {
    return { ok: true, data: undefined as T };
  }

  return { ok: true, data: (await response.json()).data as T };
}

/**
 * File upload, multipart.
 *
 * The `Content-Type` is left out on purpose: the browser builds that header itself, because
 * it has to append the `boundary`. Writing it by hand breaks the upload.
 */
export async function apiUpload<T>(
  path: string,
  file: File,
  fields: Record<string, string>,
  fallback: string,
): Promise<Result<T>> {
  const body = new FormData();
  body.append("file", file);

  for (const [name, value] of Object.entries(fields)) {
    body.append(name, value);
  }

  const response = await fetch(`/api/backend/${path}`, { method: "POST", body });

  if (!response.ok) {
    return { ok: false, error: await messageOf(response, fallback) };
  }

  return { ok: true, data: (await response.json()).data as T };
}
