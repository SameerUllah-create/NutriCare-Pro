"use client";
import {useEffect,useState} from "react";
import {PortalShell} from "@/components/PortalShell";
import {useSession} from "@/components/SessionProvider";
import {useLocale} from "@/components/LocaleProvider";
import {Notice,Empty} from "@/components/Forms";
import {useResource} from "@/lib/useResource";
type Intake={id:number;goals:string;medicalHistory?:string;allergies?:string;currentMedications?:string;lifestyle?:string;submittedAt:string;version:number};
export default function Page(){return <PortalShell roles={["Patient","Nutritionist"]}><Intake/></PortalShell>;}
function Intake(){const {user}=useSession(),{t,date}=useLocale(),[patient,setPatient]=useState("");useEffect(()=>{setPatient(new URLSearchParams(window.location.search).get("patient")||"");},[]);const resource=useResource<Intake[]>(user?.role==="Patient"?"/api/clinical/intake":patient?`/api/clinical/intake?patientId=${patient}`:null);return <main id="main-content" className="content-page"><h1>{t("Intake")}</h1><Notice error={resource.error} loading={resource.loading}/>{resource.data?.length===0&&<Empty/>}{resource.data?.map(item=><article className="panel" key={item.id}><h2>{t("Version")} {item.version}</h2><p>{date(item.submittedAt)}</p><dl>{([['goals','Goals'],['medicalHistory','Medical history'],['allergies','Allergies'],['currentMedications','Current medications'],['lifestyle','Lifestyle']]as const).map(([key,label])=><div key={key}><dt>{t(label)}</dt><dd style={{whiteSpace:"pre-wrap"}}>{item[key]||"—"}</dd></div>)}</dl></article>)}</main>;}
