/**
 * URL of the API as seen by the Next SERVER (route handlers and server components).
 * Inside the compose network the Next server talks to the API directly; the browser never
 * calls the API, it always goes through /api/backend on this same host.
 */
export const INTERNAL_API_URL = process.env.INTERNAL_API_URL ?? "http://localhost:5100";

export const ACCESS_COOKIE = "rp_access";
export const REFRESH_COOKIE = "rp_refresh";
