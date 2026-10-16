"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import type { ReactNode } from "react";
import { useSession } from "./SessionProvider";
import type { Role } from "@/lib/types";
import { dashboardPath } from "@/lib/types";
import { LanguageSwitch, useLocale } from "./LocaleProvider";

const common = [{ href: "/profile", label: "Profile" }, { href: "/appointments", label: "Appointments" }, { href: "/notifications", label: "Notifications" }, { href: "/privacy", label: "Privacy & policies" }];
export function PortalShell({ children, roles }: { children: ReactNode; roles?: Role[] }) {
  const { user, loading, error, reload, signOut } = useSession();
  const { t } = useLocale();
  const path = usePathname(); const [signOutError, setSignOutError] = useState("");
  if (loading) return <main id="main-content" className="auth-page"><p role="status">{t("Checking your session…")}</p></main>;
  if (error) return <main id="main-content" className="auth-page"><p role="alert">{error}</p><button onClick={() => void reload()}>{t("Retry")}</button></main>;
  if (!user) return <main id="main-content" className="auth-page"><section className="panel"><h1>{t("Sign in to continue")}</h1><p>Your session has ended or you have not signed in yet.</p><Link className="button" href={`/login?next=${encodeURIComponent(path)}`}>{t("Sign in")}</Link></section></main>;
  if (roles && !roles.includes(user.role)) return <main id="main-content" className="auth-page"><section className="panel"><h1>{t("Access restricted")}</h1><p>This workspace is available to {roles.join(" or ")} accounts.</p><Link href={dashboardPath(user.role)}>Go to your dashboard</Link></section></main>;
  const links = [
    { href: dashboardPath(user.role), label: "Dashboard" }, ...common,
    ...(user.role === "Patient" ? [{ href: "/book", label: "Book a consultation" }, { href: "/plans", label: "Diet plans" }, { href: "/progress", label: "Progress" }, { href: "/reports", label: "Reports" }, { href: "/payments", label: "Payments" }, { href: "/feedback", label: "Feedback" }] : []),
    ...(user.role === "Nutritionist" ? [{ href: "/patients", label: "Patients" }, { href: "/availability", label: "Availability" }, { href: "/plans", label: "Diet plans" }, { href: "/configuration", label: "Services & content" }, {href:"/management",label:"Support & feedback"}] : []),
    ...(user.role === "Admin" ? [{ href: "/configuration", label: "Services & content" }, { href: "/management", label: "Support & feedback" }, { href: "/admin/clinician", label:"Clinician onboarding" }, { href: "/payments", label: "Payments" }] : []),
    ...(user.role !== "Admin" ? [{ href: "/messages", label: "Messages" }, { href: "/video-consultations", label: "Video consultations" }] : [])
  ];
  return <div className="workspace"><header className="workspace-header"><Link href="/" className="workspace-brand">NutriCare Pro</Link><span>{user.firstName} {user.lastName} · {t(user.role)}</span><LanguageSwitch /><button className="button secondary" onClick={() => void signOut().catch(e => setSignOutError(e.message))}>{t("Sign out")}</button></header><nav className="workspace-nav" aria-label="Workspace">{links.map(link => <Link key={link.href} href={link.href} aria-current={path === link.href ? "page" : undefined}>{t(link.label)}</Link>)}</nav>{signOutError && <p role="alert" className="error">{signOutError}</p>}{children}</div>;
}

