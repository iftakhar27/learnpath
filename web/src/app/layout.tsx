import type { Metadata, Viewport } from "next";
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
    <html lang="en">
      <body className="min-h-dvh">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
