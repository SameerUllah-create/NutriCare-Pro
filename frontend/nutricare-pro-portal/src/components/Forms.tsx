"use client";
import type { ReactNode } from "react";
import { useLocale } from "./LocaleProvider";
export function Field({ label, children, hint }: { label: string; children: ReactNode; hint?: string }) { const { t } = useLocale(); return <label className="field"><span>{t(label)}</span>{children}{hint && <small>{t(hint)}</small>}</label>; }
export function Notice({ error, message, loading }: { error?: string; message?: string; loading?: boolean }) { const { t } = useLocale(); return <>{loading && <p role="status">{t("Loading…")}</p>}{error && <p role="alert" className="error">{error}</p>}{message && <p role="status" className="notice">{t(message)}</p>}</>; }
export function Empty({ children }: { children?: ReactNode }) { const { t } = useLocale(); return <p className="empty">{children || t("No records yet.")}</p>; }
