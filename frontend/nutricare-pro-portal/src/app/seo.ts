import type { Metadata } from "next";

export const siteUrl = process.env.NEXT_PUBLIC_SITE_URL ?? "https://www.nutricarepro.com";

export const site = {
  name: "NutriCare Pro",
  title: "NutriCare Pro | Premium Nutrition Consultation Platform",
  description:
    "Premium healthcare platform for nutrition consultations, appointment booking, personalized diet plans, medical report review, progress tracking, secure chat, and video care.",
  keywords: [
    "nutrition consultation",
    "online nutritionist",
    "diet plan platform",
    "healthcare appointment booking",
    "medical report review",
    "patient progress tracking",
    "video nutrition consultation"
  ],
  image: `${siteUrl}/opengraph-image`
};

export function pageMetadata({
  title,
  description,
  path,
  keywords = []
}: {
  title: string;
  description: string;
  path: string;
  keywords?: string[];
}): Metadata {
  const url = `${siteUrl}${path}`;

  return {
    title,
    description,
    keywords: [...site.keywords, ...keywords],
    alternates: {
      canonical: url
    },
    openGraph: {
      title,
      description,
      url,
      siteName: site.name,
      images: [
        {
          url: site.image,
          width: 1200,
          height: 630,
          alt: `${site.name} premium nutrition consultation platform`
        }
      ],
      locale: "en_US",
      type: "website"
    },
    twitter: {
      card: "summary_large_image",
      title,
      description,
      images: [site.image]
    }
  };
}

export function noIndexMetadata(title: string): Metadata {
  return {
    title,
    robots: {
      index: false,
      follow: false,
      googleBot: {
        index: false,
        follow: false
      }
    }
  };
}
