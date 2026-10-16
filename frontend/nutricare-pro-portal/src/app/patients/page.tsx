"use client";
import Link from "next/link";
import {PortalShell} from "@/components/PortalShell";
import {useResource} from "@/lib/useResource";
import {Notice,Empty} from "@/components/Forms";
import {useLocale} from "@/components/LocaleProvider";
import type {PatientSummary} from "@/types/clinical";
export default function Page(){return <PortalShell roles={["Nutritionist"]}><Patients/></PortalShell>;}
function Patients(){const {data,error,loading}=useResource<PatientSummary[]>("/api/clinical/patients"),{t}=useLocale();return <main id="main-content" className="content-page"><h1>{t("Patients")}</h1><Notice error={error} loading={loading}/>{data?.length===0&&<Empty/>}<ul className="record-list">{data?.map(patient=><li key={patient.id}><h2>{patient.name||patient.fullName||`#${patient.id}`}</h2><p>{patient.currentWeight} kg · {t("Target weight (kg)")}: {patient.targetWeight||"—"}</p><div className="inline-actions"><Link className="button secondary" href={`/plans?patient=${patient.id}`}>{t("Diet plans")}</Link><Link className="button secondary" href={`/progress?patient=${patient.id}`}>{t("Progress")}</Link><Link className="button secondary" href={`/reports?patient=${patient.id}`}>{t("Reports")}</Link><Link className="button secondary" href={`/intake?patient=${patient.id}`}>{t("Intake")}</Link></div></li>)}</ul></main>;}
