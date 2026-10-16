"use client";
import { useLocale } from "./LocaleProvider";
export type CancellationPolicy = { fullRefundHours?:number; freeRescheduleHours?:number; maxFreeReschedules?:number; providerCancellationRefund?:boolean; noShowAutomaticRefund?:boolean; version?:number; name?:string };
export function CancellationSummary({ policy, snapshot }: { policy?:CancellationPolicy|null; snapshot?:string }) {
  const {t}=useLocale(); let value=policy;
  if(snapshot){try{const raw=JSON.parse(snapshot) as Record<string,unknown>;const normalized=Object.fromEntries(Object.entries(raw).map(([key,item])=>[key[0].toLowerCase()+key.slice(1),item]));value=normalized as CancellationPolicy;}catch{value=null;}}
  if(!value)return <p>{t("An approved cancellation policy is not available. Contact support before booking.")}</p>;
  return <section className="policy-summary"><h3>{t("Cancellation policy")}{value.version?` · ${t("Version")} ${value.version}`:""}</h3><dl><dt>{t("Full refund cutoff (hours)")}</dt><dd>{value.fullRefundHours??"—"}</dd><dt>{t("Free reschedule cutoff (hours)")}</dt><dd>{value.freeRescheduleHours??"—"}</dd><dt>{t("Maximum free reschedules")}</dt><dd>{value.maxFreeReschedules??"—"}</dd><dt>{t("Refund provider cancellations")}</dt><dd>{t(value.providerCancellationRefund?"Yes":"No")}</dd><dt>{t("Automatic no-show refund")}</dt><dd>{t(value.noShowAutomaticRefund?"Yes":"No")}</dd></dl></section>;
}
