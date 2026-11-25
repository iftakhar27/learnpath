import type { Metadata, Viewport } from "next";
import Script from "next/script";
import { themeBootScript } from "@/lib/theme-boot";
import { Providers } from "./providers";
import "./globals.css";

const brand = process.env.NEXT_PUBLIC_BRAND_NAME ?? "LearnPath";

export const metadata: Metadata = {
  title: { default: brand, template: `%s · ${brand}` },
  description: "Exam preparation for NEET, JEE and Boards.",
  applicationName: brand,
  // Added to an iPhone's home screen, the site opens full screen like an app.
  appleWebApp: { capable: true, title: brand, statusBarStyle: "default" },
  formatDetection: { telephone: false },
  // Older iPhones (before iOS 16.4) read this instead of the manifest's display mode.
  other: { "apple-mobile-web-app-capable": "yes" },
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  // Draw to the screen edges; the shell pads for the notch and home bar (env(safe-area-inset-*)).
  viewportFit: "cover",
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#ffffff" },
    { media: "(prefers-color-scheme: dark)", color: "#1a1b26" },
  ],
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    // data-theme (night mode) is set by the boot script before React hydrates.
    <html lang="en" suppressHydrationWarning>
      <body className="min-h-dvh">
        <Script id="theme-boot" strategy="beforeInteractive">
          {themeBootScript}
        </Script>
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
