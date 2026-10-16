"use client";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import type { FormEvent } from "react";
import { useSession } from "./SessionProvider";
import { dashboardPath } from "@/lib/types";
import { LanguageSwitch, useLocale } from "./LocaleProvider";
import { Button } from "./ui/button";
import { ApiError, errorMessage, validationErrors } from "@/lib/api";
type Mode = "login" | "register" | "forgot-password" | "reset-password" | "resend-verification" | "verify-email";
const titles: Record<Mode, string> = { login: "Welcome back", register: "Create your patient account", "forgot-password": "Reset your password", "reset-password": "Choose a new password", "resend-verification": "Resend verification email", "verify-email": "Verify your email" };
export function AuthForm({ mode }: { mode: Mode }) {
  const [busy, setBusy] = useState(false), [error, setError] = useState(""), [message, setMessage] = useState("");
  const router = useRouter(); const session = useSession();
  const { t } = useLocale();
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError(""); setMessage("");
    const form = new FormData(event.currentTarget), values = Object.fromEntries(form.entries());
    const query = new URLSearchParams(window.location.search);
    if (mode === "reset-password") { values.token = query.get("token") || String(values.token || ""); values.email = query.get("email") || String(values.email || ""); }
    if (mode === "verify-email") { values.token = query.get("token") || String(values.token || ""); values.userId = query.get("userId") || String(values.userId || ""); }
    try {
      const response = await fetch(`/api/auth/${mode}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(values) });
      const payload = await response.json();
      if (!response.ok || !payload.success) throw new ApiError(payload.message || "Please check your details and try again.", response.status, validationErrors(payload));
      if (mode === "login") {
        await session.reload(); const next = query.get("next");
        router.replace(next?.startsWith("/") && !/^\/[\/\\]/.test(next) && !/[\\\u0000-\u001f\u007f]/.test(next) ? next : dashboardPath(payload.data?.role)); router.refresh();
      } else setMessage(mode === "register" ? "Your account has been created. Check your email to verify it before signing in." : mode === "verify-email" ? "Your email is verified. You can sign in now." : mode === "reset-password" ? "Your password has been updated. Sign in with your new password." : "If this email is eligible, instructions have been sent. Check your inbox and spam folder.");
    } catch (e) { setError(errorMessage(e)); } finally { setBusy(false); }
  }
  return <main className="auth-page"><Link className="workspace-brand" href="/">NutriCare Pro</Link><LanguageSwitch /><section className="panel auth-panel"><h1>{t(titles[mode])}</h1><p>{mode === "register" ? "Patient registration. Nutritionist accounts are provisioned by your administrator." : "Secure access to your nutrition care."}</p><form onSubmit={submit} className="form">
    {mode === "register" && <><label className="field">{t("First name")}<input name="firstName" autoComplete="given-name" required maxLength={100} /></label><label className="field">{t("Last name")}<input name="lastName" autoComplete="family-name" required maxLength={100} /></label></>}
    {mode !== "verify-email" && <label className="field">{t("Email")}<input type="email" name="email" autoComplete="email" required /></label>}
    {(mode === "login" || mode === "register") && <label className="field">{t("Password")}<input name="password" type="password" autoComplete={mode === "login" ? "current-password" : "new-password"} required minLength={mode === "login" ? 1 : 8} /></label>}
    {mode === "register" && <label className="field">{t("Confirm password")}<input name="confirmPassword" type="password" autoComplete="new-password" required minLength={8} /></label>}
    {mode === "reset-password" && <><label className="field">Reset code<input name="token" placeholder="Used automatically when opening your email link" /></label><label className="field">{t("New password")}<input name="newPassword" type="password" autoComplete="new-password" minLength={8} required /></label><label className="field">{t("Confirm password")}<input name="confirmPassword" type="password" autoComplete="new-password" minLength={8} required /></label></>}
    {mode === "verify-email" && <><p>Open the verification link from your email, then confirm below.</p><label className="field">User ID<input name="userId" inputMode="numeric" placeholder="Filled from your email link" /></label><label className="field">Verification code<input name="token" placeholder="Filled from your email link" /></label></>}
    {mode === "register" && <p className="meta">Use at least eight characters with an uppercase letter, a lowercase letter, and a number. Review our <Link href="/policies">privacy information</Link> before sharing health information.</p>}
    {error && <p role="alert" className="error">{error}</p>}{message && <p role="status" className="notice">{message}</p>}
    <Button disabled={busy} type="submit">{t(busy ? "Please wait…" : mode === "login" ? "Sign in" : mode === "register" ? "Create account" : mode === "verify-email" ? "Verify email" : "Continue")}</Button>
  </form><div className="auth-links"><Link href="/login">{t("Sign in")}</Link><Link href="/register">{t("Create account")}</Link><Link href="/forgot-password">{t("Forgot password?")}</Link><Link href="/resend-verification">{t("Resend verification")}</Link></div></section><p className="meta">{t("This service is not for urgent or emergency care.")}</p></main>;
}

