"use client";
import Link from "next/link";
import { MarketingShell } from "./components/MarketingShell";
import { ServiceCatalog } from "@/components/PublicContent";
import { useLocale } from "@/components/LocaleProvider";
export default function Page(){const {locale,t}=useLocale();return <MarketingShell><section className="subpage-hero"><div><span>NutriCare Pro</span><h1>{locale==="ur"?"غذائیت کی بہتر سمجھ، بہتر معمولات":"Nutrition care, built around you."}</h1><p>{locale==="ur"?"مشاورت کے اوقات دیکھیں، اپنے غذائی منصوبے تک رسائی حاصل کریں اور پیش رفت ایک جگہ محفوظ رکھیں۔":"Explore available consultations, access your nutrition plan, and keep your progress in one place."}</p><Link className="marketing-cta" href="/book">{t("Book a consultation")}</Link></div></section><section className="marketing-section"><h2>{t("Services")}</h2><ServiceCatalog/></section><section className="marketing-band"><div><h2>{t("Contact support")}</h2><p>{t("This service is not for urgent or emergency care.")}</p></div><Link className="marketing-cta" href="/contact">{t("Contact")}</Link></section></MarketingShell>;}
