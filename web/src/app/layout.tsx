import type { Metadata } from "next";
import { Providers } from "./providers";
import "./globals.css";

const brand = process.env.NEXT_PUBLIC_BRAND_NAME ?? "LearnPath";

export const metadata: Metadata = {
  title: { default: brand, template: `%s · ${brand}` },
  description: "Exam preparation for NEET, JEE and Boards.",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body className="min-h-dvh">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
