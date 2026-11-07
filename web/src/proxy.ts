import { NextResponse, type NextRequest } from "next/server";

/**
 * Navigation gate only: sends visitors without a session to the right login page before a protected page renders,
 * and signed-in visitors from the landing/login pages to their home. Every API call is still authorised by the API.
 */
const STUDENT_AREAS = ["/home", "/onboarding", "/learn", "/topics", "/watch", "/practice", "/tests", "/exam", "/results", "/plan", "/progress", "/profile", "/notifications", "/bookmarks", "/purchases", "/support", "/search", "/doubts"];
const STUDENT_PUBLIC = ["/", "/login", "/register"];

export function proxy(request: NextRequest) {
  const { pathname, search, searchParams } = request.nextUrl;
  const signedIn = request.cookies.has("lp_rt");
  const redirect = (path: string, next?: string) => {
    const url = new URL(path, request.url);
    if (next) url.searchParams.set("next", next);
    return NextResponse.redirect(url);
  };

  if (pathname.startsWith("/admin")) {
    if (pathname === "/admin/login" || signedIn) return NextResponse.next();
    return redirect("/admin/login", pathname + search);
  }
  if (STUDENT_PUBLIC.includes(pathname)) {
    // "expired"/"replaced" mean the API just rejected the session; let the visitor sign in again instead of bouncing.
    return signedIn && !searchParams.has("expired") && !searchParams.has("replaced") ? redirect("/home") : NextResponse.next();
  }
  if (!signedIn && STUDENT_AREAS.some((a) => pathname === a || pathname.startsWith(a + "/"))) return redirect("/login", pathname + search);
  return NextResponse.next();
}

// Must be literal strings (read at build time); keep in step with STUDENT_AREAS.
export const config = {
  matcher: [
    "/",
    "/login",
    "/register",
    "/admin/:path*",
    "/home/:path*",
    "/onboarding/:path*",
    "/learn/:path*",
    "/topics/:path*",
    "/watch/:path*",
    "/practice/:path*",
    "/tests/:path*",
    "/exam/:path*",
    "/results/:path*",
    "/plan/:path*",
    "/progress/:path*",
    "/profile/:path*",
    "/notifications/:path*",
    "/bookmarks/:path*",
    "/purchases/:path*",
    "/support/:path*",
    "/doubts/:path*",
    "/search/:path*",
  ],
};
