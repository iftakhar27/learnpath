import type { MetadataRoute } from "next";

const brand = process.env.NEXT_PUBLIC_BRAND_NAME ?? "LearnPath";

/** Lets phones install the website as an app (Add to Home Screen): own icon, full screen, opens at Home. */
export default function manifest(): MetadataRoute.Manifest {
  return {
    id: "/",
    name: brand,
    short_name: brand,
    description: "Exam preparation for NEET, JEE and Boards.",
    start_url: "/home",
    scope: "/",
    display: "standalone",
    orientation: "portrait",
    background_color: "#f7f7fb",
    theme_color: "#4f46e5",
    lang: "en",
    categories: ["education"],
    icons: [
      { src: "/icons/icon-192.png", sizes: "192x192", type: "image/png", purpose: "any" },
      { src: "/icons/icon-512.png", sizes: "512x512", type: "image/png", purpose: "any" },
      { src: "/icons/icon-maskable-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
    ],
  };
}
