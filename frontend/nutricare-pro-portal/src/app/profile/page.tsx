"use client";
import { useState } from "react";
import type { FormEvent } from "react";
import { PortalShell } from "@/components/PortalShell";
import { Field, Notice } from "@/components/Forms";
import { api, json, errorMessage, download } from "@/lib/api";
import { useResource } from "@/lib/useResource";
import { useSession } from "@/components/SessionProvider";
import { useLocale } from "@/components/LocaleProvider";
import type { User } from "@/lib/types";
type Profile = { user: User & {phoneNumber?: string}; patient: {id: number; dateOfBirth?: string; gender?: string; height?: number; currentWeight?: number; targetWeight?: number; medicalHistory?: string; allergies?: string; currentMedications?: string} | null; nutritionist: {id: number; qualification?: string; licenseNumber?: string} | null };
export default function Page() { return <PortalShell><ProfileForm /></PortalShell>; }
function ProfileForm() {
  const { data, loading, error, reload } = useResource<Profile>("/api/profile"), { t } = useLocale(), session = useSession();
  const [busy, setBusy] = useState(false), [message, setMessage] = useState(""), [saveError, setError] = useState("");
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const values = Object.fromEntries(new FormData(event.currentTarget).entries());
    const body: Record<string, unknown> = { ...values };
    for (const key of ["height", "currentWeight", "targetWeight"]) body[key] = values[key] ? Number(values[key]) : null;
    if (!values.dateOfBirth) delete body.dateOfBirth;
    setBusy(true); setError("");
    try { await api("/api/profile", json("PUT", body)); await reload(); await session.reload(); setMessage("Your profile has been updated."); } catch(e) { setError(errorMessage(e)); } finally { setBusy(false); }
  }
  return <main id="main-content" className="content-page"><h1>{t("Profile")}</h1><Notice error={error || saveError} loading={loading} message={message} />{data && <><form className="panel form" onSubmit={save} key={data.user.id}><div className="form-grid"><Field label="First name"><input name="firstName" defaultValue={data.user.firstName} required maxLength={100} autoComplete="given-name" /></Field><Field label="Last name"><input name="lastName" defaultValue={data.user.lastName} required maxLength={100} autoComplete="family-name" /></Field><Field label="Phone number"><input name="phoneNumber" type="tel" defaultValue={data.user.phoneNumber} autoComplete="tel" /></Field>
    {data.patient && <><Field label="Date of birth"><input name="dateOfBirth" type="date" max={new Date().toISOString().slice(0,10)} defaultValue={data.patient.dateOfBirth?.slice(0,10)} /></Field><Field label="Gender"><select name="gender" defaultValue={data.patient.gender || ""}><option value="">â€”</option><option>Female</option><option>Male</option><option>Other</option><option>Prefer not to say</option></select></Field><Field label="Height (cm)"><input name="height" type="number" min={20} max={300} step="0.1" defaultValue={data.patient.height || ""} /></Field><Field label="Weight (kg)"><input name="currentWeight" type="number" min={1} max={500} step="0.1" defaultValue={data.patient.currentWeight || ""} /></Field><Field label="Target weight (kg)"><input name="targetWeight" type="number" min={1} max={500} step="0.1" defaultValue={data.patient.targetWeight || ""} /></Field><Field label="Medical history"><textarea name="medicalHistory" defaultValue={data.patient.medicalHistory} maxLength={4000} rows={3} /></Field><Field label="Allergies"><textarea name="allergies" defaultValue={data.patient.allergies} maxLength={2000} rows={3} /></Field><Field label="Current medications"><textarea name="currentMedications" defaultValue={data.patient.currentMedications} maxLength={2000} rows={3} /></Field></>}
    </div><button className="button" disabled={busy}>{t(busy ? "Please waitâ€¦" : "Save changes")}</button></form><section className="panel"><h2>{t("Privacy & policies")}</h2><p>Health information is used for your care under the approved policy shown during intake. Optional publication or marketing consent is separate.</p><button className="button secondary" onClick={() => void download("/api/privacy/export", "my-nutricare-data.json").catch(e => setError(errorMessage(e)))}>Export my data</button><a href="/policies">{t("Privacy & policies")}</a></section></>}</main>;
}


