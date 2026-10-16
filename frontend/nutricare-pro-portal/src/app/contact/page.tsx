"use client";
import { useState } from "react";
import type {FormEvent} from "react";
import {MarketingShell} from "../components/MarketingShell";
import {Field,Notice} from "@/components/Forms";
import {PublishedContent} from "@/components/PublicContent";
import {useLocale} from "@/components/LocaleProvider";
import {api,json,errorMessage} from "@/lib/api";
export default function Page(){const {t,locale}=useLocale(),[busy,setBusy]=useState(false),[error,setError]=useState(""),[message,setMessage]=useState("");
async function submit(event:FormEvent<HTMLFormElement>){event.preventDefault();const form=event.currentTarget;setBusy(true);setError("");try{await api("/api/contact",json("POST",Object.fromEntries(new FormData(form).entries())));setMessage(locale==="ur"?"آپ کا پیغام موصول ہوگیا ہے۔":"Your message has been received. The care team will review it.");form.reset();}catch(e){setError(errorMessage(e));}finally{setBusy(false);}}
return <MarketingShell><section className="marketing-section"><h1>{t("Contact support")}</h1><p>{t("This service is not for urgent or emergency care.")}</p><p>{locale==="ur"?"اس فارم میں حساس طبی معلومات یا رپورٹس شامل نہ کریں۔":"Please keep medical details and reports out of this public form."}</p><div className="contact-grid"><form className="panel form" onSubmit={submit}><Field label="Name"><input name="name" required maxLength={120} autoComplete="name"/></Field><Field label="Email"><input name="email" type="email" required autoComplete="email"/></Field><Field label="Your message"><textarea name="message" required minLength={10} maxLength={4000} rows={6}/></Field><div className="honeypot" aria-hidden="true"><label>Website<input name="website" tabIndex={-1} autoComplete="off"/></label></div><Notice error={error} message={message}/><button className="button" disabled={busy}>{t(busy?"Please wait…":"Submit")}</button></form><PublishedContent kind="contact"/></div></section></MarketingShell>;}
