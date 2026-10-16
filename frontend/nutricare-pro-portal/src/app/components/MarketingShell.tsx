"use client";
import Link from "next/link";
import { CalendarCheck, HeartPulse } from "lucide-react";
import type { ReactNode } from "react";
import { LanguageSwitch, useLocale } from "@/components/LocaleProvider";

const navItems = [
  { href: "/", label: "Home" },
  { href: "/about", label: "About" },
  { href: "/services", label: "Services" },
  { href: "/fees", label: "Fees" },
  { href: "/faqs", label: "FAQs" },
  { href: "/blogs", label: "Blogs" },
  { href: "/contact", label: "Contact" }
];

export function MarketingShell({ children }: { children: ReactNode }) {
  const { t } = useLocale();
  return (
    <main className="marketing-page" id="main-content">
      <header className="marketing-nav">
        <Link className="marketing-brand" href="/">
          <span>
            <HeartPulse size={22} />
          </span>
          NutriCare Pro
        </Link>
        <nav>
          {navItems.map((item) => (
            <Link href={item.href} key={item.href}>
              {t(item.label)}
            </Link>
          ))}
        </nav>
        <LanguageSwitch />
        <Link href="/login">{t("Sign in")}</Link>
        <Link className="marketing-cta small" href="/book">
          <CalendarCheck size={16} />
          {t("Book")}
        </Link>
      </header>
      {children}
      <footer className="marketing-footer">
        <div>
          <strong>NutriCare Pro</strong>
          <p>{t("This service is not for urgent or emergency care.")}</p>
        </div>
        <div className="footer-links">
          <Link href="/policies">{t("Privacy & policies")}</Link>
          <Link href="/contact">{t("Contact support")}</Link>
          <Link href="/login">{t("Sign in")}</Link>
        </div>
      </footer>
    </main>
  );
}

export function SectionHeader({ eyebrow, title, copy }: { eyebrow: string; title: string; copy: string }) {
  return (
    <div className="section-header">
      <span>{eyebrow}</span>
      <h2>{title}</h2>
      <p>{copy}</p>
    </div>
  );
}
