import { NextResponse, type NextRequest } from "next/server";

/**
 * Sends visitors without a session to the admin login page before any admin page renders.
 * This is only a gate for navigation; every API call is still authorised by the backend.
 */
export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  if (pathname === "/admin/login") return NextResponse.next();
  if (!request.cookies.has("lp_rt")) {
    const login = new URL("/admin/login", request.url);
    login.searchParams.set("next", pathname + search);
    return NextResponse.redirect(login);
  }
  return NextResponse.next();
}

export const config = {
  matcher: ["/admin", "/admin/:path*"],
};
