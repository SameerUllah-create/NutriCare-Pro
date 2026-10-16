"use client";
import Link from "next/link";
import { useResource } from "@/lib/useResource";
import { useLocale } from "./LocaleProvider";
import { Empty, Notice } from "./Forms";
import type { Content, Policy, Service } from "@/types/clinical";
export function ServiceCatalog({ fees = false }: { fees?: boolean }) {
  const { data, error, loading } = useResource<Service[]>("/api/services"), { locale, t, money } = useLocale();
  return <><Notice error={error} loading={loading} />{data?.length === 0 && <Empty>{t("No published services are available yet.")}</Empty>}<div className="service-grid">{data?.map(service => <article className="marketing-card" key={service.id}><h2>{locale === "ur" && service.nameUrdu ? service.nameUrdu : service.name}</h2>{!fees && <p>{locale === "ur" && service.descriptionUrdu ? service.descriptionUrdu : service.description}</p>}<p>{money(service.price, service.currency)}</p><p className="meta">{service.durationMinutes ? `${service.durationMinutes} ${locale === "ur" ? "منٹ" : "minutes"}` : `${service.deliveryHours || 0} ${locale === "ur" ? "گھنٹے" : "hours delivery"}`} · {service.mode}</p><Link className="marketing-cta small" href={`/book?service=${service.id}`}>{t("Book")}</Link></article>)}</div></>;
}
export function PublishedContent({ kind }: { kind: string }) {
  const { data, error, loading } = useResource<Content[]>(`/api/content/${({about:"About",credentials:"Credentials",faqs:"FAQ",contact:"Contact",testimonials:"Testimonial"} as Record<string,string>)[kind] || kind}`), { locale } = useLocale();
  return <><Notice error={error} loading={loading} />{data?.length === 0 && <Empty>{locale === "ur" ? "منظور شدہ معلومات ابھی دستیاب نہیں ہیں۔" : "Approved information has not been published yet."}</Empty>}<div className="grid">{data?.map(item => <article className="panel" key={item.id}><h2>{locale === "ur" && item.titleUrdu ? item.titleUrdu : item.title || item.name}</h2><p style={{ whiteSpace: "pre-wrap" }}>{locale === "ur" && item.contentUrdu ? item.contentUrdu : item.content}</p></article>)}</div></>;
}
export function PolicyList() {
  const { data, error, loading } = useResource<Policy[]>("/api/policies"), { locale } = useLocale();
  return <><Notice error={error} loading={loading} />{data?.length === 0 && <Empty>{locale === "ur" ? "پالیسی دستاویزات کا جائزہ جاری ہے۔ حساس معلومات دینے سے پہلے معاونت سے رابطہ کریں۔" : "Policy documents are awaiting publication. Contact support before sharing sensitive information."}</Empty>}{data?.map(policy => <article key={policy.id} id={policy.purpose} className="panel"><h2>{locale === "ur" && policy.titleUrdu ? policy.titleUrdu : policy.title}</h2><p className="meta">{policy.version}</p><p style={{whiteSpace:"pre-wrap"}}>{locale === "ur" && policy.contentUrdu ? policy.contentUrdu : policy.content}</p></article>)}</>;
}

