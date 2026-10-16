"use client";

import { useState } from "react";
import type { FormEvent } from "react";
import { api, ApiError, errorMessage, json } from "@/lib/api";
import { useResource } from "@/lib/useResource";
import { useSession } from "./SessionProvider";
import { useLocale } from "./LocaleProvider";
import { Empty, Field, Notice } from "./Forms";
import { Button } from "./ui/button";

type Enrollment = {
  id: number; servicePackageId: number; patientId: number; packageVersion: number;
  purchasedAtUtc: string; firstConsultationDueAtUtc?: string|null;
  activatedAtUtc?: string|null; expiresAtUtc?: string|null; status: string; rowVersion: string;
};
type History = { id: number; action: string; reason?: string; actorUserId: number; previousExpiryUtc?: string|null; newExpiryUtc?: string|null; createdAt: string };
type Usage = { id: number; creditKind: string; resourceType: string; resourceId: number; status: string; serviceCode?: string };
type EnrollmentList = { items: Enrollment[]; total: number; page: number; pageSize: number; checkoutEnabled: false };
type EnrollmentDetail = { enrollment: Enrollment; usage: Usage[]; history: History[]; checkoutEnabled: false };

export function PackageValidityManager() {
  const { user } = useSession(), { t, date } = useLocale();
  const [page, setPage] = useState(1), [selectedId, setSelectedId] = useState<number|null>(null);
  const [days, setDays] = useState(""), [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false), [conflict, setConflict] = useState(false);
  const [error, setError] = useState(""), [message, setMessage] = useState("");
  const list = useResource<EnrollmentList>(user?.role === "Admin" ? `/api/package-enrollments?page=${page}` : null);
  const detail = useResource<EnrollmentDetail>(user?.role === "Admin" && selectedId ? `/api/package-enrollments/${selectedId}` : null);
  const enrollment = detail.data?.enrollment.id === selectedId ? detail.data.enrollment : null;
  if (user?.role !== "Admin") return null;
  const dated = (value?: string|null) => value ? date(value) : "—";

  function select(id: number|null) {
    setSelectedId(id); setDays(""); setReason(""); setError(""); setMessage(""); setConflict(false);
  }
  async function reloadLatest() {
    const latest = await detail.reload();
    await list.reload();
    if (latest?.enrollment.id === selectedId) {
      setConflict(false); setError(""); setMessage("Latest enrollment loaded. Review its expiry before submitting the extension again.");
    }
  }
  async function extend(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!enrollment || conflict || !reason.trim() || !Number.isInteger(Number(days)) || Number(days) <= 0) return;
    setBusy(true); setError(""); setMessage("");
    try {
      await api(`/api/package-enrollments/${enrollment.id}/extend`, json("POST", { days: Number(days), reason: reason.trim(), rowVersion: enrollment.rowVersion }));
      await Promise.all([list.reload(), detail.reload()]);
      setDays(""); setReason(""); setMessage("Package validity extended. The audit history records the reason and previous expiry.");
    } catch (value) {
      setError(errorMessage(value)); setConflict(value instanceof ApiError && value.status === 409);
    } finally { setBusy(false); }
  }

  return <section className="panel" aria-label={t("Existing package validity")}>
    <h2>{t("Existing package validity")}</h2>
    <p>{t("Manage validity for existing paid enrollments. Package checkout remains disabled.")}</p>
    <Notice error={error || list.error || detail.error} message={message} loading={list.loading || detail.loading}/>
    {!list.loading && list.data?.items.length === 0 && <Empty>{t("No paid package enrollments are available.")}</Empty>}
    <ul className="record-list">{list.data?.items.map(item => <li key={item.id}>
      <strong>{t("Enrollment")} #{item.id} · {t("Package")} #{item.servicePackageId} · {t("Version")} {item.packageVersion}</strong>
      <p>{t("Patient")} #{item.patientId} · {item.status} · {t("Expiry")}: {dated(item.expiresAtUtc)}</p>
      <Button type="button" variant="outline" disabled={busy} onClick={() => select(item.id)} aria-label={`${t("Manage validity")} #${item.id}`}>{t("Manage validity")}</Button>
    </li>)}</ul>
    {list.data && list.data.total > list.data.pageSize && <nav className="inline-actions" aria-label={t("Enrollment pages")}>
      <Button type="button" variant="outline" disabled={busy || page <= 1} onClick={() => { select(null); setPage(value => value - 1); }}>{t("Previous")}</Button>
      <span>{page} / {Math.ceil(list.data.total / list.data.pageSize)}</span>
      <Button type="button" variant="outline" disabled={busy || page * list.data.pageSize >= list.data.total} onClick={() => { select(null); setPage(value => value + 1); }}>{t("Next")}</Button>
    </nav>}
    {enrollment && <section className="panel">
      <h3>{t("Enrollment")} #{enrollment.id}</h3>
      <dl><dt>{t("Purchased")}</dt><dd>{dated(enrollment.purchasedAtUtc)}</dd><dt>{t("First consultation due")}</dt><dd>{dated(enrollment.firstConsultationDueAtUtc)}</dd><dt>{t("Activated")}</dt><dd>{dated(enrollment.activatedAtUtc)}</dd><dt>{t("Expiry")}</dt><dd>{dated(enrollment.expiresAtUtc)}</dd></dl>
      {conflict && <div className="notice" role="alert"><p>{t("This enrollment changed elsewhere. Your reason is preserved. Reload its latest expiry before retrying.")}</p><Button type="button" variant="outline" disabled={busy} onClick={() => void reloadLatest()}>{t("Reload latest enrollment")}</Button></div>}
      <form className="form" onSubmit={extend}>
        <Field label="Extend by (days)"><input type="number" min={1} step={1} required value={days} onChange={event => setDays(event.target.value)} disabled={busy}/></Field>
        <Field label="Extension reason"><textarea required maxLength={1000} rows={3} value={reason} onChange={event => setReason(event.target.value)} disabled={busy}/></Field>
        <Button disabled={busy || conflict || !reason.trim() || !Number.isInteger(Number(days)) || Number(days) <= 0 || !enrollment.rowVersion}>{t(busy ? "Please wait…" : "Extend package validity")}</Button>
      </form>
      <h3>{t("Credit usage")}</h3>
      {detail.data?.usage.length === 0 && <Empty/>}
      <ul className="record-list">{detail.data?.usage.map(item => <li key={item.id}>{item.creditKind} · {item.status} · {item.resourceType} #{item.resourceId}{item.serviceCode ? ` · ${item.serviceCode}` : ""}</li>)}</ul>
      <h3>{t("Validity audit history")}</h3>
      {detail.data?.history.length === 0 && <Empty/>}
      <ul className="record-list">{detail.data?.history.map(item => <li key={item.id}><strong>{item.action}</strong><p>{date(item.createdAt)} · {t("Actor")} #{item.actorUserId}</p><p>{item.reason}</p><p>{dated(item.previousExpiryUtc)} → {dated(item.newExpiryUtc)}</p></li>)}</ul>
    </section>}
  </section>;
}
