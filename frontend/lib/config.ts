/**
 * URL of the API as seen by the Next SERVER (route handlers and server components).
 * Inside the compose network the Next server talks to the API directly; the browser never
 * calls the API, it always goes through /api/backend on this same host.
 */
export const INTERNAL_API_URL = process.env.INTERNAL_API_URL ?? "http://localhost:5100";

export const ACCESS_COOKIE = "rp_access";
export const REFRESH_COOKIE = "rp_refresh";

/**
 * `Secure` flag on the session cookies. Production is served over HTTPS and needs it.
 *
 * The local-network server is served over plain HTTP, and there a `Secure` cookie is silently
 * dropped by the browser: the login returns 200 and the person lands back on the login screen
 * with no error at all. Hence the flag is configurable, with the safe default — only a
 * deployment that knowingly serves HTTP sets `COOKIE_SECURE=false`.
 */
export const COOKIE_SECURE = process.env.COOKIE_SECURE
  ? process.env.COOKIE_SECURE !== "false"
  : process.env.NODE_ENV === "production";
