import type { Metadata } from "next";
import "./globals.css";
import { site, siteUrl } from "./seo";
import { SessionProvider } from "@/components/SessionProvider";
import { LocaleProvider } from "@/components/LocaleProvider";
import { cookies } from "next/headers";

export const metadata: Metadata = {
  metadataBase: new URL(siteUrl),
  applicationName: site.name,
  title: {
    default: site.title,
    template: `%s | ${site.name}`
  },
  description: site.description,
  keywords: site.keywords,
  authors: [{ name: site.name }],
  creator: site.name,
  publisher: site.name,
  category: "Healthcare",
  alternates: {
    canonical: siteUrl
  },
  openGraph: {
    title: site.title,
    description: site.description,
    url: siteUrl,
    siteName: site.name,
    images: [
      {
        url: site.image,
        width: 1200,
        height: 630,
        alt: `${site.name} nutrition consultation platform`
      }
    ],
    locale: "en_US",
    type: "website"
  },
  twitter: {
    card: "summary_large_image",
    title: site.title,
    description: site.description,
    images: [site.image]
  },
  robots: {
    index: true,
    follow: true,
    googleBot: {
      index: true,
      follow: true,
      "max-image-preview": "large",
      "max-snippet": -1,
      "max-video-preview": -1
    }
  }
};

export default async function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const locale = (await cookies()).get("nc_locale")?.value === "ur" ? "ur" : "en";
  return (
    <html lang={locale} dir={locale === "ur" ? "rtl" : "ltr"}>
      <body><a className="skip-link" href="#main-content">{locale === "ur" ? "مواد پر جائیں" : "Skip to content"}</a><LocaleProvider initialLocale={locale}><SessionProvider>{children}</SessionProvider></LocaleProvider></body>
    </html>
  );
}
