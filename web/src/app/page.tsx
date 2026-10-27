import Link from "next/link";

const brand = process.env.NEXT_PUBLIC_BRAND_NAME ?? "LearnPath";

/** Placeholder until the student web app (Phase 1c) lands. */
export default function Home() {
  return (
    <main className="mx-auto flex min-h-dvh max-w-xl flex-col justify-center gap-4 px-4">
      <h1 className="text-3xl font-semibold">{brand}</h1>
      <p className="text-fg-muted">The student website is coming soon. Staff can sign in to the admin portal.</p>
      <Link href="/admin" className="font-medium text-primary hover:underline">
        Go to the admin portal →
      </Link>
    </main>
  );
}
