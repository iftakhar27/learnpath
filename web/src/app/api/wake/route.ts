import { connection } from "next/server";
import { API_BASE_URL } from "@/lib/server/session";

/**
 * Where the browser can wake the API. On free hosting a sleeping API is woken by outside requests, but not by calls
 * from this web server, so the visitor's browser pings it directly (see lib/api/warm-up.ts). The API's address is
 * public anyway (the mobile app uses it).
 */
export async function GET() {
  // Answer per request (not prerendered at build time), so the address is the server's runtime setting.
  await connection();
  return Response.json({ url: `${API_BASE_URL}/health` }, { headers: { "cache-control": "no-store" } });
}
